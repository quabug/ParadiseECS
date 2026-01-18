using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// Extension methods for Span&lt;byte&gt; to access typed data at byte offsets.
/// </summary>
internal static class SpanExtensions
{
    /// <summary>
    /// Gets a reference to a value at the specified byte offset.
    /// </summary>
    /// <typeparam name="T">The unmanaged type.</typeparam>
    /// <param name="span">The byte span.</param>
    /// <param name="byteOffset">The offset from the start.</param>
    /// <returns>A reference to the value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetRef<T>(this Span<byte> span, int byteOffset) where T : unmanaged
    {
        return ref Unsafe.As<byte, T>(ref span[byteOffset]);
    }

    /// <summary>
    /// Gets a span over data at the specified byte offset.
    /// </summary>
    /// <typeparam name="T">The unmanaged type.</typeparam>
    /// <param name="span">The byte span.</param>
    /// <param name="byteOffset">The offset from the start.</param>
    /// <param name="count">The number of elements.</param>
    /// <returns>A span over the data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> GetSpan<T>(this Span<byte> span, int byteOffset, int count) where T : unmanaged
    {
        return MemoryMarshal.Cast<byte, T>(span.Slice(byteOffset, count * Unsafe.SizeOf<T>()));
    }

    /// <summary>
    /// Gets raw bytes at a specific offset.
    /// </summary>
    /// <param name="span">The byte span.</param>
    /// <param name="byteOffset">The offset from the start.</param>
    /// <param name="size">The number of bytes.</param>
    /// <returns>A span over the bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> GetBytesAt(this Span<byte> span, int byteOffset, int size)
    {
        return span.Slice(byteOffset, size);
    }
}
