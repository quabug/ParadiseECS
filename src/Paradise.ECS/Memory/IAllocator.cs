namespace Paradise.ECS;

/// <summary>
/// Interface for memory allocation strategies.
/// Allows swapping between different allocators (native, arena, virtual memory, etc.).
/// </summary>
public unsafe interface IAllocator
{
    /// <summary>
    /// Allocates a block of memory of the specified size.
    /// The memory contents are undefined.
    /// </summary>
    /// <param name="size">The size in bytes to allocate.</param>
    /// <returns>A pointer to the allocated memory block.</returns>
    void* Allocate(nuint size);

    /// <summary>
    /// Allocates a block of memory of the specified size and zeros it.
    /// </summary>
    /// <param name="size">The size in bytes to allocate.</param>
    /// <returns>A pointer to the allocated and zeroed memory block.</returns>
    void* AllocateZeroed(nuint size);

    /// <summary>
    /// Frees a previously allocated memory block.
    /// </summary>
    /// <param name="ptr">The pointer to the memory block to free.</param>
    void Free(void* ptr);

    /// <summary>
    /// Clears (zeros) a memory region.
    /// </summary>
    /// <param name="ptr">The pointer to the start of the memory region.</param>
    /// <param name="size">The size in bytes to clear.</param>
    void Clear(void* ptr, nuint size);
}
