namespace Paradise.ECS;

/// <summary>
/// Specifies that a system must execute after the target system.
/// Creates an explicit dependency edge in the system execution DAG.
/// </summary>
/// <typeparam name="T">The system type that must execute before this one.</typeparam>
/// <example>
/// <code>
/// [After&lt;MovementSystem&gt;]
/// public ref partial struct BoundsSystem : IEntitySystem
/// {
///     public ref Position Position;
///     public void Execute() { /* clamp position */ }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class AfterAttribute<T> : Attribute where T : allows ref struct
{
}

/// <summary>
/// Specifies that a system must execute before the target system.
/// Creates an explicit dependency edge in the system execution DAG.
/// </summary>
/// <typeparam name="T">The system type that must execute after this one.</typeparam>
/// <example>
/// <code>
/// [Before&lt;RenderSystem&gt;]
/// public ref partial struct MovementSystem : IEntitySystem
/// {
///     public ref Position Position;
///     public ref readonly Velocity Velocity;
///     public void Execute() { /* update position */ }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class BeforeAttribute<T> : Attribute where T : allows ref struct
{
}

