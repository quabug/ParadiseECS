using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Manager for entity lifecycle and location tracking.
/// Handles entity creation, destruction, validation, and archetype location using version-based handles.
/// Uses a contiguous list for entity metadata indexed by Entity.Id for O(1) lookups.
/// Single-threaded version without concurrent access support.
/// </summary>
public sealed class EntityManager
{
    private readonly List<EntityLocation> _locations;
    private readonly Stack<int> _freeSlots = new();
    private int _nextEntityId; // Next fresh entity ID to allocate
    private int _aliveCount; // Number of currently alive entities

    /// <summary>
    /// Creates a new EntityManager.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity for entity storage.</param>
    public EntityManager(int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialCapacity, 0);
        _locations = new List<EntityLocation>(initialCapacity);
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
        get => _locations.Count;
    }

    /// <summary>
    /// Creates a new entity and returns a handle to it.
    /// The entity has no archetype until components are added.
    /// </summary>
    /// <returns>A valid entity handle.</returns>
    public Entity Create()
    {
        if (_freeSlots.TryPop(out int id))
        {
            // Reuse a freed entity slot - version was already incremented on destroy
            uint version = _locations[id].Version;
            _locations[id] = new EntityLocation(version, -1, -1);
            _aliveCount++;
            return new Entity(id, version);
        }

        // Allocate a new entity ID
        id = _nextEntityId;
        _nextEntityId++;

        // Ensure capacity - adds default entries to grow the list
        EnsureCapacity(id);

        // Initialize location with version 1 and no archetype
        _locations[id] = new EntityLocation(1, -1, -1);

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

        var location = _locations[entity.Id];

        // Check if already destroyed (stale handle)
        if (location.Version != entity.Version)
            return;

        // Increment version to invalidate all existing handles, clear archetype info
        _locations[entity.Id] = new EntityLocation(location.Version + 1, -1, -1);

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

        if (entity.Id >= _nextEntityId)
            return false;

        return _locations[entity.Id].Version == entity.Version;
    }

    /// <summary>
    /// Gets the location for the specified entity ID.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <returns>The entity location.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityLocation GetLocation(int entityId)
    {
        return _locations[entityId];
    }

    /// <summary>
    /// Sets the location for the specified entity ID.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="location">The new location.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetLocation(int entityId, in EntityLocation location)
    {
        _locations[entityId] = location;
    }

    /// <summary>
    /// Ensures the list has enough elements for the given entity id.
    /// </summary>
    private void EnsureCapacity(int id)
    {
        int requiredCount = id + 1;
        if (requiredCount <= _locations.Count)
            return;

        // Ensure internal capacity, then add default elements
        _locations.EnsureCapacity(requiredCount);
        for (int i = _locations.Count; i < requiredCount; i++)
        {
            _locations.Add(default);
        }
    }

    /// <summary>
    /// Releases all resources used by this instance.
    /// </summary>
    public void Clear()
    {
        _freeSlots.Clear();
        _locations.Clear();
        _nextEntityId = 0;
        _aliveCount = 0;
    }
}
