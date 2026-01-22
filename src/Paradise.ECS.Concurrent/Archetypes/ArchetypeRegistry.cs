using System.Diagnostics.CodeAnalysis;

namespace Paradise.ECS.Concurrent;

/// <summary>
/// Manages unique archetypes and provides lookup by component mask.
/// Thread-safe for concurrent archetype creation and lookup.
/// Uses shared metadata for archetype IDs, layouts, and graph edges.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
/// <typeparam name="TConfig">The world configuration type that determines chunk size and limits.</typeparam>
public sealed class ArchetypeRegistry<TMask, TRegistry, TConfig>
    : IArchetypeRegistry<TMask, TRegistry, TConfig, Archetype<TMask, TRegistry, TConfig>>, IDisposable
    where TMask : unmanaged, IBitSet<TMask>
    where TRegistry : IComponentRegistry
    where TConfig : IConfig, new()
{
    /// <summary>
    /// Thread-local temporary list for collecting matched query IDs during archetype operations.
    /// Reused to avoid allocations on each operation.
    /// </summary>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
    [ThreadStatic]
    private static List<int>? s_tempMatchedQueries;

    private readonly SharedArchetypeMetadata<TMask, TRegistry, TConfig> _sharedMetadata;
    private readonly ConcurrentAppendOnlyList<Archetype<TMask, TRegistry, TConfig>?> _archetypes = new();
    private readonly ConcurrentAppendOnlyList<List<Archetype<TMask, TRegistry, TConfig>>?> _queryCache = new();
    private readonly Lock _lock = new();
    private readonly ChunkManager _chunkManager;
    private readonly OperationGuard _operationGuard = new();
    private int _disposed;

    /// <summary>
    /// Creates a new archetype registry using the specified shared metadata.
    /// </summary>
    /// <param name="sharedMetadata">The shared metadata to use.</param>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    public ArchetypeRegistry(SharedArchetypeMetadata<TMask, TRegistry, TConfig> sharedMetadata, ChunkManager chunkManager)
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
    public Query<TMask, TRegistry, TConfig, Archetype<TMask, TRegistry, TConfig>> GetOrCreateQuery(HashedKey<ImmutableQueryDescription<TMask>> description)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = _operationGuard.EnterScope();

        // Get or create global query ID
        int queryId = _sharedMetadata.GetOrCreateQueryId(description);

        // Fast path: query already exists in this world
        if ((uint)queryId < (uint)_queryCache.Count && _queryCache[queryId] is { } existingList)
        {
            return new Query<TMask, TRegistry, TConfig, Archetype<TMask, TRegistry, TConfig>>(existingList);
        }

        using var lockScope = _lock.EnterScope();

        // Grow list if needed by adding nulls
        int requiredCount = queryId + 1;
        for (int i = _queryCache.Count; i < requiredCount; i++)
        {
            _queryCache.Add(null);
        }

        // Double-check after acquiring lock
        ref var slot = ref _queryCache.GetRef(queryId);
        if (slot is not null)
        {
            return new Query<TMask, TRegistry, TConfig, Archetype<TMask, TRegistry, TConfig>>(slot);
        }

        // Get matched archetype IDs from shared metadata and add only locally existing archetypes
        var matchedIds = _sharedMetadata.GetMatchedArchetypeIds(queryId);
        int matchedCount = matchedIds.Count;
        var archetypes = new List<Archetype<TMask, TRegistry, TConfig>>(matchedCount);

        int localArchetypeCount = _archetypes.Count;
        for (int i = 0; i < matchedCount; i++)
        {
            int archetypeId = matchedIds[i];
            // Only add archetypes that already exist in this world
            // New archetypes will be added via NotifyQueries when created
            if ((uint)archetypeId < (uint)localArchetypeCount &&
                _archetypes[archetypeId] is { } archetype)
            {
                archetypes.Add(archetype);
            }
        }

        slot = archetypes;
        return new Query<TMask, TRegistry, TConfig, Archetype<TMask, TRegistry, TConfig>>(archetypes);
    }

    /// <summary>
    /// Gets or creates an archetype for the given component mask.
    /// </summary>
    /// <param name="mask">The component mask defining the archetype.</param>
    /// <returns>The archetype store for this mask.</returns>
    public Archetype<TMask, TRegistry, TConfig> GetOrCreate(HashedKey<TMask> mask)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = _operationGuard.EnterScope();

        var matchedQueries = s_tempMatchedQueries ??= new List<int>();
        matchedQueries.Clear();

        // Get or create global archetype ID and layout
        int archetypeId = _sharedMetadata.GetOrCreateArchetypeId(mask, matchedQueries);

        // Get or create archetype instance in this world
        return GetOrCreateById(archetypeId, matchedQueries);
    }

    /// <summary>
    /// Notifies matching queries about a newly created archetype.
    /// Uses pre-computed matched query IDs for efficient iteration.
    /// </summary>
    /// <param name="archetype">The newly created archetype.</param>
    /// <param name="matchedQueries">The list of matching query IDs from shared metadata.</param>
    private void NotifyQueries(Archetype<TMask, TRegistry, TConfig> archetype, List<int> matchedQueries)
    {
        int localQueryCount = _queryCache.Count;
        int matchedCount = matchedQueries.Count;

        for (int i = 0; i < matchedCount; i++)
        {
            int queryId = matchedQueries[i];
            // Only notify queries that exist locally in this world
            if ((uint)queryId < (uint)localQueryCount &&
                _queryCache[queryId] is { } archetypes)
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
    public Archetype<TMask, TRegistry, TConfig>? GetById(int archetypeId)
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
    public bool TryGet(HashedKey<TMask> mask, out Archetype<TMask, TRegistry, TConfig>? store)
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
    public Archetype<TMask, TRegistry, TConfig> GetOrCreateWithAdd(
        Archetype<TMask, TRegistry, TConfig> source,
        ComponentId componentId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = _operationGuard.EnterScope();
        ArgumentNullException.ThrowIfNull(source);

        var matchedQueries = s_tempMatchedQueries ??= new List<int>();
        matchedQueries.Clear();

        // Get target archetype ID from shared metadata (O(1) if cached)
        int targetId = _sharedMetadata.GetOrCreateWithAdd(source.Id, componentId, matchedQueries);

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
    public Archetype<TMask, TRegistry, TConfig> GetOrCreateWithRemove(
        Archetype<TMask, TRegistry, TConfig> source,
        ComponentId componentId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = _operationGuard.EnterScope();
        ArgumentNullException.ThrowIfNull(source);

        var matchedQueries = s_tempMatchedQueries ??= new List<int>();
        matchedQueries.Clear();

        // Get target archetype ID from shared metadata (O(1) if cached)
        int targetId = _sharedMetadata.GetOrCreateWithRemove(source.Id, componentId, matchedQueries);

        // Get or create archetype instance in this world
        return GetOrCreateById(targetId, matchedQueries);
    }

    /// <summary>
    /// Gets or creates an archetype instance by its global ID.
    /// </summary>
    /// <param name="archetypeId">The global archetype ID.</param>
    /// <param name="matchedQueries">The list of matching query IDs from shared metadata.</param>
    /// <returns>The archetype instance for this world.</returns>
    private Archetype<TMask, TRegistry, TConfig> GetOrCreateById(int archetypeId, List<int> matchedQueries)
    {
        // Fast path: archetype already exists (lock-free read)
        if ((uint)archetypeId < (uint)_archetypes.Count &&
            _archetypes[archetypeId] is { } existing)
        {
            return existing;
        }

        // Slow path: need to create
        using var lockScope = _lock.EnterScope();

        // Grow list if needed by adding nulls
        int requiredCount = archetypeId + 1;
        for (int i = _archetypes.Count; i < requiredCount; i++)
        {
            _archetypes.Add(null);
        }

        // Double-check after acquiring lock
        ref var slot = ref _archetypes.GetRef(archetypeId);
        if (slot is not null)
            return slot;

        // Create new archetype instance for this world
        var layoutData = _sharedMetadata.GetLayoutData(archetypeId);
        var archetype = new Archetype<TMask, TRegistry, TConfig>(archetypeId, layoutData, _chunkManager);

        slot = archetype;

        // Notify matching queries about the new archetype using pre-computed matched query IDs
        NotifyQueries(archetype, matchedQueries);

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
