namespace Paradise.ECS;

/// <summary>
/// Marks a partial empty struct as a tag type for ECS storage.
/// The source generator will implement <see cref="ITag"/> automatically.
/// </summary>
/// <remarks>
/// <para>
/// Tags are marker types with no instance fields. They are stored in a per-entity
/// bitmask within chunks, enabling O(1) tag operations without archetype changes.
/// </para>
/// <para>
/// Tags are assigned IDs at module initialization time. By default, IDs are
/// auto-assigned based on fully qualified type name. You can manually specify
/// an ID using the <see cref="Id"/> property.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Tag]
/// public partial struct IsPlayer;
///
/// [Tag("12345678-1234-1234-1234-123456789012")]
/// public partial struct IsEnemy;
///
/// [Tag(Id = 100)]
/// public partial struct FixedIdTag;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class TagAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TagAttribute"/> class.
    /// </summary>
    /// <param name="guid">Optional GUID for stable tag identification.</param>
    public TagAttribute(string? guid = null)
    {
        Guid = guid;
    }

    /// <summary>
    /// Gets the optional GUID for stable tag identification across compilations.
    /// </summary>
    /// <remarks>
    /// When specified, this GUID provides a stable identifier that persists even when
    /// tags are added/removed (unlike TagId which changes based on auto-assignment).
    /// </remarks>
    public string? Guid { get; }

    /// <summary>
    /// Gets or sets the manual tag ID. When set, this ID is used instead of auto-assignment.
    /// </summary>
    /// <remarks>
    /// Use this to ensure a tag always has the same ID regardless of other tags
    /// in the project. Auto-assigned IDs will skip over manually assigned values.
    /// Must be a non-negative integer. -1 (default) means auto-assign.
    /// </remarks>
    public int Id { get; set; } = -1;
}
