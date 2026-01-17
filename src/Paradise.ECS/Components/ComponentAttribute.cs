namespace Paradise.ECS;

/// <summary>
/// Marks a partial unmanaged struct as a component type for ECS storage.
/// The source generator will implement <see cref="IComponent"/> automatically.
/// </summary>
/// <remarks>
/// <para>
/// Components are assigned IDs at module initialization time. By default, IDs are
/// auto-assigned based on alignment (descending) then fully qualified type name.
/// </para>
/// <para>
/// You can manually specify an ID using the <see cref="Id"/> property. Manual IDs
/// take precedence, and auto-assigned IDs will skip over any manually assigned values.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Component]
/// public partial struct Position
/// {
///     public float X, Y, Z;
/// }
///
/// [Component(Id = 100)]  // Manually assign ID 100
/// public partial struct FixedIdComponent
/// {
///     public int Value;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ComponentAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentAttribute"/> class.
    /// </summary>
    /// <param name="guid">Optional GUID for stable component identification.</param>
    public ComponentAttribute(string? guid = null)
    {
        Guid = guid;
    }

    /// <summary>
    /// Gets the optional GUID for stable component identification across compilations.
    /// </summary>
    /// <remarks>
    /// When specified, this GUID provides a stable identifier that persists even when
    /// components are added/removed (unlike TypeId which changes based on auto-assignment).
    /// </remarks>
    public string? Guid { get; }

    /// <summary>
    /// Gets or sets the manual component ID. When set, this ID is used instead of auto-assignment.
    /// </summary>
    /// <remarks>
    /// Use this to ensure a component always has the same ID regardless of other components
    /// in the project. Auto-assigned IDs will skip over manually assigned values.
    /// Must be a non-negative integer. -1 (default) means auto-assign.
    /// </remarks>
    public int Id { get; set; } = -1;
}
