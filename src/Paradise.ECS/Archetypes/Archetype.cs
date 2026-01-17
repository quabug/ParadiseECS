using System.Buffers;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Stores data for a single archetype: its layout, allocated chunks, and entity count.
/// Uses SoA (Struct of Arrays) memory layout within chunks.
/// Single-threaded version without concurrent access support.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
public sealed class Archetype<TBits, TRegistry> : IDisposable
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private const int InitialChunkCapacity = 32;

    private readonly nint _layoutData;
    private readonly ChunkManager _chunkManager;
    private readonly ArrayPool<ChunkHandle> _chunkPool;
    private ChunkHandle[] _chunks;
    private int _chunkCount;
    private bool _disposed;

    /// <summary>
    /// Gets the unique ID of this archetype.
    /// </summary>
    public int Id
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    /// <summary>
    /// Gets the layout describing component offsets within this archetype.
    /// </summary>
    public ImmutableArchetypeLayout<TBits, TRegistry> Layout
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_layoutData);
    }

    /// <summary>
    /// Gets the current number of entities in this archetype.
    /// </summary>
    public int EntityCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private set;
    }

    /// <summary>
    /// Creates a new archetype store.
    /// </summary>
    /// <param name="id">The unique archetype ID.</param>
    /// <param name="layoutData">The layout data pointer (as nint) for this archetype.</param>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    /// <param name="chunkPool">The array pool for chunk handle arrays.</param>
    public Archetype(int id, nint layoutData, ChunkManager chunkManager, ArrayPool<ChunkHandle> chunkPool)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);
        ArgumentNullException.ThrowIfNull(chunkPool);

        Id = id;
        _layoutData = layoutData;
        _chunkManager = chunkManager;
        _chunkPool = chunkPool;
        _chunks = _chunkPool.Rent(InitialChunkCapacity);
    }

    /// <summary>
    /// Allocates space for a new entity in this archetype.
    /// Returns the chunk handle and index where the entity should be stored.
    /// </summary>
    /// <param name="entity">The entity to allocate space for.</param>
    /// <returns>A tuple containing the chunk handle and index within the chunk.</returns>
    public (ChunkHandle ChunkHandle, int IndexInChunk) AllocateEntity(Entity entity)
    {
        int entitiesPerChunk = Layout.EntitiesPerChunk;

        if (EntityCount >= _chunkCount * entitiesPerChunk)
        {
            EnsureChunkCapacity();
            _chunks[_chunkCount++] = _chunkManager.Allocate();
        }

        int chunkIndex = EntityCount / entitiesPerChunk;
        int indexInChunk = EntityCount % entitiesPerChunk;

        GetEntityIdRef(_chunks[chunkIndex], indexInChunk) = entity.Id;
        EntityCount++;

        return (_chunks[chunkIndex], indexInChunk);
    }

    private void EnsureChunkCapacity()
    {
        if (_chunkCount < _chunks.Length)
            return;

        var newChunks = _chunkPool.Rent(_chunks.Length * 2);
        Array.Copy(_chunks, newChunks, _chunkCount);
        _chunkPool.Return(_chunks, clearArray: true);
        _chunks = newChunks;
    }

    /// <summary>
    /// Removes an entity from this archetype by swapping with the last entity.
    /// With SoA layout, each component is copied separately.
    /// </summary>
    /// <param name="indexToRemove">The global entity index to remove.</param>
    /// <returns>The entity ID that was moved to fill the gap, or -1 if no swap occurred.</returns>
    public int RemoveEntity(int indexToRemove)
    {
        int currentCount = EntityCount;
        if (indexToRemove < 0 || indexToRemove >= currentCount)
            return -1;

        int lastIndex = currentCount - 1;
        EntityCount = lastIndex;

        // Swap-remove: copy last entity's data to the removed slot
        int entitiesPerChunk = Layout.EntitiesPerChunk;

        int srcChunkIdx = lastIndex / entitiesPerChunk;
        int srcIndexInChunk = lastIndex % entitiesPerChunk;

        int dstChunkIdx = indexToRemove / entitiesPerChunk;
        int dstIndexInChunk = indexToRemove % entitiesPerChunk;

        // If removing the last entity, no swap needed
        if (indexToRemove == lastIndex)
        {
            TrimEmptyChunks();
            return -1;
        }

        var srcChunkHandle = _chunks[srcChunkIdx];
        var dstChunkHandle = _chunks[dstChunkIdx];

        // Read the entity ID being moved from the source chunk
        int movedEntityId = GetEntityIdRef(srcChunkHandle, srcIndexInChunk);

        // With SoA, copy each component separately (including entity ID)
        using var srcChunk = _chunkManager.Get(srcChunkHandle);
        using var dstChunk = _chunkManager.Get(dstChunkHandle);

        // Copy entity ID
        int srcEntityIdOffset = ImmutableArchetypeLayout<TBits, TRegistry>.GetEntityIdOffset(srcIndexInChunk);
        int dstEntityIdOffset = ImmutableArchetypeLayout<TBits, TRegistry>.GetEntityIdOffset(dstIndexInChunk);
        var srcEntityIdData = srcChunk.GetBytesAt(srcEntityIdOffset, sizeof(int));
        var dstEntityIdData = dstChunk.GetBytesAt(dstEntityIdOffset, sizeof(int));
        srcEntityIdData.CopyTo(dstEntityIdData);

        // Iterate from min to max component ID in this archetype's layout
        int minId = Layout.MinComponentId;
        int maxId = Layout.MaxComponentId;
        for (int id = minId; id <= maxId; id++)
        {
            var componentId = new ComponentId(id);
            int baseOffset = Layout.GetBaseOffset(componentId);
            if (baseOffset < 0)
                continue; // Component not in this archetype

            int size = TRegistry.TypeInfos[id].Size;
            if (size == 0)
                continue; // Skip tag components

            int srcOffset = baseOffset + srcIndexInChunk * size;
            int dstOffset = baseOffset + dstIndexInChunk * size;

            var srcData = srcChunk.GetBytesAt(srcOffset, size);
            var dstData = dstChunk.GetBytesAt(dstOffset, size);
            srcData.CopyTo(dstData);
        }

        TrimEmptyChunks();

        return movedEntityId;
    }

    /// <summary>
    /// Gets a chunk by its index in this archetype.
    /// </summary>
    /// <param name="chunkIndex">The chunk index.</param>
    /// <returns>The chunk handle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunkHandle GetChunk(int chunkIndex)
    {
        return _chunks[chunkIndex];
    }

    /// <summary>
    /// Gets all chunk handles for this archetype.
    /// </summary>
    /// <returns>A read-only span of chunk handles.</returns>
    public ReadOnlySpan<ChunkHandle> GetChunks()
    {
        return _chunks.AsSpan(0, _chunkCount);
    }

    /// <summary>
    /// Calculates the global entity index from chunk index and index within chunk.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetGlobalIndex(int chunkIndex, int indexInChunk)
    {
        return chunkIndex * Layout.EntitiesPerChunk + indexInChunk;
    }

    /// <summary>
    /// Converts a global entity index to chunk index and index within chunk.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetChunkLocation(int globalIndex, out int chunkIndex, out int indexInChunk)
    {
        int entitiesPerChunk = Layout.EntitiesPerChunk;
        chunkIndex = globalIndex / entitiesPerChunk;
        indexInChunk = globalIndex % entitiesPerChunk;
    }

    /// <summary>
    /// Clears all entities from this archetype, freeing all chunks.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _chunkCount; i++)
        {
            _chunkManager.Free(_chunks[i]);
            _chunks[i] = default;
        }
        _chunkCount = 0;
        EntityCount = 0;
    }

    /// <summary>
    /// Frees trailing empty chunks.
    /// </summary>
    private void TrimEmptyChunks()
    {
        // Free trailing empty chunks
        int entitiesPerChunk = Layout.EntitiesPerChunk;
        int entityCount = EntityCount;
        int neededChunks = (entityCount + entitiesPerChunk - 1) / entitiesPerChunk;
        while (_chunkCount > neededChunks)
        {
            _chunkCount--;
            _chunkManager.Free(_chunks[_chunkCount]);
            _chunks[_chunkCount] = default;
        }
    }

    /// <summary>
    /// Reads an entity ID from a chunk at the specified index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref int GetEntityIdRef(ChunkHandle chunkHandle, int indexInChunk)
    {
        using var chunk = _chunkManager.Get(chunkHandle);
        int offset = ImmutableArchetypeLayout<TBits, TRegistry>.GetEntityIdOffset(indexInChunk);
        return ref chunk.GetRef<int>(offset);
    }

    /// <summary>
    /// Releases resources used by this archetype, returning the chunk array to the pool.
    /// Note: Does not free the chunks themselves - that is handled by the ChunkManager.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _chunkPool.Return(_chunks, clearArray: true);
        _chunks = [];
        _chunkCount = 0;
    }
}
