using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A fluent, immutable builder for creating query descriptions.
/// This is a <c>ref struct</c> to ensure stack allocation and avoid heap allocations during query construction.
/// Each method returns a new builder instance, allowing for safe and efficient branching of query definitions.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
public readonly ref struct QueryBuilder<TMask> where TMask : unmanaged, IBitSet<TMask>
{
    private readonly ImmutableQueryDescription<TMask> _description;

    /// <summary>
    /// Creates a new query builder with the specified description.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private QueryBuilder(ImmutableQueryDescription<TMask> description)
    {
        _description = description;
    }

    /// <summary>
    /// Creates a new empty query builder.
    /// </summary>
    /// <returns>A new query builder with no constraints.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueryBuilder<TMask> Create() => new();

    /// <summary>
    /// Adds a required component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must be present.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TMask> With<T>() where T : unmanaged, IComponent
    {
        return With(T.TypeId);
    }

    /// <summary>
    /// Adds a required component constraint by component ID.
    /// </summary>
    /// <param name="componentId">The component ID that must be present.</param>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TMask> With(int componentId)
    {
        return new(_description with { All = _description.All.Set(componentId) });
    }

    /// <summary>
    /// Adds an excluded component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must not be present.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TMask> Without<T>() where T : unmanaged, IComponent
    {
        return Without(T.TypeId);
    }

    /// <summary>
    /// Adds an excluded component constraint by component ID.
    /// </summary>
    /// <param name="componentId">The component ID that must not be present.</param>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TMask> Without(int componentId)
    {
        return new(_description with { None = _description.None.Set(componentId) });
    }

    /// <summary>
    /// Adds an "any of" component constraint.
    /// </summary>
    /// <typeparam name="T">The component type to add to the any-of set.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TMask> WithAny<T>() where T : unmanaged, IComponent
    {
        return WithAny(T.TypeId);
    }

    /// <summary>
    /// Adds an "any of" component constraint by component ID.
    /// </summary>
    /// <param name="componentId">The component ID to add to the any-of set.</param>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TMask> WithAny(int componentId)
    {
        return new(_description with { Any = _description.Any.Set(componentId) });
    }

    /// <summary>
    /// Gets the immutable query description.
    /// </summary>
    public ImmutableQueryDescription<TMask> Description
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _description;
    }

    /// <summary>
    /// Builds a query from this description.
    /// </summary>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <typeparam name="TArchetype">The concrete archetype type.</typeparam>
    /// <param name="archetypeRegistry">The archetype registry to query.</param>
    /// <returns>A cached query that matches archetypes based on this description.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query<TMask, TConfig, TArchetype> Build<TConfig, TArchetype>(
        IArchetypeRegistry<TMask, TConfig, TArchetype> archetypeRegistry)
        where TConfig : IConfig, new()
        where TArchetype : class, IArchetype<TMask, TConfig>
        => archetypeRegistry.GetOrCreateQuery((HashedKey<ImmutableQueryDescription<TMask>>)_description);

    /// <summary>
    /// Implicit conversion to immutable query description.
    /// </summary>
    public static implicit operator ImmutableQueryDescription<TMask>(QueryBuilder<TMask> builder) => builder._description;
}
