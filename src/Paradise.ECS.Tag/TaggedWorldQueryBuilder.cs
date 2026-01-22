using System.Runtime.CompilerServices;

namespace Paradise.ECS;

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
