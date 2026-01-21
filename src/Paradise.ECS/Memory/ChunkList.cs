using System.Numerics;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A block-based list that stores unmanaged elements in fixed-size native memory blocks.
/// Provides O(1) indexed access with lazy block allocation.
/// </summary>
/// <typeparam name="T">The unmanaged element type.</typeparam>
/// <remarks>
/// <para>
/// Memory is organized into fixed-size blocks, each containing multiple entries.
/// Blocks are allocated lazily when first accessed for writes.
/// This pattern is cache-friendly and avoids large contiguous allocations.
/// </para>
/// <para>
/// Used internally by ChunkManager and ChunkTagRegistry for consistent memory management.
/// </para>
/// </remarks>
public sealed unsafe class ChunkList<T> : IDisposable where T : unmanaged
{
    private readonly IAllocator _allocator;
    private readonly nint[] _blocks;
    private readonly int _entriesPerBlock;
    private readonly int _entriesPerBlockShift;
    private readonly int _entriesPerBlockMask;
    private readonly int _blockByteSize;
    private bool _disposed;

    /// <summary>
    /// Gets the maximum number of blocks.
    /// </summary>
    public int MaxBlocks => _blocks.Length;

    /// <summary>
    /// Gets the number of entries per block.
    /// </summary>
    public int EntriesPerBlock => _entriesPerBlock;

    /// <summary>
    /// Gets the maximum capacity (MaxBlocks * EntriesPerBlock).
    /// </summary>
    public int MaxCapacity => _blocks.Length * _entriesPerBlock;

    /// <summary>
    /// Creates a new ChunkList with the specified configuration.
    /// </summary>
    /// <param name="allocator">The memory allocator to use.</param>
    /// <param name="blockByteSize">The size of each block in bytes (should be power of 2).</param>
    /// <param name="maxBlocks">The maximum number of blocks.</param>
    /// <param name="initialBlocks">The number of blocks to pre-allocate (0 for lazy allocation).</param>
    public ChunkList(IAllocator allocator, int blockByteSize, int maxBlocks, int initialBlocks = 0)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        _blocks = new nint[maxBlocks];
        _blockByteSize = blockByteSize;
        _entriesPerBlock = blockByteSize / sizeof(T);
        _entriesPerBlockShift = BitOperations.Log2((uint)_entriesPerBlock);
        _entriesPerBlockMask = _entriesPerBlock - 1;

        // Pre-allocate initial blocks if requested
        int blocksToAllocate = Math.Min(initialBlocks, maxBlocks);
        for (int i = 0; i < blocksToAllocate; i++)
        {
            _blocks[i] = (nint)_allocator.AllocateZeroed((nuint)blockByteSize);
        }
    }

    /// <summary>
    /// Gets a pointer to the block at the given index, or null if not allocated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T* GetBlock(int blockIndex) => (T*)_blocks[blockIndex];

    /// <summary>
    /// Checks if the block at the given index is allocated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBlockAllocated(int blockIndex) => _blocks[blockIndex] != 0;

    /// <summary>
    /// Ensures the block for the given element index is allocated.
    /// </summary>
    /// <param name="index">The element index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int index)
    {
        int blockIndex = index >> _entriesPerBlockShift;
        EnsureBlockAllocated(blockIndex);
    }

    /// <summary>
    /// Ensures the block at the given index is allocated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureBlockAllocated(int blockIndex)
    {
        if (_blocks[blockIndex] == 0)
        {
            _blocks[blockIndex] = (nint)_allocator.AllocateZeroed((nuint)_blockByteSize);
        }
    }

    /// <summary>
    /// Gets a reference to the element at the given index.
    /// The block must already be allocated.
    /// </summary>
    /// <param name="index">The element index.</param>
    /// <returns>A reference to the element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef(int index)
    {
        int blockIndex = index >> _entriesPerBlockShift;
        int indexInBlock = index & _entriesPerBlockMask;
        return ref GetBlock(blockIndex)[indexInBlock];
    }

    /// <summary>
    /// Gets a reference to the element at the given index, allocating the block if needed.
    /// </summary>
    /// <param name="index">The element index.</param>
    /// <returns>A reference to the element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetOrCreateRef(int index)
    {
        int blockIndex = index >> _entriesPerBlockShift;
        int indexInBlock = index & _entriesPerBlockMask;
        EnsureBlockAllocated(blockIndex);
        return ref GetBlock(blockIndex)[indexInBlock];
    }

    /// <summary>
    /// Tries to get the value at the given index.
    /// Returns default if the block is not allocated.
    /// </summary>
    /// <param name="index">The element index.</param>
    /// <returns>The value, or default if block not allocated.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValueOrDefault(int index)
    {
        int blockIndex = index >> _entriesPerBlockShift;
        int indexInBlock = index & _entriesPerBlockMask;
        var block = GetBlock(blockIndex);
        return block == null ? default : block[indexInBlock];
    }

    /// <summary>
    /// Sets the value at the given index, allocating the block if needed.
    /// </summary>
    /// <param name="index">The element index.</param>
    /// <param name="value">The value to set.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetValue(int index, T value)
    {
        ref var slot = ref GetOrCreateRef(index);
        slot = value;
    }

    /// <summary>
    /// Clears all allocated blocks by zeroing their memory.
    /// Does not deallocate the blocks.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _blocks.Length; i++)
        {
            if (_blocks[i] != 0)
            {
                _allocator.Clear((void*)_blocks[i], (nuint)_blockByteSize);
            }
        }
    }

    /// <summary>
    /// Disposes of all native memory used by this list.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = 0; i < _blocks.Length; i++)
        {
            if (_blocks[i] != 0)
            {
                _allocator.Free((void*)_blocks[i]);
                _blocks[i] = 0;
            }
        }
    }
}
