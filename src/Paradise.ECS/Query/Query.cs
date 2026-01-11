using System.Collections;

namespace Paradise.ECS;

/// <summary>
/// A query that iterates matching archetypes.
/// Matches archetypes on-demand without caching for simplicity.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
public sealed class Query<TBits, TRegistry>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private readonly ArchetypeRegistry<TBits, TRegistry> _archetypeRegistry;
    private readonly ImmutableQueryDescription<TBits> _description;

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
    /// Gets the matching archetypes by iterating and filtering the registry.
    /// </summary>
    public MatchingArchetypesEnumerable MatchingArchetypes => new(_archetypeRegistry, _description);

    /// <summary>
    /// Gets the total number of entities matching this query across all archetypes.
    /// </summary>
    public int EntityCount
    {
        get
        {
            int count = 0;
            foreach (var archetype in MatchingArchetypes)
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
            foreach (var archetype in MatchingArchetypes)
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
            int count = 0;
            foreach (var _ in MatchingArchetypes)
            {
                count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Enumerable that iterates matching archetypes without allocation.
    /// </summary>
    public readonly struct MatchingArchetypesEnumerable : IEnumerable<ArchetypeStore<TBits, TRegistry>>
    {
        private readonly ArchetypeRegistry<TBits, TRegistry> _registry;
        private readonly ImmutableQueryDescription<TBits> _description;

        internal MatchingArchetypesEnumerable(
            ArchetypeRegistry<TBits, TRegistry> registry,
            ImmutableQueryDescription<TBits> description)
        {
            _registry = registry;
            _description = description;
        }

        public Enumerator GetEnumerator() => new(_registry, _description);

        IEnumerator<ArchetypeStore<TBits, TRegistry>> IEnumerable<ArchetypeStore<TBits, TRegistry>>.GetEnumerator()
            => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Enumerator that filters archetypes by query description.
        /// </summary>
        public struct Enumerator : IEnumerator<ArchetypeStore<TBits, TRegistry>>
        {
            private readonly ArchetypeRegistry<TBits, TRegistry> _registry;
            private readonly ImmutableQueryDescription<TBits> _description;
            private readonly int _count;
            private int _index;
            private ArchetypeStore<TBits, TRegistry>? _current;

            internal Enumerator(
                ArchetypeRegistry<TBits, TRegistry> registry,
                ImmutableQueryDescription<TBits> description)
            {
                _registry = registry;
                _description = description;
                _count = registry.Count;
                _index = -1;
                _current = null;
            }

            public ArchetypeStore<TBits, TRegistry> Current => _current!;

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                while (++_index < _count)
                {
                    var archetype = _registry.GetById(_index);
                    if (archetype != null && _description.Matches(archetype.Layout.ComponentMask))
                    {
                        _current = archetype;
                        return true;
                    }
                }
                return false;
            }

            public void Reset()
            {
                _index = -1;
                _current = null;
            }

            public void Dispose() { }
        }
    }
}
