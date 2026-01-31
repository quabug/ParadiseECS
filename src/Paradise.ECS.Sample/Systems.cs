namespace Paradise.ECS.Sample;

// ============================================================================
// System Definitions - Demonstrating the System API
// ============================================================================

/// <summary>
/// Movement system that updates position based on velocity.
/// Uses ref Movable to write Position (Velocity is read-only in Movable).
/// </summary>
[System]
public partial struct MovementSystem
{
    /// <summary>
    /// Updates position by adding velocity.
    /// </summary>
    /// <param name="movable">The movable entity data (ref = can write writable components).</param>
    public static void Execute(ref MovableData movable)
    {
        // Position is writable in Movable, Velocity is read-only
        ref var pos = ref movable.Position;
        var vel = movable.Velocity;
        pos = new Position(pos.X + vel.X, pos.Y + vel.Y);
    }
}

/// <summary>
/// Damage system that reduces health for entities in danger zones.
/// Uses ref for Health (writable), in for Position (read-only).
/// </summary>
[System]
[Without<Name>]  // Only unnamed entities (enemies)
public partial struct DamageSystem
{
    /// <summary>
    /// Applies damage to entities in the danger zone.
    /// </summary>
    /// <param name="damageable">Health access (ref = can write).</param>
    /// <param name="positioned">Position access (in = read-only).</param>
    public static void Execute(
        ref DamageableData damageable,
        in PositionedData positioned)
    {
        // Check if in danger zone (x < 0)
        if (positioned.Position.X < 0)
        {
            ref var health = ref damageable.Health;
            health = new Health(health.Current - 1, health.Max);
        }
    }
}

/// <summary>
/// Render system that displays entities (read-only access to all data).
/// </summary>
[System]
public partial struct RenderSystem
{
    /// <summary>
    /// Renders entities at their positions.
    /// </summary>
    /// <param name="positioned">Position data (in = read-only).</param>
    public static void Execute(in PositionedData positioned)
    {
        var pos = positioned.Position;
        // In a real game, this would draw a sprite at the position
        Console.WriteLine($"Rendering entity at ({pos.X}, {pos.Y})");
    }
}
