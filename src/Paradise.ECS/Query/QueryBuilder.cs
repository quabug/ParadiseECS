using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Mutable builder for creating query descriptions.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
public ref struct QueryBuilder<TBits, TRegistry>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private ImmutableQueryDescription<TBits> _description;

    /// <summary>
    /// Adds a required component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must be present.</typeparam>
    /// <returns>This builder for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry> With<T>() where T : unmanaged, IComponent
    {
        With(T.TypeId);
        return this;
    }

    /// <summary>
    /// Adds a required component constraint by component ID.
    /// </summary>
    /// <param name="componentId">The component ID that must be present.</param>
    /// <returns>This builder for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry> With(int componentId)
    {
        _description = _description with { All = _description.All.Set(componentId) };
        return this;
    }

    /// <summary>
    /// Adds a required component constraint by Type.
    /// </summary>
    /// <param name="type">The component type that must be present.</param>
    /// <returns>This builder for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry> With(Type type)
    {
        var id = TRegistry.GetId(type);
        ThrowHelper.ThrowIfInvalidComponentId(id);
        With(id);
        return this;
    }

    /// <summary>
    /// Adds an excluded component constraint.
    /// </summary>
    /// <typeparam name="T">The component type that must not be present.</typeparam>
    /// <returns>This builder for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry> Without<T>() where T : unmanaged, IComponent
    {
        Without(T.TypeId);
        return this;
    }

    /// <summary>
    /// Adds an excluded component constraint by component ID.
    /// </summary>
    /// <param name="componentId">The component ID that must not be present.</param>
    /// <returns>This builder for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry> Without(int componentId)
    {
        _description = _description with { None = _description.None.Set(componentId) };
        return this;
    }

    /// <summary>
    /// Adds an excluded component constraint by Type.
    /// </summary>
    /// <param name="type">The component type that must not be present.</param>
    /// <returns>This builder for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry> Without(Type type)
    {
        var id = TRegistry.GetId(type);
        ThrowHelper.ThrowIfInvalidComponentId(id);
        Without(id);
        return this;
    }

    /// <summary>
    /// Adds an "any of" component constraint.
    /// </summary>
    /// <typeparam name="T">The component type to add to the any-of set.</typeparam>
    /// <returns>This builder for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry> WithAny<T>() where T : unmanaged, IComponent
    {
        WithAny(T.TypeId);
        return this;
    }

    /// <summary>
    /// Adds an "any of" component constraint by component ID.
    /// </summary>
    /// <param name="componentId">The component ID to add to the any-of set.</param>
    /// <returns>This builder for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry> WithAny(int componentId)
    {
        _description = _description with { Any = _description.Any.Set(componentId) };
        return this;
    }

    /// <summary>
    /// Adds an "any of" component constraint by Type.
    /// </summary>
    /// <param name="type">The component type to add to the any-of set.</param>
    /// <returns>This builder for chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryBuilder<TBits, TRegistry> WithAny(Type type)
    {
        var id = TRegistry.GetId(type);
        ThrowHelper.ThrowIfInvalidComponentId(id);
        WithAny(id);
        return this;
    }

    /// <summary>
    /// Returns the immutable query description.
    /// </summary>
    /// <returns>The built immutable query description.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ImmutableQueryDescription<TBits> ToImmutable() => _description;

    /// <summary>
    /// Builds a query from this description.
    /// </summary>
    /// <param name="archetypeRegistry">The archetype registry to query.</param>
    /// <returns>A new query that matches archetypes based on this description.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Query<TBits, TRegistry> Build(ArchetypeRegistry<TBits, TRegistry> archetypeRegistry)
        => new(archetypeRegistry, _description);

    /// <summary>
    /// Implicit conversion to immutable query description.
    /// </summary>
    public static implicit operator ImmutableQueryDescription<TBits>(QueryBuilder<TBits, TRegistry> builder)
        => builder._description;
}
