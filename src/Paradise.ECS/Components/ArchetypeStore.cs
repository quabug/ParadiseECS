using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Stores data for a single archetype: its layout, allocated chunks, and entity count.
/// Uses SoA (Struct of Arrays) memory layout within chunks.
/// Thread-safety: All operations are thread-safe. Write operations (AllocateEntity, RemoveEntity)
/// are serialized via lock. Read operations use volatile semantics for consistency.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
public sealed class ArchetypeStore<TBits, TRegistry>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private const int InitialChunkCapacity = 4;

    private readonly ImmutableArchetypeLayout<TBits, TRegistry> _layout;
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
    public ImmutableArchetypeLayout<TBits, TRegistry> Layout => _layout;

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
    /// Thread-safe: Uses lock for synchronization.
    /// </summary>
    /// <param name="chunkHandle">The chunk handle where the entity is allocated.</param>
    /// <param name="indexInChunk">The index within the chunk.</param>
    public void AllocateEntity(out ChunkHandle chunkHandle, out int indexInChunk)
    {
        using var _ = _lock.EnterScope();

        // Find a chunk with space or allocate a new one
        int entitiesPerChunk = _layout.EntitiesPerChunk;
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
        indexInChunk = currentCount % entitiesPerChunk;
        chunkHandle = Volatile.Read(ref _chunks)[chunkIndex];
        Volatile.Write(ref _entityCount, currentCount + 1);
    }

    /// <summary>
    /// Removes an entity from this archetype by swapping with the last entity.
    /// With SoA layout, each component is copied separately.
    /// Thread-safe: Uses lock for synchronization.
    /// </summary>
    /// <param name="indexToRemove">The global entity index to remove.</param>
    /// <returns>True if an entity was moved to fill the gap (caller must update entity location).</returns>
    public bool RemoveEntity(int indexToRemove)
    {
        using var _ = _lock.EnterScope();

        int currentCount = _entityCount;
        if (indexToRemove < 0 || indexToRemove >= currentCount)
            return false;

        int lastIndex = currentCount - 1;
        Volatile.Write(ref _entityCount, lastIndex);

        // If removing the last entity, no swap needed
        if (indexToRemove == lastIndex)
        {
            TrimEmptyChunksLocked();
            return false;
        }

        // Swap-remove: copy last entity's components to the removed slot
        int entitiesPerChunk = _layout.EntitiesPerChunk;

        int srcChunkIdx = lastIndex / entitiesPerChunk;
        int srcIndexInChunk = lastIndex % entitiesPerChunk;

        int dstChunkIdx = indexToRemove / entitiesPerChunk;
        int dstIndexInChunk = indexToRemove % entitiesPerChunk;

        var chunks = _chunks;
        var srcChunkHandle = chunks[srcChunkIdx];
        var dstChunkHandle = chunks[dstChunkIdx];

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

        TrimEmptyChunksLocked();

        // Note: The caller needs to look up which entity was at lastIndex
        // and update its EntityLocation. We return true to signal this.
        return true;
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
        return Volatile.Read(ref _chunks)[chunkIndex];
    }

    /// <summary>
    /// Gets all chunk handles for this archetype.
    /// Thread-safe: Uses volatile reads for array and count access.
    /// Note: The returned span is a snapshot; contents may change if modified concurrently.
    /// </summary>
    /// <returns>A read-only span of chunk handles.</returns>
    public ReadOnlySpan<ChunkHandle> GetChunks()
    {
        var chunks = Volatile.Read(ref _chunks);
        int count = Volatile.Read(ref _chunkCount);
        return chunks.AsSpan(0, count);
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

    /// <summary>
    /// Frees trailing empty chunks. Must be called while holding the lock.
    /// </summary>
    private void TrimEmptyChunksLocked()
    {
        // Free trailing empty chunks
        int entitiesPerChunk = _layout.EntitiesPerChunk;
        int entityCount = _entityCount;
        int neededChunks = (entityCount + entitiesPerChunk - 1) / entitiesPerChunk;
        if (neededChunks == 0 && entityCount == 0)
            neededChunks = 0;

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
}
