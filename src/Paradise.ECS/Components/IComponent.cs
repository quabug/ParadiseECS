namespace Paradise.ECS;

/// <summary>
/// Interface for ECS component types.
/// Use <see cref="ComponentAttribute"/> on partial structs to implement this automatically.
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
public interface IComponent
{
    /// <summary>
    /// The unique component type ID assigned at compile time.
    /// </summary>
    static abstract ComponentId TypeId { get; }

    /// <summary>
    /// The stable GUID for this component type, or <see cref="System.Guid.Empty"/> if not specified.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="TypeId"/> which changes based on alphabetical ordering,
    /// this GUID provides stable identification across compilations when specified
    /// via <see cref="ComponentAttribute.Guid"/>.
    /// </remarks>
    static abstract System.Guid Guid { get; }

    /// <summary>
    /// The size of this component in bytes.
    /// </summary>
    static abstract int Size { get; }

    /// <summary>
    /// The alignment of this component in bytes.
    /// </summary>
    static abstract int Alignment { get; }
}
