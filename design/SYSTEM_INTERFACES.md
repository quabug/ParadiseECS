# System API: Interface Specifications

This document defines the concrete C# interfaces and types for the System API.

---

## Core Interfaces

### ISystem

```csharp
namespace Paradise.ECS;

/// <summary>
/// Marker interface for systems. Implemented by generated code.
/// </summary>
/// <typeparam name="TMask">The component mask type.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public interface ISystem<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    /// <summary>
    /// Unique system identifier, assigned at compile time.
    /// </summary>
    static abstract int SystemId { get; }

    /// <summary>
    /// Human-readable system name for debugging.
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Executes the system on the given world.
    /// </summary>
    static abstract void Run<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>;
}
```

### IChunkSystem (for SIMD-friendly execution)

```csharp
namespace Paradise.ECS;

/// <summary>
/// Interface for systems that process entire chunks at once.
/// Enables SIMD vectorization and batch optimizations.
/// </summary>
public interface IChunkSystem<TMask, TConfig> : ISystem<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    /// <summary>
    /// Executes the system on a single chunk.
    /// Called once per matching chunk.
    /// </summary>
    static abstract void ExecuteChunk<TWorld>(
        TWorld world,
        ChunkHandle chunk,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        int entityCount)
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
/// Used by the scheduler to determine execution order.
/// </summary>
/// <typeparam name="TMask">The component mask type.</typeparam>
public readonly record struct SystemMetadata<TMask>
    where TMask : unmanaged, IBitSet<TMask>
{
    /// <summary>Unique system identifier.</summary>
    public int Id { get; init; }

    /// <summary>System name for debugging.</summary>
    public string Name { get; init; }

    /// <summary>Components this system reads (includes writes).</summary>
    public TMask ReadMask { get; init; }

    /// <summary>Components this system writes.</summary>
    public TMask WriteMask { get; init; }

    /// <summary>Query description for matching entities.</summary>
    public HashedKey<ImmutableQueryDescription<TMask>> QueryDescription { get; init; }

    /// <summary>Whether this system can run in parallel with itself (chunk-level).</summary>
    public bool AllowChunkParallelism { get; init; }

    /// <summary>
    /// Creates system metadata.
    /// </summary>
    public SystemMetadata(
        int id,
        string name,
        TMask readMask,
        TMask writeMask,
        HashedKey<ImmutableQueryDescription<TMask>> queryDescription,
        bool allowChunkParallelism = true)
    {
        Id = id;
        Name = name;
        ReadMask = readMask;
        WriteMask = writeMask;
        QueryDescription = queryDescription;
        AllowChunkParallelism = allowChunkParallelism;
    }

    /// <summary>
    /// Checks if this system has a read-write conflict with another system.
    /// Returns true if they cannot run in parallel.
    /// </summary>
    public bool ConflictsWith(in SystemMetadata<TMask> other)
    {
        // Write-Read conflict: this writes something other reads
        if (!WriteMask.And(other.ReadMask).IsEmpty)
            return true;

        // Read-Write conflict: this reads something other writes
        if (!ReadMask.And(other.WriteMask).IsEmpty)
            return true;

        // Write-Write conflict: both write same component
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

    /// <summary>Reason for the dependency (for debugging).</summary>
    public DependencyReason Reason { get; init; }

    /// <summary>
    /// Creates a dependency edge.
    /// </summary>
    public SystemDependency(int before, int after, DependencyReason reason)
    {
        Before = before;
        After = after;
        Reason = reason;
    }
}

/// <summary>
/// Why two systems have a dependency.
/// </summary>
public enum DependencyReason
{
    /// <summary>System A writes a component that System B reads.</summary>
    WriteReadConflict,

    /// <summary>System A reads a component that System B writes.</summary>
    ReadWriteConflict,

    /// <summary>Both systems write the same component.</summary>
    WriteWriteConflict,

    /// <summary>Explicit [After] attribute.</summary>
    ExplicitAfter,

    /// <summary>Explicit [Before] attribute.</summary>
    ExplicitBefore,

    /// <summary>System group ordering.</summary>
    GroupOrdering
}
```

---

## Attributes

### System Attributes

```csharp
namespace Paradise.ECS;

/// <summary>
/// Marks a struct as a system.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
public sealed class SystemAttribute : Attribute
{
    /// <summary>
    /// Optional system group this system belongs to.
    /// </summary>
    public Type? Group { get; set; }

    /// <summary>
    /// Optional manual system ID. Auto-assigned if not specified.
    /// </summary>
    public int Id { get; set; } = -1;
}

/// <summary>
/// Marks a struct as a system group for logical organization.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
public sealed class SystemGroupAttribute : Attribute;

/// <summary>
/// Indicates the system processes chunks directly for SIMD operations.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
public sealed class ChunkExecutionAttribute : Attribute;
```

### Component Access Attributes

```csharp
namespace Paradise.ECS;

/// <summary>
/// Declares that a system reads a component type.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class ReadsAttribute<T> : Attribute
    where T : unmanaged;

/// <summary>
/// Declares that a system writes a component type.
/// Implicitly includes read access.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class WritesAttribute<T> : Attribute
    where T : unmanaged;
```

### Ordering Attributes

```csharp
namespace Paradise.ECS;

/// <summary>
/// Declares that this system must run after the specified system or group.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class AfterAttribute<T> : Attribute;

/// <summary>
/// Declares that this system must run before the specified system or group.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class BeforeAttribute<T> : Attribute;

/// <summary>
/// Declares that this system should run at the very beginning.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
public sealed class RunFirstAttribute : Attribute;

/// <summary>
/// Declares that this system should run at the very end.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
public sealed class RunLastAttribute : Attribute;
```

---

## Generated Registry

### SystemRegistry

```csharp
namespace Paradise.ECS;

/// <summary>
/// Registry containing all system metadata and the execution DAG.
/// Generated at compile time by SystemGenerator.
/// </summary>
public static class SystemRegistry<TMask>
    where TMask : unmanaged, IBitSet<TMask>
{
    /// <summary>
    /// All system metadata, indexed by SystemId.
    /// </summary>
    public static ImmutableArray<SystemMetadata<TMask>> Systems { get; }

    /// <summary>
    /// All dependency edges in the system DAG.
    /// </summary>
    public static ImmutableArray<SystemDependency> Dependencies { get; }

    /// <summary>
    /// Pre-computed parallel execution waves.
    /// Systems in the same wave have no conflicts and can run in parallel.
    /// </summary>
    public static ImmutableArray<ImmutableArray<int>> ExecutionWaves { get; }

    /// <summary>
    /// Total number of registered systems.
    /// </summary>
    public static int Count { get; }
}
```

---

## Scheduler

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
    private readonly ImmutableArray<Action<object>> _systemRunners;

    /// <summary>
    /// Creates a new system scheduler.
    /// </summary>
    public SystemScheduler()
    {
        // Build runner delegates from generated registry
        _systemRunners = BuildSystemRunners();
    }

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
                RunSystem(world, wave[0]);
            }
            else
            {
                // Parallel execution of independent systems
                Parallel.For(0, wave.Length, i => RunSystem(world, wave[i]));
            }
        }
    }

    /// <summary>
    /// Executes all systems with chunk-level parallelism.
    /// Different chunks can be processed by different threads.
    /// </summary>
    public void RunAllParallelChunks<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        foreach (var wave in SystemRegistry<TMask>.ExecutionWaves)
        {
            // Collect all chunk work items from systems in this wave
            var workItems = new List<ChunkWorkItem>();

            foreach (var systemId in wave)
            {
                var metadata = SystemRegistry<TMask>.Systems[systemId];
                if (!metadata.AllowChunkParallelism)
                {
                    // System doesn't allow chunk parallelism - run sequentially
                    RunSystem(world, systemId);
                    continue;
                }

                // Collect chunks for this system
                var query = world.ArchetypeRegistry.GetOrCreateQuery(metadata.QueryDescription);
                foreach (var chunkInfo in query.Chunks)
                {
                    workItems.Add(new ChunkWorkItem(
                        systemId,
                        chunkInfo.Handle,
                        chunkInfo.Archetype.Layout,
                        chunkInfo.EntityCount));
                }
            }

            // Process all chunks in parallel
            if (workItems.Count > 0)
            {
                Parallel.ForEach(workItems, item =>
                {
                    RunSystemChunk(world, item.SystemId, item.Chunk, item.Layout, item.EntityCount);
                });
            }
        }
    }

    private void RunSystem<TWorld>(TWorld world, int systemId)
        where TWorld : IWorld<TMask, TConfig>
    {
        _systemRunners[systemId](world);
    }

    private void RunSystemChunk<TWorld>(
        TWorld world,
        int systemId,
        ChunkHandle chunk,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        int entityCount)
        where TWorld : IWorld<TMask, TConfig>
    {
        // Dispatch to generated chunk executor
        // This would be generated code that calls the specific system's ExecuteChunk
    }

    private readonly record struct ChunkWorkItem(
        int SystemId,
        ChunkHandle Chunk,
        ImmutableArchetypeLayout<TMask, TConfig> Layout,
        int EntityCount);
}
```

---

## Generated System Example

For user code:
```csharp
[System]
[Reads<Position>]
[Reads<Velocity>]
[Writes<Position>]
public partial struct MovementSystem;

public partial struct MovementSystem
{
    public static void Execute(ref Position position, in Velocity velocity)
    {
        position = new Position(position.X + velocity.X, position.Y + velocity.Y);
    }
}
```

The generator produces:
```csharp
// Generated: MovementSystem.g.cs
public partial struct MovementSystem : ISystem<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    public static int SystemId => 0;
    public static string Name => "MovementSystem";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Run<TWorld>(TWorld world)
        where TWorld : IWorld<TMask, TConfig>
    {
        var query = QueryHelpers.CreateChunkQueryResult<ChunkData, TMask, TConfig>(
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

    // User-defined partial method declaration
    public static partial void Execute(ref Position position, in Velocity velocity);

    /// <summary>
    /// Chunk data for MovementSystem providing span access.
    /// </summary>
    public readonly ref struct ChunkData
    {
        private readonly ChunkManager _chunkManager;
        private readonly ChunkHandle _chunk;
        private readonly ImmutableArchetypeLayout<TMask, TConfig> _layout;
        private readonly int _entityCount;

        public int EntityCount => _entityCount;

        public Span<Position> GetSpan<Position>() { /* ... */ }
        public ReadOnlySpan<Velocity> GetReadOnlySpan<Velocity>() { /* ... */ }
    }
}
```

---

## DAG Computation Algorithm

```csharp
internal static class DagBuilder
{
    /// <summary>
    /// Builds the system execution DAG and computes parallel waves.
    /// Called by the source generator at compile time.
    /// </summary>
    public static (ImmutableArray<SystemDependency> edges, ImmutableArray<ImmutableArray<int>> waves)
        BuildDag<TMask>(ImmutableArray<SystemMetadata<TMask>> systems)
        where TMask : unmanaged, IBitSet<TMask>
    {
        var edges = new List<SystemDependency>();
        var n = systems.Length;

        // 1. Add edges from component access conflicts
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                var a = systems[i];
                var b = systems[j];

                // Check write-read conflict: a writes, b reads
                if (!a.WriteMask.And(b.ReadMask).IsEmpty)
                {
                    edges.Add(new SystemDependency(i, j, DependencyReason.WriteReadConflict));
                }
                // Check read-write conflict: a reads, b writes
                else if (!a.ReadMask.And(b.WriteMask).IsEmpty)
                {
                    edges.Add(new SystemDependency(i, j, DependencyReason.ReadWriteConflict));
                }
                // Check write-write conflict
                else if (!a.WriteMask.And(b.WriteMask).IsEmpty)
                {
                    // Use declaration order as tiebreaker
                    edges.Add(new SystemDependency(i, j, DependencyReason.WriteWriteConflict));
                }
            }
        }

        // 2. Add explicit ordering edges (from attributes)
        // ... handled during system info extraction

        // 3. Topological sort to detect cycles
        var (sorted, hasCycle) = TopologicalSort(n, edges);
        if (hasCycle)
        {
            // Report diagnostic error
            throw new InvalidOperationException("Cycle detected in system dependencies");
        }

        // 4. Compute parallel waves using graph coloring
        var waves = ComputeWaves(n, edges, sorted);

        return (edges.ToImmutableArray(), waves);
    }

    /// <summary>
    /// Partitions systems into waves where systems in the same wave can run in parallel.
    /// </summary>
    private static ImmutableArray<ImmutableArray<int>> ComputeWaves(
        int n,
        List<SystemDependency> edges,
        int[] topoOrder)
    {
        // Compute longest path to each node (this determines the wave)
        var waveIndex = new int[n];
        var adjList = BuildAdjacencyList(n, edges);

        foreach (var systemId in topoOrder)
        {
            int maxPredWave = -1;
            foreach (var (pred, _) in GetPredecessors(systemId, edges))
            {
                maxPredWave = Math.Max(maxPredWave, waveIndex[pred]);
            }
            waveIndex[systemId] = maxPredWave + 1;
        }

        // Group by wave
        int maxWave = waveIndex.Max();
        var waves = new List<int>[maxWave + 1];
        for (int i = 0; i <= maxWave; i++)
            waves[i] = new List<int>();

        for (int i = 0; i < n; i++)
            waves[waveIndex[i]].Add(i);

        return waves.Select(w => w.ToImmutableArray()).ToImmutableArray();
    }
}
```

---

## Usage Example

```csharp
// Game.cs
public class Game
{
    private readonly World _world;
    private readonly SystemScheduler<Mask, Config> _scheduler;

    public Game()
    {
        _world = new World();
        _scheduler = new SystemScheduler<Mask, Config>();
    }

    public void Run()
    {
        while (IsRunning)
        {
            // Execute all systems in optimized order
            _scheduler.RunAllParallelChunks(_world);

            // Or sequential with system-level parallelism only:
            // _scheduler.RunAll(_world);
        }
    }
}
```
