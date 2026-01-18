using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A memory block for storing entity component data.
/// Sized to fit within L1 cache (typically 16KB).
/// This is a ref struct that borrows memory from ChunkManager and must be disposed.
/// </summary>
public readonly unsafe ref struct Chunk : IDisposable
{
    private readonly ChunkManager _manager;
    private readonly int _id;
    private readonly nint _memory;

    /// <summary>
    /// Creates a Chunk view that borrows memory from the manager.
    /// </summary>
    internal Chunk(ChunkManager manager, int id, nint memory)
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
    /// Gets the entire chunk memory as raw bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetRawBytes() => new((void*)_memory, _manager.ChunkSize);
}
