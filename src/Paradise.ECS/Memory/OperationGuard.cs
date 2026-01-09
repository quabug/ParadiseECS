using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// RAII guard for tracking active operations. Prevents Dispose from freeing memory
/// while operations are in-flight.
/// </summary>
internal readonly ref struct OperationGuard : IDisposable
{
    private readonly ref int _counter;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationGuard(ref int counter)
    {
        _counter = ref counter;
        Interlocked.Increment(ref _counter);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Decrement(ref _counter);
}
