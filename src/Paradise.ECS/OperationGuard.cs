using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Tracks active operations to prevent disposal while operations are in-flight.
/// </summary>
internal sealed class OperationGuard
{
    [SuppressMessage("Style", "IDE0044:Add readonly modifier")]
    private int _counter;

    /// <summary>
    /// Enters an operation scope, incrementing the counter.
    /// </summary>
    /// <returns>A scope that decrements the counter when disposed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Scope EnterScope()
    {
        return new Scope(ref _counter);
    }

    /// <summary>
    /// Waits for all in-flight operations to complete.
    /// </summary>
    public void WaitForCompletion()
    {
        var spinWait = new SpinWait();
        while (Volatile.Read(ref _counter) > 0)
            spinWait.SpinOnce();
    }

    /// <summary>
    /// Waits for all in-flight operations to complete with a timeout.
    /// </summary>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns><c>true</c> if all operations completed; <c>false</c> if the timeout expired.</returns>
    public bool WaitForCompletion(TimeSpan timeout)
    {
        var startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        var spinWait = new SpinWait();
        while (Volatile.Read(ref _counter) > 0)
        {
            if (System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp) >= timeout)
                return false;
            spinWait.SpinOnce();
        }
        return true;
    }

    /// <summary>
    /// RAII scope that decrements the operation counter when disposed.
    /// </summary>
    public ref struct Scope : IDisposable
    {
        private ref int _counter;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Scope(ref int counter)
        {
            _counter = ref counter;
            Interlocked.Increment(ref _counter);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => Interlocked.Decrement(ref _counter);
    }
}
