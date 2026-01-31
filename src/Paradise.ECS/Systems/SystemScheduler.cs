using System.Collections.Immutable;

namespace Paradise.ECS;

/// <summary>
/// Executes systems in dependency order with optional parallelism.
/// Uses the pre-computed DAG from SystemRegistry.
/// </summary>
/// <typeparam name="TMask">The component mask type.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public sealed class SystemScheduler<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    private readonly Action<object>[] _systemRunners;

    /// <summary>
    /// Creates a new system scheduler.
    /// </summary>
    public SystemScheduler()
    {
        _systemRunners = new Action<object>[SystemRegistry<TMask>.Count];
        // System runners will be populated by generated code or via registration
    }

    /// <summary>
    /// Registers a system runner for the given system ID.
    /// </summary>
    /// <param name="systemId">The system ID.</param>
    /// <param name="runner">The runner delegate that takes a world and executes the system.</param>
    public void Register(int systemId, Action<object> runner)
    {
        if (systemId >= 0 && systemId < _systemRunners.Length)
        {
            _systemRunners[systemId] = runner;
        }
    }

    /// <summary>
    /// Executes all systems sequentially in dependency order.
    /// </summary>
    /// <typeparam name="TWorld">The world type.</typeparam>
    /// <param name="world">The world to execute systems on.</param>
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
    /// Executes all systems in dependency order.
    /// Systems within the same wave run in parallel.
    /// </summary>
    /// <typeparam name="TWorld">The world type.</typeparam>
    /// <param name="world">The world to execute systems on.</param>
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
    /// <typeparam name="TWorld">The world type.</typeparam>
    /// <param name="world">The world to execute systems on.</param>
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
                ExecuteChunkWorkItem(world, workItems[0]);
            }
            else
            {
                Parallel.ForEach(workItems, item => ExecuteChunkWorkItem(world, item));
            }
        }
    }

    private void RunSystem<TWorld>(TWorld world, int systemId)
        where TWorld : IWorld<TMask, TConfig>
    {
        var runner = _systemRunners[systemId];
        runner?.Invoke(world!);
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
                for (int chunkIdx = 0; chunkIdx < archetype.ChunkCount; chunkIdx++)
                {
                    items.Add(new ChunkWorkItem(systemId, archetype.Id, chunkIdx));
                }
            }
        }

        return items;
    }

    private void ExecuteChunkWorkItem<TWorld>(TWorld world, ChunkWorkItem item)
        where TWorld : IWorld<TMask, TConfig>
    {
        // For now, just run the full system
        // A more optimized implementation would run only the specific chunk
        RunSystem(world, item.SystemId);
    }

    private readonly struct ChunkWorkItem
    {
        public int SystemId { get; }
        public int ArchetypeId { get; }
        public int ChunkIndex { get; }

        public ChunkWorkItem(int systemId, int archetypeId, int chunkIndex)
        {
            SystemId = systemId;
            ArchetypeId = archetypeId;
            ChunkIndex = chunkIndex;
        }
    }
}
