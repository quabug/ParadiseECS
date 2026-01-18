namespace Paradise.ECS.IntegrationTest;

// ============================================================================
// Queryable Definitions - Demonstrating various query patterns
// ============================================================================

/// <summary>
/// Query for player entities with position, health (readonly), and optional velocity.
/// Demonstrates: Custom property name, IsReadOnly, Optional component.
/// </summary>
[Queryable]
[With<Position>]
[With<Health>(Name = "Hp", IsReadOnly = true)]
[Without<Name>]
[Optional<Velocity>]
public readonly ref partial struct Player;

/// <summary>
/// Query for enemy entities with position and health, requiring velocity via Any.
/// Demonstrates: Any constraint (at least one must match).
/// </summary>
[Queryable(Id = 4)]
[With<Position>]
[With<Health>]
[Without<Name>]
[Any<Velocity>]
public readonly ref partial struct Enemy;

/// <summary>
/// Query for player positions using PlayerTag as filter-only.
/// Demonstrates: QueryOnly (component used for filtering, no property generated).
/// </summary>
[Queryable]
[With<Position>]
[With<PlayerTag>(QueryOnly = true)]
public readonly ref partial struct PlayerPosition;

/// <summary>
/// Query for all movable entities (has both Position and Velocity).
/// Demonstrates: Simple query with two required components.
/// </summary>
[Queryable]
[With<Position>]
[With<Velocity>]
public readonly ref partial struct Movable;

/// <summary>
/// Query for entities with health, optionally having position.
/// Demonstrates: Optional component for entities that may or may not have position.
/// </summary>
[Queryable]
[With<Health>]
[Optional<Position>(IsReadOnly = true)]
public readonly ref partial struct Damageable;

/// <summary>
/// Query for named entities with position.
/// Demonstrates: Using the Name component.
/// </summary>
[Queryable]
[With<Position>]
[With<Name>]
public readonly ref partial struct NamedEntity;

/// <summary>
/// Query for all entities with position, regardless of other components.
/// Demonstrates: Minimal query for broad iteration.
/// </summary>
[Queryable]
[With<Position>]
public readonly ref partial struct Positioned;
