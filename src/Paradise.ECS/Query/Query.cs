namespace Paradise.ECS;

/// <summary>
/// A query that iterates matching archetypes with incremental caching.
/// Caches matching archetypes and updates incrementally when new archetypes are added.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
public sealed class Query<TBits, TRegistry>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private readonly ArchetypeRegistry<TBits, TRegistry> _archetypeRegistry;
    private readonly ImmutableQueryDescription<TBits> _description;
    private readonly List<Archetype<TBits, TRegistry>> _matchingArchetypes = new(32);
    private int _lastCheckedCount;

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
    }

    /// <summary>
    /// Gets the total number of entities matching this query across all archetypes.
    /// </summary>
    public int EntityCount
    {
        get
        {
            UpdateCache();
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
    public bool IsEmpty => EntityCount == 0;

    /// <summary>
    /// Gets the number of matching archetypes.
    /// </summary>
    public int ArchetypeCount
    {
        get
        {
            UpdateCache();
            return _matchingArchetypes.Count;
        }
    }

    /// <summary>
    /// Updates the cache with any new archetypes added since the last check.
    /// </summary>
    private void UpdateCache()
    {
        int currentCount = _archetypeRegistry.Count;
        if (currentCount > _lastCheckedCount)
        {
            _archetypeRegistry.GetMatching(_description, _matchingArchetypes, _lastCheckedCount);
            _lastCheckedCount = currentCount;
        }
    }
}
