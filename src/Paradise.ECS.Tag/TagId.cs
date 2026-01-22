using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Represents a unique identifier for a tag type.
/// Tags have their own ID space separate from components.
/// </summary>
/// <remarks>
/// Tag IDs are assigned at compile time by the source generator based on
/// alphabetical ordering of fully qualified tag type names.
/// </remarks>
public readonly record struct TagId
{
    /// <summary>
    /// The unique value for this tag.
    /// </summary>
    public int Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    /// <summary>
    /// Creates a TagId with the specified value.
    /// </summary>
    /// <param name="value">The tag ID value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TagId(int value)
    {
        Value = value;
    }

    /// <summary>
    /// Invalid tag ID representing no tag.
    /// </summary>
    public static readonly TagId Invalid = new(-1);

    /// <summary>
    /// Gets whether this ID is valid (non-negative).
    /// </summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value >= 0;
    }

    /// <summary>
    /// Implicitly converts a TagId to its integer value.
    /// </summary>
    /// <param name="id">The tag ID.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(TagId id) => id.Value;

    /// <inheritdoc/>
    public override string ToString() => IsValid ? $"TagId({Value})" : "TagId(Invalid)";
}
