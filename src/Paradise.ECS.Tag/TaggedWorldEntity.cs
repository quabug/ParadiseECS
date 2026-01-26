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
    private readonly WorldEntity<TMask, TConfig> _entity;

    /// <summary>Creates a new TaggedWorldEntity instance. Required by IQueryData interface.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaggedWorldEntity<TMask, TConfig, TEntityTags, TTagMask> Create(
        ChunkManager chunkManager,
        IEntityManager entityManager,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        ChunkHandle chunk,
        int indexInChunk)
        => new(WorldEntity<TMask, TConfig>.Create(chunkManager, entityManager, layout, chunk, indexInChunk));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TaggedWorldEntity(WorldEntity<TMask, TConfig> entity)
    {
        _entity = entity;
    }

    /// <summary>Creates a TaggedWorldEntity from a WorldEntity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TaggedWorldEntity<TMask, TConfig, TEntityTags, TTagMask> FromWorldEntity(WorldEntity<TMask, TConfig> entity)
        => new(entity);

    /// <summary>
    /// Gets the entity.
    /// </summary>
    public Entity Entity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _entity.Entity;
    }

    /// <summary>
    /// Gets a reference to a component on this entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>A reference to the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get<T>() where T : unmanaged, IComponent
        => ref _entity.Get<T>();

    /// <summary>
    /// Checks if this entity has a specific component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>True if the entity has the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>() where T : unmanaged, IComponent
        => _entity.Has<T>();

    /// <summary>
    /// Checks if this entity has a specific tag.
    /// </summary>
    /// <typeparam name="TTag">The tag type.</typeparam>
    /// <returns>True if the entity has the tag.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasTag<TTag>() where TTag : ITag
        => Get<TEntityTags>().Mask.Get(TTag.TagId);

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
        => taggedWorldEntity.Entity;
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
    private readonly WorldEntityChunk<TMask, TConfig> _chunk;

    /// <summary>Creates a new TaggedWorldEntityChunk instance. Required by IQueryChunkData interface.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaggedWorldEntityChunk<TMask, TConfig, TEntityTags, TTagMask> Create(
        ChunkManager chunkManager,
        IEntityManager entityManager,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        ChunkHandle chunk,
        int entityCount)
        => new(WorldEntityChunk<TMask, TConfig>.Create(chunkManager, entityManager, layout, chunk, entityCount));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TaggedWorldEntityChunk(WorldEntityChunk<TMask, TConfig> chunk)
    {
        _chunk = chunk;
    }

    /// <summary>Gets the number of entities in this chunk.</summary>
    public int EntityCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunk.EntityCount;
    }

    /// <summary>
    /// Gets a TaggedWorldEntity at the specified index within this chunk.
    /// </summary>
    /// <param name="index">The index within this chunk.</param>
    /// <returns>A TaggedWorldEntity providing access to the entity at the specified index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedWorldEntity<TMask, TConfig, TEntityTags, TTagMask> GetEntityAt(int index)
        => TaggedWorldEntity<TMask, TConfig, TEntityTags, TTagMask>.FromWorldEntity(_chunk.GetEntityAt(index));

    /// <summary>
    /// Gets a span over all components of type T in this chunk.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>A span over the components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> Get<T>() where T : unmanaged, IComponent
        => _chunk.Get<T>();

    /// <summary>
    /// Checks if this chunk's archetype has a specific component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>True if the archetype has the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>() where T : unmanaged, IComponent
        => _chunk.Has<T>();

    /// <summary>
    /// Gets a span over all entity tag masks in this chunk.
    /// </summary>
    public Span<TEntityTags> TagMasks
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Get<TEntityTags>();
    }

    /// <summary>Gets the chunk handle.</summary>
    public ChunkHandle Handle
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunk.Handle;
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
