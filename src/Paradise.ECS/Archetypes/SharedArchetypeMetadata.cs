using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Shared archetype metadata that can be used across multiple worlds.
/// Contains archetype masks, layouts, graph edges, and query descriptions.
/// Single-threaded version without concurrent access support.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
public sealed class SharedArchetypeMetadata<TBits, TRegistry> : IDisposable
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    /// <summary>
    /// Gets the shared singleton instance for this TBits/TRegistry combination.
    /// </summary>
    public static SharedArchetypeMetadata<TBits, TRegistry> Shared { get; } = new();

    /// <summary>
    /// Temporary list for collecting matched query IDs during archetype operations.
    /// Reused to avoid allocations on each operation.
    /// </summary>
    private List<int>? _tempMatchedQueries;

    private readonly IAllocator _allocator;
    private readonly Dictionary<HashedKey<ImmutableBitSet<TBits>>, int> _maskToArchetypeId = new();
    private readonly AppendOnlyList<nint/* ArchetypeLayout* */> _layouts = new();
    private readonly Dictionary<EdgeKey, int> _edges = new();
    private readonly Dictionary<HashedKey<ImmutableQueryDescription<TBits>>, int> _queryDescToId = new();
    private readonly AppendOnlyList<QueryData> _queries = new();

    /// <summary>
    /// Holds query description and its matched archetype IDs together for cache locality.
    /// </summary>
    private readonly struct QueryData
    {
        public readonly ImmutableQueryDescription<TBits> Description;
        public readonly AppendOnlyList<int> MatchedArchetypeIds;

        public QueryData(ImmutableQueryDescription<TBits> description)
        {
            Description = description;
            MatchedArchetypeIds = new AppendOnlyList<int>();
        }
    }

    private bool _disposed;

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
        var tempList = _tempMatchedQueries ??= new List<int>();
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
        ThrowHelper.ThrowIfDisposed(_disposed, this);

        // Check if already exists
        if (_maskToArchetypeId.TryGetValue(mask, out int existingId))
        {
            GetMatchedQueryIds(mask.Value, matchedQueries);
            return existingId;
        }

        var layoutData = ImmutableArchetypeLayout<TBits, TRegistry>.Create(_allocator, mask);
        int newId = _layouts.Add(layoutData);
        if (newId > EcsLimits.MaxArchetypeId)
            throw new InvalidOperationException($"Archetype count exceeded maximum of {EcsLimits.MaxArchetypeId}.");
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
        ThrowHelper.ThrowIfDisposed(_disposed, this);

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
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        return _layouts[archetypeId];
    }

    /// <summary>
    /// Gets the layout for the specified archetype ID.
    /// </summary>
    /// <param name="archetypeId">The archetype ID.</param>
    /// <returns>The layout for this archetype.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the archetype ID is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableArchetypeLayout<TBits, TRegistry> GetLayout(int archetypeId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        return new ImmutableArchetypeLayout<TBits, TRegistry>(_layouts[archetypeId]);
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
        var tempList = _tempMatchedQueries ??= new List<int>();
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
        ThrowHelper.ThrowIfDisposed(_disposed, this);

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
        var tempList = _tempMatchedQueries ??= new List<int>();
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
        ThrowHelper.ThrowIfDisposed(_disposed, this);

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
        ThrowHelper.ThrowIfDisposed(_disposed, this);

        // Check if already exists
        if (_queryDescToId.TryGetValue(description, out int existingId))
        {
            return existingId;
        }

        // Create query data and populate matched archetypes with existing matches
        var queryData = new QueryData(description.Value);
        int archetypeCount = _layouts.Count;
        for (int i = 0; i < archetypeCount; i++)
        {
            var layout = new ImmutableArchetypeLayout<TBits, TRegistry>(_layouts[i]);
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
        ThrowHelper.ThrowIfDisposed(_disposed, this);
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
        ThrowHelper.ThrowIfDisposed(_disposed, this);
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
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        return _maskToArchetypeId.TryGetValue(mask, out archetypeId);
    }

    /// <summary>
    /// Releases all resources used by this instance.
    /// Note: The static <see cref="Shared"/> instance should typically not be disposed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Free all layouts
        for (int i = 0; i < _layouts.Count; i++)
        {
            ImmutableArchetypeLayout<TBits, TRegistry>.Free(_allocator, _layouts[i]);
        }
    }
}
