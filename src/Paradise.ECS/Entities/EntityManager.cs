using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// Manager for entity lifecycle and location tracking.
/// Handles entity creation, destruction, validation, and archetype location using version-based handles.
/// Uses a contiguous list for packed entity metadata indexed by Entity.Id for O(1) lookups.
/// Single-threaded version without concurrent access support.
/// </summary>
public sealed class EntityManager : IEntityManager
{
    private readonly List<ulong> _packedLocations; // Uses packed EntityLocation format for memory efficiency
    private readonly List<int> _freeSlots = new();
    private int _nextEntityId; // Next fresh entity ID to allocate
    private int _aliveCount; // Number of currently alive entities

    /// <summary>
    /// Creates a new EntityManager.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity for entity storage.</param>
    public EntityManager(int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialCapacity, 0);
        _packedLocations = new List<ulong>(initialCapacity);
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
        get => _packedLocations.Count;
    }

    /// <summary>
    /// Returns the ID that would be assigned to the next created entity,
    /// without actually creating it. Used for validation before creation.
    /// </summary>
    /// <returns>The next entity ID that would be allocated.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PeekNextId()
    {
        // If there's a free slot, that ID will be reused
        if (_freeSlots.Count > 0)
            return _freeSlots[^1];

        // Otherwise, a new ID will be allocated
        return _nextEntityId;
    }

    /// <summary>
    /// Creates a new entity and returns a handle to it.
    /// The entity has no archetype until components are added.
    /// </summary>
    /// <returns>A valid entity handle.</returns>
    public Entity Create()
    {
        int id;
        if (_freeSlots.Count > 0)
        {
            // Reuse a freed entity slot - version was already incremented on destroy
            id = _freeSlots[^1];
            _freeSlots.RemoveAt(_freeSlots.Count - 1);
            uint version = EntityLocation.FromPacked(_packedLocations[id]).Version;
            _packedLocations[id] = new EntityLocation(version, -1, -1).Packed;
            _aliveCount++;
            return new Entity(id, version);
        }

        // Allocate a new entity ID
        id = _nextEntityId;
        _nextEntityId++;

        // Ensure capacity - adds default entries to grow the list
        EnsureCapacity(id);

        // Initialize location with version 1 and no archetype
        _packedLocations[id] = new EntityLocation(1, -1, -1).Packed;

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

        if (entity.Id >= _nextEntityId)
            return;

        var location = EntityLocation.FromPacked(_packedLocations[entity.Id]);

        // Check if already destroyed (stale handle)
        if (location.Version != entity.Version)
            return;

        // Increment version to invalidate all existing handles, clear archetype info
        _packedLocations[entity.Id] = new EntityLocation(location.Version + 1, -1, -1).Packed;

        // Add to free list
        _freeSlots.Add(entity.Id);
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

        if (entity.Id >= _nextEntityId)
            return false;

        return EntityLocation.FromPacked(_packedLocations[entity.Id]).Version == entity.Version;
    }

    /// <summary>
    /// Gets the location for the specified entity ID.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <returns>The entity location.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityLocation GetLocation(int entityId)
    {
        return EntityLocation.FromPacked(_packedLocations[entityId]);
    }

    /// <summary>
    /// Sets the location for the specified entity ID.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="location">The new location.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetLocation(int entityId, in EntityLocation location)
    {
        _packedLocations[entityId] = location.Packed;
    }

    /// <summary>
    /// Ensures the list has enough elements for the given entity id.
    /// </summary>
    private void EnsureCapacity(int id)
    {
        int requiredCount = id + 1;
        if (requiredCount <= _packedLocations.Count)
            return;

        // Ensure internal capacity, then add default elements
        _packedLocations.EnsureCapacity(requiredCount);
        for (int i = _packedLocations.Count; i < requiredCount; i++)
        {
            _packedLocations.Add(default);
        }
    }

    /// <summary>
    /// Releases all resources used by this instance.
    /// </summary>
    public void Clear()
    {
        _freeSlots.Clear();
        _packedLocations.Clear();
        _nextEntityId = 0;
        _aliveCount = 0;
    }

    /// <summary>
    /// Copies all entity state from the source manager to this manager.
    /// </summary>
    /// <param name="source">The source EntityManager to copy from.</param>
    internal void CopyFrom(EntityManager source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Copy packed locations (direct memory copy)
        CollectionsMarshal.SetCount(_packedLocations, source._packedLocations.Count);
        CollectionsMarshal.AsSpan(source._packedLocations).CopyTo(CollectionsMarshal.AsSpan(_packedLocations));

        // Copy free slots (direct memory copy)
        CollectionsMarshal.SetCount(_freeSlots, source._freeSlots.Count);
        CollectionsMarshal.AsSpan(source._freeSlots).CopyTo(CollectionsMarshal.AsSpan(_freeSlots));

        // Copy counters
        _nextEntityId = source._nextEntityId;
        _aliveCount = source._aliveCount;
    }
}
