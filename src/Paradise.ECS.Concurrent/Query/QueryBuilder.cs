using System.Runtime.CompilerServices;

namespace Paradise.ECS.Concurrent;

/// <summary>
/// A fluent, immutable builder for creating query descriptions.
/// This is a <c>ref struct</c> to ensure stack allocation and avoid heap allocations during query construction.
/// Each method returns a new builder instance, allowing for safe and efficient branching of query definitions.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
/// <typeparam name="TConfig">The world configuration type that determines chunk size and limits.</typeparam>
public readonly ref struct QueryBuilder<TBits, TRegistry, TConfig>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
    where TConfig : IConfig
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
    /// Adds a required component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must be present.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry, TConfig> With<T>() where T : unmanaged, IComponent
    {
        return With(T.TypeId);
    }

    /// <summary>
    /// Adds a required component constraint by component ID.
    /// </summary>
    /// <param name="componentId">The component ID that must be present.</param>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry, TConfig> With(int componentId)
    {
        return new(_description with { All = _description.All.Set(componentId) });
    }

    /// <summary>
    /// Adds a required component constraint by Type.
    /// </summary>
    /// <param name="type">The component type that must be present.</param>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry, TConfig> With(Type type)
    {
        var id = TRegistry.GetId(type);
        ThrowHelper.ThrowIfInvalidComponentId(id);
        return With(id);
    }

    /// <summary>
    /// Adds an excluded component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must not be present.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry, TConfig> Without<T>() where T : unmanaged, IComponent
    {
        return Without(T.TypeId);
    }

    /// <summary>
    /// Adds an excluded component constraint by component ID.
    /// </summary>
    /// <param name="componentId">The component ID that must not be present.</param>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry, TConfig> Without(int componentId)
    {
        return new(_description with { None = _description.None.Set(componentId) });
    }

    /// <summary>
    /// Adds an excluded component constraint by Type.
    /// </summary>
    /// <param name="type">The component type that must not be present.</param>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry, TConfig> Without(Type type)
    {
        var id = TRegistry.GetId(type);
        ThrowHelper.ThrowIfInvalidComponentId(id);
        return Without(id);
    }

    /// <summary>
    /// Adds an "any of" component constraint.
    /// </summary>
    /// <typeparam name="T">The component type to add to the any-of set.</typeparam>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry, TConfig> WithAny<T>() where T : unmanaged, IComponent
    {
        return WithAny(T.TypeId);
    }

    /// <summary>
    /// Adds an "any of" component constraint by component ID.
    /// </summary>
    /// <param name="componentId">The component ID to add to the any-of set.</param>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry, TConfig> WithAny(int componentId)
    {
        return new(_description with { Any = _description.Any.Set(componentId) });
    }

    /// <summary>
    /// Adds an "any of" component constraint by Type.
    /// </summary>
    /// <param name="type">The component type to add to the any-of set.</param>
    /// <returns>A new builder with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry, TConfig> WithAny(Type type)
    {
        var id = TRegistry.GetId(type);
        ThrowHelper.ThrowIfInvalidComponentId(id);
        return WithAny(id);
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
    /// <param name="archetypeRegistry">The archetype registry to query.</param>
    /// <returns>A cached query that matches archetypes based on this description.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Query<TBits, TRegistry, TConfig> Build(ArchetypeRegistry<TBits, TRegistry, TConfig> archetypeRegistry)
        => archetypeRegistry.GetOrCreateQuery((HashedKey<ImmutableQueryDescription<TBits>>)_description);

    /// <summary>
    /// Implicit conversion to immutable query description.
    /// </summary>
    public static implicit operator ImmutableQueryDescription<TBits>(QueryBuilder<TBits, TRegistry, TConfig> builder)
        => builder._description;
}
