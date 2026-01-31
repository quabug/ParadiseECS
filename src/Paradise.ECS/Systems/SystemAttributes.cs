namespace Paradise.ECS;

/// <summary>
/// Marks a partial struct as a system for automatic code generation.
/// The generator analyzes the Execute method parameters to derive query and access patterns.
/// </summary>
/// <remarks>
/// <para>
/// Execute method parameters determine the query:
/// - Each parameter must be a Queryable type (marked with [Queryable])
/// - <c>ref</c> parameters can write to components (respecting Queryable's IsReadOnly settings)
/// - <c>in</c> parameters are read-only (all components read-only regardless of Queryable settings)
/// </para>
/// <para>
/// The generator creates:
/// - <c>Run&lt;TWorld&gt;(world)</c> method that iterates matching entities and calls Execute
/// - <c>SystemId</c> property for scheduler lookup
/// - DAG metadata for parallel scheduling
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Queryable]
/// [With&lt;Position&gt;]
/// [With&lt;Velocity&gt;(IsReadOnly = true)]
/// public readonly ref partial struct Moveable;
///
/// [System]
/// public partial struct MovementSystem
/// {
///     public static void Execute(ref Moveable moveable)
///     {
///         moveable.Position = new Position(
///             moveable.Position.X + moveable.Velocity.X,
///             moveable.Position.Y + moveable.Velocity.Y);
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class SystemAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the manual system ID. -1 (default) means auto-assign.
    /// </summary>
    /// <remarks>
    /// Use this to ensure a system always has the same ID regardless of other systems.
    /// Auto-assigned IDs will skip over manually assigned values.
    /// </remarks>
    public int Id { get; set; } = -1;

    /// <summary>
    /// Gets or sets the system group this system belongs to.
    /// </summary>
    /// <remarks>
    /// Systems in the same group are scheduled together according to group ordering.
    /// Use <see cref="AfterAttribute{T}"/> and <see cref="BeforeAttribute{T}"/> on groups
    /// for inter-group ordering.
    /// </remarks>
    public Type? Group { get; set; }
}

/// <summary>
/// Marks a partial struct as a system group for logical organization.
/// Groups can be ordered relative to other groups using <see cref="AfterAttribute{T}"/>
/// and <see cref="BeforeAttribute{T}"/>.
/// </summary>
/// <example>
/// <code>
/// [SystemGroup]
/// public partial struct PhysicsGroup;
///
/// [SystemGroup]
/// [After&lt;PhysicsGroup&gt;]
/// public partial struct RenderGroup;
///
/// [System(Group = typeof(PhysicsGroup))]
/// public partial struct MovementSystem
/// {
///     public static void Execute(ref Moveable moveable) { ... }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class SystemGroupAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the manual group ID. -1 (default) means auto-assign.
    /// </summary>
    public int Id { get; set; } = -1;
}

/// <summary>
/// Declares that this system or group must run after the specified system or group.
/// </summary>
/// <typeparam name="T">The system or group type that must run first.</typeparam>
/// <example>
/// <code>
/// [System]
/// [After&lt;MovementSystem&gt;]
/// public partial struct CollisionSystem
/// {
///     public static void Execute(in Moveable moveable) { ... }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class AfterAttribute<T> : Attribute;

/// <summary>
/// Declares that this system or group must run before the specified system or group.
/// </summary>
/// <typeparam name="T">The system or group type that must run after.</typeparam>
/// <example>
/// <code>
/// [System]
/// [Before&lt;RenderSystem&gt;]
/// public partial struct MovementSystem
/// {
///     public static void Execute(ref Moveable moveable) { ... }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class BeforeAttribute<T> : Attribute;
