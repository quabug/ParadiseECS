using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Paradise.ECS.Concurrent;

/// <summary>
/// Shared archetype metadata that can be used across multiple worlds.
/// Contains archetype masks, layouts, graph edges, and query descriptions.
/// Thread-safe for concurrent access from multiple worlds.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
/// <typeparam name="TConfig">The world configuration type that determines chunk size and limits.</typeparam>
public sealed class SharedArchetypeMetadata<TBits, TRegistry, TConfig> : IDisposable
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
    where TConfig : IWorldConfig
{
    /// <summary>
    /// Gets the shared singleton instance for this TBits/TRegistry/TConfig combination.
    /// </summary>
    public static SharedArchetypeMetadata<TBits, TRegistry, TConfig> Shared { get; } = new();

    /// <summary>
    /// Thread-local temporary list for collecting matched query IDs during archetype operations.
    /// Reused to avoid allocations on each operation.
    /// </summary>
    [ThreadStatic]
    private static List<int>? s_tempMatchedQueries;

    private readonly IAllocator _allocator;
    private readonly ConcurrentDictionary<HashedKey<ImmutableBitSet<TBits>>, int> _maskToArchetypeId = new();
    private readonly ConcurrentAppendOnlyList<nint/* ArchetypeLayout* */> _layouts = new();
    private readonly ConcurrentDictionary<EdgeKey, int> _edges = new();
    private readonly ConcurrentDictionary<HashedKey<ImmutableQueryDescription<TBits>>, int> _queryDescToId = new();
    private readonly ConcurrentAppendOnlyList<QueryData> _queries = new();
    private readonly Lock _createLock = new();

    /// <summary>
    /// Holds query description and its matched archetype IDs together for cache locality.
    /// </summary>
    private readonly struct QueryData
    {
        public readonly ImmutableQueryDescription<TBits> Description;
        public readonly ConcurrentAppendOnlyList<int> MatchedArchetypeIds;

        public QueryData(ImmutableQueryDescription<TBits> description)
        {
            Description = description;
            MatchedArchetypeIds = new ConcurrentAppendOnlyList<int>();
        }
    }

    private int _disposed;

    /// <summary>
    /// Gets the number of registered archetypes.
    /// </summary>
    public int ArchetypeCount => _layouts.Count;

    /// <summary>
    /// Gets the number of registered query descriptions.
    /// </summary>
    public int QueryDescriptionCount => _queries.Count;

    /// <summary>
    /// Creates a new shared archetype metadata instance.
    /// </summary>
    /// <param name="allocator">The memory allocator to use. If null, uses <see cref="NativeMemoryAllocator.Shared"/>.</param>
    public SharedArchetypeMetadata(IAllocator? allocator = null)
    {
        _allocator = allocator ?? NativeMemoryAllocator.Shared;
    }

    /// <summary>
    /// Gets or creates an archetype ID for the given component mask.
    /// Also creates and stores the layout for the archetype.
    /// </summary>
    /// <param name="mask">The component mask defining the archetype.</param>
    /// <returns>The archetype ID for this mask.</returns>
    public int GetOrCreateArchetypeId(HashedKey<ImmutableBitSet<TBits>> mask)
    {
        var tempList = s_tempMatchedQueries ??= new List<int>();
        tempList.Clear();
        return GetOrCreateArchetypeId(mask, tempList);
    }

    /// <summary>
    /// Gets or creates an archetype ID for the given component mask.
    /// Also creates and stores the layout for the archetype.
    /// Outputs the IDs of queries that match this archetype.
    /// </summary>
    /// <typeparam name="T">A list type to collect the matching query IDs.</typeparam>
    /// <param name="mask">The component mask defining the archetype.</param>
    /// <param name="matchedQueries">The list to add matching query IDs to.</param>
    /// <returns>The archetype ID for this mask.</returns>
    public int GetOrCreateArchetypeId<T>(HashedKey<ImmutableBitSet<TBits>> mask, T matchedQueries) where T : IList<int>
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        // Fast path: already exists
        if (_maskToArchetypeId.TryGetValue(mask, out int existingId))
        {
            GetMatchedQueryIds(mask.Value, matchedQueries);
            return existingId;
        }

        // Slow path: create new archetype
        // Lock prevents duplicate creation - without it, two threads could both pass the
        // fast path check, create separate layouts for the same mask, and one would be orphaned.
        using var createScope = _createLock.EnterScope();

        // Double-check after acquiring lock
        if (_maskToArchetypeId.TryGetValue(mask, out existingId))
        {
            GetMatchedQueryIds(mask.Value, matchedQueries);
            return existingId;
        }

        var layoutData = ImmutableArchetypeLayout<TBits, TRegistry, TConfig>.Create(_allocator, mask);
        int newId = _layouts.Add(layoutData);
        ThrowHelper.ThrowIfArchetypeIdExceedsLimit(newId);
        _maskToArchetypeId[mask] = newId;

        // Notify all existing queries about the new archetype
        NotifyQueriesOfNewArchetype(newId, mask.Value, matchedQueries);

        return newId;
    }

    /// <summary>
    /// Notifies all existing queries about a newly created archetype.
    /// Adds the archetype ID to matching query lists.
    /// </summary>
    /// <typeparam name="T">A list type to collect the matching query IDs.</typeparam>
    /// <param name="archetypeId">The new archetype ID.</param>
    /// <param name="mask">The component mask of the new archetype.</param>
    /// <param name="matchedQueries">The list to add matching query IDs to.</param>
    private void NotifyQueriesOfNewArchetype<T>(int archetypeId, ImmutableBitSet<TBits> mask, T matchedQueries) where T : IList<int>
    {
        // TODO: Optimize with inverted index when query count becomes a bottleneck.
        // Current: O(queries) linear scan over all queries.
        // Optimization: Maintain Dictionary<ComponentId, List<int>> mapping components to query indices.
        // When matching, find the component in the mask with fewest associated queries, then only
        // check those candidates. Reduces to O(queries containing rarest component).
        int queryCount = _queries.Count;
        for (int i = 0; i < queryCount; i++)
        {
            ref readonly var query = ref _queries.GetRef(i);
            if (query.Description.Matches(mask))
            {
                matchedQueries.Add(i);
                query.MatchedArchetypeIds.Add(archetypeId);
            }
        }
    }

    /// <summary>
    /// Gets the IDs of all queries that match the given component mask.
    /// </summary>
    /// <typeparam name="T">A list type to collect the matching query IDs.</typeparam>
    /// <param name="mask">The component mask to match against.</param>
    /// <param name="result">The list to add matching query IDs to.</param>
    public void GetMatchedQueryIds<T>(ImmutableBitSet<TBits> mask, T result) where T : IList<int>
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        // TODO: Optimize with inverted index when query count becomes a bottleneck.
        // See NotifyQueriesOfNewArchetype for details.
        int queryCount = _queries.Count;
        for (int i = 0; i < queryCount; i++)
        {
            ref readonly var query = ref _queries.GetRef(i);
            if (query.Description.Matches(mask))
            {
                result.Add(i);
            }
        }
    }

    /// <summary>
    /// Gets the layout data pointer for the specified archetype ID.
    /// </summary>
    /// <param name="archetypeId">The archetype ID.</param>
    /// <returns>The layout data pointer (as nint) for this archetype.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the archetype ID is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nint GetLayoutData(int archetypeId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        return _layouts[archetypeId];
    }

    /// <summary>
    /// Gets the layout for the specified archetype ID.
    /// </summary>
    /// <param name="archetypeId">The archetype ID.</param>
    /// <returns>The layout for this archetype.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the archetype ID is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableArchetypeLayout<TBits, TRegistry, TConfig> GetLayout(int archetypeId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        return new ImmutableArchetypeLayout<TBits, TRegistry, TConfig>(_layouts[archetypeId]);
    }

    /// <summary>
    /// Gets the archetype ID resulting from adding a component to a source archetype.
    /// Uses cached graph edges for O(1) lookup on subsequent calls.
    /// </summary>
    /// <param name="sourceArchetypeId">The source archetype ID.</param>
    /// <param name="componentId">The component to add.</param>
    /// <returns>The target archetype ID with the component added.</returns>
    public int GetOrCreateWithAdd(int sourceArchetypeId, ComponentId componentId)
    {
        var tempList = s_tempMatchedQueries ??= new List<int>();
        tempList.Clear();
        return GetOrCreateWithAdd(sourceArchetypeId, componentId, tempList);
    }

    /// <summary>
    /// Gets the archetype ID resulting from adding a component to a source archetype.
    /// Uses cached graph edges for O(1) lookup on subsequent calls.
    /// </summary>
    /// <typeparam name="T">A list type to collect the matching query IDs.</typeparam>
    /// <param name="sourceArchetypeId">The source archetype ID.</param>
    /// <param name="componentId">The component to add.</param>
    /// <param name="matchedQueries">The list to add matching query IDs to.</param>
    /// <returns>The target archetype ID with the component added.</returns>
    public int GetOrCreateWithAdd<T>(int sourceArchetypeId, ComponentId componentId, T matchedQueries) where T : IList<int>
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        var addKey = EdgeKey.ForAdd(sourceArchetypeId, componentId.Value);

        // Fast path: edge already exists
        if (_edges.TryGetValue(addKey, out int targetId))
        {
            var targetLayout = GetLayout(targetId);
            GetMatchedQueryIds(targetLayout.ComponentMask, matchedQueries);
            return targetId;
        }

        // Slow path: compute mask and get/create archetype
        var sourceLayout = GetLayout(sourceArchetypeId);
        // TODO: Use incremental hash computation instead of recomputing from scratch (#14)
        var newMask = (HashedKey<ImmutableBitSet<TBits>>)sourceLayout.ComponentMask.Set(componentId);
        targetId = GetOrCreateArchetypeId(newMask, matchedQueries);

        // Cache bidirectional edges
        var removeKey = EdgeKey.ForRemove(targetId, componentId.Value);
        _edges[addKey] = targetId;
        _edges[removeKey] = sourceArchetypeId;

        return targetId;
    }

    /// <summary>
    /// Gets the archetype ID resulting from removing a component from a source archetype.
    /// Uses cached graph edges for O(1) lookup on subsequent calls.
    /// </summary>
    /// <param name="sourceArchetypeId">The source archetype ID.</param>
    /// <param name="componentId">The component to remove.</param>
    /// <returns>The target archetype ID with the component removed.</returns>
    public int GetOrCreateWithRemove(int sourceArchetypeId, ComponentId componentId)
    {
        var tempList = s_tempMatchedQueries ??= new List<int>();
        tempList.Clear();
        return GetOrCreateWithRemove(sourceArchetypeId, componentId, tempList);
    }

    /// <summary>
    /// Gets the archetype ID resulting from removing a component from a source archetype.
    /// Uses cached graph edges for O(1) lookup on subsequent calls.
    /// </summary>
    /// <typeparam name="T">A list type to collect the matching query IDs.</typeparam>
    /// <param name="sourceArchetypeId">The source archetype ID.</param>
    /// <param name="componentId">The component to remove.</param>
    /// <param name="matchedQueries">The list to add matching query IDs to.</param>
    /// <returns>The target archetype ID with the component removed.</returns>
    public int GetOrCreateWithRemove<T>(int sourceArchetypeId, ComponentId componentId, T matchedQueries) where T : IList<int>
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        var removeKey = EdgeKey.ForRemove(sourceArchetypeId, componentId.Value);

        // Fast path: edge already exists
        if (_edges.TryGetValue(removeKey, out int targetId))
        {
            var targetLayout = GetLayout(targetId);
            GetMatchedQueryIds(targetLayout.ComponentMask, matchedQueries);
            return targetId;
        }

        // Slow path: compute mask and get/create archetype
        var sourceLayout = GetLayout(sourceArchetypeId);
        // TODO: Use incremental hash computation instead of recomputing from scratch (#14)
        var newMask = (HashedKey<ImmutableBitSet<TBits>>)sourceLayout.ComponentMask.Clear(componentId);
        targetId = GetOrCreateArchetypeId(newMask, matchedQueries);

        // Cache bidirectional edges
        var addKey = EdgeKey.ForAdd(targetId, componentId.Value);
        _edges[removeKey] = targetId;
        _edges[addKey] = sourceArchetypeId;

        return targetId;
    }

    /// <summary>
    /// Gets or creates a query ID for the given query description.
    /// </summary>
    /// <param name="description">The query description.</param>
    /// <returns>The query ID for this description.</returns>
    public int GetOrCreateQueryId(HashedKey<ImmutableQueryDescription<TBits>> description)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        // Fast path: already exists
        if (_queryDescToId.TryGetValue(description, out int existingId))
        {
            return existingId;
        }

        // Slow path: create new query ID
        using var createScope = _createLock.EnterScope();

        // Double-check after acquiring lock
        if (_queryDescToId.TryGetValue(description, out existingId))
        {
            return existingId;
        }

        // Create query data and populate matched archetypes with existing matches
        var queryData = new QueryData(description.Value);
        int archetypeCount = _layouts.Count;
        for (int i = 0; i < archetypeCount; i++)
        {
            var layout = new ImmutableArchetypeLayout<TBits, TRegistry, TConfig>(_layouts[i]);
            if (description.Value.Matches(layout.ComponentMask))
            {
                queryData.MatchedArchetypeIds.Add(i);
            }
        }

        int newId = _queries.Add(queryData);
        _queryDescToId[description] = newId;

        return newId;
    }

    /// <summary>
    /// Gets the list of archetype IDs that match the specified query.
    /// </summary>
    /// <param name="queryId">The query ID.</param>
    /// <returns>The list of matching archetype IDs.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the query ID is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<int> GetMatchedArchetypeIds(int queryId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        return _queries[queryId].MatchedArchetypeIds;
    }

    /// <summary>
    /// Gets the query description for the specified query ID.
    /// </summary>
    /// <param name="queryId">The query ID.</param>
    /// <returns>A reference to the query description.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the query ID is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly ImmutableQueryDescription<TBits> GetQueryDescription(int queryId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        return ref _queries.GetRef(queryId).Description;
    }

    /// <summary>
    /// Tries to get an existing archetype ID for the given component mask.
    /// </summary>
    /// <param name="mask">The component mask.</param>
    /// <param name="archetypeId">The archetype ID if found.</param>
    /// <returns>True if the archetype exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetArchetypeId(HashedKey<ImmutableBitSet<TBits>> mask, out int archetypeId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        return _maskToArchetypeId.TryGetValue(mask, out archetypeId);
    }

    /// <summary>
    /// Releases all resources used by this instance.
    /// Note: The static <see cref="Shared"/> instance should typically not be disposed.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Free all layouts
        for (int i = 0; i < _layouts.Count; i++)
        {
            ImmutableArchetypeLayout<TBits, TRegistry, TConfig>.Free(_allocator, _layouts[i]);
        }
    }
}
