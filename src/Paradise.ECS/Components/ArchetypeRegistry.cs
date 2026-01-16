using System.Collections.Concurrent;

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
    private readonly Dictionary<int, Archetype<TBits, TRegistry>> _archetypes = new();
    private readonly ConcurrentDictionary<int, List<Archetype<TBits, TRegistry>>> _queryCache = new();
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
        if (_queryCache.TryGetValue(queryId, out var existingList))
        {
            return new Query<TBits, TRegistry>(existingList);
        }

        using var lockScope = _lock.EnterScope();

        // Double-check after acquiring lock
        if (_queryCache.TryGetValue(queryId, out existingList))
        {
            return new Query<TBits, TRegistry>(existingList);
        }

        // Create new list and populate with existing matching archetypes
        var archetypes = new List<Archetype<TBits, TRegistry>>(32);

        foreach (var (_, archetype) in _archetypes)
        {
            if (description.Value.Matches(archetype.Layout.ComponentMask))
            {
                archetypes.Add(archetype);
            }
        }

        _queryCache[queryId] = archetypes;
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

        foreach (var (queryId, archetypes) in _queryCache)
        {
            var description = _sharedMetadata.GetQueryDescription(queryId);
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
        using var __ = _lock.EnterScope();

        return _archetypes.TryGetValue(archetypeId, out var archetype) ? archetype : null;
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
        using var __ = _lock.EnterScope();

        if (_sharedMetadata.TryGetArchetypeId(mask, out int archetypeId) &&
            _archetypes.TryGetValue(archetypeId, out var archetype))
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

        // Fast path: check if already exists in this world
        if (_archetypes.TryGetValue(archetypeId, out var existing))
            return existing;

        // Slow path: create new archetype instance for this world
        var layoutData = _sharedMetadata.GetLayoutData(archetypeId);
        var archetype = new Archetype<TBits, TRegistry>(archetypeId, layoutData, _chunkManager);

        _archetypes[archetypeId] = archetype;

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

        using var _ = _lock.EnterScope();

        // Note: We don't dispose layouts here since they are owned by SharedArchetypeMetadata
        _archetypes.Clear();
        _queryCache.Clear();
    }
}
