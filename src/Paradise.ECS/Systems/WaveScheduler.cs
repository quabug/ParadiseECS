namespace Paradise.ECS;

/// <summary>
/// Represents a unit of work within a system execution wave.
/// Stores all invocation data inline to avoid closure allocations.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public readonly struct WorkItem<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    /// <summary>The system that produced this work item.</summary>
    public int SystemId { get; }

    /// <summary>The chunk this work item operates on.</summary>
    public ChunkHandle Chunk { get; }

    private readonly SystemRunChunkAction<TMask, TConfig> _dispatcher;
    private readonly IWorld<TMask, TConfig> _world;
    private readonly nint _layoutPtr;
    private readonly int _entityCount;

    internal WorkItem(
        int systemId,
        ChunkHandle chunk,
        SystemRunChunkAction<TMask, TConfig> dispatcher,
        IWorld<TMask, TConfig> world,
        nint layoutPtr,
        int entityCount)
    {
        SystemId = systemId;
        Chunk = chunk;
        _dispatcher = dispatcher;
        _world = world;
        _layoutPtr = layoutPtr;
        _entityCount = entityCount;
    }

    /// <summary>Executes this work item.</summary>
    public void Invoke() =>
        _dispatcher(_world, Chunk, new ImmutableArchetypeLayout<TMask, TConfig>(_layoutPtr), _entityCount);
}

/// <summary>
/// Strategy interface for scheduling work items within an execution wave.
/// Implement this to provide custom parallel execution strategies (e.g., job systems,
/// thread pools with data affinity, or custom work-stealing schedulers).
/// </summary>
public interface IWaveScheduler
{
    /// <summary>Executes all work items for a single wave. Must complete before returning.</summary>
    /// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <param name="items">The work items to execute.</param>
    void Execute<TMask, TConfig>(IReadOnlyList<WorkItem<TMask, TConfig>> items)
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new();
}

/// <summary>Executes work items sequentially on the calling thread.</summary>
public sealed class SequentialWaveScheduler : IWaveScheduler
{
    /// <inheritdoc/>
    public void Execute<TMask, TConfig>(IReadOnlyList<WorkItem<TMask, TConfig>> items)
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
    {
        for (int index = 0; index < items.Count; index++)
        {
            items[index].Invoke();
        }
    }
}

/// <summary>Executes work items in parallel using the .NET ThreadPool via <see cref="System.Threading.Tasks.Parallel"/>.</summary>
public sealed class ParallelWaveScheduler : IWaveScheduler
{
    /// <inheritdoc/>
    public void Execute<TMask, TConfig>(IReadOnlyList<WorkItem<TMask, TConfig>> items)
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
    {
        switch (items.Count)
        {
            case 0: return;
            case 1:
                items[0].Invoke();
                return;
            default:
                Parallel.ForEach(items, static item => item.Invoke());
                return;
        }
    }
}
