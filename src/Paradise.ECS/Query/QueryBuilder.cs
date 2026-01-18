using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A fluent, immutable builder for creating query descriptions.
/// This is a <c>ref struct</c> to ensure stack allocation and avoid heap allocations during query construction.
/// Each method returns a new builder instance, allowing for safe and efficient branching of query definitions.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
public readonly ref struct QueryBuilder<TBits> where TBits : unmanaged, IStorage
{
    private readonly ImmutableQueryDescription<TBits> _description;

    /// <summary>
    /// Creates a new query builder with the specified description.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private QueryBuilder(ImmutableQueryDescription<TBits> description)
    {
        _description = description;
    }

    /// <summary>
    /// Creates a new empty query builder.
    /// </summary>
    /// <returns>A new query builder with no constraints.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueryBuilder<TBits> Create() => default;

    /// <summary>
    /// Adds a required component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must be present.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits> With<T>() where T : unmanaged, IComponent
    {
        return With(T.TypeId);
    }

    /// <summary>
    /// Adds a required component constraint by component ID.
    /// </summary>
    /// <param name="componentId">The component ID that must be present.</param>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits> With(int componentId)
    {
        return new(_description with { All = _description.All.Set(componentId) });
    }

    /// <summary>
    /// Adds an excluded component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must not be present.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits> Without<T>() where T : unmanaged, IComponent
    {
        return Without(T.TypeId);
    }

    /// <summary>
    /// Adds an excluded component constraint by component ID.
    /// </summary>
    /// <param name="componentId">The component ID that must not be present.</param>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits> Without(int componentId)
    {
        return new(_description with { None = _description.None.Set(componentId) });
    }

    /// <summary>
    /// Adds an "any of" component constraint.
    /// </summary>
    /// <typeparam name="T">The component type to add to the any-of set.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits> WithAny<T>() where T : unmanaged, IComponent
    {
        return WithAny(T.TypeId);
    }

    /// <summary>
    /// Adds an "any of" component constraint by component ID.
    /// </summary>
    /// <param name="componentId">The component ID to add to the any-of set.</param>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits> WithAny(int componentId)
    {
        return new(_description with { Any = _description.Any.Set(componentId) });
    }

    /// <summary>
    /// Gets the immutable query description.
    /// </summary>
    public ImmutableQueryDescription<TBits> Description
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _description;
    }

    /// <summary>
    /// Builds a query from this description.
    /// </summary>
    /// <typeparam name="TRegistry">The component registry type.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <typeparam name="TArchetype">The concrete archetype type.</typeparam>
    /// <param name="archetypeRegistry">The archetype registry to query.</param>
    /// <returns>A cached query that matches archetypes based on this description.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query<TBits, TRegistry, TConfig, TArchetype> Build<TRegistry, TConfig, TArchetype>(
        IArchetypeRegistry<TBits, TRegistry, TConfig, TArchetype> archetypeRegistry)
        where TRegistry : IComponentRegistry
        where TConfig : IConfig, new()
        where TArchetype : class, IArchetype<TBits, TRegistry, TConfig>
        => archetypeRegistry.GetOrCreateQuery((HashedKey<ImmutableQueryDescription<TBits>>)_description);

    /// <summary>
    /// Builds a WorldQuery from this description, enabling Entity enumeration.
    /// </summary>
    /// <typeparam name="TRegistry">The component registry type.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <param name="world">The world to query.</param>
    /// <returns>A WorldQuery that iterates over Entity handles.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WorldQuery<TBits, TRegistry, TConfig> Build<TRegistry, TConfig>(
        World<TBits, TRegistry, TConfig> world)
        where TRegistry : IComponentRegistry
        where TConfig : IConfig, new()
    {
        var query = world.Registry.GetOrCreateQuery((HashedKey<ImmutableQueryDescription<TBits>>)_description);
        return new WorldQuery<TBits, TRegistry, TConfig>(world, query);
    }

    /// <summary>
    /// Implicit conversion to immutable query description.
    /// </summary>
    public static implicit operator ImmutableQueryDescription<TBits>(QueryBuilder<TBits> builder) => builder._description;
}
