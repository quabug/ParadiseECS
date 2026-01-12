using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A ref struct that provides safe access to a component in a chunk.
/// Must be disposed to release the chunk borrow.
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
public readonly ref struct ComponentRef<T> where T : unmanaged, IComponent
{
    private readonly Chunk _chunk;
    private readonly int _offset;

    /// <summary>
    /// Creates a new component reference.
    /// </summary>
    /// <param name="chunk">The borrowed chunk containing the component.</param>
    /// <param name="offset">The byte offset to the component data.</param>
    internal ComponentRef(Chunk chunk, int offset)
    {
        _chunk = chunk;
        _offset = offset;
    }

    /// <summary>
    /// Gets a reference to the component value.
    /// </summary>
    public ref T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _chunk.GetSpan<T>(_offset, 1)[0];
    }

    /// <summary>
    /// Releases the chunk borrow.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _chunk.Dispose();
}
