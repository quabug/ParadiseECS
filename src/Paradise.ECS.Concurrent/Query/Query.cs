namespace Paradise.ECS.Concurrent;

/// <summary>
/// A lightweight, read-only view over a collection of archetypes that match specific component criteria.
/// The underlying list of archetypes is managed by the <see cref="ArchetypeRegistry{TBits, TRegistry}"/>
/// and is updated automatically as new matching archetypes are created.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
public readonly struct Query<TBits, TRegistry>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private readonly List<Archetype<TBits, TRegistry>> _matchingArchetypes;

    /// <summary>
    /// Creates a new query wrapping the specified archetype list.
    /// </summary>
    /// <param name="matchingArchetypes">The list of matching archetypes, owned by the registry.</param>
    internal Query(List<Archetype<TBits, TRegistry>> matchingArchetypes)
    {
        _matchingArchetypes = matchingArchetypes;
    }

    /// <summary>
    /// Gets the total number of entities matching this query across all archetypes.
    /// </summary>
    public int EntityCount
    {
        get
        {
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
            foreach (var archetype in _matchingArchetypes)
            {
                if (archetype.EntityCount > 0) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Gets the number of matching archetypes.
    /// </summary>
    public int ArchetypeCount => _matchingArchetypes.Count;

    /// <summary>
    /// Returns an enumerator that iterates through the matching archetypes.
    /// </summary>
    /// <returns>A struct enumerator for the matching archetypes.</returns>
    public List<Archetype<TBits, TRegistry>>.Enumerator GetEnumerator()
        => _matchingArchetypes.GetEnumerator();
}
