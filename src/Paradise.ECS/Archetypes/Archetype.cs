using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Stores data for a single archetype: its layout, allocated chunks, and entity count.
/// Uses SoA (Struct of Arrays) memory layout within chunks.
/// Single-threaded version without concurrent access support.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public sealed class Archetype<TBits, TRegistry, TConfig>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
    where TConfig : IWorldConfig
{
    private readonly nint _layoutData;
    private readonly ChunkManager<TConfig> _chunkManager;
    private readonly List<ChunkHandle> _chunks = new();

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
    public ImmutableArchetypeLayout<TBits, TRegistry, TConfig> Layout
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
    /// Gets the current number of chunks in this archetype.
    /// </summary>
    public int ChunkCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunks.Count;
    }

    /// <summary>
    /// Creates a new archetype store.
    /// </summary>
    /// <param name="id">The unique archetype ID.</param>
    /// <param name="layoutData">The layout data pointer (as nint) for this archetype.</param>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    public Archetype(int id, nint layoutData, ChunkManager<TConfig> chunkManager)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);

        Id = id;
        _layoutData = layoutData;
        _chunkManager = chunkManager;
    }

    /// <summary>
    /// Allocates space for a new entity in this archetype.
    /// Returns the global index where the entity is stored.
    /// </summary>
    /// <param name="entity">The entity to allocate space for.</param>
    /// <returns>The global index of the entity within this archetype.</returns>
    public int AllocateEntity(Entity entity)
    {
        int entitiesPerChunk = Layout.EntitiesPerChunk;

        if (EntityCount >= _chunks.Count * entitiesPerChunk)
        {
            _chunks.Add(_chunkManager.Allocate());
        }

        int globalIndex = EntityCount;
        int chunkIndex = globalIndex / entitiesPerChunk;
        int indexInChunk = globalIndex % entitiesPerChunk;

        SetEntityId(_chunks[chunkIndex], indexInChunk, entity.Id);
        EntityCount++;

        return globalIndex;
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
        int movedEntityId = GetEntityId(srcChunkHandle, srcIndexInChunk);

        // With SoA, copy each component separately (including entity ID)
        using var srcChunk = _chunkManager.Get(srcChunkHandle);
        using var dstChunk = _chunkManager.Get(dstChunkHandle);

        // Copy entity ID
        int srcEntityIdOffset = ImmutableArchetypeLayout<TBits, TRegistry, TConfig>.GetEntityIdOffset(srcIndexInChunk);
        int dstEntityIdOffset = ImmutableArchetypeLayout<TBits, TRegistry, TConfig>.GetEntityIdOffset(dstIndexInChunk);
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
    public (int ChunkIndex, int IndexInChunk) GetChunkLocation(int globalIndex)
    {
        int entitiesPerChunk = Layout.EntitiesPerChunk;
        return (globalIndex / entitiesPerChunk, globalIndex % entitiesPerChunk);
    }

    /// <summary>
    /// Clears all entities from this archetype, freeing all chunks.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _chunks.Count; i++)
        {
            _chunkManager.Free(_chunks[i]);
        }
        _chunks.Clear();
        EntityCount = 0;
    }

    /// <summary>
    /// Frees trailing empty chunks.
    /// </summary>
    private void TrimEmptyChunks()
    {
        // Free trailing empty chunks
        int entitiesPerChunk = Layout.EntitiesPerChunk;
        int neededChunks = (EntityCount + entitiesPerChunk - 1) / entitiesPerChunk;
        while (_chunks.Count > neededChunks)
        {
            int lastIndex = _chunks.Count - 1;
            _chunkManager.Free(_chunks[lastIndex]);
            _chunks.RemoveAt(lastIndex);
        }
    }

    /// <summary>
    /// Reads an entity ID from a chunk at the specified index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetEntityId(ChunkHandle chunkHandle, int indexInChunk)
    {
        using var chunk = _chunkManager.Get(chunkHandle);
        int offset = ImmutableArchetypeLayout<TBits, TRegistry, TConfig>.GetEntityIdOffset(indexInChunk);
        return chunk.GetRef<int>(offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int SetEntityId(ChunkHandle chunkHandle, int indexInChunk, int entityId)
    {
        using var chunk = _chunkManager.Get(chunkHandle);
        int offset = ImmutableArchetypeLayout<TBits, TRegistry, TConfig>.GetEntityIdOffset(indexInChunk);
        return chunk.GetRef<int>(offset) = entityId;
    }
}
