using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A 16KB memory block for storing data.
/// The size is chosen to fit within L1 cache for optimal iteration performance.
/// This is a ref struct that borrows memory from ChunkManager and must be disposed.
/// </summary>
public readonly unsafe ref struct Chunk : IDisposable
{
    /// <summary>
    /// The size of each chunk in bytes (16KB).
    /// Chosen to fit within typical L1 cache sizes (32KB+).
    /// </summary>
    public const int ChunkSize = 16 * 1024;

    private readonly ChunkManager _manager;
    private readonly int _id;
    private readonly void* _memory;

    /// <summary>
    /// Creates a Chunk view that borrows memory from the manager.
    /// </summary>
    internal Chunk(ChunkManager manager, int id, void* memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        _manager = manager;
        _id = id;
        _memory = memory;
    }

    /// <summary>
    /// Releases the borrow on the chunk memory.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _manager?.Release(_id);

    /// <summary>
    /// Gets a span over data at the specified byte offset from chunk start.
    /// </summary>
    /// <typeparam name="T">The unmanaged type.</typeparam>
    /// <param name="byteOffset">The offset from the start of the chunk data.</param>
    /// <param name="count">The number of elements.</param>
    /// <returns>A span over the data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan<T>(int byteOffset, int count) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(byteOffset + count * sizeof(T), ChunkSize);
        return new((byte*)_memory + byteOffset, count);
    }

    /// <summary>
    /// Gets the raw bytes of the entire data area.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetDataBytes() => new(_memory, ChunkSize);

    /// <summary>
    /// Gets the raw bytes of the data area up to a specified size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetDataBytes(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, ChunkSize);
        return new(_memory, size);
    }

    /// <summary>
    /// Gets raw bytes at a specific offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetBytesAt(int byteOffset, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(byteOffset + size, ChunkSize);
        return new((byte*)_memory + byteOffset, size);
    }

    /// <summary>
    /// Gets the entire chunk memory as raw bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetRawBytes() => new(_memory, ChunkSize);

    /// <summary>
    /// Converts to a read-only view.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyChunk AsReadOnly() => new(_memory);

    public static implicit operator ReadOnlyChunk(Chunk chunk) => chunk.AsReadOnly();
}

/// <summary>
/// A read-only view over a 16KB memory block.
/// Use this for systems that only need to read data.
/// </summary>
public readonly unsafe ref struct ReadOnlyChunk
{
    private readonly void* _memory;

    internal ReadOnlyChunk(void* memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        _memory = memory;
    }

    /// <summary>
    /// Gets a read-only span over data at the specified byte offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetSpan<T>(int byteOffset, int count) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(byteOffset + count * sizeof(T), Chunk.ChunkSize);
        return new((byte*)_memory + byteOffset, count);
    }

    /// <summary>
    /// Gets the raw bytes of the entire data area.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetDataBytes() => new(_memory, Chunk.ChunkSize);

    /// <summary>
    /// Gets the raw bytes of the data area up to a specified size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetDataBytes(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, Chunk.ChunkSize);
        return new(_memory, size);
    }

    /// <summary>
    /// Gets raw bytes at a specific offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetBytesAt(int byteOffset, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(byteOffset + size, Chunk.ChunkSize);
        return new((byte*)_memory + byteOffset, size);
    }

    /// <summary>
    /// Gets the entire chunk memory as raw bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetRawBytes() => new(_memory, Chunk.ChunkSize);
}
