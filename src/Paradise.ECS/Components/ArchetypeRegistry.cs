namespace Paradise.ECS;

/// <summary>
/// Manages unique archetypes and provides lookup by component mask.
/// Thread-safe for concurrent archetype creation and lookup.
/// Uses shared metadata for archetype IDs, layouts, and graph edges.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
public sealed class ArchetypeRegistry<TBits, TRegistry> : IDisposable
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private readonly SharedArchetypeMetadata<TBits, TRegistry> _sharedMetadata;
    private readonly ConcurrentAppendOnlyList<Archetype<TBits, TRegistry>?> _archetypes = new();
    private readonly ConcurrentAppendOnlyList<List<Archetype<TBits, TRegistry>>?> _queryCache = new();
    private readonly Lock _lock = new();
    private readonly ChunkManager _chunkManager;
    private readonly OperationGuard _operationGuard = new();
    private int _disposed;

    /// <summary>
    /// Gets the shared metadata used by this registry.
    /// </summary>
    public SharedArchetypeMetadata<TBits, TRegistry> SharedMetadata => _sharedMetadata;

    /// <summary>
    /// Creates a new archetype registry using the global shared metadata.
    /// </summary>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    public ArchetypeRegistry(ChunkManager chunkManager)
        : this(SharedArchetypeMetadata<TBits, TRegistry>.Shared, chunkManager)
    {
    }

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
    /// Gets or creates a query for the given description.
    /// Queries are cached and reused for the same description.
    /// </summary>
    /// <param name="description">The query description defining matching criteria.</param>
    /// <returns>The query for this description.</returns>
    public Query<TBits, TRegistry> GetOrCreateQuery(HashedKey<ImmutableQueryDescription<TBits>> description)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = _operationGuard.EnterScope();

        // Get or create global query ID
        int queryId = _sharedMetadata.GetOrCreateQueryId(description);

        // Fast path: query already exists in this world
        if ((uint)queryId < (uint)_queryCache.Count && _queryCache[queryId] is { } existingList)
        {
            return new Query<TBits, TRegistry>(existingList);
        }

        using var lockScope = _lock.EnterScope();

        // Grow list if needed by adding nulls
        int requiredCount = queryId + 1;
        int currentCount = _queryCache.Count;
        if (currentCount < requiredCount)
        {
            int nullsToAdd = requiredCount - currentCount;
            _queryCache.AddRange(new List<Archetype<TBits, TRegistry>>?[nullsToAdd]);
        }

        // Double-check after acquiring lock
        ref var slot = ref _queryCache.GetRef(queryId);
        if (slot is not null)
        {
            return new Query<TBits, TRegistry>(slot);
        }

        // Get matched archetype IDs from shared metadata and create local archetype instances
        var matchedIds = _sharedMetadata.GetMatchedArchetypeIds(queryId);
        int matchedCount = matchedIds.Count;
        var archetypes = new List<Archetype<TBits, TRegistry>>(matchedCount);

        for (int i = 0; i < matchedCount; i++)
        {
            int archetypeId = matchedIds[i];
            // GetOrCreateByIdNoLock will create the archetype if it doesn't exist locally
            // Note: This won't cause recursion as NotifyQueries only updates existing query caches
            archetypes.Add(GetOrCreateByIdNoLock(archetypeId));
        }

        slot = archetypes;
        return new Query<TBits, TRegistry>(archetypes);
    }

    /// <summary>
    /// Gets or creates an archetype for the given component mask.
    /// </summary>
    /// <param name="mask">The component mask defining the archetype.</param>
    /// <returns>The archetype store for this mask.</returns>
    public Archetype<TBits, TRegistry> GetOrCreate(HashedKey<ImmutableBitSet<TBits>> mask)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = _operationGuard.EnterScope();

        // Get or create global archetype ID and layout
        int archetypeId = _sharedMetadata.GetOrCreateArchetypeId(mask);

        // Get or create archetype instance in this world
        return GetOrCreateById(archetypeId);
    }

    /// <summary>
    /// Notifies all registered queries about a newly created archetype.
    /// </summary>
    /// <param name="archetype">The newly created archetype.</param>
    private void NotifyQueries(Archetype<TBits, TRegistry> archetype)
    {
        var mask = archetype.Layout.ComponentMask;

        int queryCount = _queryCache.Count;
        for (int i = 0; i < queryCount; i++)
        {
            var archetypes = _queryCache[i];
            if (archetypes is null)
                continue;

            var description = _sharedMetadata.GetQueryDescription(i);
            if (description.Matches(mask))
            {
                archetypes.Add(archetype);
            }
        }
    }

    /// <summary>
    /// Gets an archetype by its ID.
    /// </summary>
    /// <param name="archetypeId">The archetype ID.</param>
    /// <returns>The archetype store, or null if not found in this world.</returns>
    public Archetype<TBits, TRegistry>? GetById(int archetypeId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = _operationGuard.EnterScope();

        return (uint)archetypeId < (uint)_archetypes.Count ? _archetypes[archetypeId] : null;
    }

    /// <summary>
    /// Tries to get an archetype by its component mask.
    /// </summary>
    /// <param name="mask">The component mask.</param>
    /// <param name="store">The archetype store if found.</param>
    /// <returns>True if found in this world.</returns>
    public bool TryGet(HashedKey<ImmutableBitSet<TBits>> mask, out Archetype<TBits, TRegistry>? store)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = _operationGuard.EnterScope();

        if (_sharedMetadata.TryGetArchetypeId(mask, out int archetypeId) &&
            (uint)archetypeId < (uint)_archetypes.Count &&
            _archetypes[archetypeId] is { } archetype)
        {
            store = archetype;
            return true;
        }

        store = null;
        return false;
    }

    /// <summary>
    /// Gets or creates the archetype resulting from adding a component to the source archetype.
    /// Uses cached graph edges for O(1) lookup on subsequent calls.
    /// </summary>
    /// <param name="source">The source archetype.</param>
    /// <param name="componentId">The component to add.</param>
    /// <returns>The target archetype with the component added.</returns>
    public Archetype<TBits, TRegistry> GetOrCreateWithAdd(
        Archetype<TBits, TRegistry> source,
        ComponentId componentId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = _operationGuard.EnterScope();
        ArgumentNullException.ThrowIfNull(source);

        // Get target archetype ID from shared metadata (O(1) if cached)
        int targetId = _sharedMetadata.GetOrCreateWithAdd(source.Id, componentId);

        // Get or create archetype instance in this world
        return GetOrCreateById(targetId);
    }

    /// <summary>
    /// Gets or creates the archetype resulting from removing a component from the source archetype.
    /// Uses cached graph edges for O(1) lookup on subsequent calls.
    /// </summary>
    /// <param name="source">The source archetype.</param>
    /// <param name="componentId">The component to remove.</param>
    /// <returns>The target archetype with the component removed.</returns>
    public Archetype<TBits, TRegistry> GetOrCreateWithRemove(
        Archetype<TBits, TRegistry> source,
        ComponentId componentId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = _operationGuard.EnterScope();
        ArgumentNullException.ThrowIfNull(source);

        // Get target archetype ID from shared metadata (O(1) if cached)
        int targetId = _sharedMetadata.GetOrCreateWithRemove(source.Id, componentId);

        // Get or create archetype instance in this world
        return GetOrCreateById(targetId);
    }

    /// <summary>
    /// Gets or creates an archetype instance by its global ID.
    /// </summary>
    /// <param name="archetypeId">The global archetype ID.</param>
    /// <returns>The archetype instance for this world.</returns>
    private Archetype<TBits, TRegistry> GetOrCreateById(int archetypeId)
    {
        using var lockScope = _lock.EnterScope();
        return GetOrCreateByIdNoLock(archetypeId);
    }

    /// <summary>
    /// Gets or creates an archetype instance by its global ID without acquiring the lock.
    /// Caller must hold the lock.
    /// </summary>
    /// <param name="archetypeId">The global archetype ID.</param>
    /// <returns>The archetype instance for this world.</returns>
    private Archetype<TBits, TRegistry> GetOrCreateByIdNoLock(int archetypeId)
    {
        // Grow list if needed by adding nulls
        int requiredCount = archetypeId + 1;
        int currentCount = _archetypes.Count;
        if (currentCount < requiredCount)
        {
            int nullsToAdd = requiredCount - currentCount;
            _archetypes.AddRange(new Archetype<TBits, TRegistry>?[nullsToAdd]);
        }

        // Fast path: check if already exists in this world
        ref var slot = ref _archetypes.GetRef(archetypeId);
        if (slot is not null)
            return slot;

        // Slow path: create new archetype instance for this world
        var layoutData = _sharedMetadata.GetLayoutData(archetypeId);
        var archetype = new Archetype<TBits, TRegistry>(archetypeId, layoutData, _chunkManager);

        slot = archetype;

        // Notify all registered queries about the new archetype
        NotifyQueries(archetype);

        return archetype;
    }

    /// <summary>
    /// Releases resources used by this registry.
    /// Note: Does not dispose the shared metadata.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Wait for all in-flight operations to complete
        _operationGuard.WaitForCompletion();

        // Note: We don't dispose layouts here since they are owned by SharedArchetypeMetadata
        // ConcurrentAppendOnlyList doesn't need clearing - GC will handle the references
    }
}
