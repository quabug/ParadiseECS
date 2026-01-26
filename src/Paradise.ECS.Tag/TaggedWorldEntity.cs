using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A general-purpose query data type providing dynamic component and tag access.
/// Use this with <see cref="QueryBuilder{TMask}"/> for ad-hoc queries on TaggedWorld without defining a [Queryable] type.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
/// <typeparam name="TEntityTags">The entity tags component type.</typeparam>
/// <typeparam name="TTagMask">The tag mask type implementing IBitSet.</typeparam>
public readonly ref struct TaggedWorldEntity<TMask, TConfig, TEntityTags, TTagMask>
    : IQueryData<TaggedWorldEntity<TMask, TConfig, TEntityTags, TTagMask>, TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
    where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    private readonly ChunkManager _chunkManager;
    private readonly ImmutableArchetypeLayout<TMask, TConfig> _layout;
    private readonly ChunkHandle _chunk;
    private readonly int _indexInChunk;
    private readonly Entity _entity;

    /// <summary>Creates a new TaggedWorldEntity instance. Required by IQueryData interface.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaggedWorldEntity<TMask, TConfig, TEntityTags, TTagMask> Create(
        ChunkManager chunkManager,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        ChunkHandle chunk,
        int indexInChunk,
        Entity entity)
        => new(chunkManager, layout, chunk, indexInChunk, entity);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TaggedWorldEntity(
        ChunkManager chunkManager,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        ChunkHandle chunk,
        int indexInChunk,
        Entity entity)
    {
        _chunkManager = chunkManager;
        _layout = layout;
        _chunk = chunk;
        _indexInChunk = indexInChunk;
        _entity = entity;
    }

    /// <summary>
    /// Gets the entity.
    /// </summary>
    public Entity Entity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _entity;
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
    {
        return _layout.HasComponent(T.TypeId);
    }

    /// <summary>
    /// Checks if this entity has a specific tag.
    /// </summary>
    /// <typeparam name="TTag">The tag type.</typeparam>
    /// <returns>True if the entity has the tag.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasTag<TTag>() where TTag : ITag
    {
        ref readonly var tags = ref Get<TEntityTags>();
        return tags.Mask.Get(TTag.TagId);
    }

    /// <summary>
    /// Sets or clears a specific tag on this entity.
    /// </summary>
    /// <typeparam name="TTag">The tag type.</typeparam>
    /// <param name="value">True to set the tag, false to clear it.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetTag<TTag>(bool value) where TTag : ITag
    {
        ref var tags = ref Get<TEntityTags>();
        tags.Mask = value ? tags.Mask.Set(TTag.TagId) : tags.Mask.Clear(TTag.TagId);
    }

    /// <summary>
    /// Gets the tag mask for this entity.
    /// </summary>
    public TTagMask TagMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Get<TEntityTags>().Mask;
    }

    /// <summary>
    /// Implicitly converts a TaggedWorldEntity to its underlying Entity.
    /// </summary>
    /// <param name="taggedWorldEntity">The TaggedWorldEntity to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Entity(TaggedWorldEntity<TMask, TConfig, TEntityTags, TTagMask> taggedWorldEntity)
        => taggedWorldEntity._entity;
}

/// <summary>
/// A general-purpose chunk data type providing dynamic span-based component and tag access.
/// Use this with <see cref="QueryBuilder{TMask}"/> for ad-hoc chunk queries on TaggedWorld without defining a [Queryable] type.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
/// <typeparam name="TEntityTags">The entity tags component type.</typeparam>
/// <typeparam name="TTagMask">The tag mask type implementing IBitSet.</typeparam>
public readonly ref struct TaggedWorldEntityChunk<TMask, TConfig, TEntityTags, TTagMask>
    : IQueryChunkData<TaggedWorldEntityChunk<TMask, TConfig, TEntityTags, TTagMask>, TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
    where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    private readonly ChunkManager _chunkManager;
    private readonly IEntityManager _entityManager;
    private readonly ImmutableArchetypeLayout<TMask, TConfig> _layout;
    private readonly ChunkHandle _chunk;
    private readonly int _entityCount;

    /// <summary>Creates a new TaggedWorldEntityChunk instance. Required by IQueryChunkData interface.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaggedWorldEntityChunk<TMask, TConfig, TEntityTags, TTagMask> Create(
        ChunkManager chunkManager,
        IEntityManager entityManager,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        ChunkHandle chunk,
        int entityCount)
        => new(chunkManager, entityManager, layout, chunk, entityCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TaggedWorldEntityChunk(
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

    /// <summary>
    /// Gets the entity ID at a specific index within this chunk.
    /// </summary>
    /// <param name="index">The index within this chunk.</param>
    /// <returns>The entity ID at the specified index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetEntityIdAt(int index)
    {
        var bytes = _chunkManager.GetBytes(_chunk);
        int offset = ImmutableArchetypeLayout<TMask, TConfig>.GetEntityIdOffset(index);
        return TConfig.EntityIdByteSize switch
        {
            1 => bytes.GetRef<byte>(offset),
            2 => bytes.GetRef<ushort>(offset),
            4 => bytes.GetRef<int>(offset),
            _ => ThrowHelper.ThrowInvalidEntityIdByteSize<int>(TConfig.EntityIdByteSize)
        };
    }

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

    /// <summary>
    /// Gets a span over all entity tag masks in this chunk.
    /// </summary>
    public Span<TEntityTags> TagMasks
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Get<TEntityTags>();
    }
}

/// <summary>
/// Extension methods for <see cref="QueryBuilder{TMask}"/> to support <see cref="TaggedWorld{TMask, TConfig, TEntityTags, TTagMask}"/>.
/// </summary>
public static class TaggedWorldQueryBuilderExtensions
{
    /// <summary>
    /// Builds a query result for entity-level iteration using <see cref="TaggedWorldEntity{TMask, TConfig, TEntityTags, TTagMask}"/>.
    /// </summary>
    /// <typeparam name="TMask">The component mask type.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <typeparam name="TEntityTags">The entity tags component type.</typeparam>
    /// <typeparam name="TTagMask">The tag mask type.</typeparam>
    /// <param name="builder">The query builder.</param>
    /// <param name="world">The tagged world to query.</param>
    /// <returns>A query result for iterating over entities with dynamic component and tag access.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueryResult<TaggedWorldEntity<TMask, TConfig, TEntityTags, TTagMask>, Archetype<TMask, TConfig>, TMask, TConfig>
        Build<TMask, TConfig, TEntityTags, TTagMask>(
            this QueryBuilder<TMask> builder,
            TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> world)
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
        where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
        where TTagMask : unmanaged, IBitSet<TTagMask>
    {
        var query = world.ArchetypeRegistry.GetOrCreateQuery((HashedKey<ImmutableQueryDescription<TMask>>)builder.Description);
        return new QueryResult<TaggedWorldEntity<TMask, TConfig, TEntityTags, TTagMask>, Archetype<TMask, TConfig>, TMask, TConfig>(
            world.ChunkManager, world.EntityManager, query);
    }

    /// <summary>
    /// Builds a chunk query result for batch processing using <see cref="TaggedWorldEntityChunk{TMask, TConfig, TEntityTags, TTagMask}"/>.
    /// </summary>
    /// <typeparam name="TMask">The component mask type.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <typeparam name="TEntityTags">The entity tags component type.</typeparam>
    /// <typeparam name="TTagMask">The tag mask type.</typeparam>
    /// <param name="builder">The query builder.</param>
    /// <param name="world">The tagged world to query.</param>
    /// <returns>A chunk query result for iterating over chunks with dynamic component and tag access.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChunkQueryResult<TaggedWorldEntityChunk<TMask, TConfig, TEntityTags, TTagMask>, Archetype<TMask, TConfig>, TMask, TConfig>
        BuildChunk<TMask, TConfig, TEntityTags, TTagMask>(
            this QueryBuilder<TMask> builder,
            TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> world)
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
        where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
        where TTagMask : unmanaged, IBitSet<TTagMask>
    {
        var query = world.ArchetypeRegistry.GetOrCreateQuery((HashedKey<ImmutableQueryDescription<TMask>>)builder.Description);
        return new ChunkQueryResult<TaggedWorldEntityChunk<TMask, TConfig, TEntityTags, TTagMask>, Archetype<TMask, TConfig>, TMask, TConfig>(
            world.ChunkManager, world.EntityManager, query);
    }
}
