# System API Design: Compile-Time DAG with Chunk-Level Parallelism

## Goals

1. **Easier to understand** - Declarative, attribute-based API with minimal boilerplate
2. **Compile-time DAG generation** - Source generators analyze read/write patterns to build dependency graph
3. **Chunk-level parallelism** - Independent chunks execute in parallel when no conflicts exist

---

## Integration with Queryable

**Key Insight**: A System is a Queryable with an Execute method. We reuse the existing `[With<T>]` attributes:

| Existing Attribute | System Interpretation |
|-------------------|----------------------|
| `[With<T>]` | **Writes** component T (read-write access) |
| `[With<T>(IsReadOnly = true)]` | **Reads** component T (read-only access) |
| `[Without<T>]` | Exclude entities with component T |
| `[WithAny<T>]` | Include if has at least one |
| `[Optional<T>]` | Optional component access |

This design:
- **Reuses existing infrastructure** - No new component access attributes
- **Consistent mental model** - Same attributes work for queries and systems
- **Leverages existing generator** - Extend `QueryableGenerator` for systems

---

## Design Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Compile Time                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│  [System] + [With<T>] attributes → SystemGenerator                         │
│  IsReadOnly = true → Read mask | IsReadOnly = false → Write mask           │
│  Topological sort → Execution waves (parallel groups)                      │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                               Runtime                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│  SystemScheduler → Uses DAG to schedule system execution                   │
│  Parallel execution of systems in same "wave" (no conflicts)               │
│  Chunk-level parallelism within systems (different chunks, same data)     │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## API Design

### 1. Basic System (Per-Entity Execution)

```csharp
// MovementSystem: reads Velocity, writes Position
[System]
[With<Position>]                      // Write access (default)
[With<Velocity>(IsReadOnly = true)]   // Read-only access
public partial struct MovementSystem;

// User implements the Execute method:
public partial struct MovementSystem
{
    public static void Execute(ref Position position, in Velocity velocity)
    {
        position = new Position(position.X + velocity.X, position.Y + velocity.Y);
    }
}
```

**Generated code** (conceptual):
```csharp
public partial struct MovementSystem
{
    public static int SystemId => 0;

    // Read mask: Position, Velocity
    // Write mask: Position

    public static void Run<TWorld, TMask, TConfig>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
    {
        foreach (var chunk in world.ChunkQuery(default(MovementSystem.Query)))
        {
            var positions = chunk.Positions;       // Span<Position>
            var velocities = chunk.Velocitys;      // ReadOnlySpan<Velocity>

            for (int i = 0; i < chunk.EntityCount; i++)
            {
                Execute(ref positions[i], in velocities[i]);
            }
        }
    }
}
```

### 2. Chunk-Level System (SIMD-Friendly)

```csharp
// Opt-in to chunk-level execution for vectorized operations
[System(ChunkExecution = true)]
[With<Position>]
[With<Velocity>(IsReadOnly = true)]
public partial struct MovementSystemSIMD;

public partial struct MovementSystemSIMD
{
    // Receives spans instead of individual refs
    public static void Execute(
        Span<Position> positions,
        ReadOnlySpan<Velocity> velocities,
        int entityCount)
    {
        // SIMD-friendly: process all entities in chunk at once
        for (int i = 0; i < entityCount; i++)
        {
            positions[i] = new Position(
                positions[i].X + velocities[i].X,
                positions[i].Y + velocities[i].Y);
        }
    }
}
```

### 3. Query Filtering (Reusing Existing Attributes)

```csharp
// All existing query attributes work with systems
[System]
[With<Health>]                        // Write
[With<Position>(IsReadOnly = true)]   // Read
[Without<Invulnerable>]               // Exclude
public partial struct DamageSystem;

public partial struct DamageSystem
{
    public static void Execute(ref Health health, in Position position)
    {
        if (IsInDamageZone(position))
        {
            health = new Health(health.Current - 10, health.Max);
        }
    }

    private static bool IsInDamageZone(in Position pos) => pos.X < 0 || pos.X > 100;
}
```

### 4. Optional Components

```csharp
// Systems can use optional components too
[System]
[With<Position>]
[Optional<Velocity>(IsReadOnly = true)]
public partial struct MaybeMovingSystem;

public partial struct MaybeMovingSystem
{
    public static void Execute(
        ref Position position,
        bool hasVelocity,
        in Velocity velocity)  // Default value if not present
    {
        if (hasVelocity)
        {
            position = new Position(position.X + velocity.X, position.Y + velocity.Y);
        }
    }
}
```

### 5. Explicit Ordering

```csharp
// Override automatic DAG with explicit ordering
[System]
[With<Position>(IsReadOnly = true)]
[After<MovementSystem>]   // Must run after MovementSystem
[Before<RenderSystem>]    // Must run before RenderSystem
public partial struct CollisionSystem;
```

### 6. System Groups

```csharp
// Logical grouping of related systems
[SystemGroup]
public partial struct PhysicsGroup;

[SystemGroup]
[After<PhysicsGroup>]
public partial struct RenderGroup;

// Assign system to group
[System(Group = typeof(PhysicsGroup))]
[With<Position>]
[With<Velocity>(IsReadOnly = true)]
public partial struct MovementSystem;

[System(Group = typeof(RenderGroup))]
[With<Position>(IsReadOnly = true)]
[With<Sprite>(IsReadOnly = true)]
public partial struct SpriteRenderSystem;
```

---

## Read/Write Derivation from Attributes

The generator analyzes `[With<T>]` attributes to determine access patterns:

```csharp
// Analyzing MovementSystem:
[System]
[With<Position>]                      // IsReadOnly = false (default) → WRITE
[With<Velocity>(IsReadOnly = true)]   // IsReadOnly = true → READ ONLY

// Generated masks:
// ReadMask  = { Position, Velocity }  // All accessed components
// WriteMask = { Position }            // Only non-readonly components
```

**Access Rules:**

| Attribute Configuration | Read Mask | Write Mask |
|------------------------|-----------|------------|
| `[With<T>]` | ✓ | ✓ |
| `[With<T>(IsReadOnly = true)]` | ✓ | ✗ |
| `[With<T>(QueryOnly = true)]` | ✗ | ✗ |
| `[Without<T>]` | ✗ | ✗ |
| `[WithAny<T>]` | ✗ | ✗ |
| `[Optional<T>]` | ✓ | ✓ |
| `[Optional<T>(IsReadOnly = true)]` | ✓ | ✗ |

---

## DAG Computation

### Conflict Matrix

```
┌────────────┬─────────────────┬─────────────────┐
│            │ System B Reads  │ System B Writes │
├────────────┼─────────────────┼─────────────────┤
│ A Reads    │ ✓ Parallel OK   │ A → B edge      │
│ A Writes   │ A → B edge      │ A → B edge (*)  │
└────────────┴─────────────────┴─────────────────┘
(*) Write-write: use declaration order as tiebreaker
```

### Algorithm (Compile-Time)

```
1. Collect all [System] declarations
2. For each system:
   a. Compute ReadMask from all [With<T>] components
   b. Compute WriteMask from [With<T>] where IsReadOnly = false
3. For each pair of systems (A, B):
   a. If A.WriteMask ∩ B.ReadMask ≠ ∅ → edge A → B
   b. If A.ReadMask ∩ B.WriteMask ≠ ∅ → edge A → B
   c. If A.WriteMask ∩ B.WriteMask ≠ ∅ → edge A → B (by declaration order)
4. Add explicit [After<T>] / [Before<T>] edges
5. Topological sort (detect cycles → compile error)
6. Partition into waves (systems with no edges between them)
```

### Example DAG

```csharp
[System]
[With<InputState>]
public partial struct InputSystem;  // Wave 0

[System]
[With<Velocity>]
[With<InputState>(IsReadOnly = true)]
public partial struct PlayerControlSystem;  // Wave 1 (reads InputState)

[System]
[With<Position>]
[With<Velocity>(IsReadOnly = true)]
public partial struct MovementSystem;  // Wave 2 (reads Velocity)

[System]
[With<Health>]
[With<Position>(IsReadOnly = true)]
[After<MovementSystem>]
public partial struct DamageSystem;  // Wave 3 (explicit after)

[System]
[With<Position>(IsReadOnly = true)]
[With<Sprite>(IsReadOnly = true)]
public partial struct RenderSystem;  // Wave 3 (reads Position, no writes)
```

**Generated waves:**
```
Wave 0: [InputSystem]
Wave 1: [PlayerControlSystem]
Wave 2: [MovementSystem]
Wave 3: [DamageSystem, RenderSystem]  // Can run in parallel!
```

---

## Generated Code Structure

### Per-System Generated Code

```csharp
// MovementSystem.g.cs
public partial struct MovementSystem : ISystem<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    /// <summary>Unique system identifier.</summary>
    public static int SystemId => 0;

    /// <summary>System name for debugging.</summary>
    public static string Name => "MovementSystem";

    /// <summary>
    /// Executes this system on all matching entities.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Run<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        var chunkQuery = QueryHelpers.CreateChunkQueryResult<ChunkData<TMask, TConfig>, TMask, TConfig>(
            world,
            SystemRegistry<TMask>.Systems[SystemId].QueryDescription);

        foreach (var chunk in chunkQuery)
        {
            var positions = chunk.Positions;
            var velocities = chunk.Velocitys;

            for (int i = 0; i < chunk.EntityCount; i++)
            {
                Execute(ref positions[i], in velocities[i]);
            }
        }
    }

    /// <summary>User-defined execution logic.</summary>
    public static partial void Execute(ref Position position, in Velocity velocity);

    /// <summary>
    /// Nested query type for this system, reusing Queryable infrastructure.
    /// </summary>
    [Queryable]
    [With<Position>]
    [With<Velocity>(IsReadOnly = true)]
    public readonly ref partial struct Query;
}
```

### SystemRegistry

```csharp
// SystemRegistry.g.cs
public static class SystemRegistry<TMask>
    where TMask : unmanaged, IBitSet<TMask>
{
    /// <summary>System metadata indexed by SystemId.</summary>
    public static ImmutableArray<SystemMetadata<TMask>> Systems { get; }

    /// <summary>Dependency edges between systems.</summary>
    public static ImmutableArray<SystemDependency> Dependencies { get; }

    /// <summary>Pre-computed parallel execution waves.</summary>
    public static ImmutableArray<ImmutableArray<int>> ExecutionWaves { get; }

    /// <summary>Total number of registered systems.</summary>
    public static int Count => 5;

    static SystemRegistry()
    {
        // MovementSystem (id=0): reads [Position, Velocity], writes [Position]
        var system0ReadMask = TMask.Empty
            .Set(Position.TypeId)
            .Set(Velocity.TypeId);
        var system0WriteMask = TMask.Empty
            .Set(Position.TypeId);
        var system0Query = new ImmutableQueryDescription<TMask>(
            all: system0ReadMask,
            none: TMask.Empty,
            any: TMask.Empty);

        Systems = ImmutableArray.Create(
            new SystemMetadata<TMask>(0, "MovementSystem", system0ReadMask, system0WriteMask,
                (HashedKey<ImmutableQueryDescription<TMask>>)system0Query),
            // ... more systems
        );

        // Pre-computed dependency edges
        Dependencies = ImmutableArray.Create(
            new SystemDependency(0, 2, DependencyReason.WriteReadConflict),
            // ... more edges
        );

        // Pre-computed parallel waves
        ExecutionWaves = ImmutableArray.Create(
            ImmutableArray.Create(0),        // Wave 0
            ImmutableArray.Create(1),        // Wave 1
            ImmutableArray.Create(2),        // Wave 2
            ImmutableArray.Create(3, 4)      // Wave 3 (parallel)
        );
    }
}
```

---

## Runtime Scheduler

```csharp
public sealed class SystemScheduler<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    private readonly Action<object>[] _systemRunners;

    public SystemScheduler()
    {
        // Build delegate array from generated registry
        _systemRunners = BuildSystemRunners();
    }

    /// <summary>
    /// Execute all systems in dependency order.
    /// Systems in the same wave run in parallel.
    /// </summary>
    public void RunAll<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        foreach (var wave in SystemRegistry<TMask>.ExecutionWaves)
        {
            if (wave.Length == 1)
            {
                // Single system - direct call
                RunSystem(world, wave[0]);
            }
            else
            {
                // Multiple systems - parallel execution
                Parallel.For(0, wave.Length, i => RunSystem(world, wave[i]));
            }
        }
    }

    /// <summary>
    /// Execute all systems with chunk-level parallelism.
    /// </summary>
    public void RunAllParallelChunks<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        foreach (var wave in SystemRegistry<TMask>.ExecutionWaves)
        {
            // Collect all chunks from all systems in this wave
            var workItems = new List<(int SystemId, ChunkHandle Chunk, int EntityCount)>();

            foreach (var systemId in wave)
            {
                var metadata = SystemRegistry<TMask>.Systems[systemId];
                var query = world.ArchetypeRegistry.GetOrCreateQuery(metadata.QueryDescription);

                foreach (var archetype in query.Archetypes)
                {
                    for (int i = 0; i < archetype.ChunkCount; i++)
                    {
                        var (chunk, entityCount) = archetype.GetChunkInfo(i);
                        workItems.Add((systemId, chunk, entityCount));
                    }
                }
            }

            // Process all chunks in parallel
            Parallel.ForEach(workItems, item =>
            {
                RunSystemChunk(world, item.SystemId, item.Chunk, item.EntityCount);
            });
        }
    }
}
```

---

## Complete Example

```csharp
// ============================================
// Components (existing)
// ============================================
[Component]
public partial struct Position { public float X, Y; }

[Component]
public partial struct Velocity { public float X, Y; }

[Component]
public partial struct Health { public int Current, Max; }

[Component]
public partial struct Sprite { public int TextureId; }

// ============================================
// Systems (new)
// ============================================

// Input system: writes InputState
[System]
[With<InputState>]
public partial struct InputSystem;

public partial struct InputSystem
{
    public static void Execute(ref InputState input)
    {
        // Poll input hardware
        input = PollInput();
    }
}

// Movement system: reads Velocity, writes Position
[System]
[With<Position>]
[With<Velocity>(IsReadOnly = true)]
public partial struct MovementSystem;

public partial struct MovementSystem
{
    public static void Execute(ref Position pos, in Velocity vel)
    {
        pos = new Position { X = pos.X + vel.X, Y = pos.Y + vel.Y };
    }
}

// Damage system: reads Position, writes Health
[System]
[With<Health>]
[With<Position>(IsReadOnly = true)]
[Without<Invulnerable>]
[After<MovementSystem>]
public partial struct DamageSystem;

public partial struct DamageSystem
{
    public static void Execute(ref Health health, in Position pos)
    {
        if (IsInDamageZone(pos))
        {
            health = new Health { Current = health.Current - 10, Max = health.Max };
        }
    }
}

// Render system: reads Position and Sprite (read-only, can parallelize with DamageSystem)
[System]
[With<Position>(IsReadOnly = true)]
[With<Sprite>(IsReadOnly = true)]
public partial struct RenderSystem;

public partial struct RenderSystem
{
    public static void Execute(in Position pos, in Sprite sprite)
    {
        DrawSprite(sprite.TextureId, pos.X, pos.Y);
    }
}

// ============================================
// Generated DAG
// ============================================
//
//     InputSystem
//          │
//          ▼
//     MovementSystem
//          │
//     ┌────┴────┐
//     ▼         ▼
// DamageSystem  RenderSystem  ← Can run in PARALLEL!
//
// Waves:
//   Wave 0: [InputSystem]
//   Wave 1: [MovementSystem]
//   Wave 2: [DamageSystem, RenderSystem]

// ============================================
// Usage
// ============================================
var world = new World();
var scheduler = new SystemScheduler<Mask, Config>();

while (gameRunning)
{
    // Automatic parallel execution based on DAG
    scheduler.RunAllParallelChunks(world);
}
```

---

## Attributes Summary

### New Attributes

| Attribute | Target | Description |
|-----------|--------|-------------|
| `[System]` | struct | Marks as a system with auto-generated Run method |
| `[System(ChunkExecution = true)]` | struct | System receives spans instead of individual refs |
| `[System(Group = typeof(T))]` | struct | Assigns system to a group |
| `[SystemGroup]` | struct | Defines a group for logical organization |
| `[After<T>]` | struct | Explicit ordering: run after system/group T |
| `[Before<T>]` | struct | Explicit ordering: run before system/group T |

### Reused Queryable Attributes

| Attribute | System Behavior |
|-----------|-----------------|
| `[With<T>]` | Component T is written (read-write) |
| `[With<T>(IsReadOnly = true)]` | Component T is read-only |
| `[With<T>(QueryOnly = true)]` | Filter only, no access generated |
| `[Without<T>]` | Exclude entities with T |
| `[WithAny<T>]` | Include if has any |
| `[Optional<T>]` | Optional component access |

---

## Implementation Phases

### Phase 1: Core System Infrastructure
- Add `[System]` attribute
- Extend `QueryableGenerator` to detect `[System]` attribute
- Generate `ISystem<TMask, TConfig>` implementation with `Run<TWorld>()` method
- Generate nested `Query` type reusing existing Queryable infrastructure
- Sequential execution (no parallelism yet)

### Phase 2: DAG Computation
- Compute read/write masks from `[With<T>]` attributes
- Build dependency edges based on conflict matrix
- Add `[After<T>]` and `[Before<T>]` explicit edge support
- Topological sort with cycle detection (compile error)
- Generate `SystemRegistry<TMask>` with metadata

### Phase 3: Parallel Execution
- Generate `ExecutionWaves` in `SystemRegistry`
- Implement `SystemScheduler.RunAll()` with wave-based parallelism
- Implement `SystemScheduler.RunAllParallelChunks()` for chunk-level distribution

### Phase 4: Advanced Features
- `[SystemGroup]` for logical organization
- `[System(ChunkExecution = true)]` for SIMD-friendly API
- Enable/disable systems at runtime
- Profiling hooks

---

## Benefits of This Design

1. **Familiar API** - Uses same `[With<T>]` attributes developers already know
2. **No redundancy** - Read/write derived from `IsReadOnly`, not separate attributes
3. **Code reuse** - Systems leverage existing Queryable infrastructure
4. **Type safety** - Execute method signature matches component access
5. **Zero runtime overhead** - All DAG computation at compile time
6. **Parallel by default** - Automatic parallelization where safe
