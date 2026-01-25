using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A generic query result that iterates over entities and returns typed data instances.
/// This struct is reused across all queryable types, reducing generated code.
/// </summary>
/// <typeparam name="TData">The data type providing component access, must implement IQueryData.</typeparam>
/// <typeparam name="TArchetype">The archetype type implementing IArchetype.</typeparam>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public readonly ref struct QueryResult<TData, TArchetype, TMask, TConfig>
    where TData : IQueryData<TData, TMask, TConfig>, allows ref struct
    where TArchetype : IArchetype<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    private readonly ChunkManager _chunkManager;
    private readonly Query<TMask, TConfig, TArchetype> _query;

    /// <summary>
    /// Creates a new query result.
    /// </summary>
    /// <param name="chunkManager">The chunk manager for memory access.</param>
    /// <param name="query">The underlying query.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public QueryResult(ChunkManager chunkManager, Query<TMask, TConfig, TArchetype> query)
    {
        _chunkManager = chunkManager;
        _query = query;
    }

    /// <summary>
    /// Gets the total number of entities matching this query.
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_chunkManager, _query);

    /// <summary>
    /// Enumerator for iterating over TData instances.
    /// </summary>
    public ref struct Enumerator
    {
        private readonly ChunkManager _chunkManager;
        private Query<TMask, TConfig, TArchetype>.ChunkEnumerator _chunkEnumerator;
        private ImmutableArchetypeLayout<TMask, TConfig> _currentLayout;
        private ChunkHandle _currentChunk;
        private int _indexInChunk;
        private int _entitiesInChunk;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(ChunkManager chunkManager, Query<TMask, TConfig, TArchetype> query)
        {
            _chunkManager = chunkManager;
            _chunkEnumerator = query.Chunks.GetEnumerator();
            _currentLayout = default;
            _currentChunk = default;
            _indexInChunk = -1;
            _entitiesInChunk = 0;
        }

        /// <summary>
        /// Gets the current data instance.
        /// </summary>
        public TData Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => TData.Create(_chunkManager, _currentLayout, _currentChunk, _indexInChunk);
        }

        /// <summary>
        /// Advances to the next entity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _indexInChunk++;
            while (_indexInChunk >= _entitiesInChunk)
            {
                if (!_chunkEnumerator.MoveNext()) return false;
                var info = _chunkEnumerator.Current;
                _currentLayout = info.Archetype.Layout;
                _currentChunk = info.Handle;
                _entitiesInChunk = info.EntityCount;
                _indexInChunk = 0;
            }
            return true;
        }
    }
}

/// <summary>
/// A generic chunk query result that iterates over chunks and returns typed chunk data instances.
/// This struct is reused across all queryable types, reducing generated code.
/// </summary>
/// <typeparam name="TChunkData">The chunk data type providing span access, must implement IQueryChunkData.</typeparam>
/// <typeparam name="TArchetype">The archetype type implementing IArchetype.</typeparam>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public readonly ref struct ChunkQueryResult<TChunkData, TArchetype, TMask, TConfig>
    where TChunkData : IQueryChunkData<TChunkData, TMask, TConfig>, allows ref struct
    where TArchetype : IArchetype<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    private readonly ChunkManager _chunkManager;
    private readonly Query<TMask, TConfig, TArchetype> _query;

    /// <summary>
    /// Creates a new chunk query result.
    /// </summary>
    /// <param name="chunkManager">The chunk manager for memory access.</param>
    /// <param name="query">The underlying query.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunkQueryResult(ChunkManager chunkManager, Query<TMask, TConfig, TArchetype> query)
    {
        _chunkManager = chunkManager;
        _query = query;
    }

    /// <summary>
    /// Gets the total number of entities matching this query.
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
    /// Returns an enumerator that iterates through all chunks in the matching archetypes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_chunkManager, _query);

    /// <summary>
    /// Enumerator for iterating over TChunkData instances.
    /// </summary>
    public ref struct Enumerator
    {
        private readonly ChunkManager _chunkManager;
        private Query<TMask, TConfig, TArchetype>.ChunkEnumerator _chunkEnumerator;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(ChunkManager chunkManager, Query<TMask, TConfig, TArchetype> query)
        {
            _chunkManager = chunkManager;
            _chunkEnumerator = query.Chunks.GetEnumerator();
        }

        /// <summary>
        /// Gets the current chunk data instance.
        /// </summary>
        public TChunkData Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var info = _chunkEnumerator.Current;
                return TChunkData.Create(_chunkManager, info.Archetype.Layout, info.Handle, info.EntityCount);
            }
        }

        /// <summary>
        /// Advances to the next chunk.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => _chunkEnumerator.MoveNext();
    }
}
