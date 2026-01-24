using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A fluent, immutable builder for creating tag-filtered queries.
/// Wraps a <see cref="QueryBuilder{TMask}"/> with an additional tag mask for entity-level filtering.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TTagMask">The tag mask type.</typeparam>
public readonly ref struct TaggedQueryBuilder<TMask, TTagMask>
    where TMask : unmanaged, IBitSet<TMask>
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    private readonly QueryBuilder<TMask> _queryBuilder;
    private readonly TTagMask _requiredTags;

    /// <summary>
    /// Creates a new tagged query builder wrapping the specified query builder.
    /// </summary>
    /// <param name="queryBuilder">The underlying query builder.</param>
    /// <param name="requiredTags">The required tag mask.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TaggedQueryBuilder(QueryBuilder<TMask> queryBuilder, TTagMask requiredTags)
    {
        _queryBuilder = queryBuilder;
        _requiredTags = requiredTags;
    }

    /// <summary>
    /// Gets the underlying query builder.
    /// </summary>
    public QueryBuilder<TMask> QueryBuilder
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
    public TaggedQueryBuilder<TMask, TTagMask> With<T>() where T : unmanaged, IComponent
        => new(_queryBuilder.With<T>(), _requiredTags);

    /// <summary>
    /// Adds an excluded component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must not be present.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedQueryBuilder<TMask, TTagMask> Without<T>() where T : unmanaged, IComponent
        => new(_queryBuilder.Without<T>(), _requiredTags);

    /// <summary>
    /// Adds an "any of" component constraint.
    /// </summary>
    /// <typeparam name="T">The component type to add to the any-of set.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedQueryBuilder<TMask, TTagMask> WithAny<T>() where T : unmanaged, IComponent
        => new(_queryBuilder.WithAny<T>(), _requiredTags);

    /// <summary>
    /// Adds an additional required tag constraint.
    /// </summary>
    /// <typeparam name="TTag">The tag type that must be present.</typeparam>
    /// <returns>A new builder with the added tag constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedQueryBuilder<TMask, TTagMask> WithTag<TTag>() where TTag : ITag
        => new(_queryBuilder, _requiredTags.Set(TTag.TagId));

    /// <summary>
    /// Builds a TaggedWorldQuery from this builder for a TaggedWorld.
    /// The resulting query filters entities by both component constraints and tag constraints.
    /// </summary>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
    /// <param name="taggedWorld">The tagged world to query.</param>
    /// <returns>A query that iterates entities matching both component and tag constraints.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TaggedWorldQuery<TMask, TConfig, TEntityTags, TTagMask> Build<TConfig, TEntityTags>(
        TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> taggedWorld)
        where TConfig : IConfig, new()
        where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
    {
        var query = _queryBuilder.Build(taggedWorld.World);
        return new TaggedWorldQuery<TMask, TConfig, TEntityTags, TTagMask>(
            taggedWorld,
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
    /// Requires explicit type parameters for TMask, TTag, and TTagMask.
    /// </summary>
    /// <param name="builder">The query builder to extend.</param>
    /// <typeparam name="TTag">The tag type that must be present.</typeparam>
    /// <typeparam name="TTagMask">The tag mask type.</typeparam>
    /// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
    /// <returns>A new TaggedQueryBuilder with the tag constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TaggedQueryBuilder<TMask, TTagMask> WithTag<TMask, TTag, TTagMask>(this QueryBuilder<TMask> builder)
        where TMask : unmanaged, IBitSet<TMask>
        where TTag : ITag
        where TTagMask : unmanaged, IBitSet<TTagMask>
    {
        var mask = default(TTagMask).Set(TTag.TagId);
        return new TaggedQueryBuilder<TMask, TTagMask>(builder, mask);
    }
}
