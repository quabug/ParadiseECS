using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// Helper struct for calculating alignment of unmanaged types at runtime.
/// </summary>
/// <typeparam name="T">The unmanaged type to calculate alignment for.</typeparam>
/// <remarks>
/// The alignment of T equals the offset of Value in this struct,
/// which can be calculated as: SizeOf&lt;AlignmentHelper&lt;T&gt;&gt; - SizeOf&lt;T&gt;
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct AlignmentHelper<T> where T : unmanaged
{
    private readonly byte _padding;
    private readonly T _value;

    /// <summary>
    /// Gets the alignment of type T in bytes.
    /// </summary>
    public static int Alignment
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Unsafe.SizeOf<AlignmentHelper<T>>() - Unsafe.SizeOf<T>();
    }
}
