using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// RAII guard for tracking active operations. Prevents Dispose from freeing memory
/// while operations are in-flight.
/// </summary>
internal readonly ref struct OperationGuard : IDisposable
{
    private readonly ref int _operationCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationGuard(ref int operationCount)
    {
        _operationCount = ref operationCount;
        Interlocked.Increment(ref _operationCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Decrement(ref _operationCount);
}
