using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Thread-safe manager for entity lifecycle.
/// Handles entity creation, destruction, and validation using version-based handles.
/// Uses a contiguous array for entity metadata and lock-free CAS operations for thread safety.
/// </summary>
public sealed class EntityManager : IDisposable
{
    /// <summary>
    /// Metadata for a single entity slot.
    /// </summary>
    private struct EntityMeta
    {
        public uint Version; // Version counter for stale handle detection
    }

    private const int DefaultInitialCapacity = 1024;

    private EntityMeta[] _metas;
    private readonly ConcurrentStack<int> _freeSlots = new();
    private readonly Lock _growLock = new();
    private int _nextEntityId = 1; // Next fresh entity ID to allocate (atomic), starts at 1 (0 is reserved)
    private int _disposed; // 0 = not disposed, 1 = disposed
    private int _aliveCount; // Number of currently alive entities (atomic)

    /// <summary>
    /// Creates a new EntityManager.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity for entity storage. Default is 1024.</param>
    public EntityManager(int initialCapacity = DefaultInitialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialCapacity, 0);
        _metas = new EntityMeta[initialCapacity];
    }

    /// <summary>
    /// Gets the number of currently alive entities.
    /// </summary>
    public int AliveCount => Volatile.Read(ref _aliveCount);

    /// <summary>
    /// Gets the current capacity of the entity storage.
    /// </summary>
    public int Capacity => Volatile.Read(ref _metas).Length;

    /// <summary>
    /// Creates a new entity and returns a handle to it.
    /// Uses lock-free operations for thread safety.
    /// </summary>
    /// <returns>A valid entity handle.</returns>
    public Entity Create()
    {
        ThrowIfDisposed();

        if (_freeSlots.TryPop(out int id))
        {
            // Reuse a freed entity slot
            var metas = Volatile.Read(ref _metas);
            ref var meta = ref metas[id];
            uint version = Volatile.Read(ref meta.Version);
            Interlocked.Increment(ref _aliveCount);
            return new Entity(id, version);
        }

        // Allocate a new entity ID
        id = Interlocked.Increment(ref _nextEntityId) - 1;

        // Ensure capacity (thread-safe array growth)
        EnsureCapacity(id);

        var metasAfterGrow = Volatile.Read(ref _metas);
        ref var newMeta = ref metasAfterGrow[id];
        // Initialize version to 1 for new entities (0 is reserved for invalid state)
        Volatile.Write(ref newMeta.Version, 1);
        Interlocked.Increment(ref _aliveCount);
        return new Entity(id, 1);
    }

    /// <summary>
    /// Destroys the entity associated with the handle.
    /// Increments the version to invalidate the handle and returns the ID to the free pool.
    /// Safe to call multiple times or with invalid/stale handles (no-op).
    /// </summary>
    /// <param name="entity">The entity to destroy.</param>
    public void Destroy(Entity entity)
    {
        ThrowIfDisposed();

        if (!entity.IsValid)
            return;

        if (entity.Id >= Volatile.Read(ref _nextEntityId))
            return;

        var metas = Volatile.Read(ref _metas);
        ref var meta = ref metas[entity.Id];

        // Atomically check version and increment it
        while (true)
        {
            uint currentVersion = Volatile.Read(ref meta.Version);

            // Check if already destroyed (stale handle)
            if (currentVersion != entity.Version)
                return;

            // Increment version to invalidate all existing handles
            uint nextVersion = currentVersion + 1;
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

        var metas = Volatile.Read(ref _metas);
        ref var meta = ref metas[entity.Id];
        return Volatile.Read(ref meta.Version) == entity.Version;
    }

    /// <summary>
    /// Ensures the array has capacity for the given entity id.
    /// Uses lock for thread-safe array growth.
    /// </summary>
    private void EnsureCapacity(int id)
    {
        var metas = Volatile.Read(ref _metas);
        if (id < metas.Length)
            return;

        lock (_growLock)
        {
            metas = _metas;
            if (id < metas.Length)
                return;

            // Grow by doubling, ensuring we have room for the new id
            int newCapacity = Math.Max(metas.Length * 2, id + 1);
            var newMetas = new EntityMeta[newCapacity];
            Array.Copy(metas, newMetas, metas.Length);
            Volatile.Write(ref _metas, newMetas);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ThrowHelper.ThrowIfDisposed(Volatile.Read(ref _disposed) != 0, this);
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        _freeSlots.Clear();
        _metas = [];
        _nextEntityId = 0;
        _aliveCount = 0;
    }
}
