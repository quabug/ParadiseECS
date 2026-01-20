using System.Numerics;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Stores per-chunk tag masks for fast chunk-level tag filtering.
/// Each chunk's mask is the union (OR) of all entity tag masks within that chunk.
/// </summary>
/// <typeparam name="TTagMask">The tag mask type.</typeparam>
/// <remarks>
/// <para>
/// This is a pure storage class with no World dependency, allowing it to be shared
/// between multiple TaggedWorld instances that use the same ChunkManager.
/// </para>
/// <para>
/// Uses native memory blocks similar to ChunkManager for consistent memory management.
/// </para>
/// </remarks>
public sealed unsafe class ChunkTagRegistry<TTagMask> : IDisposable
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    private readonly IAllocator _allocator;
    private readonly nint[] _blocks;
    private readonly int _blockSize;
    private readonly int _blockShift;
    private readonly int _blockMask;
    private bool _disposed;

    /// <summary>
    /// Creates a new ChunkTagRegistry with the specified allocator and capacity.
    /// </summary>
    /// <param name="allocator">The memory allocator to use.</param>
    /// <param name="maxMetaBlocks">The maximum number of meta blocks (determines capacity).</param>
    /// <param name="blockByteSize">The size of each block in bytes (e.g., 16384 for 16KB blocks).</param>
    public ChunkTagRegistry(IAllocator allocator, int maxMetaBlocks, int blockByteSize)
    {
        _allocator = allocator;
        _blocks = new nint[maxMetaBlocks];
        _blockSize = blockByteSize / sizeof(TTagMask);
        _blockShift = BitOperations.Log2((uint)_blockSize);
        _blockMask = _blockSize - 1;
    }

    /// <summary>
    /// Gets a pointer to the block for a given block index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TTagMask* GetBlock(int blockIndex)
    {
        return (TTagMask*)_blocks[blockIndex];
    }

    /// <summary>
    /// Gets the combined tag mask for a specific chunk.
    /// </summary>
    /// <param name="chunkHandle">The chunk handle.</param>
    /// <returns>The combined tag mask for all entities in the chunk, or default if not tracked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TTagMask GetChunkMask(ChunkHandle chunkHandle)
    {
        int blockIndex = chunkHandle.Id >> _blockShift;
        int indexInBlock = chunkHandle.Id & _blockMask;

        var block = GetBlock(blockIndex);
        if (block == null)
            return default;

        return block[indexInBlock];
    }

    /// <summary>
    /// Gets a reference to the chunk mask for direct modification.
    /// Ensures the block is allocated.
    /// </summary>
    /// <param name="chunkId">The chunk ID.</param>
    /// <returns>A reference to the chunk's tag mask.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref TTagMask GetOrCreateMaskRef(int chunkId)
    {
        int blockIndex = chunkId >> _blockShift;
        int indexInBlock = chunkId & _blockMask;

        if (_blocks[blockIndex] == 0)
        {
            _blocks[blockIndex] = (nint)_allocator.AllocateZeroed((nuint)(_blockSize * sizeof(TTagMask)));
        }

        return ref GetBlock(blockIndex)[indexInBlock];
    }

    /// <summary>
    /// Sets the tag mask for a specific chunk.
    /// </summary>
    /// <param name="chunkHandle">The chunk handle.</param>
    /// <param name="mask">The tag mask to set.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetChunkMask(ChunkHandle chunkHandle, TTagMask mask)
    {
        ref var chunkMask = ref GetOrCreateMaskRef(chunkHandle.Id);
        chunkMask = mask;
    }

    /// <summary>
    /// ORs a tag bit into the chunk's mask. Used when adding a tag.
    /// </summary>
    /// <param name="chunkHandle">The chunk handle.</param>
    /// <param name="tagBit">The tag bit to OR into the mask.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OrChunkMask(ChunkHandle chunkHandle, TTagMask tagBit)
    {
        ref var chunkMask = ref GetOrCreateMaskRef(chunkHandle.Id);
        chunkMask = chunkMask.Or(tagBit);
    }

    /// <summary>
    /// Checks if a chunk potentially contains entities with all the specified tags.
    /// </summary>
    /// <param name="chunkHandle">The chunk handle.</param>
    /// <param name="requiredTags">The required tag mask.</param>
    /// <returns>True if the chunk may contain matching entities; false if it definitely doesn't.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ChunkMayMatch(ChunkHandle chunkHandle, TTagMask requiredTags)
    {
        var chunkMask = GetChunkMask(chunkHandle);
        return chunkMask.ContainsAll(requiredTags);
    }

    /// <summary>
    /// Clears all tracked chunk tag masks by zeroing memory.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _blocks.Length; i++)
        {
            if (_blocks[i] != 0)
            {
                _allocator.Clear((void*)_blocks[i], (nuint)(_blockSize * sizeof(TTagMask)));
            }
        }
    }

    /// <summary>
    /// Disposes of native memory used by this registry.
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
