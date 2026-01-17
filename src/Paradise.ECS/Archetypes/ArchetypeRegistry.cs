namespace Paradise.ECS;

/// <summary>
/// Manages unique archetypes and provides lookup by component mask.
/// Uses shared metadata for archetype IDs, layouts, and graph edges.
/// Single-threaded version without concurrent access support.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
public sealed class ArchetypeRegistry<TBits, TRegistry>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private readonly SharedArchetypeMetadata<TBits, TRegistry> _sharedMetadata;
    private readonly ChunkManager _chunkManager;

    private readonly List<Archetype<TBits, TRegistry>?> _archetypes = new();
    private readonly List<List<Archetype<TBits, TRegistry>>?> _queryCache = new();

    /// <summary>
    /// Temporary list for collecting matched query IDs during archetype operations.
    /// Reused to avoid allocations on each operation.
    /// </summary>
    private readonly List<int> _tempMatchedQueries = new();

    /// <summary>
    /// Creates a new archetype registry using the specified shared metadata.
    /// </summary>
    /// <param name="sharedMetadata">The shared metadata to use.</param>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    public ArchetypeRegistry(SharedArchetypeMetadata<TBits, TRegistry> sharedMetadata, ChunkManager chunkManager)
    {
        ArgumentNullException.ThrowIfNull(sharedMetadata);
        ArgumentNullException.ThrowIfNull(chunkManager);
        _sharedMetadata = sharedMetadata;
        _chunkManager = chunkManager;
    }

    /// <summary>
    /// Gets or creates an archetype for the given component mask.
    /// </summary>
    /// <param name="mask">The component mask defining the archetype.</param>
    /// <returns>The archetype store for this mask.</returns>
    public Archetype<TBits, TRegistry> GetOrCreateArchetype(HashedKey<ImmutableBitSet<TBits>> mask)
    {
        var matchedQueries = _tempMatchedQueries;
        matchedQueries.Clear();

        // Get or create global archetype ID and layout
        int archetypeId = _sharedMetadata.GetOrCreateArchetypeId(mask, matchedQueries);

        // Get or create archetype instance in this world
        return GetOrCreateById(archetypeId, matchedQueries);
    }

    /// <summary>
    /// Gets or creates the archetype resulting from adding a component to the source archetype.
    /// Uses cached graph edges for O(1) lookup on subsequent calls.
    /// </summary>
    /// <param name="source">The source archetype.</param>
    /// <param name="componentId">The component to add.</param>
    /// <returns>The target archetype with the component added.</returns>
    public Archetype<TBits, TRegistry> GetOrCreateArchetypeWithAdd(
        Archetype<TBits, TRegistry> source,
        ComponentId componentId)
    {
        ArgumentNullException.ThrowIfNull(source);

        var matchedQueries = _tempMatchedQueries;
        matchedQueries.Clear();

        // Get target archetype ID from shared metadata (O(1) if cached)
        int targetId = _sharedMetadata.GetOrCreateArchetypeIdWithAdd(source.Id, componentId, matchedQueries);

        // Get or create archetype instance in this world
        return GetOrCreateById(targetId, matchedQueries);
    }

    /// <summary>
    /// Gets or creates the archetype resulting from removing a component from the source archetype.
    /// Uses cached graph edges for O(1) lookup on subsequent calls.
    /// </summary>
    /// <param name="source">The source archetype.</param>
    /// <param name="componentId">The component to remove.</param>
    /// <returns>The target archetype with the component removed.</returns>
    public Archetype<TBits, TRegistry> GetOrCreateArchetypeWithRemove(
        Archetype<TBits, TRegistry> source,
        ComponentId componentId)
    {
        ArgumentNullException.ThrowIfNull(source);

        var matchedQueries = _tempMatchedQueries;
        matchedQueries.Clear();

        // Get target archetype ID from shared metadata (O(1) if cached)
        int targetId = _sharedMetadata.GetOrCreateArchetypeIdWithRemove(source.Id, componentId, matchedQueries);

        // Get or create archetype instance in this world
        return GetOrCreateById(targetId, matchedQueries);
    }

    /// <summary>
    /// Gets or creates a query for the given description.
    /// Queries are cached and reused for the same description.
    /// </summary>
    /// <param name="description">The query description defining matching criteria.</param>
    /// <returns>The query for this description.</returns>
    public Query<TBits, TRegistry> GetOrCreateQuery(HashedKey<ImmutableQueryDescription<TBits>> description)
    {
        // Get or create global query ID
        int queryId = _sharedMetadata.GetOrCreateQueryId(description);

        // Fast path: query already exists in this world
        if ((uint)queryId < (uint)_queryCache.Count && _queryCache[queryId] is { } existingList)
        {
            return new Query<TBits, TRegistry>(existingList);
        }

        // Grow list if needed by adding nulls
        int requiredCount = queryId + 1;
        _queryCache.EnsureCapacity(requiredCount);
        for (int i = _queryCache.Count; i < requiredCount; i++)
        {
            _queryCache.Add(null);
        }

        // Get matched archetype IDs from shared metadata and add only locally existing archetypes
        var matchedIds = _sharedMetadata.GetMatchedArchetypeIds(queryId);
        int matchedCount = matchedIds.Count;
        var archetypes = new List<Archetype<TBits, TRegistry>>(matchedCount);

        int localArchetypeCount = _archetypes.Count;
        for (int i = 0; i < matchedCount; i++)
        {
            int archetypeId = matchedIds[i];
            // Only add archetypes that already exist in this world
            // New archetypes will be added via NotifyQueries when created
            if ((uint)archetypeId < (uint)localArchetypeCount && _archetypes[archetypeId] is { } archetype)
            {
                archetypes.Add(archetype);
            }
        }

        _queryCache[queryId] = archetypes;
        return new Query<TBits, TRegistry>(archetypes);
    }

    /// <summary>
    /// Gets an archetype by its ID.
    /// </summary>
    /// <param name="archetypeId">The archetype ID.</param>
    /// <returns>The archetype store, or null if not found in this world.</returns>
    public Archetype<TBits, TRegistry>? GetArchetypeById(int archetypeId)
    {
        return (uint)archetypeId < (uint)_archetypes.Count ? _archetypes[archetypeId] : null;
    }

    /// <summary>
    /// Clears all archetypes and query caches from this registry.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _archetypes.Count; i++)
        {
            _archetypes[i]?.Clear();
        }
        _archetypes.Clear();
        _queryCache.Clear();
        _tempMatchedQueries.Clear();
    }

    /// <summary>
    /// Gets or creates an archetype instance by its global ID.
    /// </summary>
    /// <param name="archetypeId">The global archetype ID.</param>
    /// <param name="matchedQueries">The list of matching query IDs from shared metadata.</param>
    /// <returns>The archetype instance for this world.</returns>
    private Archetype<TBits, TRegistry> GetOrCreateById(int archetypeId, List<int> matchedQueries)
    {
        // Fast path: archetype already exists
        if ((uint)archetypeId < (uint)_archetypes.Count && _archetypes[archetypeId] is { } existing)
        {
            return existing;
        }

        // Grow list if needed by adding nulls
        int requiredCount = archetypeId + 1;
        _archetypes.EnsureCapacity(requiredCount);
        for (int i = _archetypes.Count; i < requiredCount; i++)
        {
            _archetypes.Add(null);
        }

        // Create new archetype instance for this world
        var layoutData = _sharedMetadata.GetLayoutData(archetypeId);
        var archetype = new Archetype<TBits, TRegistry>(archetypeId, layoutData, _chunkManager);

        _archetypes[archetypeId] = archetype;

        // Notify matching queries about the new archetype using pre-computed matched query IDs
        NotifyQueries(archetype, matchedQueries);
        matchedQueries.Clear();
        return archetype;
    }

    /// <summary>
    /// Notifies matching queries about a newly created archetype.
    /// Uses pre-computed matched query IDs for efficient iteration.
    /// </summary>
    /// <param name="archetype">The newly created archetype.</param>
    /// <param name="matchedQueries">The list of matching query IDs from shared metadata.</param>
    private void NotifyQueries(Archetype<TBits, TRegistry> archetype, List<int> matchedQueries)
    {
        int localQueryCount = _queryCache.Count;
        int matchedCount = matchedQueries.Count;

        for (int i = 0; i < matchedCount; i++)
        {
            int queryId = matchedQueries[i];
            // Only notify queries that exist locally in this world
            if ((uint)queryId < (uint)localQueryCount && _queryCache[queryId] is { } archetypes)
            {
                archetypes.Add(archetype);
            }
        }
    }
}
