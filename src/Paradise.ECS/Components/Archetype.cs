using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Represents an archetype (set of component types) using a bitset.
/// </summary>
/// <typeparam name="TBits">The bit storage type (Bit64, Bit128, Bit256, etc.).</typeparam>
public readonly record struct Archetype<TBits> where TBits : unmanaged, IStorage
{
    private readonly ImmutableBitSet<TBits> _mask;

    private Archetype(ImmutableBitSet<TBits> mask)
    {
        _mask = mask;
    }

    /// <summary>
    /// An empty archetype with no components.
    /// </summary>
    public static Archetype<TBits> Empty => new(ImmutableBitSet<TBits>.Empty);

    /// <summary>
    /// Gets the underlying component mask.
    /// </summary>
    public ImmutableBitSet<TBits> Mask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _mask;
    }

    /// <summary>
    /// Gets the maximum number of component types this archetype can represent.
    /// </summary>
    public static int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ImmutableBitSet<TBits>.Capacity;
    }

    /// <summary>
    /// Returns a new archetype with the specified component type added.
    /// </summary>
    /// <typeparam name="T">The component type to add.</typeparam>
    /// <returns>A new archetype containing the component type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Archetype<TBits> With<T>() where T : unmanaged, IComponent
    {
        return With(T.TypeId);
    }

    /// <summary>
    /// Returns a new archetype with the specified component ID added.
    /// </summary>
    /// <param name="id">The component ID to add.</param>
    /// <returns>A new archetype containing the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Archetype<TBits> With(ComponentId id)
    {
        return new(_mask.Set(id.Value));
    }

    /// <summary>
    /// Returns a new archetype with the specified component type removed.
    /// </summary>
    /// <typeparam name="T">The component type to remove.</typeparam>
    /// <returns>A new archetype without the component type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Archetype<TBits> Without<T>() where T : unmanaged, IComponent
    {
        return Without(T.TypeId);
    }

    /// <summary>
    /// Returns a new archetype with the specified component ID removed.
    /// </summary>
    /// <param name="id">The component ID to remove.</param>
    /// <returns>A new archetype without the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Archetype<TBits> Without(ComponentId id)
    {
        return new(_mask.Clear(id.Value));
    }

    /// <summary>
    /// Checks if this archetype contains the specified component type.
    /// </summary>
    /// <typeparam name="T">The component type to check.</typeparam>
    /// <returns><c>true</c> if the archetype contains the component type; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>() where T : unmanaged, IComponent
    {
        return Has(T.TypeId);
    }

    /// <summary>
    /// Checks if this archetype contains the specified component ID.
    /// </summary>
    /// <param name="id">The component ID to check.</param>
    /// <returns><c>true</c> if the archetype contains the component; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has(ComponentId id)
    {
        return _mask.Get(id.Value);
    }

    /// <summary>
    /// Checks if this archetype contains all components in the other archetype.
    /// </summary>
    /// <param name="other">The archetype to check against.</param>
    /// <returns><c>true</c> if this archetype is a superset of the other; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAll(in Archetype<TBits> other)
    {
        return _mask.ContainsAll(other._mask);
    }

    /// <summary>
    /// Checks if this archetype contains any components in the other archetype.
    /// </summary>
    /// <param name="other">The archetype to check against.</param>
    /// <returns><c>true</c> if the archetypes have any overlapping components; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAny(in Archetype<TBits> other)
    {
        return _mask.ContainsAny(other._mask);
    }

    /// <summary>
    /// Checks if this archetype contains none of the components in the other archetype.
    /// </summary>
    /// <param name="other">The archetype to check against.</param>
    /// <returns><c>true</c> if the archetypes have no overlapping components; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsNone(in Archetype<TBits> other)
    {
        return _mask.ContainsNone(other._mask);
    }

    /// <summary>
    /// Returns the number of component types in this archetype.
    /// </summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _mask.PopCount();
    }

    /// <summary>
    /// Gets whether this archetype has no components.
    /// </summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _mask.IsEmpty;
    }

    /// <summary>
    /// Performs a bitwise OR operation (union) between two archetypes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Archetype<TBits> operator |(in Archetype<TBits> left, in Archetype<TBits> right)
        => new(left._mask | right._mask);

    /// <summary>
    /// Performs a bitwise AND operation (intersection) between two archetypes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Archetype<TBits> operator &(in Archetype<TBits> left, in Archetype<TBits> right)
        => new(left._mask & right._mask);

    /// <summary>
    /// Performs a bitwise XOR operation (symmetric difference) between two archetypes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Archetype<TBits> operator ^(in Archetype<TBits> left, in Archetype<TBits> right)
        => new(left._mask ^ right._mask);

    /// <inheritdoc/>
    public override string ToString() => $"Archetype<{typeof(TBits).Name}>({Count} components)";
}
