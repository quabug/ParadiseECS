using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Thread-safe manager for entity lifecycle.
/// Handles entity creation, destruction, and validation using version-based handles.
/// Uses lock-free CAS operations for thread safety.
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

    private const int InitialCapacity = 1024;
    private const int MaxEntities = int.MaxValue;

    private EntityMeta[] _entities;
    private readonly ConcurrentStack<int> _freeSlots = new();
    private int _nextEntityId; // Next fresh entity ID to allocate (atomic)
    private int _disposed; // 0 = not disposed, 1 = disposed
    private int _aliveCount; // Number of currently alive entities (atomic)

    /// <summary>
    /// Creates a new EntityManager with the default initial capacity.
    /// </summary>
    public EntityManager()
        : this(InitialCapacity)
    {
    }

    /// <summary>
    /// Creates a new EntityManager with a custom initial capacity.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity for entity storage.</param>
    public EntityManager(int initialCapacity)
    {
        if (initialCapacity <= 0)
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(initialCapacity));

        _entities = new EntityMeta[initialCapacity];
    }

    /// <summary>
    /// Gets the number of currently alive entities.
    /// </summary>
    public int AliveCount => Volatile.Read(ref _aliveCount);

    /// <summary>
    /// Gets the total capacity of the entity storage (including free slots).
    /// </summary>
    public int Capacity => _entities.Length;

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

        if (id >= MaxEntities)
            ThrowCapacityExceeded();

        // Ensure capacity (thread-safe resize)
        EnsureCapacity(id);

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
        return ref _entities[id];
    }

    /// <summary>
    /// Ensures the entity array has capacity for the given id.
    /// Uses lock-free resizing with CAS.
    /// </summary>
    private void EnsureCapacity(int id)
    {
        var currentArray = Volatile.Read(ref _entities);
        if (id < currentArray.Length)
            return;

        // Calculate new capacity (double until sufficient)
        int newCapacity = currentArray.Length;
        while (id >= newCapacity)
        {
            newCapacity *= 2;
            if (newCapacity < 0) // Overflow
                newCapacity = MaxEntities;
        }

        // Lock to prevent multiple threads resizing simultaneously
        lock (_freeSlots)
        {
            // Double-check after acquiring lock
            if (id < _entities.Length)
                return;

            var newArray = new EntityMeta[newCapacity];
            Array.Copy(_entities, newArray, _entities.Length);
            Volatile.Write(ref _entities, newArray);
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
        => throw new InvalidOperationException($"EntityManager capacity exceeded (max {MaxEntities} entities)");

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        _entities = Array.Empty<EntityMeta>();
        _freeSlots.Clear();
        _nextEntityId = 0;
        _aliveCount = 0;
    }
}
