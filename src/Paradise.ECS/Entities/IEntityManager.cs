namespace Paradise.ECS;

/// <summary>
/// Interface for entity lifecycle and validation management.
/// Provides entity creation, destruction, and liveness checking.
/// </summary>
public interface IEntityManager
{
    /// <summary>
    /// Gets the number of currently alive entities.
    /// </summary>
    int AliveCount { get; }

    /// <summary>
    /// Gets the current capacity of the entity storage.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Returns the ID that would be assigned to the next created entity,
    /// without actually creating it. Used for validation before creation.
    /// </summary>
    /// <returns>The next entity ID that would be allocated.</returns>
    int PeekNextId();

    /// <summary>
    /// Creates a new entity and returns a handle to it.
    /// The entity has no archetype until components are added.
    /// </summary>
    /// <returns>A valid entity handle.</returns>
    Entity Create();

    /// <summary>
    /// Destroys the entity associated with the handle.
    /// Safe to call multiple times or with invalid/stale handles (no-op).
    /// </summary>
    /// <param name="entity">The entity to destroy.</param>
    void Destroy(Entity entity);

    /// <summary>
    /// Checks if the entity is currently alive.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <returns>True if the entity is alive, false if destroyed or invalid.</returns>
    bool IsAlive(Entity entity);

    /// <summary>
    /// Gets the location for the specified entity ID.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <returns>The entity location containing version and archetype info.</returns>
    EntityLocation GetLocation(int entityId);
}
