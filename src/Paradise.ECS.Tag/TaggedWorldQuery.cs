using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A query that filters entities by both component constraints and tag constraints.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
/// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
/// <typeparam name="TTagMask">The tag mask type.</typeparam>
public readonly struct TaggedWorldQuery<TBits, TRegistry, TConfig, TEntityTags, TTagMask>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
    where TConfig : IConfig, new()
    where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    private readonly TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> _taggedWorld;
    private readonly Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>> _query;
    private readonly TTagMask _requiredTags;

    /// <summary>
    /// Creates a new TaggedWorldQuery.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TaggedWorldQuery(
        TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> taggedWorld,
        Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>> query,
        TTagMask requiredTags)
    {
        _taggedWorld = taggedWorld;
        _query = query;
        _requiredTags = requiredTags;
    }

    /// <summary>
    /// Gets the underlying component query.
    /// </summary>
    public Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>> Query
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _query;
    }

    /// <summary>
    /// Gets the required tag mask.
    /// </summary>
    public TTagMask RequiredTags
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _requiredTags;
    }

    /// <summary>
    /// Returns an enumerator that iterates through entities matching both component and tag constraints.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_taggedWorld, _query, _requiredTags);

    /// <summary>
    /// Enumerator for iterating over tagged entities in the query.
    /// </summary>
    public ref struct Enumerator
    {
        private readonly TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> _taggedWorld;
        private readonly TTagMask _requiredTags;
        private Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>>.EntityIdEnumerator _inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(
            TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> taggedWorld,
            Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>> query,
            TTagMask requiredTags)
        {
            _taggedWorld = taggedWorld;
            _requiredTags = requiredTags;
            _inner = query.GetEnumerator();
        }

        /// <summary>
        /// Gets the current WorldEntity.
        /// </summary>
        public WorldEntity<TBits, TRegistry, TConfig> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_taggedWorld.World, _taggedWorld.World.GetEntity(_inner.Current));
        }

        /// <summary>
        /// Advances to the next entity that matches the tag constraints.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (_inner.MoveNext())
            {
                var entity = _taggedWorld.World.GetEntity(_inner.Current);
                var entityTags = _taggedWorld.GetTags(entity);
                if (entityTags.ContainsAll(_requiredTags))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
