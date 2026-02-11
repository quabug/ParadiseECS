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
    private readonly int[][] _waves;
    private readonly SystemRunChunkAction<TMask, TConfig>?[] _dispatchers;
    private readonly HashedKey<ImmutableQueryDescription<TMask>>[] _queryDescriptions;
    private readonly IWaveScheduler _scheduler;
    private readonly List<WorkItem> _workItems = new();

    internal SystemSchedule(
        IWorld<TMask, TConfig> world,
        int[][] waves,
        SystemRunChunkAction<TMask, TConfig>?[] dispatchers,
        HashedKey<ImmutableQueryDescription<TMask>>[] queryDescriptions,
        IWaveScheduler scheduler)
    {
        _world = world;
        _waves = waves;
        _dispatchers = dispatchers;
        _queryDescriptions = queryDescriptions;
        _scheduler = scheduler;
    }

    /// <summary>
    /// Creates a schedule builder for the given world.
    /// </summary>
    /// <param name="world">The world to create a schedule for.</param>
    /// <param name="systemCount">The total number of registered systems.</param>
    /// <param name="waves">The pre-computed execution waves from DAG analysis.</param>
    /// <returns>A new schedule builder.</returns>
    public static SystemScheduleBuilder<TMask, TConfig> Create(
        IWorld<TMask, TConfig> world, int systemCount, int[][] waves)
        => new(world, systemCount, waves);

    /// <summary>Runs all enabled systems using the scheduler provided at build time.</summary>
    public void Run()
    {
        foreach (var wave in _waves)
        {
            _workItems.Clear();
            foreach (var systemId in wave)
            {
                var dispatcher = _dispatchers[systemId];
                if (dispatcher == null) continue;
                var q = _world.ArchetypeRegistry.GetOrCreateQuery(_queryDescriptions[systemId]);
                foreach (var ci in q.Chunks)
                {
                    var handle = ci.Handle;
                    var layoutPtr = ci.Archetype.Layout.DataPointer;
                    var entityCount = ci.EntityCount;
                    var d = dispatcher;
                    var world = _world;
                    _workItems.Add(new WorkItem(
                        systemId,
                        handle,
                        () => d(world, handle, new ImmutableArchetypeLayout<TMask, TConfig>(layoutPtr), entityCount)));
                }
            }
            _scheduler.Execute(CollectionsMarshal.AsSpan(_workItems));
        }
    }
}

/// <summary>
/// Builder for selecting which systems to include in a schedule.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public readonly struct SystemScheduleBuilder<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    private readonly IWorld<TMask, TConfig> _world;
    private readonly int[][] _waves;
    private readonly SystemRunChunkAction<TMask, TConfig>?[] _dispatchers;
    private readonly HashedKey<ImmutableQueryDescription<TMask>>[] _queryDescriptions;

    internal SystemScheduleBuilder(IWorld<TMask, TConfig> world, int count, int[][] waves)
    {
        _world = world;
        _waves = waves;
        _dispatchers = new SystemRunChunkAction<TMask, TConfig>?[count];
        _queryDescriptions = new HashedKey<ImmutableQueryDescription<TMask>>[count];
    }

    /// <summary>Adds a system to the schedule.</summary>
    /// <typeparam name="T">The system type implementing <see cref="ISystem{TMask,TConfig}"/>.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public SystemScheduleBuilder<TMask, TConfig> Add<T>()
        where T : ISystem<TMask, TConfig>, allows ref struct
    {
        _dispatchers[T.SystemId] = T.RunChunk;
        _queryDescriptions[T.SystemId] = T.QueryDescription;
        return this;
    }

    /// <summary>Builds a schedule with a custom wave scheduler.</summary>
    /// <typeparam name="TScheduler">The scheduler type implementing <see cref="IWaveScheduler"/>.</typeparam>
    /// <returns>A new <see cref="SystemSchedule{TMask,TConfig}"/> using the provided scheduler.</returns>
    public SystemSchedule<TMask, TConfig> Build<TScheduler>()
        where TScheduler : IWaveScheduler, new()
        => Build(new TScheduler());

    /// <summary>Builds a schedule with a custom wave scheduler instance.</summary>
    /// <param name="scheduler">The scheduler strategy to use for executing work items within each wave.</param>
    /// <returns>A new <see cref="SystemSchedule{TMask,TConfig}"/> using the provided scheduler.</returns>
    public SystemSchedule<TMask, TConfig> Build(IWaveScheduler scheduler)
    {
        return new SystemSchedule<TMask, TConfig>(_world, _waves, _dispatchers, _queryDescriptions, scheduler);
    }
}
