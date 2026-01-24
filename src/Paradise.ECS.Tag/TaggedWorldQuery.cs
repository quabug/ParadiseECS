using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A query that filters entities by both component constraints and tag constraints.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
/// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
/// <typeparam name="TTagMask">The tag mask type.</typeparam>
public readonly struct TaggedWorldQuery<TMask, TConfig, TEntityTags, TTagMask>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
    where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    private readonly TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> _taggedWorld;
    private readonly Query<TMask, TConfig, Archetype<TMask, TConfig>> _query;
    private readonly TTagMask _requiredTags;

    /// <summary>
    /// Creates a new TaggedWorldQuery.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TaggedWorldQuery(
        TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> taggedWorld,
        Query<TMask, TConfig, Archetype<TMask, TConfig>> query,
        TTagMask requiredTags)
    {
        _taggedWorld = taggedWorld;
        _query = query;
        _requiredTags = requiredTags;
    }

    /// <summary>
    /// Gets the underlying component query.
    /// </summary>
    public Query<TMask, TConfig, Archetype<TMask, TConfig>> Query
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
        private readonly TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> _taggedWorld;
        private readonly TTagMask _requiredTags;
        private Query<TMask, TConfig, Archetype<TMask, TConfig>>.EntityIdEnumerator _inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(
            TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> taggedWorld,
            Query<TMask, TConfig, Archetype<TMask, TConfig>> query,
            TTagMask requiredTags)
        {
            _taggedWorld = taggedWorld;
            _requiredTags = requiredTags;
            _inner = query.GetEnumerator();
        }

        /// <summary>
        /// Gets the current WorldEntity.
        /// </summary>
        public WorldEntity<TMask, TConfig> Current
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
