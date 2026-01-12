namespace Paradise.ECS;

/// <summary>
/// A lightweight view over matching archetypes.
/// The underlying list is owned and updated by the archetype registry.
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
    public bool IsEmpty => EntityCount == 0;

    /// <summary>
    /// Gets the number of matching archetypes.
    /// </summary>
    public int ArchetypeCount => _matchingArchetypes.Count;
}
