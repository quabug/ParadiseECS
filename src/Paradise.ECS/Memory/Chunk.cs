using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A memory block for storing entity component data.
/// The size is configured via TConfig.ChunkSize, defaulting to 16KB to fit within L1 cache.
/// This is a ref struct that borrows memory from ChunkManager and must be disposed.
/// </summary>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public readonly unsafe ref struct Chunk<TConfig> : IDisposable
    where TConfig : IConfig
{
    private readonly ChunkManager<TConfig>? _manager;
    private readonly int _id;
    private readonly nint _memory;

    /// <summary>
    /// Creates a Chunk view that borrows memory from the manager.
    /// </summary>
    internal Chunk(ChunkManager<TConfig> manager, int id, nint memory)
    {
        ThrowHelper.ThrowIfNull((void*)memory);
        _manager = manager;
        _id = id;
        _memory = memory;
    }

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _memory != 0 && _manager != null;
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
        ThrowHelper.ValidateChunkRange<TConfig>(byteOffset, count, sizeof(T));
        return new((byte*)_memory + byteOffset, count);
    }

    /// <summary>
    /// Gets a reference to a value at the specified byte offset from chunk start.
    /// </summary>
    /// <typeparam name="T">The unmanaged type.</typeparam>
    /// <param name="byteOffset">The offset from the start of the chunk data.</param>
    /// <returns>A reference to the value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>(int byteOffset) where T : unmanaged
    {
        ThrowHelper.ValidateChunkRange<TConfig>(byteOffset, 1, sizeof(T));
        return ref Unsafe.AsRef<T>((byte*)_memory + byteOffset);
    }

    /// <summary>
    /// Gets the raw bytes of the entire data area.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetDataBytes() => new((void*)_memory, TConfig.ChunkSize);

    /// <summary>
    /// Gets the raw bytes of the data area up to a specified size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetDataBytes(int size)
    {
        ThrowHelper.ValidateChunkSize<TConfig>(size);
        return new((void*)_memory, size);
    }

    /// <summary>
    /// Gets raw bytes at a specific offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetBytesAt(int byteOffset, int size)
    {
        ThrowHelper.ValidateChunkRange<TConfig>(byteOffset, size);
        return new((byte*)_memory + byteOffset, size);
    }

    /// <summary>
    /// Gets the entire chunk memory as raw bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetRawBytes() => new((void*)_memory, TConfig.ChunkSize);
}
