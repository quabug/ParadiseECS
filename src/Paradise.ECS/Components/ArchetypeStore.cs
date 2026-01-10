using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Stores data for a single archetype: its layout, allocated chunks, and entity count.
/// Uses SoA (Struct of Arrays) memory layout within chunks.
/// Thread-safety: Individual operations are thread-safe, but compound operations
/// (e.g., check-then-act) require external synchronization.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
public sealed class ArchetypeStore<TBits, TRegistry>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private readonly ImmutableArchetypeLayout<TBits, TRegistry> _layout;
    private readonly ChunkManager _chunkManager;
    private readonly List<ChunkHandle> _chunks = [];
    private int _entityCount;

    /// <summary>
    /// Gets the unique ID of this archetype.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the layout describing component offsets within this archetype.
    /// </summary>
    public ImmutableArchetypeLayout<TBits, TRegistry> Layout => _layout;

    /// <summary>
    /// Gets the current number of entities in this archetype.
    /// </summary>
    public int EntityCount => Volatile.Read(ref _entityCount);

    /// <summary>
    /// Gets the number of chunks allocated to this archetype.
    /// </summary>
    public int ChunkCount
    {
        get
        {
            return _chunks.Count;
        }
    }

    /// <summary>
    /// Creates a new archetype store.
    /// </summary>
    /// <param name="id">The unique archetype ID.</param>
    /// <param name="layout">The component layout for this archetype.</param>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    public ArchetypeStore(int id, ImmutableArchetypeLayout<TBits, TRegistry> layout, ChunkManager chunkManager)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(chunkManager);

        Id = id;
        _layout = layout;
        _chunkManager = chunkManager;
    }

    /// <summary>
    /// Allocates space for a new entity in this archetype.
    /// Returns the chunk handle and index where the entity should be stored.
    /// </summary>
    /// <param name="chunkHandle">The chunk handle where the entity is allocated.</param>
    /// <param name="indexInChunk">The index within the chunk.</param>
    public void AllocateEntity(out ChunkHandle chunkHandle, out int indexInChunk)
    {
        // Find a chunk with space or allocate a new one
        int entitiesPerChunk = _layout.EntitiesPerChunk;
        int totalSlots = _chunks.Count * entitiesPerChunk;
        int currentCount = _entityCount;

        if (currentCount >= totalSlots)
        {
            // Need a new chunk
            var newChunk = _chunkManager.Allocate();
            _chunks.Add(newChunk);
        }

        // Find the chunk and index for the new entity
        int chunkIndex = currentCount / entitiesPerChunk;
        indexInChunk = currentCount % entitiesPerChunk;
        chunkHandle = _chunks[chunkIndex];
        _entityCount = currentCount + 1;
    }

    /// <summary>
    /// Removes an entity from this archetype by swapping with the last entity.
    /// With SoA layout, each component is copied separately.
    /// </summary>
    /// <param name="indexToRemove">The global entity index to remove.</param>
    /// <param name="movedEntity">Output: the entity that was moved to fill the gap (Invalid if none).</param>
    /// <param name="movedEntityNewIndex">Output: the new index of the moved entity.</param>
    /// <returns>True if an entity was moved to fill the gap.</returns>
    public bool RemoveEntity(int indexToRemove, out Entity movedEntity, out int movedEntityNewIndex)
    {
        movedEntity = Entity.Invalid;
        movedEntityNewIndex = -1;

        int currentCount = _entityCount;
        if (indexToRemove < 0 || indexToRemove >= currentCount)
            return false;

        int lastIndex = currentCount - 1;
        _entityCount = lastIndex;

        // If removing the last entity, no swap needed
        if (indexToRemove == lastIndex)
        {
            TrimEmptyChunks();
            return false;
        }

        // Swap-remove: copy last entity's components to the removed slot
        int entitiesPerChunk = _layout.EntitiesPerChunk;

        int srcChunkIdx = lastIndex / entitiesPerChunk;
        int srcIndexInChunk = lastIndex % entitiesPerChunk;

        int dstChunkIdx = indexToRemove / entitiesPerChunk;
        int dstIndexInChunk = indexToRemove % entitiesPerChunk;

        var srcChunkHandle = _chunks[srcChunkIdx];
        var dstChunkHandle = _chunks[dstChunkIdx];

        // With SoA, copy each component separately
        using var srcChunk = _chunkManager.Get(srcChunkHandle);
        using var dstChunk = _chunkManager.Get(dstChunkHandle);

        // Iterate from min to max component ID in this archetype's layout
        int minId = _layout.MinComponentId;
        int maxId = _layout.MaxComponentId;
        for (int id = minId; id <= maxId; id++)
        {
            var componentId = new ComponentId(id);
            int baseOffset = _layout.GetBaseOffset(componentId);
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

        movedEntityNewIndex = indexToRemove;
        TrimEmptyChunks();

        // Note: The caller needs to look up which entity was at lastIndex
        // and update its EntityLocation. We return true to signal this.
        return true;
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
    /// <param name="destination">Span to receive chunk handles.</param>
    /// <returns>Number of chunks copied.</returns>
    public int GetChunks(Span<ChunkHandle> destination)
    {
        int count = Math.Min(destination.Length, _chunks.Count);
        for (int i = 0; i < count; i++)
        {
            destination[i] = _chunks[i];
        }
        return count;
    }

    /// <summary>
    /// Calculates the global entity index from chunk index and index within chunk.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetGlobalIndex(int chunkIndex, int indexInChunk)
    {
        return chunkIndex * _layout.EntitiesPerChunk + indexInChunk;
    }

    /// <summary>
    /// Converts a global entity index to chunk index and index within chunk.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetChunkLocation(int globalIndex, out int chunkIndex, out int indexInChunk)
    {
        int entitiesPerChunk = _layout.EntitiesPerChunk;
        chunkIndex = globalIndex / entitiesPerChunk;
        indexInChunk = globalIndex % entitiesPerChunk;
    }

    private void TrimEmptyChunks()
    {
        // Free trailing empty chunks
        int entitiesPerChunk = _layout.EntitiesPerChunk;
        int neededChunks = (_entityCount + entitiesPerChunk - 1) / entitiesPerChunk;
        if (neededChunks == 0 && _entityCount == 0)
            neededChunks = 0;

        while (_chunks.Count > neededChunks)
        {
            int lastIdx = _chunks.Count - 1;
            _chunkManager.Free(_chunks[lastIdx]);
            _chunks.RemoveAt(lastIdx);
        }
    }
}
