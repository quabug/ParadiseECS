namespace Paradise.ECS;

/// <summary>
/// A query that iterates matching archetypes.
/// Cache is automatically updated by the archetype registry when new archetypes are created.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
public sealed class Query<TBits, TRegistry>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private readonly ImmutableQueryDescription<TBits> _description;
    private readonly List<Archetype<TBits, TRegistry>> _matchingArchetypes = new(32);

    /// <summary>
    /// Creates a new query with the specified description.
    /// </summary>
    /// <param name="archetypeRegistry">The archetype registry to query.</param>
    /// <param name="description">The query description defining matching criteria.</param>
    internal Query(ArchetypeRegistry<TBits, TRegistry> archetypeRegistry, ImmutableQueryDescription<TBits> description)
    {
        ArgumentNullException.ThrowIfNull(archetypeRegistry);

        _description = description;
        archetypeRegistry.RegisterQuery(this);
    }

    /// <summary>
    /// Gets the query description defining matching criteria.
    /// </summary>
    internal ImmutableQueryDescription<TBits> Description => _description;

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

    /// <summary>
    /// Adds an archetype to the matching list.
    /// Called by the archetype registry when a matching archetype is created.
    /// </summary>
    /// <param name="archetype">The archetype to add.</param>
    internal void AddMatchingArchetype(Archetype<TBits, TRegistry> archetype)
    {
        _matchingArchetypes.Add(archetype);
    }
}
