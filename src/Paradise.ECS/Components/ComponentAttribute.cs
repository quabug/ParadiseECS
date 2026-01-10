namespace Paradise.ECS;

/// <summary>
/// Marks a partial unmanaged struct as a component type for ECS storage.
/// The source generator will implement <see cref="IComponent"/> automatically.
/// </summary>
/// <remarks>
/// <para>
/// Components are assigned sequential IDs (0, 1, 2...) based on alphabetical
/// ordering of their fully qualified type names. The component ID is assigned
/// at compile time by the Paradise.ECS source generator.
/// </para>
/// <para>
/// This ensures deterministic IDs across different devices and compilation cycles.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Component]
/// public partial struct Position
/// {
///     public float X, Y, Z;
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
    /// components are added/removed (unlike TypeId which changes based on alphabetical ordering).
    /// </remarks>
    public string? Guid { get; }
}
