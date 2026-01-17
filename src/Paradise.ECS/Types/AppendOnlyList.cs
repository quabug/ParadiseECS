using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A simple append-only list using a chunked storage strategy.
/// Single-threaded version without concurrent access support.
/// </summary>
/// <remarks>
/// Uses a chunked (unrolled linked list) approach where each chunk is a fixed-size array.
/// Growth only allocates new chunks - existing data never moves.
/// By default, chunk size is calculated to make each chunk approximately 16KB (L1 cache size).
/// </remarks>
/// <typeparam name="T">The element type.</typeparam>
public sealed class AppendOnlyList<T> : IReadOnlyList<T>
{
    private const int TargetChunkBytes = 16 * 1024; // 16KB target chunk size
    private const int MinChunkShift = 2;            // Minimum 4 elements per chunk
    private const int MaxChunkShift = 20;           // Maximum ~1M elements per chunk

    private readonly int _chunkShift;
    private readonly int _chunkSize;
    private readonly int _chunkMask;

    private T[][] _chunks;
    private int _chunkCount;
    private int _count;

    /// <summary>
    /// Creates a new <see cref="AppendOnlyList{T}"/> with chunk size optimized for L1 cache (~16KB per chunk).
    /// </summary>
    public AppendOnlyList() : this(CalculateDefaultChunkShift())
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
    /// Creates a new <see cref="AppendOnlyList{T}"/> with specified chunk size.
    /// </summary>
    /// <param name="chunkShift">
    /// The power of 2 for chunk size. Default is 10 (1024 elements per chunk).
    /// Valid range is 2-20 (4 to ~1M elements per chunk).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="chunkShift"/> is outside the valid range.
    /// </exception>
    public AppendOnlyList(int chunkShift)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkShift, MinChunkShift);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(chunkShift, MaxChunkShift);

        _chunkShift = chunkShift;
        _chunkSize = 1 << chunkShift;
        _chunkMask = _chunkSize - 1;
        _chunks = new T[4][];
    }

    /// <summary>
    /// Gets the number of elements in the list.
    /// </summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
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
    /// Adds a value to the list.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <returns>The index at which the value was stored.</returns>
    public int Add(T value)
    {
        int index = _count;
        int chunkIndex = index >> _chunkShift;
        int indexInChunk = index & _chunkMask;

        EnsureChunk(chunkIndex);

        _chunks[chunkIndex][indexInChunk] = value;
        _count = index + 1;

        return index;
    }

    /// <summary>
    /// Adds a range of values to the list.
    /// More efficient than calling Add multiple times.
    /// </summary>
    /// <param name="values">The values to add.</param>
    /// <returns>The starting index at which the values were stored.</returns>
    public int AddRange(ReadOnlySpan<T> values)
    {
        if (values.IsEmpty)
            return _count;

        int length = values.Length;
        int startIndex = _count;
        int endChunkIndex = (startIndex + length - 1) >> _chunkShift;

        EnsureChunk(endChunkIndex);

        for (int i = 0; i < length; i++)
        {
            int index = startIndex + i;
            int chunkIndex = index >> _chunkShift;
            int indexInChunk = index & _chunkMask;
            _chunks[chunkIndex][indexInChunk] = values[i];
        }

        _count = startIndex + length;
        return startIndex;
    }

    /// <summary>
    /// Gets the value at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get.</param>
    /// <returns>The value at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if index is out of range.</exception>
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_count)
                ThrowArgumentOutOfRange(index, _count);

            return _chunks[index >> _chunkShift][index & _chunkMask];
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
        if ((uint)index >= (uint)_count)
            ThrowArgumentOutOfRange(index, _count);

        return ref _chunks[index >> _chunkShift][index & _chunkMask];
    }

    /// <summary>
    /// Ensures the chunk at the given index exists.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureChunk(int chunkIndex)
    {
        if (chunkIndex < _chunkCount)
            return;

        EnsureChunkSlow(chunkIndex);
    }

    /// <summary>
    /// Slow path for chunk allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void EnsureChunkSlow(int chunkIndex)
    {
        // Grow chunks array if needed
        if (chunkIndex >= _chunks.Length)
        {
            int newLength = chunkIndex + 1;
            var newChunks = new T[newLength][];
            Array.Copy(_chunks, newChunks, _chunkCount);
            _chunks = newChunks;
        }

        // Allocate all chunks up to and including chunkIndex
        for (int i = _chunkCount; i <= chunkIndex; i++)
        {
            _chunks[i] = new T[_chunkSize];
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
    /// Enumerator for <see cref="AppendOnlyList{T}"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly AppendOnlyList<T> _list;
        private readonly int _count;
        private int _index;

        internal Enumerator(AppendOnlyList<T> list)
        {
            _list = list;
            _count = list._count;
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
