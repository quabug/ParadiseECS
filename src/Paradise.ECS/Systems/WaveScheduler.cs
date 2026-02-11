namespace Paradise.ECS;

/// <summary>
/// Represents a unit of work within a system execution wave.
/// Contains metadata for scheduler decision-making and an invocable action.
/// </summary>
public readonly struct WorkItem
{
    /// <summary>The system that produced this work item.</summary>
    public int SystemId { get; }

    /// <summary>The chunk this work item operates on.</summary>
    public ChunkHandle Chunk { get; }

    private readonly Action _action;

    internal WorkItem(int systemId, ChunkHandle chunk, Action action)
    {
        SystemId = systemId;
        Chunk = chunk;
        _action = action;
    }

    /// <summary>Executes this work item.</summary>
    public void Invoke() => _action();
}

/// <summary>
/// Strategy interface for scheduling work items within an execution wave.
/// Implement this to provide custom parallel execution strategies (e.g., job systems,
/// thread pools with data affinity, or custom work-stealing schedulers).
/// </summary>
public interface IWaveScheduler
{
    /// <summary>Executes all work items for a single wave. Must complete before returning.</summary>
    /// <param name="items">The work items to execute. The span is only valid for the duration of this call.</param>
    void Execute(Span<WorkItem> items);
}

/// <summary>Executes work items sequentially on the calling thread.</summary>
public sealed class SequentialWaveScheduler : IWaveScheduler
{
    /// <inheritdoc/>
    public void Execute(Span<WorkItem> items)
    {
        foreach (ref readonly var item in items)
            item.Invoke();
    }
}

/// <summary>Executes work items in parallel using the .NET ThreadPool via <see cref="System.Threading.Tasks.Parallel"/>.</summary>
public sealed class ParallelWaveScheduler : IWaveScheduler
{
    /// <inheritdoc/>
    public void Execute(Span<WorkItem> items)
    {
        switch (items.Length)
        {
            case 0: return;
            case 1:
                items[0].Invoke();
                return;
            default:
                var actions = new Action[items.Length];
                for (var i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    actions[i] = item.Invoke;
                }
                System.Threading.Tasks.Parallel.Invoke(actions);
                return;
        }
    }
}
