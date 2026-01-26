using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Helper methods for creating query results.
/// Used by generated extension methods to minimize generated code.
/// </summary>
public static class QueryHelpers
{
    /// <summary>
    /// Creates a query result for entity-level iteration.
    /// </summary>
    /// <typeparam name="TData">The data type providing component access.</typeparam>
    /// <typeparam name="TMask">The component mask type.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <param name="world">The world to query.</param>
    /// <param name="description">The query description.</param>
    /// <returns>A query result for iterating over entities.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueryResult<TData, Archetype<TMask, TConfig>, TMask, TConfig>
        CreateQueryResult<TData, TMask, TConfig>(
            IWorld<TMask, TConfig> world,
            HashedKey<ImmutableQueryDescription<TMask>> description)
        where TData : IQueryData<TData, TMask, TConfig>, allows ref struct
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
    {
        var query = world.ArchetypeRegistry.GetOrCreateQuery(description);
        return new QueryResult<TData, Archetype<TMask, TConfig>, TMask, TConfig>(
            world.ChunkManager, world.EntityManager, query);
    }

    /// <summary>
    /// Creates a chunk query result for batch processing.
    /// </summary>
    /// <typeparam name="TChunkData">The chunk data type providing span access.</typeparam>
    /// <typeparam name="TMask">The component mask type.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <param name="world">The world to query.</param>
    /// <param name="description">The query description.</param>
    /// <returns>A chunk query result for iterating over chunks.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ChunkQueryResult<TChunkData, Archetype<TMask, TConfig>, TMask, TConfig>
        CreateChunkQueryResult<TChunkData, TMask, TConfig>(
            IWorld<TMask, TConfig> world,
            HashedKey<ImmutableQueryDescription<TMask>> description)
        where TChunkData : IQueryChunkData<TChunkData, TMask, TConfig>, allows ref struct
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
    {
        var query = world.ArchetypeRegistry.GetOrCreateQuery(description);
        return new ChunkQueryResult<TChunkData, Archetype<TMask, TConfig>, TMask, TConfig>(
            world.ChunkManager, world.EntityManager, query);
    }
}
