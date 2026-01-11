using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A read-only view into a chunk for efficient component iteration.
/// This is a ref struct that provides zero-copy access to component data.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
public readonly ref struct ChunkView<TBits, TRegistry>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private readonly Chunk _chunk;
    private readonly ImmutableArchetypeLayout<TBits, TRegistry> _layout;
    private readonly int _entityCount;

    /// <summary>
    /// Gets the number of entities in this chunk.
    /// </summary>
    public int EntityCount => _entityCount;

    /// <summary>
    /// Gets the archetype layout for this chunk.
    /// </summary>
    public ImmutableArchetypeLayout<TBits, TRegistry> Layout => _layout;

    /// <summary>
    /// Creates a new chunk view.
    /// </summary>
    /// <param name="chunk">The chunk to view.</param>
    /// <param name="layout">The archetype layout describing the chunk structure.</param>
    /// <param name="entityCount">The number of entities in this chunk.</param>
    internal ChunkView(Chunk chunk, ImmutableArchetypeLayout<TBits, TRegistry> layout, int entityCount)
    {
        _chunk = chunk;
        _layout = layout;
        _entityCount = entityCount;
    }

    /// <summary>
    /// Gets a span of component data for a specific component type.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>A span over the component data for all entities in this chunk.</returns>
    /// <exception cref="InvalidOperationException">If the chunk doesn't contain the component.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan<T>() where T : unmanaged, IComponent
    {
        int baseOffset = _layout.GetBaseOffset<T>();
        if (baseOffset < 0)
            throw new InvalidOperationException($"Chunk does not contain component {typeof(T).Name}");

        return _chunk.GetSpan<T>(baseOffset, _entityCount);
    }

    /// <summary>
    /// Tries to get a span of component data for a specific component type.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="span">The component span if found.</param>
    /// <returns>True if the component was found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetSpan<T>(out Span<T> span) where T : unmanaged, IComponent
    {
        int baseOffset = _layout.GetBaseOffset<T>();
        if (baseOffset < 0)
        {
            span = default;
            return false;
        }

        span = _chunk.GetSpan<T>(baseOffset, _entityCount);
        return true;
    }

    /// <summary>
    /// Gets a read-only span of entities in this chunk.
    /// </summary>
    /// <returns>A span over the entities in this chunk.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<Entity> GetEntities()
    {
        int offset = _layout.EntityColumnOffset;
        return _chunk.GetSpan<Entity>(offset, _entityCount);
    }

    /// <summary>
    /// Gets an entity at a specific index within the chunk.
    /// </summary>
    /// <param name="index">The index within the chunk.</param>
    /// <returns>The entity at the specified index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity GetEntity(int index)
    {
        int offset = _layout.GetEntityOffset(index);
        return _chunk.GetRefAt<Entity>(offset);
    }

    /// <summary>
    /// Checks if this chunk contains the specified component type.
    /// </summary>
    /// <typeparam name="T">The component type to check.</typeparam>
    /// <returns>True if the component is present.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasComponent<T>() where T : unmanaged, IComponent
    {
        return _layout.HasComponent<T>();
    }
}
