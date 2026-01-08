using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// Default allocator using <see cref="NativeMemory"/> for unmanaged heap allocations.
/// Thread-safe and suitable for most ECS use cases.
/// </summary>
public sealed unsafe class NativeMemoryAllocator : IAllocator
{
    /// <summary>
    /// Shared singleton instance for convenience.
    /// </summary>
    public static NativeMemoryAllocator Shared { get; } = new();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void* Allocate(nuint size) => NativeMemory.Alloc(size);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void* AllocateZeroed(nuint size) => NativeMemory.AllocZeroed(size);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Free(void* ptr) => NativeMemory.Free(ptr);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(void* ptr, nuint size) => NativeMemory.Clear(ptr, size);
}
