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
/// Pre-built execution schedule for systems. Supports sequential and parallel execution.
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

    internal SystemSchedule(
        IWorld<TMask, TConfig> world,
        int[][] waves,
        SystemRunChunkAction<TMask, TConfig>?[] dispatchers,
        HashedKey<ImmutableQueryDescription<TMask>>[] queryDescriptions)
    {
        _world = world;
        _waves = waves;
        _dispatchers = dispatchers;
        _queryDescriptions = queryDescriptions;
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

    /// <summary>Runs all enabled systems sequentially, wave by wave.</summary>
    public void RunSequential()
    {
        foreach (var wave in _waves)
        {
            foreach (var systemId in wave)
            {
                var dispatcher = _dispatchers[systemId];
                if (dispatcher == null) continue;
                var query = _world.ArchetypeRegistry.GetOrCreateQuery(_queryDescriptions[systemId]);
                foreach (var chunkInfo in query.Chunks)
                {
                    dispatcher(_world, chunkInfo.Handle, chunkInfo.Archetype.Layout, chunkInfo.EntityCount);
                }
            }
        }
    }

    /// <summary>Runs systems in parallel within each wave, sequentially between waves.</summary>
    public void RunParallel()
    {
        foreach (var wave in _waves)
        {
            var actions = new System.Collections.Generic.List<System.Action>();
            foreach (var systemId in wave)
            {
                var dispatcher = _dispatchers[systemId];
                if (dispatcher == null) continue;
                var sid = systemId;
                actions.Add(() =>
                {
                    var q = _world.ArchetypeRegistry.GetOrCreateQuery(_queryDescriptions[sid]);
                    foreach (var ci in q.Chunks)
                        dispatcher(_world, ci.Handle, ci.Archetype.Layout, ci.EntityCount);
                });
            }
            if (actions.Count == 1) actions[0]();
            else if (actions.Count > 1) System.Threading.Tasks.Parallel.Invoke(actions.ToArray());
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

    /// <summary>Builds the schedule with the selected systems.</summary>
    /// <returns>A new <see cref="SystemSchedule{TMask,TConfig}"/> ready for execution.</returns>
    public SystemSchedule<TMask, TConfig> Build()
    {
        return new SystemSchedule<TMask, TConfig>(_world, _waves, _dispatchers, _queryDescriptions);
    }
}
