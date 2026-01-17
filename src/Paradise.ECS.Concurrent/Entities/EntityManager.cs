using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Paradise.ECS.Concurrent;

/// <summary>
/// Thread-safe manager for entity lifecycle and location tracking.
/// Handles entity creation, destruction, validation, and archetype location using version-based handles.
/// Uses a contiguous array for entity metadata indexed by Entity.Id for O(1) lookups.
/// Array growth uses a lock to prevent wasted allocations.
/// </summary>
public sealed class EntityManager : IDisposable
{
    private EntityLocation[] _locations;
    private readonly ConcurrentStack<int> _freeSlots = new();
    private readonly Lock _growLock = new();
    private readonly OperationGuard _operationGuard = new();
    private int _nextEntityId; // Next fresh entity ID to allocate (atomic)
    private int _disposed; // 0 = not disposed, 1 = disposed
    private int _aliveCount; // Number of currently alive entities (atomic)

    /// <summary>
    /// Creates a new EntityManager.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity for entity storage.</param>
    public EntityManager(int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialCapacity, 0);
        _locations = new EntityLocation[initialCapacity];
    }

    /// <summary>
    /// Gets the number of currently alive entities.
    /// </summary>
    public int AliveCount => Volatile.Read(ref _aliveCount);

    /// <summary>
    /// Gets the current capacity of the entity storage.
    /// </summary>
    public int Capacity => Volatile.Read(ref _locations).Length;

    /// <summary>
    /// Returns the ID that would be assigned to the next created entity,
    /// without actually creating it. Used for validation before creation.
    /// Note: In concurrent scenarios, another thread may allocate this ID
    /// between peeking and creating. This is acceptable for validation
    /// since the limit check is conservative.
    /// </summary>
    /// <returns>The next entity ID that would be allocated.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PeekNextId()
    {
        // If there's a free slot, that ID will be reused
        if (_freeSlots.TryPeek(out int id))
            return id;

        // Otherwise, a new ID will be allocated
        return Volatile.Read(ref _nextEntityId);
    }

    /// <summary>
    /// Creates a new entity and returns a handle to it.
    /// The entity has no archetype until components are added.
    /// Uses lock-free operations for thread safety.
    /// </summary>
    /// <returns>A valid entity handle.</returns>
    public Entity Create()
    {
        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        if (_freeSlots.TryPop(out int id))
        {
            // Reuse a freed entity slot - version was already incremented on destroy
            var locations = Volatile.Read(ref _locations);
            ref var location = ref locations[id];
            uint version = Volatile.Read(ref location.Version);
            // Reset archetype info (already -1 from destroy, but ensure consistency)
            Volatile.Write(ref location.ArchetypeId, -1);
            Volatile.Write(ref location.GlobalIndex, -1);
            Interlocked.Increment(ref _aliveCount);
            return new Entity(id, version);
        }

        // Allocate a new entity ID
        id = Interlocked.Increment(ref _nextEntityId) - 1;

        // Ensure capacity (thread-safe array growth)
        EnsureCapacity(id);

        // Initialize location with retry in case array grows while we're writing.
        // If another thread grows the array after we read _locations but before we write,
        // our write goes to a stale array. Retry ensures we write to the current array.
        while (true)
        {
            var locations = Volatile.Read(ref _locations);
            ref var location = ref locations[id];
            Volatile.Write(ref location.Version, 1);
            Volatile.Write(ref location.ArchetypeId, -1);
            Volatile.Write(ref location.GlobalIndex, -1);

            // Check if array was replaced while we were writing
            if (Volatile.Read(ref _locations) == locations)
                break;

            // Array was replaced, retry write to new array
        }

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
        if (!entity.IsValid)
            return;

        using var _ = _operationGuard.EnterScope();
        if (_disposed != 0) return;

        if (entity.Id >= Volatile.Read(ref _nextEntityId))
            return;

        var locations = Volatile.Read(ref _locations);
        ref var location = ref locations[entity.Id];

        // Atomically check version and increment it
        while (true)
        {
            uint currentVersion = Volatile.Read(ref location.Version);

            // Check if already destroyed (stale handle)
            if (currentVersion != entity.Version)
                return;

            // Increment version to invalidate all existing handles
            uint nextVersion = currentVersion + 1;
            if (Interlocked.CompareExchange(ref location.Version, nextVersion, currentVersion) == currentVersion)
            {
                // Successfully destroyed - clear archetype info and add to free list
                Volatile.Write(ref location.ArchetypeId, -1);
                Volatile.Write(ref location.GlobalIndex, -1);
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

        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        if (entity.Id >= Volatile.Read(ref _nextEntityId))
            return false;

        var locations = Volatile.Read(ref _locations);
        ref var location = ref locations[entity.Id];
        return Volatile.Read(ref location.Version) == entity.Version;
    }

    /// <summary>
    /// Gets a reference to the location data for the specified entity.
    /// The caller must validate the entity is alive before calling this method.
    /// Note: The returned reference may become stale if the array grows.
    /// </summary>
    /// <param name="entity">The entity to get location for.</param>
    /// <returns>A reference to the entity's location data.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the manager is disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref EntityLocation GetLocationRef(Entity entity)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        var locations = Volatile.Read(ref _locations);
        return ref locations[entity.Id];
    }

    /// <summary>
    /// Gets the location data for the specified entity if it is alive.
    /// </summary>
    /// <param name="entity">The entity to get location for.</param>
    /// <param name="location">The location data if the entity is alive.</param>
    /// <returns>True if the entity is alive and location was retrieved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetLocation(Entity entity, out EntityLocation location)
    {
        if (!entity.IsValid)
        {
            location = EntityLocation.Invalid;
            return false;
        }

        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        if (entity.Id >= Volatile.Read(ref _nextEntityId))
        {
            location = EntityLocation.Invalid;
            return false;
        }

        var locations = Volatile.Read(ref _locations);
        ref var loc = ref locations[entity.Id];
        if (Volatile.Read(ref loc.Version) != entity.Version)
        {
            location = EntityLocation.Invalid;
            return false;
        }

        // Read all fields with volatile semantics
        location = new EntityLocation
        {
            Version = Volatile.Read(ref loc.Version),
            ArchetypeId = Volatile.Read(ref loc.ArchetypeId),
            GlobalIndex = Volatile.Read(ref loc.GlobalIndex)
        };
        return true;
    }

    /// <summary>
    /// Ensures the array has capacity for the given entity id.
    /// Uses lock for thread-safe array growth.
    /// </summary>
    private void EnsureCapacity(int id)
    {
        if (id < Volatile.Read(ref _locations).Length)
            return;

        using var _ = _growLock.EnterScope();

        // Double-check after acquiring lock
        var locations = _locations;
        if (id < locations.Length)
            return;

        // Grow by doubling, ensuring we have room for the new id
        int newCapacity = Math.Max(locations.Length * 2, id + 1);
        var newLocations = new EntityLocation[newCapacity];
        Array.Copy(locations, newLocations, locations.Length);
        Volatile.Write(ref _locations, newLocations);
    }

    /// <summary>
    /// Releases all resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Wait for all in-flight operations to complete
        _operationGuard.WaitForCompletion();

        _freeSlots.Clear();
        _locations = [];
        _nextEntityId = 0;
        _aliveCount = 0;
    }
}
