using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Describes which archetypes a query matches using component mask constraints.
/// Immutable after construction.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides Type-to-ComponentId mapping.</typeparam>
/// <param name="All">Components that must all be present (AND constraint).</param>
/// <param name="None">Components that must not be present (NOT constraint).</param>
/// <param name="Any">At least one of these components must be present (OR constraint). If empty, this constraint is ignored.</param>
public readonly record struct ImmutableQueryDescription<TBits, TRegistry>(
    ImmutableBitSet<TBits> All,
    ImmutableBitSet<TBits> None,
    ImmutableBitSet<TBits> Any)
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    /// <summary>
    /// Creates a query description that matches all archetypes containing all specified components.
    /// </summary>
    /// <param name="all">Components that must all be present.</param>
    public ImmutableQueryDescription(ImmutableBitSet<TBits> all)
        : this(all, ImmutableBitSet<TBits>.Empty, ImmutableBitSet<TBits>.Empty)
    {
    }

    /// <summary>
    /// Gets an empty query description that matches all archetypes.
    /// </summary>
    public static ImmutableQueryDescription<TBits, TRegistry> Empty => default;

    /// <summary>
    /// Checks if an archetype matches this query's constraints.
    /// </summary>
    /// <param name="archetypeMask">The archetype's component mask.</param>
    /// <returns>True if the archetype matches all constraints.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Matches(ImmutableBitSet<TBits> archetypeMask)
    {
        // Must contain all required components
        if (!archetypeMask.ContainsAll(All))
            return false;

        // Must not contain any excluded components
        if (!archetypeMask.ContainsNone(None))
            return false;

        // If Any constraint exists, must contain at least one
        if (!Any.IsEmpty && !archetypeMask.ContainsAny(Any))
            return false;

        return true;
    }

    /// <summary>
    /// Creates a new description with an additional required component by ComponentId.
    /// </summary>
    /// <param name="componentId">The component ID to require.</param>
    /// <returns>A new query description with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableQueryDescription<TBits, TRegistry> With(int componentId)
    {
        return this with { All = All.Set(componentId) };
    }

    /// <summary>
    /// Creates a new description with an additional required component.
    /// </summary>
    /// <typeparam name="T">The component type to require.</typeparam>
    /// <returns>A new query description with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableQueryDescription<TBits, TRegistry> With<T>() where T : unmanaged, IComponent
    {
        return With(T.TypeId);
    }

    /// <summary>
    /// Creates a new description with an additional required component by Type.
    /// </summary>
    /// <param name="type">The component type to require.</param>
    /// <returns>A new query description with the added constraint.</returns>
    /// <exception cref="InvalidOperationException">If the type is not a registered component.</exception>
    public ImmutableQueryDescription<TBits, TRegistry> With(Type type)
    {
        var id = TRegistry.GetId(type);
        ThrowHelper.ThrowIfInvalidComponentId(id);
        return With(id);
    }

    /// <summary>
    /// Creates a new description with an additional excluded component by ComponentId.
    /// </summary>
    /// <param name="componentId">The component ID to exclude.</param>
    /// <returns>A new query description with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableQueryDescription<TBits, TRegistry> Without(int componentId)
    {
        return this with { None = None.Set(componentId) };
    }

    /// <summary>
    /// Creates a new description with an additional excluded component.
    /// </summary>
    /// <typeparam name="T">The component type to exclude.</typeparam>
    /// <returns>A new query description with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableQueryDescription<TBits, TRegistry> Without<T>() where T : unmanaged, IComponent
    {
        return Without(T.TypeId);
    }

    /// <summary>
    /// Creates a new description with an additional excluded component by Type.
    /// </summary>
    /// <param name="type">The component type to exclude.</param>
    /// <returns>A new query description with the added constraint.</returns>
    /// <exception cref="InvalidOperationException">If the type is not a registered component.</exception>
    public ImmutableQueryDescription<TBits, TRegistry> Without(Type type)
    {
        var id = TRegistry.GetId(type);
        ThrowHelper.ThrowIfInvalidComponentId(id);
        return Without(id);
    }

    /// <summary>
    /// Creates a new description with an additional "any of" component by ComponentId.
    /// </summary>
    /// <param name="componentId">The component ID to add to the any-of set.</param>
    /// <returns>A new query description with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableQueryDescription<TBits, TRegistry> WithAny(int componentId)
    {
        return this with { Any = Any.Set(componentId) };
    }

    /// <summary>
    /// Creates a new description with an additional "any of" component.
    /// </summary>
    /// <typeparam name="T">The component type to add to the any-of set.</typeparam>
    /// <returns>A new query description with the added constraint.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableQueryDescription<TBits, TRegistry> WithAny<T>() where T : unmanaged, IComponent
    {
        return WithAny(T.TypeId);
    }

    /// <summary>
    /// Creates a new description with an additional "any of" component by Type.
    /// </summary>
    /// <param name="type">The component type to add to the any-of set.</param>
    /// <returns>A new query description with the added constraint.</returns>
    /// <exception cref="InvalidOperationException">If the type is not a registered component.</exception>
    public ImmutableQueryDescription<TBits, TRegistry> WithAny(Type type)
    {
        var id = TRegistry.GetId(type);
        ThrowHelper.ThrowIfInvalidComponentId(id);
        return WithAny(id);
    }
}
