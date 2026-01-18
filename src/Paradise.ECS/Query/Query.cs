using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A wrapper combining a World and an Entity, providing convenient component access APIs.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public readonly struct WorldEntity<TBits, TRegistry, TConfig>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
    where TConfig : IConfig, new()
{
    private readonly World<TBits, TRegistry, TConfig> _world;
    private readonly Entity _entity;

    /// <summary>
    /// Creates a new WorldEntity wrapper.
    /// </summary>
    /// <param name="world">The world containing the entity.</param>
    /// <param name="entity">The entity handle.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal WorldEntity(World<TBits, TRegistry, TConfig> world, Entity entity)
    {
        _world = world;
        _entity = entity;
    }

    /// <summary>
    /// Gets the underlying entity handle.
    /// </summary>
    public Entity Entity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _entity;
    }

    /// <summary>
    /// Gets the world containing this entity.
    /// </summary>
    public World<TBits, TRegistry, TConfig> World
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _world;
    }

    /// <summary>
    /// Gets a reference to a component on this entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>A reference to the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get<T>() where T : unmanaged, IComponent
        => ref _world.GetComponentRef<T>(_entity);

    /// <summary>
    /// Checks if this entity has a specific component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>True if the entity has the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has<T>() where T : unmanaged, IComponent
        => _world.HasComponent<T>(_entity);

    /// <summary>
    /// Checks if this entity is currently alive.
    /// </summary>
    /// <returns>True if the entity is alive.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAlive()
        => _world.IsAlive(_entity);

    /// <summary>
    /// Implicit conversion to Entity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Entity(WorldEntity<TBits, TRegistry, TConfig> worldEntity)
        => worldEntity._entity;
}

/// <summary>
/// A wrapper combining a World and a Query, providing WorldEntity enumeration.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public readonly struct WorldQuery<TBits, TRegistry, TConfig>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
    where TConfig : IConfig, new()
{
    private readonly World<TBits, TRegistry, TConfig> _world;
    private readonly Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>> _query;

    /// <summary>
    /// Creates a new WorldQuery wrapper.
    /// </summary>
    /// <param name="world">The world containing the entities.</param>
    /// <param name="query">The query to iterate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal WorldQuery(
        World<TBits, TRegistry, TConfig> world,
        Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>> query)
    {
        _world = world;
        _query = query;
    }

    /// <summary>
    /// Gets the underlying query for accessing archetypes and chunks directly.
    /// </summary>
    public Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>> Query
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _query;
    }

    /// <summary>
    /// Gets the total number of entities matching this query across all archetypes.
    /// </summary>
    public int EntityCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _query.EntityCount;
    }

    /// <summary>
    /// Gets whether this query has any matching entities.
    /// </summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _query.IsEmpty;
    }

    /// <summary>
    /// Returns an enumerator that iterates through all entities in the matching archetypes.
    /// </summary>
    /// <returns>A struct enumerator for WorldEntity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_world, _query);

    /// <summary>
    /// Enumerator for iterating over WorldEntity in the query.
    /// </summary>
    public ref struct Enumerator
    {
        private readonly World<TBits, TRegistry, TConfig> _world;
        private Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>>.EntityIdEnumerator _inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(
            World<TBits, TRegistry, TConfig> world,
            Query<TBits, TRegistry, TConfig, Archetype<TBits, TRegistry, TConfig>> query)
        {
            _world = world;
            _inner = query.GetEnumerator();
        }

        /// <summary>
        /// Gets the current WorldEntity.
        /// </summary>
        public WorldEntity<TBits, TRegistry, TConfig> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(_world, _world.GetEntity(_inner.Current));
        }

        /// <summary>
        /// Advances to the next entity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => _inner.MoveNext();
    }
}

/// <summary>
/// A lightweight, read-only view over a collection of archetypes that match specific component criteria.
/// The underlying list of archetypes is managed by the <see cref="ArchetypeRegistry{TBits, TRegistry, TConfig}"/>
/// and is updated automatically as new matching archetypes are created.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
/// <typeparam name="TArchetype">The archetype type.</typeparam>
public readonly struct Query<TBits, TRegistry, TConfig, TArchetype>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
    where TConfig : IConfig, new()
    where TArchetype : IArchetype<TBits, TRegistry, TConfig>
{
    private readonly List<TArchetype> _matchingArchetypes;

    /// <summary>
    /// Creates a new query wrapping the specified archetype list.
    /// </summary>
    /// <param name="matchingArchetypes">The list of matching archetypes, owned by the registry.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Query(List<TArchetype> matchingArchetypes)
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
    public int ArchetypeCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _matchingArchetypes.Count;
    }

    /// <summary>
    /// Returns an enumerator that iterates through all entity IDs in the matching archetypes.
    /// </summary>
    /// <returns>A struct enumerator for entity IDs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityIdEnumerator GetEnumerator() => new(_matchingArchetypes);

    /// <summary>
    /// Gets an enumerable for iterating over all matching archetypes.
    /// </summary>
    public ArchetypeEnumerable Archetypes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_matchingArchetypes);
    }

    /// <summary>
    /// Gets an enumerable for iterating over all chunks in the matching archetypes.
    /// </summary>
    public ChunkEnumerable Chunks
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_matchingArchetypes);
    }

    /// <summary>
    /// Represents a chunk with its associated archetype and entity count.
    /// </summary>
    public readonly struct ChunkInfo
    {
        /// <summary>
        /// The archetype this chunk belongs to.
        /// </summary>
        public TArchetype Archetype
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            init;
        }

        /// <summary>
        /// The chunk handle for memory access.
        /// </summary>
        public ChunkHandle Handle
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            init;
        }

        /// <summary>
        /// The number of entities in this chunk.
        /// </summary>
        public int EntityCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            init;
        }
    }

    /// <summary>
    /// Enumerable for iterating over chunks in the query.
    /// </summary>
    public readonly ref struct ChunkEnumerable
    {
        private readonly List<TArchetype> _archetypes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ChunkEnumerable(List<TArchetype> archetypes)
        {
            _archetypes = archetypes;
        }

        /// <summary>
        /// Returns an enumerator for iterating over chunks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChunkEnumerator GetEnumerator() => new ChunkEnumerator(_archetypes);
    }

    /// <summary>
    /// Enumerator for iterating over chunks in the query.
    /// </summary>
    public ref struct ChunkEnumerator
    {
        private readonly List<TArchetype> _archetypes;
        private int _archetypeIndex;
        private int _chunkIndex;
        private ChunkInfo _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ChunkEnumerator(List<TArchetype> archetypes)
        {
            _archetypes = archetypes;
            _archetypeIndex = 0;
            _chunkIndex = -1;
            _current = default;
        }

        /// <summary>
        /// Gets the current chunk info.
        /// </summary>
        public ChunkInfo Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }

        /// <summary>
        /// Advances to the next chunk.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (_archetypeIndex < _archetypes.Count)
            {
                var archetype = _archetypes[_archetypeIndex];
                _chunkIndex++;

                if (_chunkIndex < archetype.ChunkCount)
                {
                    int entitiesPerChunk = archetype.Layout.EntitiesPerChunk;
                    int totalEntities = archetype.EntityCount;
                    int entitiesBeforeThisChunk = _chunkIndex * entitiesPerChunk;
                    int entitiesInThisChunk = Math.Min(entitiesPerChunk, totalEntities - entitiesBeforeThisChunk);

                    _current = new ChunkInfo
                    {
                        Archetype = archetype,
                        Handle = archetype.GetChunk(_chunkIndex),
                        EntityCount = entitiesInThisChunk
                    };
                    return true;
                }

                // Move to next archetype
                _archetypeIndex++;
                _chunkIndex = -1;
            }

            return false;
        }
    }

    /// <summary>
    /// Enumerable for iterating over archetypes in the query.
    /// </summary>
    public readonly ref struct ArchetypeEnumerable
    {
        private readonly List<TArchetype> _archetypes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ArchetypeEnumerable(List<TArchetype> archetypes)
        {
            _archetypes = archetypes;
        }

        /// <summary>
        /// Returns an enumerator for iterating over archetypes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<TArchetype>.Enumerator GetEnumerator() => _archetypes.GetEnumerator();
    }

    /// <summary>
    /// Enumerator for iterating over entity IDs in the query.
    /// </summary>
    public ref struct EntityIdEnumerator
    {
        private readonly List<TArchetype> _archetypes;
        private int _archetypeIndex;
        private int _chunkIndex;
        private int _indexInChunk;
        private int _entitiesInCurrentChunk;
        private TArchetype _currentArchetype;
        private ChunkHandle _currentChunk;
        private int _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal EntityIdEnumerator(List<TArchetype> archetypes)
        {
            _archetypes = archetypes;
            _archetypeIndex = 0;
            _chunkIndex = 0;
            _indexInChunk = -1;
            _entitiesInCurrentChunk = 0;
            _currentArchetype = default!;
            _currentChunk = default;
            _current = 0;

            // Initialize to first valid chunk
            InitializeCurrentChunk();
        }

        /// <summary>
        /// Gets the current entity ID.
        /// </summary>
        public int Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }

        /// <summary>
        /// Advances to the next entity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _indexInChunk++;

            while (true)
            {
                if (_indexInChunk < _entitiesInCurrentChunk)
                {
                    _current = _currentArchetype.GetEntityId(_currentChunk, _indexInChunk);
                    return true;
                }

                // Move to next chunk
                _chunkIndex++;
                _indexInChunk = 0;

                if (_archetypeIndex >= _archetypes.Count)
                    return false;

                if (_chunkIndex >= _currentArchetype.ChunkCount)
                {
                    // Move to next archetype
                    _archetypeIndex++;
                    _chunkIndex = 0;

                    if (!InitializeCurrentChunk())
                        return false;
                }
                else
                {
                    UpdateCurrentChunk();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InitializeCurrentChunk()
        {
            while (_archetypeIndex < _archetypes.Count)
            {
                _currentArchetype = _archetypes[_archetypeIndex];
                if (_currentArchetype.ChunkCount > 0 && _currentArchetype.EntityCount > 0)
                {
                    _chunkIndex = 0;
                    UpdateCurrentChunk();
                    return true;
                }
                _archetypeIndex++;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCurrentChunk()
        {
            _currentChunk = _currentArchetype.GetChunk(_chunkIndex);
            int entitiesPerChunk = _currentArchetype.Layout.EntitiesPerChunk;
            int totalEntities = _currentArchetype.EntityCount;
            int entitiesBeforeThisChunk = _chunkIndex * entitiesPerChunk;
            _entitiesInCurrentChunk = Math.Min(entitiesPerChunk, totalEntities - entitiesBeforeThisChunk);
        }
    }
}
