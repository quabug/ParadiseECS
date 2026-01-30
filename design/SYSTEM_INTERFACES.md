# System API: Interface Specifications (Queryable-Integrated)

This document defines the concrete C# interfaces and types for the System API,
integrated with the existing Queryable infrastructure.

---

## Design Principle: System = Queryable + Execute

Systems reuse the existing `[With<T>]` attributes from the Queryable pattern:

| Attribute | Query Behavior | System Behavior |
|-----------|---------------|-----------------|
| `[With<T>]` | Required component | **Write** access |
| `[With<T>(IsReadOnly = true)]` | Required component, ref readonly | **Read-only** access |
| `[With<T>(QueryOnly = true)]` | Filter only, no property | Filter only, no parameter |
| `[Without<T>]` | Exclude entities | Exclude entities |
| `[WithAny<T>]` | At least one required | At least one required |
| `[Optional<T>]` | Has/Get pattern | Has/default pattern |

---

## New Attributes

### SystemAttribute

```csharp
namespace Paradise.ECS;

/// <summary>
/// Marks a partial struct as a system for automatic code generation.
/// Systems reuse Queryable attributes ([With], [Without], etc.) for component access.
/// </summary>
/// <remarks>
/// <para>
/// The generator analyzes [With&lt;T&gt;] attributes to determine read/write access:
/// - [With&lt;T&gt;] → Write access (adds to both ReadMask and WriteMask)
/// - [With&lt;T&gt;(IsReadOnly = true)] → Read-only access (adds to ReadMask only)
/// </para>
/// <para>
/// The generator creates:
/// - Run&lt;TWorld&gt;() method that iterates matching entities and calls Execute()
/// - SystemId property for scheduler lookup
/// - Nested Query type for entity matching (reuses Queryable infrastructure)
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [System]
/// [With&lt;Position&gt;]                      // Write access (default)
/// [With&lt;Velocity&gt;(IsReadOnly = true)]   // Read-only access
/// public partial struct MovementSystem;
///
/// public partial struct MovementSystem
/// {
///     public static void Execute(ref Position position, in Velocity velocity)
///     {
///         position = new Position(position.X + velocity.X, position.Y + velocity.Y);
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
    public int Id { get; set; } = -1;

    /// <summary>
    /// Gets or sets the system group this system belongs to.
    /// </summary>
    public Type? Group { get; set; }

    /// <summary>
    /// Gets or sets whether this system uses chunk-level execution.
    /// When true, Execute receives Span/ReadOnlySpan parameters instead of ref/in.
    /// Default: false (per-entity execution).
    /// </summary>
    public bool ChunkExecution { get; set; }
}
```

### SystemGroupAttribute

```csharp
namespace Paradise.ECS;

/// <summary>
/// Marks a partial struct as a system group for logical organization.
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
/// [With&lt;Position&gt;]
/// public partial struct MovementSystem;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class SystemGroupAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the manual group ID. -1 means auto-assign.
    /// </summary>
    public int Id { get; set; } = -1;
}
```

### Ordering Attributes

```csharp
namespace Paradise.ECS;

/// <summary>
/// Declares that this system/group must run after the specified system or group.
/// </summary>
/// <typeparam name="T">The system or group type that must run first.</typeparam>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class AfterAttribute<T> : Attribute;

/// <summary>
/// Declares that this system/group must run before the specified system or group.
/// </summary>
/// <typeparam name="T">The system or group type that must run after.</typeparam>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class BeforeAttribute<T> : Attribute;
```

---

## Core Interfaces

### ISystem

```csharp
namespace Paradise.ECS;

/// <summary>
/// Interface implemented by generated system code.
/// </summary>
/// <typeparam name="TMask">The component mask type.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public interface ISystem<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    /// <summary>
    /// Unique system identifier assigned at compile time.
    /// </summary>
    static abstract int SystemId { get; }

    /// <summary>
    /// Human-readable system name for debugging.
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Executes this system on all matching entities.
    /// </summary>
    static abstract void Run<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>;
}
```

---

## System Metadata

### SystemMetadata

```csharp
namespace Paradise.ECS;

/// <summary>
/// Compile-time metadata about a system's component access patterns.
/// Used by the scheduler to determine execution order and parallelization.
/// </summary>
/// <typeparam name="TMask">The component mask type.</typeparam>
public readonly record struct SystemMetadata<TMask>
    where TMask : unmanaged, IBitSet<TMask>
{
    /// <summary>Unique system identifier.</summary>
    public int Id { get; init; }

    /// <summary>System name for debugging.</summary>
    public string Name { get; init; }

    /// <summary>
    /// Components this system reads.
    /// Includes all [With&lt;T&gt;] components (both read-only and read-write).
    /// </summary>
    public TMask ReadMask { get; init; }

    /// <summary>
    /// Components this system writes.
    /// Only includes [With&lt;T&gt;] where IsReadOnly = false.
    /// </summary>
    public TMask WriteMask { get; init; }

    /// <summary>
    /// Query description for matching entities.
    /// Derived from [With], [Without], [WithAny] attributes.
    /// </summary>
    public HashedKey<ImmutableQueryDescription<TMask>> QueryDescription { get; init; }

    /// <summary>
    /// Whether this system uses chunk-level execution.
    /// </summary>
    public bool ChunkExecution { get; init; }

    /// <summary>
    /// Optional group ID this system belongs to. -1 if ungrouped.
    /// </summary>
    public int GroupId { get; init; }

    /// <summary>
    /// Creates system metadata.
    /// </summary>
    public SystemMetadata(
        int id,
        string name,
        TMask readMask,
        TMask writeMask,
        HashedKey<ImmutableQueryDescription<TMask>> queryDescription,
        bool chunkExecution = false,
        int groupId = -1)
    {
        Id = id;
        Name = name;
        ReadMask = readMask;
        WriteMask = writeMask;
        QueryDescription = queryDescription;
        ChunkExecution = chunkExecution;
        GroupId = groupId;
    }

    /// <summary>
    /// Checks if this system conflicts with another (cannot run in parallel).
    /// </summary>
    public bool ConflictsWith(in SystemMetadata<TMask> other)
    {
        // Write-Read: this writes something other reads
        if (!WriteMask.And(other.ReadMask).IsEmpty)
            return true;

        // Read-Write: this reads something other writes
        if (!ReadMask.And(other.WriteMask).IsEmpty)
            return true;

        // Write-Write: both write same component
        if (!WriteMask.And(other.WriteMask).IsEmpty)
            return true;

        return false;
    }
}
```

### SystemDependency

```csharp
namespace Paradise.ECS;

/// <summary>
/// Represents a dependency edge in the system DAG.
/// </summary>
public readonly record struct SystemDependency
{
    /// <summary>System that must run first.</summary>
    public int Before { get; init; }

    /// <summary>System that must run after.</summary>
    public int After { get; init; }

    /// <summary>Reason for the dependency.</summary>
    public DependencyReason Reason { get; init; }

    public SystemDependency(int before, int after, DependencyReason reason)
    {
        Before = before;
        After = after;
        Reason = reason;
    }
}

/// <summary>
/// Reason why two systems have a dependency.
/// </summary>
public enum DependencyReason : byte
{
    /// <summary>System A writes a component that System B reads.</summary>
    WriteRead,

    /// <summary>System A reads a component that System B writes.</summary>
    ReadWrite,

    /// <summary>Both systems write the same component.</summary>
    WriteWrite,

    /// <summary>Explicit [After] attribute.</summary>
    ExplicitAfter,

    /// <summary>Explicit [Before] attribute.</summary>
    ExplicitBefore,

    /// <summary>System group ordering.</summary>
    GroupOrder
}
```

---

## Generated Registry

### SystemRegistry

```csharp
namespace Paradise.ECS;

/// <summary>
/// Registry containing all system metadata and the pre-computed execution DAG.
/// Generated at compile time by SystemGenerator.
/// </summary>
public static class SystemRegistry<TMask>
    where TMask : unmanaged, IBitSet<TMask>
{
    private static readonly ImmutableArray<SystemMetadata<TMask>> s_systems;
    private static readonly ImmutableArray<SystemDependency> s_dependencies;
    private static readonly ImmutableArray<ImmutableArray<int>> s_executionWaves;

    /// <summary>All system metadata, indexed by SystemId.</summary>
    public static ImmutableArray<SystemMetadata<TMask>> Systems => s_systems;

    /// <summary>All dependency edges in the system DAG.</summary>
    public static ImmutableArray<SystemDependency> Dependencies => s_dependencies;

    /// <summary>
    /// Pre-computed parallel execution waves.
    /// Systems in the same wave can run in parallel (no conflicts).
    /// </summary>
    public static ImmutableArray<ImmutableArray<int>> ExecutionWaves => s_executionWaves;

    /// <summary>Total number of registered systems.</summary>
    public static int Count { get; }

    static SystemRegistry()
    {
        // Generated initialization...
    }
}
```

---

## Runtime Scheduler

### SystemScheduler

```csharp
namespace Paradise.ECS;

/// <summary>
/// Executes systems in dependency order with optional parallelism.
/// </summary>
public sealed class SystemScheduler<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    /// <summary>
    /// Executes all systems sequentially in dependency order.
    /// </summary>
    public void RunSequential<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        foreach (var wave in SystemRegistry<TMask>.ExecutionWaves)
        {
            foreach (var systemId in wave)
            {
                RunSystem(world, systemId);
            }
        }
    }

    /// <summary>
    /// Executes all systems with system-level parallelism.
    /// Systems in the same wave run in parallel.
    /// </summary>
    public void RunParallel<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        foreach (var wave in SystemRegistry<TMask>.ExecutionWaves)
        {
            if (wave.Length == 1)
            {
                RunSystem(world, wave[0]);
            }
            else
            {
                Parallel.For(0, wave.Length, i => RunSystem(world, wave[i]));
            }
        }
    }

    /// <summary>
    /// Executes all systems with chunk-level parallelism.
    /// Different chunks from systems in the same wave can run on different threads.
    /// </summary>
    public void RunParallelChunks<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        foreach (var wave in SystemRegistry<TMask>.ExecutionWaves)
        {
            var workItems = CollectChunkWorkItems(world, wave);

            if (workItems.Count == 0)
                continue;

            if (workItems.Count == 1)
            {
                workItems[0].Execute(world);
            }
            else
            {
                Parallel.ForEach(workItems, item => item.Execute(world));
            }
        }
    }

    private void RunSystem<TWorld>(TWorld world, int systemId)
        where TWorld : IWorld<TMask, TConfig>
    {
        // Dispatch to generated static Run method
    }

    private List<ChunkWorkItem> CollectChunkWorkItems<TWorld>(
        TWorld world,
        ImmutableArray<int> wave)
        where TWorld : IWorld<TMask, TConfig>
    {
        var items = new List<ChunkWorkItem>();

        foreach (var systemId in wave)
        {
            var metadata = SystemRegistry<TMask>.Systems[systemId];
            var query = world.ArchetypeRegistry.GetOrCreateQuery(metadata.QueryDescription);

            foreach (var archetype in query.Archetypes)
            {
                for (int i = 0; i < archetype.ChunkCount; i++)
                {
                    items.Add(new ChunkWorkItem(systemId, archetype, i));
                }
            }
        }

        return items;
    }
}
```

---

## Generated Code Pattern

### Per-Entity Execution (Default)

User code:
```csharp
[System]
[With<Position>]
[With<Velocity>(IsReadOnly = true)]
public partial struct MovementSystem;

public partial struct MovementSystem
{
    public static void Execute(ref Position position, in Velocity velocity)
    {
        position = new Position(position.X + velocity.X, position.Y + velocity.Y);
    }
}
```

Generated:
```csharp
// MovementSystem.g.cs
public partial struct MovementSystem : ISystem<TMask, TConfig>
{
    public static int SystemId => 0;
    public static string Name => "MovementSystem";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Run<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        // Reuse existing ChunkQuery infrastructure
        var chunkQuery = world.ChunkQuery(default(Query));

        foreach (var chunk in chunkQuery)
        {
            var positions = chunk.Positions;       // Span<Position>
            var velocities = chunk.Velocitys;      // ReadOnlySpan<Velocity>

            for (int i = 0; i < chunk.EntityCount; i++)
            {
                Execute(ref positions[i], in velocities[i]);
            }
        }
    }

    /// <summary>User-defined per-entity logic.</summary>
    public static partial void Execute(ref Position position, in Velocity velocity);

    /// <summary>Nested query type reusing Queryable infrastructure.</summary>
    [Queryable]
    [With<Position>]
    [With<Velocity>(IsReadOnly = true)]
    public readonly ref partial struct Query;
}
```

### Chunk-Level Execution (SIMD-Friendly)

User code:
```csharp
[System(ChunkExecution = true)]
[With<Position>]
[With<Velocity>(IsReadOnly = true)]
public partial struct MovementSystemSIMD;

public partial struct MovementSystemSIMD
{
    public static void Execute(
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

Generated:
```csharp
public partial struct MovementSystemSIMD : ISystem<TMask, TConfig>
{
    public static int SystemId => 1;
    public static string Name => "MovementSystemSIMD";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Run<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        var chunkQuery = world.ChunkQuery(default(Query));

        foreach (var chunk in chunkQuery)
        {
            // Pass spans directly
            Execute(chunk.Positions, chunk.Velocitys, chunk.EntityCount);
        }
    }

    /// <summary>User-defined chunk execution logic.</summary>
    public static partial void Execute(
        Span<Position> positions,
        ReadOnlySpan<Velocity> velocities,
        int entityCount);

    [Queryable]
    [With<Position>]
    [With<Velocity>(IsReadOnly = true)]
    public readonly ref partial struct Query;
}
```

---

## DAG Computation (Compile-Time)

```csharp
internal static class DagComputation
{
    /// <summary>
    /// Builds dependency edges from system metadata.
    /// </summary>
    public static List<SystemDependency> BuildEdges(ImmutableArray<SystemInfo> systems)
    {
        var edges = new List<SystemDependency>();

        for (int i = 0; i < systems.Length; i++)
        {
            for (int j = i + 1; j < systems.Length; j++)
            {
                var a = systems[i];
                var b = systems[j];

                // Write-Read: a writes something b reads
                if (Conflicts(a.WriteMask, b.ReadMask))
                {
                    edges.Add(new SystemDependency(i, j, DependencyReason.WriteRead));
                }
                // Read-Write: a reads something b writes
                else if (Conflicts(a.ReadMask, b.WriteMask))
                {
                    edges.Add(new SystemDependency(i, j, DependencyReason.ReadWrite));
                }
                // Write-Write: both write same component (use declaration order)
                else if (Conflicts(a.WriteMask, b.WriteMask))
                {
                    edges.Add(new SystemDependency(i, j, DependencyReason.WriteWrite));
                }
            }
        }

        return edges;
    }

    /// <summary>
    /// Computes parallel execution waves via Kahn's algorithm.
    /// </summary>
    public static ImmutableArray<ImmutableArray<int>> ComputeWaves(
        int systemCount,
        List<SystemDependency> edges)
    {
        var inDegree = new int[systemCount];
        var adjacency = new List<int>[systemCount];

        for (int i = 0; i < systemCount; i++)
            adjacency[i] = new List<int>();

        foreach (var edge in edges)
        {
            adjacency[edge.Before].Add(edge.After);
            inDegree[edge.After]++;
        }

        var waves = new List<ImmutableArray<int>>();
        var currentWave = new List<int>();

        for (int i = 0; i < systemCount; i++)
        {
            if (inDegree[i] == 0)
                currentWave.Add(i);
        }

        while (currentWave.Count > 0)
        {
            waves.Add(currentWave.ToImmutableArray());

            var nextWave = new List<int>();
            foreach (var systemId in currentWave)
            {
                foreach (var dependent in adjacency[systemId])
                {
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                        nextWave.Add(dependent);
                }
            }

            currentWave = nextWave;
        }

        // Cycle detection
        int processed = waves.Sum(w => w.Length);
        if (processed != systemCount)
        {
            throw new InvalidOperationException("Cycle detected in system dependencies");
        }

        return waves.ToImmutableArray();
    }
}
```

---

## Generator Integration

The SystemGenerator extends QueryableGenerator's infrastructure:

```csharp
[Generator]
public class SystemGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find [System] attributed structs
        var systems = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Paradise.ECS.SystemAttribute",
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => ExtractSystemInfo(ctx))
            .Where(static x => x is not null);

        var collected = systems.Collect();

        context.RegisterSourceOutput(collected, GenerateAllSystems);
    }

    private static SystemInfo ExtractSystemInfo(GeneratorAttributeSyntaxContext ctx)
    {
        // Reuse QueryableGenerator's attribute extraction logic for:
        // - [With<T>] → ReadMask (all), WriteMask (if !IsReadOnly)
        // - [Without<T>] → NoneSet
        // - [WithAny<T>] → AnySet
        // - [Optional<T>] → OptionalComponents
        //
        // Additionally extract:
        // - [After<T>] → ExplicitAfter edges
        // - [Before<T>] → ExplicitBefore edges
        // - [System] Group, ChunkExecution properties
    }

    private static void GenerateAllSystems(
        SourceProductionContext ctx,
        ImmutableArray<SystemInfo> systems)
    {
        // 1. Generate each system's partial implementation
        foreach (var system in systems)
        {
            GenerateSystemPartial(ctx, system);
        }

        // 2. Compute DAG at compile time
        var edges = DagComputation.BuildEdges(systems);
        var waves = DagComputation.ComputeWaves(systems.Length, edges);

        // 3. Generate SystemRegistry
        GenerateSystemRegistry(ctx, systems, edges, waves);
    }
}
```

---

## Complete Example

```csharp
// Components
[Component]
public partial struct Position { public float X, Y; }

[Component]
public partial struct Velocity { public float X, Y; }

[Component]
public partial struct Health { public int Current, Max; }

// Systems
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
        if (pos.X < 0)
            health = new Health { Current = health.Current - 1, Max = health.Max };
    }
}

[System]
[With<Position>(IsReadOnly = true)]
[With<Sprite>(IsReadOnly = true)]
public partial struct RenderSystem;

public partial struct RenderSystem
{
    public static void Execute(in Position pos, in Sprite sprite)
    {
        Draw(sprite.Id, pos.X, pos.Y);
    }
}

// Generated DAG:
//
//     MovementSystem (writes Position)
//          │
//     ┌────┴────┐
//     ▼         ▼
// DamageSystem  RenderSystem  ← Can run in PARALLEL!
// (writes Health, reads Position)  (reads only)
//
// Waves:
//   Wave 0: [MovementSystem]
//   Wave 1: [DamageSystem, RenderSystem]

// Usage
var world = new World();
var scheduler = new SystemScheduler<Mask, Config>();

while (running)
{
    scheduler.RunParallelChunks(world);
}
```
