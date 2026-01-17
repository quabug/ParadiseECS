using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// The central ECS world that coordinates entities, components, and systems.
/// Owns all subsystems and provides a unified API for entity manipulation.
/// Single-threaded version without concurrent access support.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
public sealed class World<TBits, TRegistry>
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private const int DefaultEntityCapacity = 1024;

    private readonly ChunkManager _chunkManager;
    private readonly ArchetypeRegistry<TBits, TRegistry> _archetypeRegistry;
    private readonly EntityManager _entityManager;
    private readonly Archetype<TBits, TRegistry> _emptyArchetype;

    /// <summary>
    /// Gets the number of currently alive entities.
    /// </summary>
    public int EntityCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _entityManager.AliveCount;
    }

    /// <summary>
    /// Creates a new ECS world using the specified shared archetype metadata.
    /// The caller is responsible for disposing the shared metadata and chunk manager.
    /// </summary>
    /// <param name="sharedMetadata">The shared archetype metadata to use.</param>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    /// <param name="initialEntityCapacity">Initial capacity for entity storage.</param>
    public World(SharedArchetypeMetadata<TBits, TRegistry> sharedMetadata,
                 ChunkManager chunkManager,
                 int initialEntityCapacity = DefaultEntityCapacity)
    {
        ArgumentNullException.ThrowIfNull(sharedMetadata);
        ArgumentNullException.ThrowIfNull(chunkManager);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialEntityCapacity, 0);

        _chunkManager = chunkManager;
        _archetypeRegistry = new ArchetypeRegistry<TBits, TRegistry>(sharedMetadata, chunkManager);
        _entityManager = new EntityManager(initialEntityCapacity);

        // Create the empty archetype for componentless entities
        _emptyArchetype = _archetypeRegistry.GetOrCreateArchetype(
            (HashedKey<ImmutableBitSet<TBits>>)ImmutableBitSet<TBits>.Empty);
    }

    /// <summary>
    /// Creates a new entity with no components.
    /// The entity is placed in the empty archetype.
    /// </summary>
    /// <returns>The created entity handle.</returns>
    public Entity Spawn()
    {
        var entity = _entityManager.Create();
        int globalIndex = _emptyArchetype.AllocateEntity(entity);
        _entityManager.SetLocation(entity.Id, new EntityLocation(entity.Version, _emptyArchetype.Id, globalIndex));
        return entity;
    }

    /// <summary>
    /// Creates a new entity using the provided builder.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <param name="builder">The component builder with initial components.</param>
    /// <returns>The created entity handle.</returns>
    internal Entity CreateEntity<TBuilder>(TBuilder builder)
        where TBuilder : IComponentsBuilder
    {
        // Collect component mask
        var mask = ImmutableBitSet<TBits>.Empty;
        builder.CollectTypes(ref mask);

        // Create entity and place in target archetype (returns empty archetype if mask is empty)
        var entity = _entityManager.Create();
        var archetype = _archetypeRegistry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<TBits>>)mask);
        PlaceEntityWithComponents(entity, archetype, builder);

        return entity;
    }

    /// <summary>
    /// Overwrites all components on an existing entity with the builder's components.
    /// Any existing components are discarded. The entity must already exist in this world.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <param name="entity">The existing entity handle.</param>
    /// <param name="builder">The component builder with components to set.</param>
    /// <returns>The entity handle.</returns>
    internal Entity OverwriteEntity<TBuilder>(Entity entity, TBuilder builder)
        where TBuilder : IComponentsBuilder
    {
        var location = GetValidatedLocation(entity);

        // Collect component mask
        var mask = ImmutableBitSet<TBits>.Empty;
        builder.CollectTypes(ref mask);

        // Remove from old archetype
        RemoveFromCurrentArchetype(location);

        // Get target archetype (returns empty archetype if mask is empty)
        var archetype = _archetypeRegistry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<TBits>>)mask);

        // Place in target archetype and write components
        PlaceEntityWithComponents(entity, archetype, builder);

        return entity;
    }

    /// <summary>
    /// Adds multiple components to an existing entity using the provided builder.
    /// Existing components are preserved. This is a structural change that moves the entity.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <param name="entity">The existing entity handle.</param>
    /// <param name="builder">The component builder with components to add or update.</param>
    /// <returns>The entity handle.</returns>
    internal Entity AddComponents<TBuilder>(Entity entity, TBuilder builder)
        where TBuilder : IComponentsBuilder
    {
        var location = GetValidatedLocation(entity);

        // Collect new component mask from builder
        var newMask = ImmutableBitSet<TBits>.Empty;
        builder.CollectTypes(ref newMask);

        if (newMask.IsEmpty)
            return entity;

        // Get current mask from entity's archetype
        var sourceArchetype = _archetypeRegistry.GetArchetypeById(location.ArchetypeId)!;
        var currentMask = sourceArchetype.Layout.ComponentMask;

        // Merge masks and get target archetype
        var targetMask = currentMask.Or(newMask);

        // If all components already exist, just update values in place
        if (targetMask.Equals(currentMask))
        {
            var (chunkIndex, indexInChunk) = sourceArchetype.GetChunkLocation(location.GlobalIndex);
            var chunkHandle = sourceArchetype.GetChunk(chunkIndex);
            builder.WriteComponents(_chunkManager, sourceArchetype.Layout, chunkHandle, indexInChunk);
            return entity;
        }

        var targetArchetype = _archetypeRegistry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<TBits>>)targetMask);

        // Allocate in target archetype
        int newGlobalIndex = targetArchetype.AllocateEntity(entity);

        // Copy existing components from source
        var (srcChunkIndex, srcIndexInChunk) = sourceArchetype.GetChunkLocation(location.GlobalIndex);
        var srcChunkHandle = sourceArchetype.GetChunk(srcChunkIndex);

        var (dstChunkIndex, dstIndexInChunk) = targetArchetype.GetChunkLocation(newGlobalIndex);
        var dstChunkHandle = targetArchetype.GetChunk(dstChunkIndex);

        CopySharedComponents(sourceArchetype, targetArchetype, srcChunkHandle, srcIndexInChunk, dstChunkHandle, dstIndexInChunk);
        RemoveFromCurrentArchetype(location);

        // Update location
        var newLocation = new EntityLocation(entity.Version, targetArchetype.Id, newGlobalIndex);
        _entityManager.SetLocation(entity.Id, newLocation);

        // Write new component data (overwrites any duplicates)
        builder.WriteComponents(_chunkManager, targetArchetype.Layout, dstChunkHandle, dstIndexInChunk);

        return entity;
    }

    /// <summary>
    /// Places an entity in the specified archetype and writes component data from the builder.
    /// </summary>
    private void PlaceEntityWithComponents<TBuilder>(
        Entity entity,
        Archetype<TBits, TRegistry> archetype,
        TBuilder builder)
        where TBuilder : IComponentsBuilder
    {
        int globalIndex = archetype.AllocateEntity(entity);
        _entityManager.SetLocation(entity.Id, new EntityLocation(entity.Version, archetype.Id, globalIndex));

        var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(globalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);
        builder.WriteComponents(_chunkManager, archetype.Layout, chunkHandle, indexInChunk);
    }

    /// <summary>
    /// Removes an entity from its current archetype.
    /// Updates the moved entity's location if a swap-remove occurred.
    /// </summary>
    private void RemoveFromCurrentArchetype(EntityLocation location)
    {
        var archetype = _archetypeRegistry.GetArchetypeById(location.ArchetypeId)!;
        int movedEntityId = archetype.RemoveEntity(location.GlobalIndex);

        // If an entity was moved during swap-remove, update its location
        if (movedEntityId >= 0)
        {
            var movedLocation = _entityManager.GetLocation(movedEntityId);
            _entityManager.SetLocation(movedEntityId, movedLocation with { GlobalIndex = location.GlobalIndex });
        }
    }

    /// <summary>
    /// Destroys an entity and removes it from its archetype.
    /// </summary>
    /// <param name="entity">The entity to destroy.</param>
    /// <returns>True if the entity was destroyed, false if it was already dead or invalid.</returns>
    public bool Despawn(Entity entity)
    {
        if (!IsAlive(entity))
            return false;

        var location = _entityManager.GetLocation(entity.Id);
        if (location.MatchesEntity(entity))
        {
            RemoveFromCurrentArchetype(location);
        }

        _entityManager.Destroy(entity);

        return true;
    }

    /// <summary>
    /// Checks if an entity is currently alive.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <returns>True if the entity is alive.</returns>
    public bool IsAlive(Entity entity)
    {
        if (!entity.IsValid)
            return false;

        return _entityManager.IsAlive(entity);
    }

    /// <summary>
    /// Gets a reference to a component on an entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <returns>A ref struct wrapping the component reference.</returns>
    /// <exception cref="InvalidOperationException">Entity doesn't have the component.</exception>
    public ComponentRef<T> GetComponent<T>(Entity entity) where T : unmanaged, IComponent
    {
        var location = GetValidatedLocation(entity);
        var archetype = _archetypeRegistry.GetArchetypeById(location.ArchetypeId)
            ?? throw new InvalidOperationException($"Entity {entity} has no archetype.");

        var layout = archetype.Layout;
        if (!layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}.");

        var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);
        int offset = layout.GetEntityComponentOffset<T>(indexInChunk);
        var chunk = _chunkManager.Get(chunkHandle);

        return new ComponentRef<T>(chunk, offset);
    }

    /// <summary>
    /// Sets a component value on an entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <param name="value">The component value.</param>
    /// <exception cref="InvalidOperationException">Entity doesn't have the component.</exception>
    public void SetComponent<T>(Entity entity, T value) where T : unmanaged, IComponent
    {
        GetComponent<T>(entity).Value = value;
    }

    /// <summary>
    /// Checks if an entity has a specific component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <returns>True if the entity has the component.</returns>
    public bool HasComponent<T>(Entity entity) where T : unmanaged, IComponent
    {
        if (!IsAlive(entity))
            return false;

        var location = _entityManager.GetLocation(entity.Id);
        var archetype = _archetypeRegistry.GetArchetypeById(location.ArchetypeId)!;
        return archetype.Layout.HasComponent<T>();
    }

    /// <summary>
    /// Adds a component to an entity. This is a structural change that may move the entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <param name="value">The component value.</param>
    /// <exception cref="InvalidOperationException">Entity is not alive or already has the component.</exception>
    public void AddComponent<T>(Entity entity, T value = default) where T : unmanaged, IComponent
    {
        var location = GetValidatedLocation(entity);
        var sourceArchetype = _archetypeRegistry.GetArchetypeById(location.ArchetypeId)!;

        // Check if already has component
        if (sourceArchetype.Layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T).Name}.");

        // Get target archetype using O(1) edge cache
        var targetArchetype = _archetypeRegistry.GetOrCreateArchetypeWithAdd(sourceArchetype, T.TypeId);

        // Move entity to target archetype
        MoveEntity(entity, location, sourceArchetype, targetArchetype);

        // Write the new component value - get updated location
        var updatedLocation = _entityManager.GetLocation(entity.Id);
        var (newChunkIndex, newIndexInChunk) = targetArchetype.GetChunkLocation(updatedLocation.GlobalIndex);
        var newChunkHandle = targetArchetype.GetChunk(newChunkIndex);
        int newOffset = targetArchetype.Layout.GetEntityComponentOffset<T>(newIndexInChunk);
        using var newChunk = _chunkManager.Get(newChunkHandle);
        var newSpan = newChunk.GetSpan<T>(newOffset, 1);
        newSpan[0] = value;
    }

    /// <summary>
    /// Removes a component from an entity. This is a structural change that may move the entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <exception cref="InvalidOperationException">Entity is not alive or doesn't have the component.</exception>
    public void RemoveComponent<T>(Entity entity) where T : unmanaged, IComponent
    {
        var location = GetValidatedLocation(entity);
        var sourceArchetype = _archetypeRegistry.GetArchetypeById(location.ArchetypeId)!;

        // Check if has component
        if (!sourceArchetype.Layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}.");

        // Get target archetype using O(1) edge cache (returns empty archetype if removing last component)
        var targetArchetype = _archetypeRegistry.GetOrCreateArchetypeWithRemove(sourceArchetype, T.TypeId);

        // Move entity to target archetype
        MoveEntity(entity, location, sourceArchetype, targetArchetype);
    }

    /// <summary>
    /// Creates a query builder for this world.
    /// </summary>
    /// <returns>A new query builder.</returns>
    public static QueryBuilder<TBits, TRegistry> Query()
    {
        return new QueryBuilder<TBits, TRegistry>();
    }

    private void MoveEntity(
        Entity entity,
        EntityLocation location,
        Archetype<TBits, TRegistry> source,
        Archetype<TBits, TRegistry> target)
    {
        // Remember old location for swap-remove handling
        int oldGlobalIndex = location.GlobalIndex;
        var (oldChunkIndex, oldIndexInChunk) = source.GetChunkLocation(oldGlobalIndex);
        var oldChunkHandle = source.GetChunk(oldChunkIndex);

        // Allocate in target archetype
        int newGlobalIndex = target.AllocateEntity(entity);
        var (newChunkIndex, newIndexInChunk) = target.GetChunkLocation(newGlobalIndex);
        var newChunkHandle = target.GetChunk(newChunkIndex);

        // Copy shared component data
        CopySharedComponents(source, target, oldChunkHandle, oldIndexInChunk, newChunkHandle, newIndexInChunk);

        // Remove from source archetype (swap-remove)
        int movedEntityId = source.RemoveEntity(oldGlobalIndex);

        // If an entity was moved during swap-remove, update its location
        if (movedEntityId >= 0)
        {
            var movedLocation = _entityManager.GetLocation(movedEntityId);
            _entityManager.SetLocation(movedEntityId, movedLocation with { GlobalIndex = oldGlobalIndex });
        }

        // Update the entity's location to the new archetype
        _entityManager.SetLocation(entity.Id, new EntityLocation(location.Version, target.Id, newGlobalIndex));
    }

    private void CopySharedComponents(
        Archetype<TBits, TRegistry> source,
        Archetype<TBits, TRegistry> target,
        ChunkHandle srcChunkHandle,
        int srcIndexInChunk,
        ChunkHandle dstChunkHandle,
        int dstIndexInChunk)
    {
        var srcLayout = source.Layout;
        var dstLayout = target.Layout;
        var sharedMask = srcLayout.ComponentMask.And(dstLayout.ComponentMask);

        if (sharedMask.IsEmpty)
            return;

        using var srcChunk = _chunkManager.Get(srcChunkHandle);
        using var dstChunk = _chunkManager.Get(dstChunkHandle);

        var typeInfos = TRegistry.TypeInfos;

        foreach (int componentId in sharedMask)
        {
            var info = typeInfos[componentId];
            if (info.Size == 0)
                continue; // Skip tag components

            int srcOffset = srcLayout.GetEntityComponentOffset(srcIndexInChunk, new ComponentId(componentId));
            int dstOffset = dstLayout.GetEntityComponentOffset(dstIndexInChunk, new ComponentId(componentId));

            var srcData = srcChunk.GetBytesAt(srcOffset, info.Size);
            var dstData = dstChunk.GetBytesAt(dstOffset, info.Size);
            srcData.CopyTo(dstData);
        }
    }

    private EntityLocation GetValidatedLocation(Entity entity)
    {
        if (!entity.IsValid)
            throw new InvalidOperationException("Invalid entity handle.");

        if (!_entityManager.IsAlive(entity))
            throw new InvalidOperationException($"Entity {entity} is not alive.");

        return _entityManager.GetLocation(entity.Id);
    }

    public void Clear()
    {
        _archetypeRegistry.Clear();
        _entityManager.Clear();
    }
}
