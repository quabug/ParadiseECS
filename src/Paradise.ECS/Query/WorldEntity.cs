using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A general-purpose query data type providing dynamic component access.
/// Use this with <see cref="QueryBuilder{TMask}"/> for ad-hoc queries without defining a [Queryable] type.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public readonly ref struct WorldEntity<TMask, TConfig>
    : IQueryData<WorldEntity<TMask, TConfig>, TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    private readonly ChunkManager _chunkManager;
    private readonly IEntityManager _entityManager;
    private readonly ImmutableArchetypeLayout<TMask, TConfig> _layout;
    private readonly ChunkHandle _chunk;
    private readonly int _indexInChunk;

    /// <summary>Creates a new WorldEntity instance. Required by IQueryData interface.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WorldEntity<TMask, TConfig> Create(
        ChunkManager chunkManager,
        IEntityManager entityManager,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        ChunkHandle chunk,
        int indexInChunk)
        => new(chunkManager, entityManager, layout, chunk, indexInChunk);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private WorldEntity(
        ChunkManager chunkManager,
        IEntityManager entityManager,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        ChunkHandle chunk,
        int indexInChunk)
    {
        _chunkManager = chunkManager;
        _entityManager = entityManager;
        _layout = layout;
        _chunk = chunk;
        _indexInChunk = indexInChunk;
    }

    /// <summary>
    /// Gets the entity.
    /// </summary>
    public Entity Entity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var bytes = _chunkManager.GetBytes(_chunk);
            int offset = ImmutableArchetypeLayout<TMask, TConfig>.GetEntityIdOffset(_indexInChunk);
            int entityId = TConfig.EntityIdByteSize switch
            {
                1 => bytes.GetRef<byte>(offset),
                2 => bytes.GetRef<ushort>(offset),
                4 => bytes.GetRef<int>(offset),
                _ => ThrowHelper.ThrowInvalidEntityIdByteSize<int>(TConfig.EntityIdByteSize)
            };
            var location = _entityManager.GetLocation(entityId);
            return new Entity(entityId, location.Version);
        }
    }

    /// <summary>
    /// Gets a reference to a component on this entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>A reference to the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get<T>() where T : unmanaged, IComponent
    {
        int offset = _layout.GetBaseOffset(T.TypeId) + _indexInChunk * T.Size;
        return ref _chunkManager.GetBytes(_chunk).GetRef<T>(offset);
    }

    /// <summary>
    /// Checks if this entity has a specific component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>True if the entity has the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>() where T : unmanaged, IComponent
        => _layout.HasComponent(T.TypeId);

    /// <summary>
    /// Implicitly converts a WorldEntity to its underlying Entity.
    /// </summary>
    /// <param name="worldEntity">The WorldEntity to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Entity(WorldEntity<TMask, TConfig> worldEntity) => worldEntity.Entity;
}

/// <summary>
/// A general-purpose chunk data type providing dynamic span-based component access.
/// Use this with <see cref="QueryBuilder{TMask}"/> for ad-hoc chunk queries without defining a [Queryable] type.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public readonly ref struct WorldEntityChunk<TMask, TConfig>
    : IQueryChunkData<WorldEntityChunk<TMask, TConfig>, TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    private readonly ChunkManager _chunkManager;
    private readonly IEntityManager _entityManager;
    private readonly ImmutableArchetypeLayout<TMask, TConfig> _layout;
    private readonly ChunkHandle _chunk;
    private readonly int _entityCount;

    /// <summary>Creates a new WorldEntityChunk instance. Required by IQueryChunkData interface.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WorldEntityChunk<TMask, TConfig> Create(
        ChunkManager chunkManager,
        IEntityManager entityManager,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        ChunkHandle chunk,
        int entityCount)
        => new(chunkManager, entityManager, layout, chunk, entityCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal WorldEntityChunk(
        ChunkManager chunkManager,
        IEntityManager entityManager,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        ChunkHandle chunk,
        int entityCount)
    {
        _chunkManager = chunkManager;
        _entityManager = entityManager;
        _layout = layout;
        _chunk = chunk;
        _entityCount = entityCount;
    }

    /// <summary>Gets the number of entities in this chunk.</summary>
    public int EntityCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _entityCount;
    }

    /// <summary>Gets the chunk handle.</summary>
    public ChunkHandle Handle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunk;
    }

    /// <summary>
    /// Gets a WorldEntity at the specified index within this chunk.
    /// </summary>
    /// <param name="index">The index within this chunk.</param>
    /// <returns>A WorldEntity providing access to the entity at the specified index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WorldEntity<TMask, TConfig> GetEntityAt(int index)
        => WorldEntity<TMask, TConfig>.Create(_chunkManager, _entityManager, _layout, _chunk, index);

    /// <summary>
    /// Gets a span over all components of type T in this chunk.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>A span over the components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> Get<T>() where T : unmanaged, IComponent
    {
        int baseOffset = _layout.GetBaseOffset(T.TypeId);
        return _chunkManager.GetBytes(_chunk).GetSpan<T>(baseOffset, _entityCount);
    }

    /// <summary>
    /// Checks if this chunk's archetype has a specific component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>True if the archetype has the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>() where T : unmanaged, IComponent
    {
        return _layout.HasComponent(T.TypeId);
    }
}

/// <summary>
/// Extension methods for <see cref="QueryBuilder{TMask}"/> to build queries returning <see cref="WorldEntity{TMask, TConfig}"/>.
/// </summary>
public static class WorldEntityQueryBuilderExtensions
{
    /// <summary>
    /// Builds a query result for entity-level iteration using <see cref="WorldEntity{TMask, TConfig}"/>.
    /// </summary>
    /// <typeparam name="TMask">The component mask type.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <param name="builder">The query builder.</param>
    /// <param name="world">The world to query.</param>
    /// <returns>A query result for iterating over entities with dynamic component access.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueryResult<WorldEntity<TMask, TConfig>, Archetype<TMask, TConfig>, TMask, TConfig>
        Build<TMask, TConfig>(this QueryBuilder<TMask> builder, World<TMask, TConfig> world)
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
    {
        var query = world.ArchetypeRegistry.GetOrCreateQuery((HashedKey<ImmutableQueryDescription<TMask>>)builder.Description);
        return new QueryResult<WorldEntity<TMask, TConfig>, Archetype<TMask, TConfig>, TMask, TConfig>(
            world.ChunkManager, world.EntityManager, query);
    }

    /// <summary>
    /// Builds a chunk query result for batch processing using <see cref="WorldEntityChunk{TMask, TConfig}"/>.
    /// </summary>
    /// <typeparam name="TMask">The component mask type.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <param name="builder">The query builder.</param>
    /// <param name="world">The world to query.</param>
    /// <returns>A chunk query result for iterating over chunks with dynamic component access.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChunkQueryResult<WorldEntityChunk<TMask, TConfig>, Archetype<TMask, TConfig>, TMask, TConfig>
        BuildChunk<TMask, TConfig>(this QueryBuilder<TMask> builder, World<TMask, TConfig> world)
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
    {
        var query = world.ArchetypeRegistry.GetOrCreateQuery((HashedKey<ImmutableQueryDescription<TMask>>)builder.Description);
        return new ChunkQueryResult<WorldEntityChunk<TMask, TConfig>, Archetype<TMask, TConfig>, TMask, TConfig>(
            world.ChunkManager, world.EntityManager, query);
    }
}
