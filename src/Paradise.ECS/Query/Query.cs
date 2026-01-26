using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A lightweight, read-only view over a collection of archetypes that match specific component criteria.
/// The underlying list of archetypes is managed by the <see cref="ArchetypeRegistry{TMask, TConfig}"/>
/// and is updated automatically as new matching archetypes are created.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
/// <typeparam name="TArchetype">The archetype type.</typeparam>
public readonly struct Query<TMask, TConfig, TArchetype>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
    where TArchetype : IArchetype<TMask, TConfig>
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
    /// Gets the list of all matching archetypes.
    /// </summary>
    public IReadOnlyList<TArchetype> Archetypes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _matchingArchetypes;
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
