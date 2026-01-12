using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A debug-only utility for asserting single-thread access to non-thread-safe types.
/// Embed this struct in classes that require thread affinity.
/// All checks are completely removed in Release builds via [Conditional("DEBUG")].
/// </summary>
internal struct ThreadAffinity(int ownerThreadId = 0)
{
    private int _ownerThreadId = ownerThreadId;

    /// <summary>
    /// Asserts that all calls come from the same thread.
    /// The first call establishes the owner thread; subsequent calls from different threads throw.
    /// This method is completely removed in Release builds.
    /// </summary>
    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Assert()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;
        if (_ownerThreadId == 0)
            _ownerThreadId = currentThreadId;
        else if (_ownerThreadId != currentThreadId)
            ThrowCrossThreadAccess(_ownerThreadId, currentThreadId);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCrossThreadAccess(int ownerThreadId, int currentThreadId) =>
        throw new InvalidOperationException(
            $"Object was accessed from thread {currentThreadId}, but it is owned by thread {ownerThreadId}. " +
            "This type is not thread-safe.");
}
