using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// RAII guard for tracking active operations. Prevents Dispose from freeing memory
/// while operations are in-flight.
/// </summary>
internal readonly unsafe ref struct OperationGuard : IDisposable
{
    private readonly int* _ptr;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OperationGuard(int* ptr)
    {
        _ptr = ptr;
        Interlocked.Increment(ref *_ptr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Decrement(ref *_ptr);

}
