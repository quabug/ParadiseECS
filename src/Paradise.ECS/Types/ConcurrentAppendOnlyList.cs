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
/// </remarks>
/// <typeparam name="T">The element type.</typeparam>
public sealed class ConcurrentAppendOnlyList<T>
{
    private const int TargetChunkBytes = 16 * 1024; // 16KB target chunk size
    private const int MinChunkShift = 2;            // Minimum 4 elements per chunk
    private const int MaxChunkShift = 20;           // Maximum ~1M elements per chunk

    private readonly int _chunkShift;
    private readonly int _chunkSize;
    private readonly int _chunkMask;
    private readonly Lock _chunkLock = new();

    private T[][] _chunks;
    private int _chunkCount;
    private int _count;
    private int _committedCount;

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
    }

    /// <summary>
    /// Gets the number of committed elements in the list.
    /// Only counts fully committed (visible) elements.
    /// </summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _committedCount);
    }

    /// <summary>
    /// Gets the current capacity of the list (total slots across all allocated chunks).
    /// </summary>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _chunkCount) * _chunkSize;
    }

    /// <summary>
    /// Gets the number of allocated chunks.
    /// </summary>
    public int ChunkCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _chunkCount);
    }

    /// <summary>
    /// Adds a value to the list. Thread-safe and lock-free when chunk exists.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <returns>The index at which the value was stored.</returns>
    public int Add(T value)
    {
        // Reserve a slot atomically
        int index = Interlocked.Increment(ref _count) - 1;
        int chunkIndex = index >> _chunkShift;
        int indexInChunk = index & _chunkMask;

        // Ensure the chunk exists
        EnsureChunk(chunkIndex);

        // Write to the slot (chunk is guaranteed to exist now)
        var chunk = Volatile.Read(ref _chunks[chunkIndex]);
        chunk[indexInChunk] = value;

        // Commit in order - wait for prior slots then publish
        SpinWait spinWait = default;
        while (Volatile.Read(ref _committedCount) != index)
        {
            spinWait.SpinOnce();
        }
        Volatile.Write(ref _committedCount, index + 1);

        return index;
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
            int count = Volatile.Read(ref _committedCount);
            if ((uint)index >= (uint)count)
                ThrowArgumentOutOfRange(index, count);

            var chunk = Volatile.Read(ref _chunks)[index >> _chunkShift];
            return chunk[index & _chunkMask];
        }
    }

    /// <summary>
    /// Ensures the chunk at the given index exists.
    /// Lock-free when chunk already exists; acquires lock only for allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureChunk(int chunkIndex)
    {
        // Fast path: chunk already exists
        if (chunkIndex < Volatile.Read(ref _chunkCount))
            return;

        EnsureChunkSlow(chunkIndex);
    }

    /// <summary>
    /// Slow path for chunk allocation. Acquires lock and allocates if needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EnsureChunkSlow(int chunkIndex)
    {
        lock (_chunkLock)
        {
            // Double-check after acquiring lock
            if (chunkIndex < _chunkCount)
                return;

            // Grow chunks array if needed (only copies pointers, not data)
            if (chunkIndex >= _chunks.Length)
            {
                int newLength = Math.Max(_chunks.Length * 2, chunkIndex + 1);
                var newChunks = new T[newLength][];
                Array.Copy(_chunks, newChunks, _chunkCount);
                Volatile.Write(ref _chunks, newChunks);
            }

            // Allocate all chunks up to and including chunkIndex
            for (int i = _chunkCount; i <= chunkIndex; i++)
            {
                Volatile.Write(ref _chunks[i], new T[_chunkSize]);
            }

            Volatile.Write(ref _chunkCount, chunkIndex + 1);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRange(int index, int count)
        => throw new ArgumentOutOfRangeException(nameof(index),
            $"Index {index} is out of range. Count: {count}");
}
