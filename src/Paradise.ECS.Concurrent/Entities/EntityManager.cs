using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Paradise.ECS.Concurrent;

/// <summary>
/// Thread-safe manager for entity lifecycle and location tracking.
/// Handles entity creation, destruction, validation, and archetype location using version-based handles.
/// Uses a contiguous array for entity metadata indexed by Entity.Id for O(1) lookups.
/// Array growth uses a lock to prevent wasted allocations.
/// Destroy operations use lock-free CAS for minimal contention.
/// </summary>
public sealed class EntityManager : IEntityManager, IDisposable
{
    private ulong[] _packedLocations; // Uses packed EntityLocation format for lock-free CAS
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
        _packedLocations = new ulong[initialCapacity];
    }

    /// <summary>
    /// Gets the number of currently alive entities.
    /// </summary>
    public int AliveCount => Volatile.Read(ref _aliveCount);

    /// <summary>
    /// Gets the current capacity of the entity storage.
    /// </summary>
    public int Capacity => Volatile.Read(ref _packedLocations).Length;

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
            var locations = Volatile.Read(ref _packedLocations);
            var current = EntityLocation.FromPacked(Volatile.Read(ref locations[id]));
            // Reset archetype info (already -1 from destroy, but ensure consistency)
            Volatile.Write(ref locations[id], new EntityLocation(current.Version, -1, -1).Packed);
            Interlocked.Increment(ref _aliveCount);
            return new Entity(id, current.Version);
        }

        // Allocate a new entity ID
        id = Interlocked.Increment(ref _nextEntityId) - 1;

        // Ensure capacity (thread-safe array growth)
        EnsureCapacity(id);

        // Initialize location with retry in case array grows while we're writing.
        // If another thread grows the array after we read _packedLocations but before we write,
        // our write goes to a stale array. Retry ensures we write to the current array.
        while (true)
        {
            var locations = Volatile.Read(ref _packedLocations);
            Volatile.Write(ref locations[id], new EntityLocation(1, -1, -1).Packed);

            // Check if array was replaced while we were writing
            if (Volatile.Read(ref _packedLocations) == locations)
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
    /// Uses lock-free CAS for minimal contention.
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

        // Lock-free CAS loop for atomic read-modify-write
        while (true)
        {
            var locations = Volatile.Read(ref _packedLocations);
            ulong currentPacked = Volatile.Read(ref locations[entity.Id]);
            var current = EntityLocation.FromPacked(currentPacked);

            // Check if already destroyed (stale handle)
            if (current.Version != entity.Version)
                return;

            // Prepare new location with incremented version and invalid archetype
            var newLocation = new EntityLocation(current.Version + 1, -1, -1);

            // Atomic compare-and-swap
            ulong original = Interlocked.CompareExchange(
                ref locations[entity.Id],
                newLocation.Packed,
                currentPacked);

            if (original == currentPacked)
            {
                // Successfully destroyed
                _freeSlots.Push(entity.Id);
                Interlocked.Decrement(ref _aliveCount);
                return;
            }

            // CAS failed - check if array was replaced during our operation
            if (Volatile.Read(ref _packedLocations) != locations)
            {
                // Array was replaced, retry with new array
                continue;
            }

            // Another thread modified this slot - re-read and check version
            // (likely already destroyed by another thread)
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

        var locations = Volatile.Read(ref _packedLocations);
        var packed = EntityLocation.FromPacked(Volatile.Read(ref locations[entity.Id]));
        return packed.Version == entity.Version;
    }

    /// <summary>
    /// Gets the location data for the specified entity.
    /// The caller must validate the entity is alive before calling this method.
    /// </summary>
    /// <param name="entity">The entity to get location for.</param>
    /// <returns>The entity's location data.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the manager is disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityLocation GetLocation(Entity entity)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        var locations = Volatile.Read(ref _packedLocations);
        return EntityLocation.FromPacked(Volatile.Read(ref locations[entity.Id]));
    }

    /// <summary>
    /// Sets the location data for the specified entity.
    /// The caller must validate the entity is alive before calling this method.
    /// </summary>
    /// <param name="entity">The entity to set location for.</param>
    /// <param name="location">The new location data.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the manager is disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetLocation(Entity entity, EntityLocation location)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        var locations = Volatile.Read(ref _packedLocations);
        Volatile.Write(ref locations[entity.Id], location.Packed);
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

        var locations = Volatile.Read(ref _packedLocations);
        location = EntityLocation.FromPacked(Volatile.Read(ref locations[entity.Id]));
        if (location.Version != entity.Version)
        {
            location = EntityLocation.Invalid;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ensures the array has capacity for the given entity id.
    /// Uses lock for thread-safe array growth.
    /// </summary>
    private void EnsureCapacity(int id)
    {
        if (id < Volatile.Read(ref _packedLocations).Length)
            return;

        using var _ = _growLock.EnterScope();

        // Double-check after acquiring lock
        var locations = _packedLocations;
        if (id < locations.Length)
            return;

        // Grow by doubling, ensuring we have room for the new id
        int newCapacity = Math.Max(locations.Length * 2, id + 1);
        var newLocations = new ulong[newCapacity];
        Array.Copy(locations, newLocations, locations.Length);
        Volatile.Write(ref _packedLocations, newLocations);
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
        _packedLocations = [];
        _nextEntityId = 0;
        _aliveCount = 0;
    }
}
