using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A thread-safe, append-only list using a chunked storage strategy.
/// Supports concurrent Add and Read operations without copying data during growth.
/// </summary>
/// <remarks>
/// Uses a chunked (unrolled linked list) approach where each chunk is a fixed-size array.
/// Growth only allocates new chunks - existing data never moves, making concurrent access simpler.
/// The Add operation is lock-free when the target chunk already exists.
/// By default, chunk size is calculated to make each chunk approximately 16KB (L1 cache size).
/// Uses atomic bitmap marking for high-performance concurrent commits without convoy effects.
/// </remarks>
/// <typeparam name="T">The element type.</typeparam>
public sealed class ConcurrentAppendOnlyList<T> : IReadOnlyList<T>
{
    private const int TargetChunkBytes = 16 * 1024; // 16KB target chunk size
    private const int MinChunkShift = 2;            // Minimum 4 elements per chunk
    private const int MaxChunkShift = 20;           // Maximum ~1M elements per chunk
    private const int BitsPerWord = 64;             // Using ulong for bitmap
    private const int BitsPerWordShift = 6;         // log2(64)
    private const int BitsPerWordMask = BitsPerWord - 1;

    private readonly int _chunkShift;
    private readonly int _chunkSize;
    private readonly int _chunkMask;
    private readonly Lock _chunkLock = new();

    private volatile T[][] _chunks;
    private volatile ulong[] _readyBitmap;  // Bitmap tracking which slots have been written
    private volatile int _chunkCount;
    private volatile int _count;
    private volatile int _committedCount;

    /// <summary>
    /// Creates a new <see cref="ConcurrentAppendOnlyList{T}"/> with chunk size optimized for L1 cache (~16KB per chunk).
    /// </summary>
    public ConcurrentAppendOnlyList() : this(CalculateDefaultChunkShift())
    {
    }

    /// <summary>
    /// Calculates the optimal chunk shift to make each chunk approximately 16KB.
    /// </summary>
    private static int CalculateDefaultChunkShift()
    {
        int elementSize = Unsafe.SizeOf<T>();
        int elementsPerChunk = TargetChunkBytes / elementSize;
        int shift = BitOperations.Log2((uint)elementsPerChunk);
        return Math.Clamp(shift, MinChunkShift, MaxChunkShift);
    }

    /// <summary>
    /// Creates a new <see cref="ConcurrentAppendOnlyList{T}"/> with specified chunk size.
    /// </summary>
    /// <param name="chunkShift">
    /// The power of 2 for chunk size. Default is 10 (1024 elements per chunk).
    /// Valid range is 2-20 (4 to ~1M elements per chunk).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="chunkShift"/> is outside the valid range.
    /// </exception>
    public ConcurrentAppendOnlyList(int chunkShift)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkShift, MinChunkShift);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(chunkShift, MaxChunkShift);

        _chunkShift = chunkShift;
        _chunkSize = 1 << chunkShift;
        _chunkMask = _chunkSize - 1;
        _chunks = new T[4][];
        _readyBitmap = new ulong[4]; // Initial bitmap capacity
    }

    /// <summary>
    /// Gets the number of committed elements in the list.
    /// Only counts fully committed (visible) elements.
    /// </summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _committedCount;
    }

    /// <summary>
    /// Gets the current capacity of the list (total slots across all allocated chunks).
    /// </summary>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunkCount * _chunkSize;
    }

    /// <summary>
    /// Gets the number of allocated chunks.
    /// </summary>
    public int ChunkCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunkCount;
    }

    /// <summary>
    /// Adds a value to the list. Thread-safe and lock-free when chunk exists.
    /// Guarantees that when this method returns, the element at the returned index is committed and readable.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <returns>The index at which the value was stored.</returns>
    public int Add(T value)
    {
        // Reserve a slot atomically
        int index = Interlocked.Increment(ref _count) - 1;
        int chunkIndex = index >> _chunkShift;
        int indexInChunk = index & _chunkMask;

        // Ensure the chunk and bitmap exist
        EnsureChunk(chunkIndex);

        // Write to the slot (chunk is guaranteed to exist now)
        // _chunks is volatile, so this read has acquire semantics
        var chunk = _chunks[chunkIndex];
        chunk[indexInChunk] = value;

        // Mark slot as ready in bitmap (atomic)
        MarkSlotReady(index);

        // Try to advance committed count
        TryAdvanceCommittedCount();

        // Wait until our slot is committed (fast path: usually already committed)
        SpinWait spinWait = default;
        while (_committedCount <= index)
        {
            // Try to help advance if possible
            TryAdvanceCommittedCount();
            spinWait.SpinOnce(-1); // -1 disables Sleep(1), only yields
        }

        return index;
    }

    /// <summary>
    /// Adds a range of values to the list. Thread-safe.
    /// Guarantees that when this method returns, all elements are committed and readable.
    /// More efficient than calling Add multiple times as it reserves all slots atomically.
    /// </summary>
    /// <param name="values">The values to add.</param>
    /// <returns>The starting index at which the values were stored.</returns>
    public int AddRange(ReadOnlySpan<T> values)
    {
        if (values.IsEmpty)
            return _committedCount;

        int length = values.Length;

        // Reserve slots atomically
        int startIndex = Interlocked.Add(ref _count, length) - length;
        int endChunkIndex = (startIndex + length - 1) >> _chunkShift;

        // Ensure all needed chunks exist
        EnsureChunk(endChunkIndex);

        // Write all values to their slots
        var chunks = _chunks;
        for (int i = 0; i < length; i++)
        {
            int index = startIndex + i;
            int chunkIndex = index >> _chunkShift;
            int indexInChunk = index & _chunkMask;
            chunks[chunkIndex][indexInChunk] = values[i];
        }

        // Mark all slots as ready in bitmap (processes 64 bits at a time)
        MarkSlotsReady(startIndex, length);

        // Try to advance committed count
        TryAdvanceCommittedCount();

        // Wait until all our slots are committed
        int lastIndex = startIndex + length - 1;
        SpinWait spinWait = default;
        while (_committedCount <= lastIndex)
        {
            TryAdvanceCommittedCount();
            spinWait.SpinOnce(-1);
        }

        return startIndex;
    }

    /// <summary>
    /// Atomically marks a slot as ready in the bitmap.
    /// Retries if the bitmap array is replaced during the operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MarkSlotReady(int index)
    {
        int wordIndex = index >> BitsPerWordShift;
        int bitIndex = index & BitsPerWordMask;
        ulong mask = 1UL << bitIndex;

        while (true)
        {
            var bitmap = _readyBitmap;
            Interlocked.Or(ref bitmap[wordIndex], mask);

            // Verify bitmap wasn't replaced during the operation.
            // If it was, retry to ensure the bit is set in the current bitmap.
            if (ReferenceEquals(bitmap, _readyBitmap))
                return;
        }
    }

    /// <summary>
    /// Atomically marks a contiguous range of slots as ready in the bitmap.
    /// More efficient than calling MarkSlotReady in a loop as it processes 64 bits at a time.
    /// </summary>
    /// <param name="startIndex">The first slot index to mark (inclusive).</param>
    /// <param name="count">The number of slots to mark.</param>
    private void MarkSlotsReady(int startIndex, int count)
    {
        if (count <= 0)
            return;

        int endIndex = startIndex + count - 1;
        int startWord = startIndex >> BitsPerWordShift;
        int endWord = endIndex >> BitsPerWordShift;
        int startBit = startIndex & BitsPerWordMask;
        int endBit = endIndex & BitsPerWordMask;

        while (true)
        {
            var bitmap = _readyBitmap;

            if (startWord == endWord)
            {
                // All bits in a single word: create mask from startBit to endBit
                // e.g., startBit=2, endBit=5 -> bits 2,3,4,5 -> mask = 0b00111100
                // Use right-shift of ~0UL to avoid undefined behavior when bitCount=64
                int bitCount = endBit - startBit + 1;
                ulong mask = (~0UL >> (64 - bitCount)) << startBit;
                Interlocked.Or(ref bitmap[startWord], mask);
            }
            else
            {
                // First word: set bits from startBit to 63
                // e.g., startBit=10 -> ~((1<<10)-1) = bits 10-63 set
                ulong firstMask = ~((1UL << startBit) - 1);
                Interlocked.Or(ref bitmap[startWord], firstMask);

                // Middle words: set all 64 bits
                for (int word = startWord + 1; word < endWord; word++)
                {
                    Interlocked.Or(ref bitmap[word], ~0UL);
                }

                // Last word: set bits from 0 to endBit
                // e.g., endBit=5 -> bits 0-5 set
                // Use right-shift of ~0UL to avoid undefined behavior when endBit=63
                ulong lastMask = ~0UL >> (63 - endBit);
                Interlocked.Or(ref bitmap[endWord], lastMask);
            }

            // Verify bitmap wasn't replaced during the operation
            if (ReferenceEquals(bitmap, _readyBitmap))
                return;
        }
    }

    /// <summary>
    /// Checks if a slot is marked as ready in the bitmap.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsSlotReady(int index)
    {
        int wordIndex = index >> BitsPerWordShift;
        int bitIndex = index & BitsPerWordMask;
        ulong mask = 1UL << bitIndex;

        var bitmap = _readyBitmap;
        if (wordIndex >= bitmap.Length)
            return false;

        return (Volatile.Read(ref bitmap[wordIndex]) & mask) != 0;
    }

    /// <summary>
    /// Tries to advance the committed count by scanning consecutive ready slots.
    /// Uses lock-free CAS to ensure only one thread advances at a time.
    /// Uses bit operations to process 64 slots at a time for better performance.
    /// </summary>
    private void TryAdvanceCommittedCount()
    {
        while (true)
        {
            int current = _committedCount;
            int reserved = _count;

            // Nothing to advance
            if (current >= reserved)
                return;

            // Count consecutive ready slots using optimized word-at-a-time scanning
            var bitmap = _readyBitmap;
            int consecutiveReady = bitmap.CountConsecutiveSetBits(current, reserved);
            if (consecutiveReady == 0)
                return;

            int newCommitted = current + consecutiveReady;

            // Try to advance committed count atomically
            if (Interlocked.CompareExchange(ref _committedCount, newCommitted, current) == current)
            {
                // Successfully advanced - check if more slots became ready
                if (newCommitted < reserved && IsSlotReady(newCommitted))
                    continue;
                return;
            }

            // CAS failed - another thread advanced, retry
        }
    }

    /// <summary>
    /// Gets the value at the specified index.
    /// Thread-safe: can be called concurrently with Add operations.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get.</param>
    /// <returns>The value at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if index is out of range.</exception>
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            int count = _committedCount;
            if ((uint)index >= (uint)count)
                ThrowArgumentOutOfRange(index, count);

            var chunk = _chunks[index >> _chunkShift];
            return chunk[index & _chunkMask];
        }
    }

    /// <summary>
    /// Gets a reference to the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get.</param>
    /// <returns>A reference to the value at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if index is out of range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef(int index)
    {
        int count = _committedCount;
        if ((uint)index >= (uint)count)
            ThrowArgumentOutOfRange(index, count);

        var chunk = _chunks[index >> _chunkShift];
        return ref chunk[index & _chunkMask];
    }

    /// <summary>
    /// Ensures the chunk at the given index exists.
    /// Lock-free when chunk already exists; acquires lock only for allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureChunk(int chunkIndex)
    {
        // Fast path: chunk already exists
        if (chunkIndex < _chunkCount)
            return;

        EnsureChunkSlow(chunkIndex);
    }

    /// <summary>
    /// Slow path for chunk allocation. Acquires lock and allocates if needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EnsureChunkSlow(int chunkIndex)
    {
        using var _ = _chunkLock.EnterScope();
        // Double-check after acquiring lock
        if (chunkIndex < _chunkCount)
            return;

        // Grow chunks array if needed (only copies pointers, not data)
        if (chunkIndex >= _chunks.Length)
        {
            int newLength = chunkIndex + 1;
            var newChunks = new T[newLength][];
            Array.Copy(_chunks, newChunks, _chunkCount);
            _chunks = newChunks;
        }

        // Calculate required bitmap size for all slots up to this chunk
        int maxIndex = (chunkIndex + 1) * _chunkSize - 1;
        int requiredBitmapWords = (maxIndex >> BitsPerWordShift) + 1;

        // Grow bitmap if needed
        if (requiredBitmapWords > _readyBitmap.Length)
        {
            var newBitmap = new ulong[requiredBitmapWords];
            Array.Copy(_readyBitmap, newBitmap, _readyBitmap.Length);
            _readyBitmap = newBitmap;
        }

        // Allocate all chunks up to and including chunkIndex
        for (int i = _chunkCount; i <= chunkIndex; i++)
        {
            Volatile.Write(ref _chunks[i], new T[_chunkSize]);
        }

        _chunkCount = chunkIndex + 1;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the list.
    /// </summary>
    /// <returns>An enumerator for the list.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc />
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRange(int index, int count)
        => throw new ArgumentOutOfRangeException(nameof(index),
            $"Index {index} is out of range. Count: {count}");

    /// <summary>
    /// Enumerator for <see cref="ConcurrentAppendOnlyList{T}"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly ConcurrentAppendOnlyList<T> _list;
        private readonly int _count;
        private int _index;

        internal Enumerator(ConcurrentAppendOnlyList<T> list)
        {
            _list = list;
            _count = list._committedCount;
            _index = -1;
        }

        /// <inheritdoc />
        public readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var chunk = _list._chunks[_index >> _list._chunkShift];
                return chunk[_index & _list._chunkMask];
            }
        }

        /// <inheritdoc />
        readonly object? IEnumerator.Current => Current;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _count)
            {
                _index = next;
                return true;
            }
            return false;
        }

        /// <inheritdoc />
        public void Reset() => _index = -1;

        /// <inheritdoc />
        public readonly void Dispose() { }
    }
}
