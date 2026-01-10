using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Thread-safe manager for entity lifecycle.
/// Handles entity creation, destruction, and validation using version-based handles.
/// Uses chunk-based storage for entity metadata and lock-free CAS operations for thread safety.
/// </summary>
public sealed class EntityManager : IDisposable
{
    /// <summary>
    /// Metadata for a single entity slot.
    /// </summary>
    private struct EntityMeta
    {
        public int Version; // Version counter for stale handle detection
    }

    private const int MetaSize = 4; // sizeof(EntityMeta): sizeof(int)
    private const int EntriesPerChunk = Chunk.ChunkSize / MetaSize; // 4096 entries per 16KB chunk
    private const int EntriesPerChunkShift = 12; // log2(4096)
    private const int EntriesPerChunkMask = EntriesPerChunk - 1; // 0xFFF
    private const int MaxChunks = 524288; // ~2B entities max (4096 * 524288)
    private const int InitialChunks = 1; // Start with 1 chunk (4096 entities)

    private readonly ChunkManager _chunkManager;
    private readonly bool _ownsChunkManager;
    private readonly ChunkHandle[] _metaChunks = new ChunkHandle[MaxChunks];
    private readonly ConcurrentStack<int> _freeSlots = new();
    private int _nextEntityId; // Next fresh entity ID to allocate (atomic)
    private int _disposed; // 0 = not disposed, 1 = disposed
    private int _aliveCount; // Number of currently alive entities (atomic)
    private int _allocatedChunks; // Number of chunks currently allocated

    /// <summary>
    /// Creates a new EntityManager with a default ChunkManager.
    /// </summary>
    public EntityManager()
        : this(new ChunkManager(), ownsChunkManager: true)
    {
    }

    /// <summary>
    /// Creates a new EntityManager with a shared ChunkManager.
    /// </summary>
    /// <param name="chunkManager">The ChunkManager to use for chunk allocation.</param>
    public EntityManager(ChunkManager chunkManager)
        : this(chunkManager, ownsChunkManager: false)
    {
    }

    /// <summary>
    /// Creates a new EntityManager with a ChunkManager.
    /// </summary>
    /// <param name="chunkManager">The ChunkManager to use for chunk allocation.</param>
    /// <param name="ownsChunkManager">Whether this EntityManager owns and should dispose the ChunkManager.</param>
    private EntityManager(ChunkManager chunkManager, bool ownsChunkManager)
    {
        _chunkManager = chunkManager ?? throw new ArgumentNullException(nameof(chunkManager));
        _ownsChunkManager = ownsChunkManager;

        // Allocate initial chunk
        for (int i = 0; i < InitialChunks; i++)
        {
            _metaChunks[i] = _chunkManager.Allocate();
            _allocatedChunks++;
        }
    }

    /// <summary>
    /// Gets the number of currently alive entities.
    /// </summary>
    public int AliveCount => Volatile.Read(ref _aliveCount);

    /// <summary>
    /// Gets the total capacity of the entity storage (including free slots).
    /// </summary>
    public int Capacity => Volatile.Read(ref _allocatedChunks) * EntriesPerChunk;

    /// <summary>
    /// Creates a new entity and returns a handle to it.
    /// Uses lock-free operations for thread safety.
    /// </summary>
    /// <returns>A valid entity handle.</returns>
    public Entity Create()
    {
        ThrowIfDisposed();

        int id;
        if (_freeSlots.TryPop(out id))
        {
            // Reuse a freed entity slot
            ref var meta = ref GetMeta(id);
            int version = Volatile.Read(ref meta.Version);
            Interlocked.Increment(ref _aliveCount);
            return new Entity(id, version);
        }

        // Allocate a new entity ID
        id = Interlocked.Increment(ref _nextEntityId) - 1;

        if (id >= MaxChunks * EntriesPerChunk)
            ThrowCapacityExceeded();

        // Ensure capacity (thread-safe chunk allocation)
        EnsureChunkAllocated(id);

        ref var newMeta = ref GetMeta(id);
        int newVersion = Volatile.Read(ref newMeta.Version);
        Interlocked.Increment(ref _aliveCount);
        return new Entity(id, newVersion);
    }

    /// <summary>
    /// Destroys the entity associated with the handle.
    /// Increments the version to invalidate the handle and returns the ID to the free pool.
    /// Safe to call multiple times or with invalid/stale handles (no-op).
    /// </summary>
    /// <param name="entity">The entity to destroy.</param>
    public void Destroy(Entity entity)
    {
        if (!entity.IsValid)
            return;

        if (entity.Id >= Volatile.Read(ref _nextEntityId))
            return;

        ThrowIfDisposed();

        ref var meta = ref GetMeta(entity.Id);

        // Atomically check version and increment it
        while (true)
        {
            int currentVersion = Volatile.Read(ref meta.Version);

            // Check if already destroyed (stale handle)
            if (currentVersion != entity.Version)
                return;

            // Increment version to invalidate all existing handles
            int nextVersion = currentVersion + 1;
            if (Interlocked.CompareExchange(ref meta.Version, nextVersion, currentVersion) == currentVersion)
            {
                // Successfully destroyed - add to free list
                _freeSlots.Push(entity.Id);
                Interlocked.Decrement(ref _aliveCount);
                return;
            }

            // CAS failed - another thread modified it, retry
        }
    }

    /// <summary>
    /// Checks if the entity is currently alive.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <returns>True if the entity is alive, false if destroyed or invalid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAlive(Entity entity)
    {
        if (!entity.IsValid)
            return false;

        if (entity.Id >= Volatile.Read(ref _nextEntityId))
            return false;

        ref var meta = ref GetMeta(entity.Id);
        return Volatile.Read(ref meta.Version) == entity.Version;
    }

    /// <summary>
    /// Gets a reference to the metadata for a given entity id.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref EntityMeta GetMeta(int id)
    {
        int chunkIndex = id >> EntriesPerChunkShift;
        int indexInChunk = id & EntriesPerChunkMask;

        var handle = _metaChunks[chunkIndex];
        var chunk = _chunkManager.Get(handle);
        ref var result = ref chunk.GetSpan<EntityMeta>(0, EntriesPerChunk)[indexInChunk];
        chunk.Dispose();
        return ref result;
    }

    /// <summary>
    /// Ensures the chunk for the given entity id is allocated.
    /// Uses lock-free allocation with CAS.
    /// </summary>
    private void EnsureChunkAllocated(int id)
    {
        int chunkIndex = id >> EntriesPerChunkShift;

        // Fast path - chunk already allocated
        var handle = Volatile.Read(ref _metaChunks[chunkIndex]);
        if (handle.IsValid)
            return;

        // Slow path - need to allocate chunk
        lock (_freeSlots)
        {
            // Double-check after acquiring lock
            handle = _metaChunks[chunkIndex];
            if (handle.IsValid)
                return;

            // Allocate new chunk
            _metaChunks[chunkIndex] = _chunkManager.Allocate();
            Interlocked.Increment(ref _allocatedChunks);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            ThrowHelper.ThrowObjectDisposedException(nameof(EntityManager));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCapacityExceeded()
        => throw new InvalidOperationException($"EntityManager capacity exceeded (max {MaxChunks * EntriesPerChunk} entities)");

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Free all allocated chunks
        int chunksToFree = _allocatedChunks;
        for (int i = 0; i < chunksToFree; i++)
        {
            var handle = _metaChunks[i];
            if (handle.IsValid)
            {
                _chunkManager.Free(handle);
                _metaChunks[i] = ChunkHandle.Invalid;
            }
        }

        // Dispose ChunkManager if we own it
        if (_ownsChunkManager)
        {
            _chunkManager.Dispose();
        }

        _freeSlots.Clear();
        _nextEntityId = 0;
        _aliveCount = 0;
        _allocatedChunks = 0;
    }
}
