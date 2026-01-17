using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Manager for entity lifecycle.
/// Handles entity creation, destruction, and validation using version-based handles.
/// Uses a contiguous array for entity metadata.
/// Single-threaded version without concurrent access support.
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
    private readonly Stack<int> _freeSlots = new();
    private int _nextEntityId; // Next fresh entity ID to allocate
    private bool _disposed;
    private int _aliveCount; // Number of currently alive entities

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
    public int AliveCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _aliveCount;
    }

    /// <summary>
    /// Gets the current capacity of the entity storage.
    /// </summary>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _metas.Length;
    }

    /// <summary>
    /// Creates a new entity and returns a handle to it.
    /// </summary>
    /// <returns>A valid entity handle.</returns>
    public Entity Create()
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);

        if (_freeSlots.TryPop(out int id))
        {
            // Reuse a freed entity slot
            ref var meta = ref _metas[id];
            uint version = meta.Version;
            _aliveCount++;
            return new Entity(id, version);
        }

        // Allocate a new entity ID
        id = _nextEntityId++;

        // Ensure capacity
        EnsureCapacity(id);

        // Initialize version
        ref var newMeta = ref _metas[id];
        newMeta.Version = 1;

        _aliveCount++;
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
        if (!entity.IsValid)
            return;

        if (_disposed) return;

        if (entity.Id >= _nextEntityId)
            return;

        ref var meta = ref _metas[entity.Id];

        // Check if already destroyed (stale handle)
        if (meta.Version != entity.Version)
            return;

        // Increment version to invalidate all existing handles
        meta.Version++;

        // Add to free list
        _freeSlots.Push(entity.Id);
        _aliveCount--;
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

        ThrowHelper.ThrowIfDisposed(_disposed, this);

        if (entity.Id >= _nextEntityId)
            return false;

        ref var meta = ref _metas[entity.Id];
        return meta.Version == entity.Version;
    }

    /// <summary>
    /// Ensures the array has capacity for the given entity id.
    /// </summary>
    private void EnsureCapacity(int id)
    {
        if (id < _metas.Length)
            return;

        // Grow by doubling, ensuring we have room for the new id
        int newCapacity = Math.Max(_metas.Length * 2, id + 1);
        var newMetas = new EntityMeta[newCapacity];
        Array.Copy(_metas, newMetas, _metas.Length);
        _metas = newMetas;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _freeSlots.Clear();
        _metas = [];
        _nextEntityId = 0;
        _aliveCount = 0;
    }
}
