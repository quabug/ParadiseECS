using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// RAII guard for tracking active operations. Prevents Dispose from freeing memory
/// while operations are in-flight.
/// </summary>
internal readonly struct OperationGuard : IDisposable
{
    private readonly ChunkManager _manager;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationGuard(ChunkManager manager)
    {
        _manager = manager;
        manager.IncrementActiveOperations();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _manager.DecrementActiveOperations();
}
