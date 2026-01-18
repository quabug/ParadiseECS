namespace Paradise.ECS;

/// <summary>
/// Interface for archetype data storage.
/// An archetype stores entities that share the same component composition.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public interface IArchetype<TBits, TRegistry, TConfig>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
    where TConfig : IConfig, new()
{
    /// <summary>
    /// Gets the unique ID of this archetype.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// Gets the layout describing component offsets within this archetype.
    /// </summary>
    ImmutableArchetypeLayout<TBits, TRegistry, TConfig> Layout { get; }

    /// <summary>
    /// Gets the current number of entities in this archetype.
    /// </summary>
    int EntityCount { get; }

    /// <summary>
    /// Gets the number of chunks allocated to this archetype.
    /// </summary>
    int ChunkCount { get; }

    /// <summary>
    /// Allocates space for a new entity in this archetype.
    /// Returns the global index where the entity is stored.
    /// </summary>
    /// <param name="entity">The entity to allocate space for.</param>
    /// <returns>The global index of the entity within this archetype.</returns>
    int AllocateEntity(Entity entity);

    /// <summary>
    /// Removes an entity from this archetype by swapping with the last entity.
    /// </summary>
    /// <param name="indexToRemove">The global entity index to remove.</param>
    /// <returns>The entity ID that was moved to fill the gap, or -1 if no swap occurred.</returns>
    int RemoveEntity(int indexToRemove);

    /// <summary>
    /// Gets a chunk by its index in this archetype.
    /// </summary>
    /// <param name="chunkIndex">The chunk index.</param>
    /// <returns>The chunk handle.</returns>
    ChunkHandle GetChunk(int chunkIndex);

    /// <summary>
    /// Calculates the global entity index from chunk index and index within chunk.
    /// </summary>
    /// <param name="chunkIndex">The chunk index.</param>
    /// <param name="indexInChunk">The index within the chunk.</param>
    /// <returns>The global entity index.</returns>
    int GetGlobalIndex(int chunkIndex, int indexInChunk);

    /// <summary>
    /// Converts a global entity index to chunk index and index within chunk.
    /// </summary>
    /// <param name="globalIndex">The global entity index.</param>
    /// <returns>A tuple of (ChunkIndex, IndexInChunk).</returns>
    (int ChunkIndex, int IndexInChunk) GetChunkLocation(int globalIndex);
}
