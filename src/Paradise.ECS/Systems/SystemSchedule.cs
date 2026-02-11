using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// Delegate matching the <c>RunChunk</c> signature for system dispatch.
/// Used by <see cref="SystemSchedule{TMask,TConfig}"/> to invoke systems without a generated switch.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
/// <param name="world">The world containing the entities.</param>
/// <param name="chunk">The chunk handle to process.</param>
/// <param name="layout">The archetype layout describing component offsets.</param>
/// <param name="entityCount">The number of entities in the chunk.</param>
public delegate void SystemRunChunkAction<TMask, TConfig>(
    IWorld<TMask, TConfig> world,
    ChunkHandle chunk,
    ImmutableArchetypeLayout<TMask, TConfig> layout,
    int entityCount)
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new();

/// <summary>
/// Pre-built execution schedule for systems. The scheduling strategy is determined at build time
/// via the <see cref="IWaveScheduler"/> provided to the builder.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public sealed class SystemSchedule<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    private readonly IWorld<TMask, TConfig> _world;
    private readonly ImmutableArray<ImmutableArray<int>> _waves;
    private readonly ImmutableArray<SystemRunChunkAction<TMask, TConfig>> _dispatchers;
    private readonly ImmutableArray<SystemMetadata<TMask>> _metadata;
    private readonly IWaveScheduler _scheduler;
    private readonly List<WorkItem<TMask, TConfig>> _workItems = new();

    internal SystemSchedule(
        IWorld<TMask, TConfig> world,
        ImmutableArray<ImmutableArray<int>> waves,
        ImmutableArray<SystemRunChunkAction<TMask, TConfig>> dispatchers,
        ImmutableArray<SystemMetadata<TMask>> metadata,
        IWaveScheduler scheduler)
    {
        _world = world;
        _waves = waves;
        _dispatchers = dispatchers;
        _metadata = metadata;
        _scheduler = scheduler;
    }

    /// <summary>
    /// Creates a schedule builder for the given world.
    /// </summary>
    /// <param name="world">The world to create a schedule for.</param>
    /// <returns>A new schedule builder.</returns>
    public static SystemScheduleBuilder<TMask, TConfig> Create(IWorld<TMask, TConfig> world)
        => new(world);

    /// <summary>Runs all enabled systems using the scheduler provided at build time.</summary>
    public void Run()
    {
        foreach (var wave in _waves)
        {
            _workItems.Clear();
            foreach (var systemId in wave)
            {
                var dispatcher = _dispatchers[systemId];
                var q = _world.ArchetypeRegistry.GetOrCreateQuery(_metadata[systemId].QueryDescription);
                foreach (var ci in q.Chunks)
                {
                    _workItems.Add(new WorkItem<TMask, TConfig>(
                        systemId,
                        ci.Handle,
                        dispatcher,
                        _world,
                        ci.Archetype.Layout.DataPointer,
                        ci.EntityCount));
                }
            }
            _scheduler.Execute(_workItems);
        }
    }
}

/// <summary>
/// Builder for selecting which systems to include in a schedule.
/// Dependency resolution happens at <see cref="Build(IDagScheduler, IWaveScheduler)"/> time,
/// computing waves only for the systems actually added.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public readonly struct SystemScheduleBuilder<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    private readonly IWorld<TMask, TConfig> _world;
    private readonly List<SystemMetadata<TMask>> _metadata;
    private readonly List<SystemRunChunkAction<TMask, TConfig>> _dispatchers;

    internal SystemScheduleBuilder(IWorld<TMask, TConfig> world)
    {
        _world = world;
        _metadata = new List<SystemMetadata<TMask>>();
        _dispatchers = new List<SystemRunChunkAction<TMask, TConfig>>();
    }

    /// <summary>Adds a system to the schedule.</summary>
    /// <typeparam name="T">The system type implementing <see cref="ISystem{TMask,TConfig}"/>.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public SystemScheduleBuilder<TMask, TConfig> Add<T>()
        where T : ISystem<TMask, TConfig>, allows ref struct
    {
        _metadata.Add(T.Metadata);
        _dispatchers.Add(T.RunChunk);
        return this;
    }

    /// <summary>Builds a schedule with the default DAG scheduler and a custom wave scheduler.</summary>
    /// <typeparam name="TScheduler">The wave scheduler type implementing <see cref="IWaveScheduler"/>.</typeparam>
    /// <returns>A new <see cref="SystemSchedule{TMask,TConfig}"/>.</returns>
    public SystemSchedule<TMask, TConfig> Build<TScheduler>()
        where TScheduler : IWaveScheduler, new()
        => Build(new DefaultDagScheduler(), new TScheduler());

    /// <summary>Builds a schedule with the default DAG scheduler and a custom wave scheduler instance.</summary>
    /// <param name="scheduler">The wave scheduler strategy to use.</param>
    /// <returns>A new <see cref="SystemSchedule{TMask,TConfig}"/>.</returns>
    public SystemSchedule<TMask, TConfig> Build(IWaveScheduler scheduler)
        => Build(new DefaultDagScheduler(), scheduler);

    /// <summary>Builds a schedule with a custom DAG scheduler and wave scheduler.</summary>
    /// <param name="dag">The DAG scheduler for computing execution waves.</param>
    /// <param name="scheduler">The wave scheduler strategy to use.</param>
    /// <returns>A new <see cref="SystemSchedule{TMask,TConfig}"/>.</returns>
    public SystemSchedule<TMask, TConfig> Build(IDagScheduler dag, IWaveScheduler scheduler)
    {
        var metadataSpan = CollectionsMarshal.AsSpan(_metadata);
        var rawWaves = dag.ComputeWaves(metadataSpan);
        var wavesBuilder = ImmutableArray.CreateBuilder<ImmutableArray<int>>(rawWaves.Length);
        foreach (var wave in rawWaves)
            wavesBuilder.Add(ImmutableArray.Create(wave));
        return new SystemSchedule<TMask, TConfig>(
            _world,
            wavesBuilder.MoveToImmutable(),
            ImmutableArray.Create(_dispatchers.ToArray()),
            ImmutableArray.Create(_metadata.ToArray()),
            scheduler);
    }
}
