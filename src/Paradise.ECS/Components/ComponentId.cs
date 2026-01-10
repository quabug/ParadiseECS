using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Represents a unique identifier for a component type.
/// The ID corresponds to the bit index in archetype bitsets.
/// </summary>
/// <remarks>
/// IDs are assigned at compile time by the source generator based on
/// alphabetical ordering of fully qualified component type names.
/// </remarks>
public readonly record struct ComponentId
{
    /// <summary>
    /// The bit index for this component in archetype masks.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Creates a ComponentId with the specified value.
    /// </summary>
    /// <param name="value">The bit index value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentId(int value)
    {
        Value = value;
    }

    /// <summary>
    /// Invalid component ID representing no component.
    /// </summary>
    public static readonly ComponentId Invalid = new(-1);

    /// <summary>
    /// Gets whether this ID is valid (non-negative).
    /// </summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value >= 0;
    }

    /// <summary>
    /// Implicitly converts a ComponentId to its integer value.
    /// </summary>
    /// <param name="id">The component ID.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(ComponentId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => IsValid ? $"ComponentId({Value})" : "ComponentId(Invalid)";
}
