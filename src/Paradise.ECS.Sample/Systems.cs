namespace Paradise.ECS.Sample;

// ============================================================================
// System Definitions - Demonstrating various system patterns
// ============================================================================

// ---- Inline Entity Systems ----

/// <summary>
/// Updates entity positions by adding velocity. Demonstrates inline entity system
/// with writable and read-only component refs.
/// </summary>
public ref partial struct MovementSystem : IEntitySystem
{
    public ref Position Position;
    public ref readonly Velocity Velocity;

    public void Execute()
    {
        Position = new(Position.X + Velocity.X, Position.Y + Velocity.Y);
    }
}

/// <summary>
/// Applies gravity to velocity. Demonstrates inline entity system with single writable ref.
/// </summary>
public ref partial struct GravitySystem : IEntitySystem
{
    public ref Velocity Velocity;

    public void Execute()
    {
        Velocity = new(Velocity.X, Velocity.Y - 9.8f);
    }
}

/// <summary>
/// Clamps position within bounds. Demonstrates [After] ordering attribute.
/// Always runs after MovementSystem.
/// </summary>
[After<MovementSystem>]
public ref partial struct BoundsSystem : IEntitySystem
{
    public ref Position Position;

    public void Execute()
    {
        Position = new(Math.Clamp(Position.X, 0, 1000), Math.Clamp(Position.Y, 0, 1000));
    }
}

// ---- Queryable Entity Systems ----

/// <summary>
/// Updates movable entity positions using queryable composition.
/// Demonstrates accessing components through a [Queryable] type's Data struct in an entity system.
/// The ref field is resolved by the generator to the Movable.Data type alias.
/// </summary>
public ref partial struct QueryableMovementSystem : IEntitySystem
{
    public ref MovableEntity Movable;

    public void Execute()
    {
        Movable.Position = new(Movable.Position.X + Movable.Velocity.X,
                               Movable.Position.Y + Movable.Velocity.Y);
    }
}

// ---- Inline Chunk Systems ----

/// <summary>
/// Applies gravity in batch using span access. Demonstrates inline chunk system.
/// </summary>
public ref partial struct GravityBatchSystem : IChunkSystem
{
    public Span<Velocity> Velocities;

    public void ExecuteChunk()
    {
        for (int i = 0; i < Velocities.Length; i++)
            Velocities[i] = new(Velocities[i].X, Velocities[i].Y - 9.8f);
    }
}

// ---- Queryable Chunk Systems ----

/// <summary>
/// Applies gravity in batch using queryable composition.
/// Demonstrates accessing components through a [Queryable] type's ChunkData struct in a chunk system.
/// The ref readonly field is resolved by the generator to the Movable.ChunkData type alias.
/// </summary>
public ref partial struct QueryableGravityBatchSystem : IChunkSystem
{
    public ref readonly MovableChunk Movable;

    public void ExecuteChunk()
    {
        var velocities = Movable.VelocitySpan;
        for (int i = 0; i < Movable.EntityCount; i++)
            velocities[i] = new(velocities[i].X, velocities[i].Y - 9.8f);
    }
}
