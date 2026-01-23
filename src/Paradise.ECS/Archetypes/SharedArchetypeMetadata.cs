using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Shared archetype metadata that can be used across multiple worlds.
/// Contains archetype masks, layouts, graph edges, and query descriptions.
/// Single-threaded version without concurrent access support.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public sealed class SharedArchetypeMetadata<TMask, TConfig> : IDisposable
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    private readonly IAllocator _layoutAllocator;
    public ImmutableArray<ComponentTypeInfo> TypeInfos { get; }
    private readonly Dictionary<HashedKey<TMask>, int> _maskToArchetypeId = new();
    private readonly List<nint/* ArchetypeLayout* */> _layouts = new();
    private readonly Dictionary<EdgeKey, int> _edges = new();
    private readonly Dictionary<HashedKey<ImmutableQueryDescription<TMask>>, int> _queryDescriptionToId = new();
    private readonly List<QueryData> _queries = new();

    /// <summary>
    /// Holds query description and its matched archetype IDs together for cache locality.
    /// </summary>
    private readonly struct QueryData(ImmutableQueryDescription<TMask> description)
    {
        public readonly ImmutableQueryDescription<TMask> Description = description;
        public readonly List<int> MatchedArchetypeIds = new();
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
    /// <param name="registry">The component registry providing type information.</param>
    /// <param name="config">The configuration instance with runtime settings including the allocators.</param>
    public SharedArchetypeMetadata(IComponentRegistry registry, TConfig config)
    {
        TypeInfos = registry?.TypeInfos ?? throw new ArgumentNullException(nameof(registry));
        _layoutAllocator = config.LayoutAllocator ?? throw new ArgumentNullException(nameof(config), "Config.LayoutAllocator cannot be null");
    }

    /// <summary>
    /// Creates a new shared archetype metadata instance using default configuration.
    /// Uses <c>new TConfig()</c> for configuration with default property values.
    /// </summary>
    /// <param name="registry">The component registry providing type information.</param>
    public SharedArchetypeMetadata(IComponentRegistry registry) : this(registry, new TConfig())
    {
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
    public int GetOrCreateArchetypeId<T>(HashedKey<TMask> mask, T matchedQueries) where T : IList<int>
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);

        // Check if already exists
        if (_maskToArchetypeId.TryGetValue(mask, out int existingId))
        {
            GetMatchedQueryIds(mask.Value, matchedQueries);
            return existingId;
        }

        var layoutData = ImmutableArchetypeLayout<TMask, TConfig>.Create(_layoutAllocator, TypeInfos, mask);
        int newId = _layouts.Count;
        _layouts.Add(layoutData);
        ThrowHelper.ThrowIfArchetypeIdExceedsLimit(newId);
        _maskToArchetypeId[mask] = newId;

        // Notify all existing queries about the new archetype
        NotifyQueriesOfNewArchetype(newId, mask.Value, matchedQueries);

        return newId;
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
    public int GetOrCreateArchetypeIdWithAdd<T>(int sourceArchetypeId, ComponentId componentId, T matchedQueries) where T : IList<int>
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
        var newMask = (HashedKey<TMask>)sourceLayout.ComponentMask.Set(componentId);
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
    /// <typeparam name="T">A list type to collect the matching query IDs.</typeparam>
    /// <param name="sourceArchetypeId">The source archetype ID.</param>
    /// <param name="componentId">The component to remove.</param>
    /// <param name="matchedQueries">The list to add matching query IDs to.</param>
    /// <returns>The target archetype ID with the component removed.</returns>
    public int GetOrCreateArchetypeIdWithRemove<T>(int sourceArchetypeId, ComponentId componentId, T matchedQueries) where T : IList<int>
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
        var newMask = (HashedKey<TMask>)sourceLayout.ComponentMask.Clear(componentId);
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
    public int GetOrCreateQueryId(HashedKey<ImmutableQueryDescription<TMask>> description)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);

        // Check if already exists
        if (_queryDescriptionToId.TryGetValue(description, out int existingId))
        {
            return existingId;
        }

        // Create query data and populate matched archetypes with existing matches
        var queryData = new QueryData(description.Value);
        int archetypeCount = _layouts.Count;
        for (int i = 0; i < archetypeCount; i++)
        {
            var layout = new ImmutableArchetypeLayout<TMask, TConfig>(_layouts[i]);
            if (description.Value.Matches(layout.ComponentMask))
            {
                queryData.MatchedArchetypeIds.Add(i);
            }
        }

        int newId = _queries.Count;
        _queries.Add(queryData);
        _queryDescriptionToId[description] = newId;

        return newId;
    }

    /// <summary>
    /// Tries to get an existing archetype ID for the given component mask.
    /// </summary>
    /// <param name="mask">The component mask.</param>
    /// <param name="archetypeId">The archetype ID if found.</param>
    /// <returns>True if the archetype exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetArchetypeId(HashedKey<TMask> mask, out int archetypeId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        return _maskToArchetypeId.TryGetValue(mask, out archetypeId);
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
    public ImmutableArchetypeLayout<TMask, TConfig> GetLayout(int archetypeId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        return new ImmutableArchetypeLayout<TMask, TConfig>(_layouts[archetypeId]);
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
    /// <returns>The query description.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the query ID is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableQueryDescription<TMask> GetQueryDescription(int queryId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        return _queries[queryId].Description;
    }

    /// <summary>
    /// Notifies all existing queries about a newly created archetype.
    /// Adds the archetype ID to matching query lists.
    /// </summary>
    /// <typeparam name="T">A list type to collect the matching query IDs.</typeparam>
    /// <param name="archetypeId">The new archetype ID.</param>
    /// <param name="mask">The component mask of the new archetype.</param>
    /// <param name="matchedQueries">The list to add matching query IDs to.</param>
    private void NotifyQueriesOfNewArchetype<T>(int archetypeId, TMask mask, T matchedQueries) where T : IList<int>
    {
        int queryCount = _queries.Count;
        for (int i = 0; i < queryCount; i++)
        {
            var query = _queries[i];
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
    private void GetMatchedQueryIds<T>(TMask mask, T result) where T : IList<int>
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);

        // TODO: Optimize with inverted index when query count becomes a bottleneck.
        // Current: O(queries) linear scan over all queries.
        // Optimization: Maintain Dictionary<ComponentId, List<int>> mapping components to query indices.
        // When matching, find the component in the mask with fewest associated queries, then only
        // check those candidates. Reduces to O(queries containing rarest component).
        int queryCount = _queries.Count;
        for (int i = 0; i < queryCount; i++)
        {
            var query = _queries[i];
            if (query.Description.Matches(mask))
            {
                result.Add(i);
            }
        }
    }

    /// <summary>
    /// Clears all archetype and query data, freeing native memory for layouts.
    /// </summary>
    public void Clear()
    {
        // Free all layouts
        for (int i = 0; i < _layouts.Count; i++)
        {
            ImmutableArchetypeLayout<TMask, TConfig>.Free(_layoutAllocator, _layouts[i]);
        }

        _layouts.Clear();
        _maskToArchetypeId.Clear();
        _edges.Clear();
        _queries.Clear();
        _queryDescriptionToId.Clear();
    }

    /// <summary>
    /// Releases all resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Clear();
    }
}
