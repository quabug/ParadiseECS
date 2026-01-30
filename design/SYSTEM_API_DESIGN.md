# System API Design: Compile-Time DAG with Chunk-Level Parallelism

## Goals

1. **Easier to understand** - Declarative, attribute-based API with minimal boilerplate
2. **Compile-time DAG generation** - Source generators analyze read/write patterns to build dependency graph
3. **Chunk-level parallelism** - Independent chunks execute in parallel when no conflicts exist

---

## Design Overview

### Core Concepts

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Compile Time                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│  [System] attribute → SystemGenerator → SystemGraph (DAG metadata)         │
│  [Reads<T>] / [Writes<T>] attributes → Component access analysis           │
│  Topological sort → Execution order with parallel groups                   │
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

### 1. System Declaration (User Code)

```csharp
// Simple system - just declare what you read and write
[System]
[Reads<Position>]
[Reads<Velocity>]
[Writes<Position>]
public partial struct MovementSystem;

// The generator creates:
// - Execute method signature
// - Chunk iteration infrastructure
// - DAG metadata for scheduling

// User implements the logic via partial method:
public partial struct MovementSystem
{
    // Per-entity execution (simple, auto-batched)
    public static void Execute(ref Position position, in Velocity velocity)
    {
        position = new Position(position.X + velocity.X, position.Y + velocity.Y);
    }
}
```

### 2. Chunk-Level System (Advanced)

```csharp
// For SIMD/vectorized operations - work with spans directly
[System]
[Reads<Position>]
[Reads<Velocity>]
[Writes<Position>]
[ChunkExecution]  // Opt-in to chunk-level API
public partial struct MovementSystemSIMD;

public partial struct MovementSystemSIMD
{
    // Chunk-level execution for batch processing
    public static void ExecuteChunk(
        Span<Position> positions,
        ReadOnlySpan<Velocity> velocities,
        int entityCount)
    {
        for (int i = 0; i < entityCount; i++)
        {
            positions[i] = new Position(
                positions[i].X + velocities[i].X,
                positions[i].Y + velocities[i].Y);
        }
    }
}
```

### 3. Query Filtering

```csharp
// Filter entities using existing query attributes
[System]
[Reads<Health>]
[Reads<Position>]
[Writes<Health>]
[Without<Invulnerable>]  // Exclude invulnerable entities
public partial struct DamageSystem;

public partial struct DamageSystem
{
    public static void Execute(ref Health health, in Position position)
    {
        // Only runs on entities with Health and Position, but not Invulnerable
        if (IsInDamageZone(position))
        {
            health = new Health(health.Value - 10);
        }
    }
}
```

### 4. Explicit Dependencies (Override DAG)

```csharp
// Sometimes you need explicit ordering beyond component access
[System]
[Reads<Position>]
[After<MovementSystem>]  // Explicit: run after MovementSystem
[Before<RenderSystem>]   // Explicit: run before RenderSystem
public partial struct CollisionSystem;
```

### 5. System Groups (Logical Grouping)

```csharp
// Group related systems
[SystemGroup]
public partial struct PhysicsGroup;

[System(Group = typeof(PhysicsGroup))]
[Reads<Position>]
[Writes<Velocity>]
public partial struct GravitySystem;

[System(Group = typeof(PhysicsGroup))]
[Reads<Position>]
[Reads<Velocity>]
[Writes<Position>]
public partial struct MovementSystem;

// Groups can have explicit ordering
[SystemGroup]
[After<PhysicsGroup>]
public partial struct RenderGroup;
```

---

## Compile-Time DAG Generation

### Source Generator Output

The `SystemGenerator` produces:

```csharp
// Generated: SystemRegistry.g.cs
public static class SystemRegistry<TMask>
    where TMask : unmanaged, IBitSet<TMask>
{
    // System metadata array indexed by SystemId
    public static readonly ImmutableArray<SystemMetadata<TMask>> Systems;

    // Dependency graph edges
    public static readonly ImmutableArray<SystemDependency> Dependencies;

    // Pre-computed parallel execution waves
    public static readonly ImmutableArray<ImmutableArray<int>> ExecutionWaves;

    static SystemRegistry()
    {
        // MovementSystem: SystemId = 0
        // Reads: Position (0), Velocity (1)
        // Writes: Position (0)
        var system0 = new SystemMetadata<TMask>(
            id: 0,
            name: "MovementSystem",
            readMask: TMask.Empty.Set(Position.TypeId).Set(Velocity.TypeId),
            writeMask: TMask.Empty.Set(Position.TypeId),
            queryDescription: new HashedKey<ImmutableQueryDescription<TMask>>(
                new ImmutableQueryDescription<TMask>(
                    all: TMask.Empty.Set(Position.TypeId).Set(Velocity.TypeId),
                    none: TMask.Empty,
                    any: TMask.Empty)));

        // ... more systems ...

        // Pre-computed waves (systems in same wave can run in parallel)
        // Wave 0: [InputSystem]
        // Wave 1: [MovementSystem, AISystem]  // No write conflicts
        // Wave 2: [CollisionSystem]           // Reads Position which MovementSystem writes
        // Wave 3: [RenderSystem]
        ExecutionWaves = ImmutableArray.Create(
            ImmutableArray.Create(0),           // Wave 0
            ImmutableArray.Create(1, 2),        // Wave 1 (parallel)
            ImmutableArray.Create(3),           // Wave 2
            ImmutableArray.Create(4));          // Wave 3
    }
}

// Generated per-system: MovementSystem.g.cs
public partial struct MovementSystem : ISystem<TMask, TConfig>
{
    public static int SystemId => 0;

    public static void Run<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        var query = QueryHelpers.CreateChunkQueryResult<ChunkData<TMask, TConfig>, TMask, TConfig>(
            world,
            SystemRegistry<TMask>.Systems[SystemId].QueryDescription);

        foreach (var chunk in query)
        {
            var positions = chunk.GetSpan<Position>();
            var velocities = chunk.GetReadOnlySpan<Velocity>();

            for (int i = 0; i < chunk.EntityCount; i++)
            {
                Execute(ref positions[i], in velocities[i]);
            }
        }
    }

    // User-defined partial method
    public static partial void Execute(ref Position position, in Velocity velocity);
}
```

### DAG Computation Rules

```
Read/Write Conflict Matrix:
┌────────────┬─────────────────┬─────────────────┐
│            │ System B Reads  │ System B Writes │
├────────────┼─────────────────┼─────────────────┤
│ A Reads    │ No conflict ✓   │ A before B      │
│ A Writes   │ B before A      │ A before B (*)  │
└────────────┴─────────────────┴─────────────────┘
(*) Write-write conflicts use declaration order as tiebreaker
```

**Algorithm:**
1. Collect all `[System]` declarations
2. For each system, compute read mask and write mask from attributes
3. Build dependency edges using conflict matrix
4. Add explicit `[After<T>]` and `[Before<T>]` edges
5. Topological sort to detect cycles (error at compile time)
6. Partition into "waves" - systems in same wave have no conflicts

---

## Runtime Execution

### SystemScheduler

```csharp
public sealed class SystemScheduler<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    /// <summary>
    /// Executes all systems in dependency order.
    /// Systems within the same wave run in parallel.
    /// </summary>
    public void RunAll<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        foreach (var wave in SystemRegistry<TMask>.ExecutionWaves)
        {
            if (wave.Length == 1)
            {
                // Single system - run directly
                RunSystem(world, wave[0]);
            }
            else
            {
                // Multiple systems - run in parallel
                Parallel.ForEach(wave, systemId => RunSystem(world, systemId));
            }
        }
    }

    /// <summary>
    /// Executes systems in parallel at chunk level.
    /// Different chunks of the same query can run on different threads.
    /// </summary>
    public void RunAllParallelChunks<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        foreach (var wave in SystemRegistry<TMask>.ExecutionWaves)
        {
            // Collect all chunks from all systems in this wave
            var workItems = CollectChunkWorkItems(world, wave);

            // Process all chunks in parallel
            Parallel.ForEach(workItems, item => item.Execute());
        }
    }
}
```

### Chunk-Level Parallelism

```
Wave 1: [MovementSystem, AISystem]
                │
                ▼
┌─────────────────────────────────────────────────────────────┐
│ Chunk Work Items (all can run in parallel):                 │
│                                                             │
│   MovementSystem:     AISystem:                            │
│   ├─ Chunk[0]         ├─ Chunk[0]                         │
│   ├─ Chunk[1]         ├─ Chunk[1]                         │
│   └─ Chunk[2]         └─ Chunk[2]                         │
│                                                             │
│   → All 6 items distributed across thread pool             │
└─────────────────────────────────────────────────────────────┘
```

**Safety guarantees:**
- Systems in the same wave have non-overlapping write sets
- Different chunks contain different entities
- Result: Complete data parallelism at chunk granularity

---

## Attributes Reference

### System Declaration

| Attribute | Description |
|-----------|-------------|
| `[System]` | Marks a struct as a system |
| `[System(Group = typeof(T))]` | Assigns system to a group |
| `[SystemGroup]` | Marks a struct as a system group |
| `[ChunkExecution]` | Opt-in to chunk-level API instead of per-entity |

### Component Access

| Attribute | Description |
|-----------|-------------|
| `[Reads<T>]` | System reads component T (shared access) |
| `[Writes<T>]` | System writes component T (exclusive access, implies read) |
| `[Without<T>]` | Exclude entities with component T |
| `[WithAny<T>]` | Include if has at least one of the WithAny components |

### Explicit Ordering

| Attribute | Description |
|-----------|-------------|
| `[After<T>]` | Run after system/group T |
| `[Before<T>]` | Run before system/group T |
| `[RunFirst]` | Run at the beginning of execution |
| `[RunLast]` | Run at the end of execution |

---

## Example: Complete Game Loop

```csharp
// ============================================
// System Declarations
// ============================================

[SystemGroup]
public partial struct InputGroup;

[SystemGroup]
[After<InputGroup>]
public partial struct SimulationGroup;

[SystemGroup]
[After<SimulationGroup>]
public partial struct RenderGroup;

// --- Input Systems ---

[System(Group = typeof(InputGroup))]
[Writes<InputState>]
public partial struct InputSystem;

public partial struct InputSystem
{
    public static void Execute(ref InputState input)
    {
        input = ReadCurrentInput();
    }
}

// --- Simulation Systems ---

[System(Group = typeof(SimulationGroup))]
[Reads<InputState>]
[Writes<Velocity>]
public partial struct PlayerControlSystem;

[System(Group = typeof(SimulationGroup))]
[Reads<Position>]
[Reads<Velocity>]
[Writes<Position>]
public partial struct MovementSystem;

public partial struct MovementSystem
{
    public static void Execute(ref Position pos, in Velocity vel)
    {
        pos = new Position(pos.X + vel.X, pos.Y + vel.Y);
    }
}

[System(Group = typeof(SimulationGroup))]
[Reads<Position>]
[Reads<Health>]
[Writes<Health>]
[After<MovementSystem>]  // Need updated positions
public partial struct CollisionDamageSystem;

// --- Render Systems ---

[System(Group = typeof(RenderGroup))]
[Reads<Position>]
[Reads<Sprite>]
public partial struct SpriteRenderSystem;

// ============================================
// Generated Execution Order (DAG)
// ============================================
// Wave 0: InputSystem
// Wave 1: PlayerControlSystem  (parallel - no write conflicts)
// Wave 2: MovementSystem
// Wave 3: CollisionDamageSystem
// Wave 4: SpriteRenderSystem

// ============================================
// Usage
// ============================================

public static void GameLoop(World world)
{
    var scheduler = new SystemScheduler<Mask, Config>();

    while (running)
    {
        // Execute all systems in DAG order with chunk-level parallelism
        scheduler.RunAllParallelChunks(world);
    }
}
```

---

## Visualization: DAG at Compile Time

The source generator can output a comment showing the computed DAG:

```csharp
// Generated: SystemGraph.g.cs
//
// System Dependency Graph:
// ========================
//
//     InputSystem (id=0)
//          │
//          ▼
//     PlayerControlSystem (id=1)
//          │
//          ▼
//     MovementSystem (id=2)
//          │
//          ▼
//     CollisionDamageSystem (id=3)
//          │
//          ▼
//     SpriteRenderSystem (id=4)
//
// Parallel Waves:
// ===============
// Wave 0: [InputSystem]
// Wave 1: [PlayerControlSystem]
// Wave 2: [MovementSystem]
// Wave 3: [CollisionDamageSystem]
// Wave 4: [SpriteRenderSystem]
//
// Component Access Summary:
// =========================
// Position: Read by [MovementSystem, CollisionDamageSystem, SpriteRenderSystem], Write by [MovementSystem]
// Velocity: Read by [MovementSystem], Write by [PlayerControlSystem]
// Health:   Read by [CollisionDamageSystem], Write by [CollisionDamageSystem]
// Sprite:   Read by [SpriteRenderSystem]
```

---

## Comparison with Existing ECS Frameworks

| Feature | Paradise.ECS (Proposed) | Unity DOTS | Flecs | Bevy |
|---------|-------------------------|------------|-------|------|
| DAG Generation | Compile-time | Runtime | Runtime | Compile-time (partial) |
| Parallelism | Chunk-level | Chunk-level | Per-system | Per-system |
| AOT Compatible | Yes | Yes | N/A (C) | N/A (Rust) |
| API Style | Attribute-based | Attribute + Code | Function-based | Derive macro |

---

## Implementation Phases

### Phase 1: Core Infrastructure
- `[System]`, `[Reads<T>]`, `[Writes<T>]` attributes
- `SystemGenerator` producing `ISystem<T>` implementations
- `SystemMetadata` with read/write masks
- Basic sequential execution

### Phase 2: DAG Computation
- Dependency analysis from component masks
- `[After<T>]`, `[Before<T>]` explicit edges
- Topological sort with cycle detection
- Wave computation for parallelism

### Phase 3: Parallel Execution
- `SystemScheduler` with wave-based execution
- Chunk-level work distribution
- Thread pool integration

### Phase 4: Advanced Features
- `[SystemGroup]` for logical organization
- `[ChunkExecution]` for SIMD-friendly API
- Conditional systems (run only when condition met)
- System enable/disable at runtime

---

## Open Questions

1. **Structural Changes**: How to handle systems that add/remove components?
   - Option A: Deferred command buffer (like Unity DOTS)
   - Option B: Immediate with invalidation detection
   - Option C: Special `[StructuralChange]` attribute that forces serial execution

2. **Cross-System Communication**: How do systems communicate?
   - Option A: Shared singleton components (Resources)
   - Option B: Event buffers
   - Option C: Both

3. **Dynamic Systems**: Should systems be addable at runtime?
   - For hot-reload/modding scenarios
   - Would require runtime DAG recomputation

4. **Profiling/Debugging**: How to expose system timing?
   - Generate timing hooks automatically
   - Integrate with existing profiling tools
