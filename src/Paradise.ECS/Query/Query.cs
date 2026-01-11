using System.Collections;

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
    private readonly List<ArchetypeStore<TBits, TRegistry>> _matchingArchetypes = [];
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
    /// Gets the query description.
    /// </summary>
    public ImmutableQueryDescription<TBits> Description => _description;

    /// <summary>
    /// Gets the matching archetypes, updating the cache if new archetypes have been added.
    /// </summary>
    public MatchingArchetypesEnumerable MatchingArchetypes
    {
        get
        {
            UpdateCache();
            return new(_matchingArchetypes);
        }
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
    public bool IsEmpty
    {
        get
        {
            UpdateCache();
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

    /// <summary>
    /// Enumerable that iterates cached matching archetypes without allocation.
    /// </summary>
    public readonly struct MatchingArchetypesEnumerable : IEnumerable<ArchetypeStore<TBits, TRegistry>>
    {
        private readonly List<ArchetypeStore<TBits, TRegistry>> _archetypes;

        internal MatchingArchetypesEnumerable(List<ArchetypeStore<TBits, TRegistry>> archetypes)
        {
            _archetypes = archetypes;
        }

        public List<ArchetypeStore<TBits, TRegistry>>.Enumerator GetEnumerator() => _archetypes.GetEnumerator();

        IEnumerator<ArchetypeStore<TBits, TRegistry>> IEnumerable<ArchetypeStore<TBits, TRegistry>>.GetEnumerator()
            => _archetypes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _archetypes.GetEnumerator();
    }
}
