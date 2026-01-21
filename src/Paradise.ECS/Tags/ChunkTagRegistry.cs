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
/// Uses <see cref="ChunkArray{T}"/> for consistent block-based memory management.
/// </para>
/// </remarks>
public sealed class ChunkTagRegistry<TTagMask> : IDisposable
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    private readonly ChunkArray<TTagMask> _masks;

    /// <summary>
    /// Creates a new ChunkTagRegistry with the specified allocator and capacity.
    /// </summary>
    /// <param name="allocator">The memory allocator to use.</param>
    /// <param name="maxMetaBlocks">The maximum number of meta blocks (determines capacity).</param>
    /// <param name="blockByteSize">The size of each block in bytes (e.g., 16384 for 16KB blocks).</param>
    public ChunkTagRegistry(IAllocator allocator, int maxMetaBlocks, int blockByteSize)
    {
        _masks = new ChunkArray<TTagMask>(allocator, blockByteSize, maxMetaBlocks);
    }

    /// <summary>
    /// Gets the combined tag mask for a specific chunk.
    /// </summary>
    /// <param name="chunkHandle">The chunk handle.</param>
    /// <returns>The combined tag mask for all entities in the chunk, or default if not tracked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TTagMask GetChunkMask(ChunkHandle chunkHandle)
    {
        return _masks.GetValueOrDefault(chunkHandle.Id);
    }

    /// <summary>
    /// Sets the tag mask for a specific chunk.
    /// </summary>
    /// <param name="chunkHandle">The chunk handle.</param>
    /// <param name="mask">The tag mask to set.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetChunkMask(ChunkHandle chunkHandle, TTagMask mask)
    {
        _masks.SetValue(chunkHandle.Id, mask);
    }

    /// <summary>
    /// ORs a tag bit into the chunk's mask. Used when adding a tag.
    /// </summary>
    /// <param name="chunkHandle">The chunk handle.</param>
    /// <param name="tagBit">The tag bit to OR into the mask.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OrChunkMask(ChunkHandle chunkHandle, TTagMask tagBit)
    {
        ref var chunkMask = ref _masks.GetOrCreateRef(chunkHandle.Id);
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
        _masks.Clear();
    }

    /// <summary>
    /// Disposes of native memory used by this registry.
    /// </summary>
    public void Dispose()
    {
        _masks.Dispose();
    }
}
