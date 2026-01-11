namespace Paradise.ECS;

/// <summary>
/// A cached query that efficiently iterates matching archetypes.
/// Caches matching archetypes for performance, with lazy invalidation when new archetypes are added.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
public sealed class Query<TBits, TRegistry>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private readonly ArchetypeRegistry<TBits, TRegistry> _archetypeRegistry;
    private readonly ImmutableQueryDescription<TBits> _description;
    private readonly List<ArchetypeStore<TBits, TRegistry>> _matchingArchetypes;
    private int _lastArchetypeCount;

    /// <summary>
    /// Gets the query description that defines matching criteria.
    /// </summary>
    public ImmutableQueryDescription<TBits> Description => _description;

    /// <summary>
    /// Creates a new query with the specified description.
    /// </summary>
    /// <param name="archetypeRegistry">The archetype registry to query.</param>
    /// <param name="description">The query description defining matching criteria.</param>
    internal Query(ArchetypeRegistry<TBits, TRegistry> archetypeRegistry, ImmutableQueryDescription<TBits> description)
    {
        ArgumentNullException.ThrowIfNull(archetypeRegistry);

        _archetypeRegistry = archetypeRegistry;
        _description = description;
        _matchingArchetypes = [];
        _lastArchetypeCount = -1; // Force initial refresh
    }

    /// <summary>
    /// Gets the matching archetypes, refreshing the cache if needed.
    /// </summary>
    /// <returns>A read-only list of matching archetype stores.</returns>
    public IReadOnlyList<ArchetypeStore<TBits, TRegistry>> GetMatchingArchetypes()
    {
        RefreshCacheIfNeeded();
        return _matchingArchetypes;
    }

    /// <summary>
    /// Gets the total number of entities matching this query across all archetypes.
    /// </summary>
    public int EntityCount
    {
        get
        {
            RefreshCacheIfNeeded();
            int count = 0;
            foreach (var archetype in _matchingArchetypes)
            {
                count += archetype.EntityCount;
            }
            return count;
        }
    }

    /// <summary>
    /// Gets whether this query has any matching entities.
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            RefreshCacheIfNeeded();
            foreach (var archetype in _matchingArchetypes)
            {
                if (archetype.EntityCount > 0)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Gets the number of matching archetypes.
    /// </summary>
    public int ArchetypeCount
    {
        get
        {
            RefreshCacheIfNeeded();
            return _matchingArchetypes.Count;
        }
    }

    /// <summary>
    /// Refreshes the cached matching archetypes if the registry has been modified.
    /// Only checks newly added archetypes for incremental updates.
    /// </summary>
    private void RefreshCacheIfNeeded()
    {
        int currentCount = _archetypeRegistry.Count;
        if (currentCount == _lastArchetypeCount)
            return;

        // Only match archetypes added since last refresh
        int startIndex = _lastArchetypeCount < 0 ? 0 : _lastArchetypeCount;
        _archetypeRegistry.GetMatching(_description, _matchingArchetypes, startIndex);
        _lastArchetypeCount = currentCount;
    }

    /// <summary>
    /// Forces a refresh of the cached matching archetypes.
    /// </summary>
    public void Refresh()
    {
        _lastArchetypeCount = -1;
        RefreshCacheIfNeeded();
    }
}
