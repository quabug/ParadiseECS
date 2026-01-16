using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS.Concurrent;

/// <summary>
/// Provides utility methods for memory alignment operations.
/// </summary>
public static class Memory
{
    /// <summary>
    /// Rounds a value up to the next multiple of alignment.
    /// </summary>
    /// <param name="value">The value to align.</param>
    /// <param name="alignment">The alignment boundary (must be a power of 2).</param>
    /// <returns>The aligned value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignUp(int value, int alignment)
    {
        return (value + alignment - 1) & ~(alignment - 1);
    }

    /// <summary>
    /// Gets the alignment of an unmanaged type in bytes.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to get alignment for.</typeparam>
    /// <returns>The alignment of the type in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignOf<T>() where T : unmanaged
    {
        return AlignOfHelper<T>.Alignment;
    }

    /// <summary>
    /// Helper struct for calculating alignment of unmanaged types at runtime.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to calculate alignment for.</typeparam>
    /// <remarks>
    /// The alignment of T equals the offset of Value in this struct,
    /// which can be calculated as: SizeOf&lt;AlignOf&lt;T&gt;&gt; - SizeOf&lt;T&gt;
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    private struct AlignOfHelper<T> where T : unmanaged
    {
        private readonly byte _padding;
        private readonly T _value;

        /// <summary>
        /// Gets the alignment of type T in bytes.
        /// </summary>
        public static int Alignment { get; } = Unsafe.SizeOf<AlignOfHelper<T>>() - Unsafe.SizeOf<T>();
    }
}
