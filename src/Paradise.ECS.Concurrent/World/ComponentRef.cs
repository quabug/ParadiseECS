using System.Runtime.CompilerServices;

namespace Paradise.ECS.Concurrent;

/// <summary>
/// A ref struct that provides safe access to a component in a chunk.
/// Must be disposed to release the chunk borrow.
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
/// <typeparam name="TConfig">The world configuration type that determines chunk size and limits.</typeparam>
public readonly unsafe ref struct ComponentRef<T, TConfig>
    where T : unmanaged, IComponent
    where TConfig : IConfig, new()
{
    private readonly ChunkManager<TConfig> _manager;
    private readonly ChunkHandle _handle;
    private readonly T* _pointer;

    /// <summary>
    /// Creates a new component reference.
    /// </summary>
    /// <param name="manager">The chunk manager.</param>
    /// <param name="handle">The chunk handle.</param>
    /// <param name="offset">The byte offset to the component data.</param>
    internal ComponentRef(ChunkManager<TConfig> manager, ChunkHandle handle, int offset)
    {
        _manager = manager;
        _handle = handle;
        manager.Acquire(handle);
        var bytes = manager.GetBytes(handle);
        _pointer = (T*)Unsafe.AsPointer(ref bytes[offset]);
    }

    /// <summary>
    /// Gets a reference to the component value.
    /// </summary>
    public ref T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef<T>(_pointer);
    }

    /// <summary>
    /// Releases the chunk borrow.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _manager?.Release(_handle);
}
