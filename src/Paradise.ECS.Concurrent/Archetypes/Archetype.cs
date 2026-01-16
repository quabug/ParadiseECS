using System.Runtime.CompilerServices;

namespace Paradise.ECS.Concurrent;

/// <summary>
/// Stores data for a single archetype: its layout, allocated chunks, and entity count.
/// Uses SoA (Struct of Arrays) memory layout within chunks.
/// Thread-safety: All operations are thread-safe. Write operations (AllocateEntity, RemoveEntity)
/// are serialized via lock. Read operations use volatile semantics for consistency.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
public sealed class Archetype<TBits, TRegistry>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private const int InitialChunkCapacity = 4;

    private readonly nint _layoutData;
    private readonly ChunkManager _chunkManager;
    private readonly Lock _lock = new();
    private ChunkHandle[] _chunks = new ChunkHandle[InitialChunkCapacity];
    private int _chunkCount;
    private int _entityCount;

    /// <summary>
    /// Gets the unique ID of this archetype.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the layout describing component offsets within this archetype.
    /// </summary>
    public ImmutableArchetypeLayout<TBits, TRegistry> Layout => new(_layoutData);

    /// <summary>
    /// Gets the current number of entities in this archetype.
    /// </summary>
    public int EntityCount => Volatile.Read(ref _entityCount);

    /// <summary>
    /// Gets the number of chunks allocated to this archetype.
    /// </summary>
    public int ChunkCount => Volatile.Read(ref _chunkCount);

    /// <summary>
    /// Creates a new archetype store.
    /// </summary>
    /// <param name="id">The unique archetype ID.</param>
    /// <param name="layoutData">The layout data pointer (as nint) for this archetype.</param>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    public Archetype(int id, nint layoutData, ChunkManager chunkManager)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);

        Id = id;
        _layoutData = layoutData;
        _chunkManager = chunkManager;
    }

    /// <summary>
    /// Allocates space for a new entity in this archetype.
    /// Returns the chunk handle and index where the entity should be stored.
    /// Thread-safe: Uses lock for synchronization.
    /// </summary>
    /// <param name="entity">The entity to allocate space for.</param>
    /// <returns>A tuple containing the chunk handle and index within the chunk.</returns>
    public (ChunkHandle ChunkHandle, int IndexInChunk) AllocateEntity(Entity entity)
    {
        using var _ = _lock.EnterScope();

        // Find a chunk with space or allocate a new one
        int entitiesPerChunk = Layout.EntitiesPerChunk;
        int chunkCount = _chunkCount;
        int totalSlots = chunkCount * entitiesPerChunk;
        int currentCount = _entityCount;

        if (currentCount >= totalSlots)
        {
            // Need a new chunk
            var newChunk = _chunkManager.Allocate();

            // Grow array if needed
            var chunks = _chunks;
            if (chunkCount >= chunks.Length)
            {
                var newChunks = new ChunkHandle[chunks.Length * 2];
                Array.Copy(chunks, newChunks, chunkCount);
                chunks = newChunks;
                Volatile.Write(ref _chunks, newChunks);
            }

            chunks[chunkCount] = newChunk;
            Volatile.Write(ref _chunkCount, chunkCount + 1);
        }

        // Find the chunk and index for the new entity
        int chunkIndex = currentCount / entitiesPerChunk;
        int indexInChunk = currentCount % entitiesPerChunk;
        var chunkHandle = Volatile.Read(ref _chunks)[chunkIndex];

        // Write entity ID to chunk
        GetEntityIdRef(chunkHandle, indexInChunk) = entity.Id;

        Volatile.Write(ref _entityCount, currentCount + 1);

        return (chunkHandle, indexInChunk);
    }

    /// <summary>
    /// Removes an entity from this archetype by swapping with the last entity.
    /// With SoA layout, each component is copied separately.
    /// Thread-safe: Uses lock for synchronization.
    /// </summary>
    /// <param name="indexToRemove">The global entity index to remove.</param>
    /// <returns>The entity ID that was moved to fill the gap, or -1 if no swap occurred.</returns>
    public int RemoveEntity(int indexToRemove)
    {
        using var _ = _lock.EnterScope();

        int currentCount = _entityCount;
        if (indexToRemove < 0 || indexToRemove >= currentCount)
            return -1;

        int lastIndex = currentCount - 1;
        Volatile.Write(ref _entityCount, lastIndex);

        // Swap-remove: copy last entity's data to the removed slot
        int entitiesPerChunk = Layout.EntitiesPerChunk;

        int srcChunkIdx = lastIndex / entitiesPerChunk;
        int srcIndexInChunk = lastIndex % entitiesPerChunk;

        int dstChunkIdx = indexToRemove / entitiesPerChunk;
        int dstIndexInChunk = indexToRemove % entitiesPerChunk;

        // If removing the last entity, no swap needed
        if (indexToRemove == lastIndex)
        {
            TrimEmptyChunksLocked();
            return -1;
        }

        var chunks = _chunks;
        var srcChunkHandle = chunks[srcChunkIdx];
        var dstChunkHandle = chunks[dstChunkIdx];

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

        TrimEmptyChunksLocked();

        return movedEntityId;
    }

    /// <summary>
    /// Gets a chunk by its index in this archetype.
    /// Thread-safe: Uses volatile read for array access.
    /// </summary>
    /// <param name="chunkIndex">The chunk index.</param>
    /// <returns>The chunk handle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunkHandle GetChunk(int chunkIndex)
    {
        using var _ = _lock.EnterScope();
        return _chunks[chunkIndex];
    }

    /// <summary>
    /// Gets all chunk handles for this archetype.
    /// Thread-safe: Uses volatile reads for array and count access.
    /// Note: The returned span is a snapshot; contents may change if modified concurrently.
    /// </summary>
    /// <returns>A read-only span of chunk handles.</returns>
    public ReadOnlySpan<ChunkHandle> GetChunks()
    {
        using var _ = _lock.EnterScope();
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
    /// Frees trailing empty chunks. Must be called while holding the lock.
    /// </summary>
    private void TrimEmptyChunksLocked()
    {
        // Free trailing empty chunks
        int entitiesPerChunk = Layout.EntitiesPerChunk;
        int entityCount = _entityCount;
        int neededChunks = (entityCount + entitiesPerChunk - 1) / entitiesPerChunk;
        var chunks = _chunks;
        int chunkCount = _chunkCount;
        while (chunkCount > neededChunks)
        {
            chunkCount--;
            _chunkManager.Free(chunks[chunkCount]);
            chunks[chunkCount] = default;
        }
        Volatile.Write(ref _chunkCount, chunkCount);
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
}
