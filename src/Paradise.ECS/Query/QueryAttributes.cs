namespace Paradise.ECS;

/// <summary>
/// Marks a partial ref struct as a queryable type for source generator processing.
/// The generator will implement query iteration and component access methods.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="WithAttribute{T}"/>, <see cref="WithoutAttribute{T}"/>, and
/// <see cref="AnyAttribute{T}"/> to specify the query constraints.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Queryable]
/// [With&lt;Position&gt;]
/// [With&lt;Health&gt;]
/// [Without&lt;Dead&gt;]
/// public readonly ref partial struct AliveEntity;
///
/// [Queryable(Id = 100)]  // Manually assign ID 100
/// [With&lt;Position&gt;]
/// public readonly ref partial struct FixedIdQuery;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class QueryableAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the manual queryable ID. When set, this ID is used instead of auto-assignment.
    /// </summary>
    /// <remarks>
    /// Use this to ensure a queryable always has the same ID regardless of other queryables
    /// in the project. Auto-assigned IDs will skip over manually assigned values.
    /// Must be a non-negative integer. -1 (default) means auto-assign.
    /// </remarks>
    public int Id { get; set; } = -1;
}

/// <summary>
/// Specifies a required component type for a queryable struct.
/// Entities must have this component to match the query.
/// </summary>
/// <typeparam name="T">The component type that must be present.</typeparam>
/// <example>
/// <code>
/// [Queryable]
/// [With&lt;Position&gt;]
/// [With&lt;Velocity&gt;]
/// public readonly ref partial struct MovingEntity;
///
/// [Queryable]
/// [With&lt;Health&gt;(Name = "Hp", IsReadOnly = true)]
/// public readonly ref partial struct ReadOnlyHealth;
///
/// [Queryable]
/// [With&lt;Dead&gt;(QueryOnly = true)]  // Filter only, no property generated
/// public readonly ref partial struct DeadEntity;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class WithAttribute<T> : Attribute where T : unmanaged, IComponent
{
    /// <summary>
    /// Gets or sets a custom property name. Defaults to the component type name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets whether the generated property returns ref readonly instead of ref.
    /// Default: false (read-write access).
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Gets or sets whether the component is used only for query filtering.
    /// When true, no property is generated for this component.
    /// Default: false (property is generated).
    /// </summary>
    public bool QueryOnly { get; set; }
}

/// <summary>
/// Specifies an excluded component type for a queryable struct.
/// Entities must NOT have this component to match the query.
/// </summary>
/// <typeparam name="T">The component type that must be absent.</typeparam>
/// <example>
/// <code>
/// [Queryable]
/// [With&lt;Health&gt;]
/// [Without&lt;Dead&gt;]
/// public readonly ref partial struct AliveEntity;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class WithoutAttribute<T> : Attribute where T : unmanaged, IComponent
{
}

/// <summary>
/// Specifies an optional component type for a queryable struct.
/// Entities must have at least one of the components marked with Any to match the query.
/// </summary>
/// <typeparam name="T">A component type that may satisfy the "any" requirement.</typeparam>
/// <example>
/// <code>
/// [Queryable]
/// [With&lt;Position&gt;]
/// [Any&lt;Player&gt;]
/// [Any&lt;Enemy&gt;]
/// public readonly ref partial struct GameEntity; // Must have Position AND (Player OR Enemy)
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class AnyAttribute<T> : Attribute where T : unmanaged, IComponent
{
}

/// <summary>
/// Specifies an optional component for property generation without filtering.
/// The component does NOT filter entities - all entities matching other constraints will be iterated.
/// Generates HasXxx property and GetXxx() method (ref or ref readonly based on IsReadOnly).
/// </summary>
/// <typeparam name="T">The component type that may or may not be present.</typeparam>
/// <example>
/// <code>
/// [Queryable]
/// [With&lt;Position&gt;]
/// [Optional&lt;Velocity&gt;]
/// public readonly ref partial struct MaybeMovingEntity;
///
/// // Usage:
/// foreach (var entity in MaybeMovingEntity.Query.Build(world))
/// {
///     entity.Position.X += 1;  // Always available
///     if (entity.HasVelocity)
///     {
///         ref var vel = ref entity.GetVelocity();  // Only if present
///         vel.X += 1;
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class OptionalAttribute<T> : Attribute where T : unmanaged, IComponent
{
    /// <summary>
    /// Gets or sets a custom property name. Defaults to the component type name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets whether the generated GetXxx() method returns ref readonly instead of ref.
    /// Default: false (read-write access).
    /// </summary>
    public bool IsReadOnly { get; set; }
}
