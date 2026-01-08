using System.Runtime.CompilerServices;

#if NETSTANDARD2_1
using ArgumentOutOfRangeException = System.ArgumentOutOfRangeExceptionPolyfill;
using ArgumentNullException = System.ArgumentNullExceptionPolyfill;
using ObjectDisposedException = System.ObjectDisposedExceptionPolyfill;
#endif

namespace Paradise.ECS;

/// <summary>
/// Static helper methods for common argument validation and exception throwing.
/// </summary>
internal static class ThrowHelper
{
    /// <summary>
    /// Throws if <paramref name="byteOffset"/> is negative.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeOffset(int byteOffset)
        => ThrowIfNegative(byteOffset);

    /// <summary>
    /// Throws if <paramref name="count"/> is negative.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeCount(int count)
        => ThrowIfNegative(count);

    /// <summary>
    /// Throws if <paramref name="size"/> is negative.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeSize(int size)
        => ThrowIfNegative(size);

    /// <summary>
    /// Throws if <paramref name="totalBytes"/> exceeds chunk size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfExceedsChunkSize(int totalBytes)
        => ThrowIfGreaterThan(totalBytes, Chunk.ChunkSize);

    /// <summary>
    /// Validates byte offset and size for chunk bounds.
    /// Throws if offset or size is negative, or if the range exceeds chunk size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateChunkRange(int byteOffset, int size)
    {
        ThrowIfNegative(byteOffset);
        ThrowIfNegative(size);
        ThrowIfGreaterThan(size, Chunk.ChunkSize - byteOffset);
    }

    /// <summary>
    /// Validates byte offset, count, and element size for chunk bounds.
    /// Throws if offset or count is negative, or if the range exceeds chunk size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateChunkRange(int byteOffset, int count, int elementSize)
    {
        ThrowIfNegative(byteOffset);
        ThrowIfNegative(count);
        // Validate count against max possible elements to prevent overflow in multiplication
        int maxCount = (Chunk.ChunkSize - byteOffset) / elementSize;
        ThrowIfGreaterThan(count, maxCount);
    }

    /// <summary>
    /// Validates size for chunk bounds.
    /// Throws if size is negative or exceeds chunk size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateChunkSize(int size)
    {
        ThrowIfNegative(size);
        ThrowIfGreaterThan(size, Chunk.ChunkSize);
    }

    /// <summary>
    /// Throws <see cref="ObjectDisposedException"/> if the condition is true.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDisposed(bool condition, object instance)
        => ObjectDisposedException.ThrowIf(condition, instance);

    /// <summary>
    /// Throws if the pointer is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ThrowIfNull(void* ptr)
        => ArgumentNullException.ThrowIfNull(ptr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfGreaterThan(int value, int other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => ArgumentOutOfRangeException.ThrowIfGreaterThan(value, other, paramName);
}
