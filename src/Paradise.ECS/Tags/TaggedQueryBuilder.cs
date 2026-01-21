using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A fluent, immutable builder for creating tag-filtered queries.
/// Wraps a <see cref="QueryBuilder{TBits}"/> with an additional tag mask for entity-level filtering.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TTagMask">The tag mask type.</typeparam>
public readonly ref struct TaggedQueryBuilder<TBits, TTagMask>
    where TBits : unmanaged, IStorage
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    private readonly QueryBuilder<TBits> _queryBuilder;
    private readonly TTagMask _requiredTags;

    /// <summary>
    /// Creates a new tagged query builder wrapping the specified query builder.
    /// </summary>
    /// <param name="queryBuilder">The underlying query builder.</param>
    /// <param name="requiredTags">The required tag mask.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TaggedQueryBuilder(QueryBuilder<TBits> queryBuilder, TTagMask requiredTags)
    {
        _queryBuilder = queryBuilder;
        _requiredTags = requiredTags;
    }

    /// <summary>
    /// Gets the underlying query builder.
    /// </summary>
    public QueryBuilder<TBits> QueryBuilder
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _queryBuilder;
    }

    /// <summary>
    /// Gets the required tag mask.
    /// </summary>
    public TTagMask RequiredTags
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _requiredTags;
    }

    /// <summary>
    /// Adds a required component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must be present.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedQueryBuilder<TBits, TTagMask> With<T>() where T : unmanaged, IComponent
        => new(_queryBuilder.With<T>(), _requiredTags);

    /// <summary>
    /// Adds an excluded component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must not be present.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedQueryBuilder<TBits, TTagMask> Without<T>() where T : unmanaged, IComponent
        => new(_queryBuilder.Without<T>(), _requiredTags);

    /// <summary>
    /// Adds an "any of" component constraint.
    /// </summary>
    /// <typeparam name="T">The component type to add to the any-of set.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedQueryBuilder<TBits, TTagMask> WithAny<T>() where T : unmanaged, IComponent
        => new(_queryBuilder.WithAny<T>(), _requiredTags);

    /// <summary>
    /// Adds an additional required tag constraint.
    /// </summary>
    /// <typeparam name="TTag">The tag type that must be present.</typeparam>
    /// <returns>A new builder with the added tag constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedQueryBuilder<TBits, TTagMask> WithTag<TTag>() where TTag : ITag
        => new(_queryBuilder, _requiredTags.Set(TTag.TagId));

    /// <summary>
    /// Builds a TaggedWorldQuery from this builder for a TaggedWorld.
    /// The resulting query filters entities by both component constraints and tag constraints.
    /// </summary>
    /// <typeparam name="TRegistry">The component registry type.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
    /// <param name="taggedWorld">The tagged world to query.</param>
    /// <returns>A query that iterates entities matching both component and tag constraints.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedWorldQuery<TBits, TRegistry, TConfig, TEntityTags, TTagMask> Build<TRegistry, TConfig, TEntityTags>(
        TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> taggedWorld)
        where TRegistry : IComponentRegistry
        where TConfig : IConfig, new()
        where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
    {
        var query = _queryBuilder.Build(taggedWorld.World);
        return new TaggedWorldQuery<TBits, TRegistry, TConfig, TEntityTags, TTagMask>(
            taggedWorld,
            query.Query,
            _requiredTags);
    }
}

/// <summary>
/// A query that filters entities by both component constraints and tag constraints.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
/// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
/// <typeparam name="TTagMask">The tag mask type.</typeparam>
public readonly struct TaggedWorldQuery<TBits, TRegistry, TConfig, TEntityTags, TTagMask>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
    where TConfig : IConfig, new()
    where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    private readonly TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> _taggedWorld;
    private readonly Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>> _query;
    private readonly TTagMask _requiredTags;

    /// <summary>
    /// Creates a new TaggedWorldQuery.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TaggedWorldQuery(
        TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> taggedWorld,
        Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>> query,
        TTagMask requiredTags)
    {
        _taggedWorld = taggedWorld;
        _query = query;
        _requiredTags = requiredTags;
    }

    /// <summary>
    /// Gets the underlying component query.
    /// </summary>
    public Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>> Query
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _query;
    }

    /// <summary>
    /// Gets the required tag mask.
    /// </summary>
    public TTagMask RequiredTags
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _requiredTags;
    }

    /// <summary>
    /// Returns an enumerator that iterates through entities matching both component and tag constraints.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_taggedWorld, _query, _requiredTags);

    /// <summary>
    /// Enumerator for iterating over tagged entities in the query.
    /// </summary>
    public ref struct Enumerator
    {
        private readonly TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> _taggedWorld;
        private readonly TTagMask _requiredTags;
        private Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>>.EntityIdEnumerator _inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(
            TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> taggedWorld,
            Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>> query,
            TTagMask requiredTags)
        {
            _taggedWorld = taggedWorld;
            _requiredTags = requiredTags;
            _inner = query.GetEnumerator();
        }

        /// <summary>
        /// Gets the current WorldEntity.
        /// </summary>
        public WorldEntity<TBits, TRegistry, TConfig> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_taggedWorld.World, _taggedWorld.World.GetEntity(_inner.Current));
        }

        /// <summary>
        /// Advances to the next entity that matches the tag constraints.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (_inner.MoveNext())
            {
                var entity = _taggedWorld.World.GetEntity(_inner.Current);
                var entityTags = _taggedWorld.GetTags(entity);
                if (entityTags.ContainsAll(_requiredTags))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

/// <summary>
/// A query builder bound to a specific TaggedWorld, enabling clean fluent API with minimal type parameters.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
/// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
/// <typeparam name="TTagMask">The tag mask type.</typeparam>
public readonly ref struct TaggedWorldQueryBuilder<TBits, TRegistry, TConfig, TEntityTags, TTagMask>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
    where TConfig : IConfig, new()
    where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    private readonly TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> _world;
    private readonly QueryBuilder<TBits> _queryBuilder;
    private readonly TTagMask _requiredTags;

    /// <summary>
    /// Creates a new query builder bound to the specified world.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TaggedWorldQueryBuilder(TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> world)
    {
        _world = world;
        _queryBuilder = QueryBuilder<TBits>.Create();
        _requiredTags = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TaggedWorldQueryBuilder(
        TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> world,
        QueryBuilder<TBits> queryBuilder,
        TTagMask requiredTags)
    {
        _world = world;
        _queryBuilder = queryBuilder;
        _requiredTags = requiredTags;
    }

    /// <summary>
    /// Adds a required component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must be present.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedWorldQueryBuilder<TBits, TRegistry, TConfig, TEntityTags, TTagMask> With<T>()
        where T : unmanaged, IComponent
        => new(_world, _queryBuilder.With<T>(), _requiredTags);

    /// <summary>
    /// Adds an excluded component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must not be present.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedWorldQueryBuilder<TBits, TRegistry, TConfig, TEntityTags, TTagMask> Without<T>()
        where T : unmanaged, IComponent
        => new(_world, _queryBuilder.Without<T>(), _requiredTags);

    /// <summary>
    /// Adds an "any of" component constraint.
    /// </summary>
    /// <typeparam name="T">The component type to add to the any-of set.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedWorldQueryBuilder<TBits, TRegistry, TConfig, TEntityTags, TTagMask> WithAny<T>()
        where T : unmanaged, IComponent
        => new(_world, _queryBuilder.WithAny<T>(), _requiredTags);

    /// <summary>
    /// Adds a required tag constraint.
    /// Only requires specifying the tag type - all other types are already bound to the world.
    /// </summary>
    /// <typeparam name="TTag">The tag type that must be present.</typeparam>
    /// <returns>A new builder with the added tag constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedWorldQueryBuilder<TBits, TRegistry, TConfig, TEntityTags, TTagMask> WithTag<TTag>()
        where TTag : ITag
        => new(_world, _queryBuilder, _requiredTags.Set(TTag.TagId));

    /// <summary>
    /// Builds and returns a TaggedWorldQuery that iterates entities matching all constraints.
    /// </summary>
    /// <returns>A query that iterates entities matching both component and tag constraints.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedWorldQuery<TBits, TRegistry, TConfig, TEntityTags, TTagMask> Build()
    {
        var query = _queryBuilder.Build(_world.World);
        return new TaggedWorldQuery<TBits, TRegistry, TConfig, TEntityTags, TTagMask>(
            _world,
            query.Query,
            _requiredTags);
    }
}

/// <summary>
/// Extension methods for creating TaggedQueryBuilder from QueryBuilder.
/// </summary>
public static class TaggedQueryBuilderExtensions
{
    /// <summary>
    /// Creates a TaggedQueryBuilder with the specified required tag.
    /// Requires explicit type parameters for TBits, TTag, and TTagMask.
    /// </summary>
    /// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
    /// <typeparam name="TTag">The tag type that must be present.</typeparam>
    /// <typeparam name="TTagMask">The tag mask type.</typeparam>
    /// <param name="builder">The query builder to extend.</param>
    /// <returns>A new TaggedQueryBuilder with the tag constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaggedQueryBuilder<TBits, TTagMask> WithTag<TBits, TTag, TTagMask>(this QueryBuilder<TBits> builder)
        where TBits : unmanaged, IStorage
        where TTag : ITag
        where TTagMask : unmanaged, IBitSet<TTagMask>
    {
        var mask = default(TTagMask).Set(TTag.TagId);
        return new TaggedQueryBuilder<TBits, TTagMask>(builder, mask);
    }

}
