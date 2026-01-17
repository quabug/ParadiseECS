namespace Paradise.ECS.Concurrent;

/// <summary>
/// The central ECS world that coordinates entities, components, and systems.
/// Owns all subsystems and provides a unified API for entity manipulation.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
/// <typeparam name="TConfig">The world configuration type that determines chunk size and limits.</typeparam>
public sealed class World<TBits, TRegistry, TConfig> : IDisposable
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
    where TConfig : IConfig, new()
{
    private readonly SharedArchetypeMetadata<TBits, TRegistry, TConfig> _sharedMetadata;
    private readonly ChunkManager<TConfig> _chunkManager;
    private readonly ArchetypeRegistry<TBits, TRegistry, TConfig> _archetypeRegistry;
    private readonly EntityManager _entityManager;
    private readonly Lock _structuralChangeLock = new();

    private readonly OperationGuard _operationGuard = new();
    private EntityLocation[] _entityLocations;
    private int _disposed;

    /// <summary>
    /// Gets the shared archetype metadata used by this world.
    /// </summary>
    public SharedArchetypeMetadata<TBits, TRegistry, TConfig> SharedMetadata => _sharedMetadata;

    /// <summary>
    /// Gets the chunk manager for memory allocation.
    /// </summary>
    public ChunkManager<TConfig> ChunkManager => _chunkManager;

    /// <summary>
    /// Gets the archetype registry.
    /// </summary>
    public ArchetypeRegistry<TBits, TRegistry, TConfig> ArchetypeRegistry => _archetypeRegistry;

    /// <summary>
    /// Gets the number of currently alive entities.
    /// </summary>
    public int EntityCount => _entityManager.AliveCount;

    /// <summary>
    /// Creates a new ECS world using the specified configuration and shared archetype metadata.
    /// The caller is responsible for disposing the shared metadata and chunk manager.
    /// </summary>
    /// <param name="config">The configuration instance with runtime settings.</param>
    /// <param name="sharedMetadata">The shared archetype metadata to use.</param>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    public World(TConfig config,
                 SharedArchetypeMetadata<TBits, TRegistry, TConfig> sharedMetadata,
                 ChunkManager<TConfig> chunkManager)
    {
        ArgumentNullException.ThrowIfNull(sharedMetadata);
        ArgumentNullException.ThrowIfNull(chunkManager);

        _sharedMetadata = sharedMetadata;
        _chunkManager = chunkManager;
        _archetypeRegistry = new ArchetypeRegistry<TBits, TRegistry, TConfig>(sharedMetadata, chunkManager);
        _entityManager = new EntityManager(config.DefaultEntityCapacity);
        _entityLocations = new EntityLocation[config.DefaultEntityCapacity];
    }

    /// <summary>
    /// Creates a new ECS world using default configuration and shared archetype metadata.
    /// Uses <c>new TConfig()</c> for configuration with default property values.
    /// The caller is responsible for disposing the shared metadata and chunk manager.
    /// </summary>
    /// <param name="sharedMetadata">The shared archetype metadata to use.</param>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    public World(SharedArchetypeMetadata<TBits, TRegistry, TConfig> sharedMetadata,
                 ChunkManager<TConfig> chunkManager)
        : this(new TConfig(), sharedMetadata, chunkManager)
    {
    }

    /// <summary>
    /// Creates a new entity with no components.
    /// Location is lazily initialized when components are first added.
    /// </summary>
    /// <returns>The created entity handle.</returns>
    public Entity Spawn()
    {
        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        // Validate before creating to avoid inconsistent state if limit exceeded
        ThrowHelper.ThrowIfEntityIdExceedsLimit<TConfig>(_entityManager.PeekNextId());
        return _entityManager.Create();
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
        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        // Collect component mask
        var mask = ImmutableBitSet<TBits>.Empty;
        builder.CollectTypes(ref mask);

        // Validate before creating to avoid inconsistent state if limit exceeded
        ThrowHelper.ThrowIfEntityIdExceedsLimit<TConfig>(_entityManager.PeekNextId());

        // Create entity
        var entity = _entityManager.Create();

        if (mask.IsEmpty)
        {
            // No components - location will be lazily initialized if needed
            return entity;
        }

        EnsureEntityLocationCapacity(entity.Id);
        ref var location = ref _entityLocations[entity.Id];

        var archetype = _archetypeRegistry.GetOrCreate((HashedKey<ImmutableBitSet<TBits>>)mask);
        PlaceEntityWithComponents(entity, ref location, archetype, builder);

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
        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        using var lockScope = _structuralChangeLock.EnterScope();

        ref var location = ref GetValidatedLocation(entity);

        // Collect component mask
        var mask = ImmutableBitSet<TBits>.Empty;
        builder.CollectTypes(ref mask);

        // Remove from old archetype if it had one
        RemoveFromCurrentArchetype(ref location);

        if (mask.IsEmpty)
        {
            // No components - entity is now componentless
            location = EntityLocation.Invalid;
            return entity;
        }

        var archetype = _archetypeRegistry.GetOrCreate((HashedKey<ImmutableBitSet<TBits>>)mask);
        PlaceEntityWithComponents(entity, ref location, archetype, builder);

        return entity;
    }

    /// <summary>
    /// Adds multiple components to an existing entity using the provided builder.
    /// Existing components are preserved. This is a structural change that moves the entity.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <param name="entity">The existing entity handle.</param>
    /// <param name="builder">The component builder with components to add.</param>
    /// <returns>The entity handle.</returns>
    /// <exception cref="InvalidOperationException">Entity already has one of the components being added.</exception>
    internal Entity AddComponents<TBuilder>(Entity entity, TBuilder builder)
        where TBuilder : IComponentsBuilder
    {
        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        using var lockScope = _structuralChangeLock.EnterScope();

        ref var location = ref GetValidatedLocation(entity);

        // Collect new component mask from builder
        var newMask = ImmutableBitSet<TBits>.Empty;
        builder.CollectTypes(ref newMask);

        if (newMask.IsEmpty)
            return entity;

        // Get current mask (empty if entity has no archetype)
        var currentMask = location.IsValid
            ? _archetypeRegistry.GetById(location.ArchetypeId)!.Layout.ComponentMask
            : ImmutableBitSet<TBits>.Empty;

        // Check for duplicates
        var overlap = currentMask.And(newMask);
        if (!overlap.IsEmpty)
        {
            foreach (int id in overlap)
            {
                throw new InvalidOperationException($"Entity {entity} already has component with ID {id}.");
            }
        }

        // Merge masks and get target archetype
        var targetMask = currentMask.Or(newMask);
        var targetArchetype = _archetypeRegistry.GetOrCreate((HashedKey<ImmutableBitSet<TBits>>)targetMask);

        // Allocate in target archetype
        int newGlobalIndex = targetArchetype.AllocateEntity(entity);

        // Copy existing components and remove from source
        if (location.IsValid)
        {
            var sourceArchetype = _archetypeRegistry.GetById(location.ArchetypeId)!;
            var (srcChunkIndex, srcIndexInChunk) = sourceArchetype.GetChunkLocation(location.GlobalIndex);
            var srcChunkHandle = sourceArchetype.GetChunk(srcChunkIndex);

            var (dstChunkIndex, dstIndexInChunk) = targetArchetype.GetChunkLocation(newGlobalIndex);
            var dstChunkHandle = targetArchetype.GetChunk(dstChunkIndex);

            CopySharedComponents(sourceArchetype, targetArchetype, srcChunkHandle, srcIndexInChunk, dstChunkHandle, dstIndexInChunk);
            RemoveFromCurrentArchetype(ref location);
        }

        // Update location
        location = new EntityLocation
        {
            Version = entity.Version,
            ArchetypeId = targetArchetype.Id,
            GlobalIndex = newGlobalIndex
        };

        // Write new component data
        var (newChunkIndex, newIndexInChunk) = targetArchetype.GetChunkLocation(newGlobalIndex);
        var newChunkHandle = targetArchetype.GetChunk(newChunkIndex);
        builder.WriteComponents(_chunkManager, targetArchetype.Layout, newChunkHandle, newIndexInChunk);

        return entity;
    }

    /// <summary>
    /// Places an entity in the specified archetype and writes component data from the builder.
    /// </summary>
    private void PlaceEntityWithComponents<TBuilder>(
        Entity entity,
        ref EntityLocation location,
        Archetype<TBits, TRegistry, TConfig> archetype,
        TBuilder builder)
        where TBuilder : IComponentsBuilder
    {
        int globalIndex = archetype.AllocateEntity(entity);

        location = new EntityLocation
        {
            Version = entity.Version,
            ArchetypeId = archetype.Id,
            GlobalIndex = globalIndex
        };

        var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(globalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);
        builder.WriteComponents(_chunkManager, archetype.Layout, chunkHandle, indexInChunk);
    }

    /// <summary>
    /// Removes an entity from its current archetype if it has one.
    /// Updates the moved entity's location if a swap-remove occurred.
    /// </summary>
    private void RemoveFromCurrentArchetype(ref EntityLocation location)
    {
        if (!location.IsValid)
            return;

        var archetype = _archetypeRegistry.GetById(location.ArchetypeId)!;
        int movedEntityId = archetype.RemoveEntity(location.GlobalIndex);

        // If an entity was moved during swap-remove, update its location
        if (movedEntityId >= 0)
        {
            ref var movedLocation = ref _entityLocations[movedEntityId];
            movedLocation.GlobalIndex = location.GlobalIndex;
        }
    }

    /// <summary>
    /// Destroys an entity and removes it from its archetype.
    /// </summary>
    /// <param name="entity">The entity to destroy.</param>
    /// <returns>True if the entity was destroyed, false if it was already dead or invalid.</returns>
    public bool Despawn(Entity entity)
    {
        if (!entity.IsValid)
            return false;

        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        if (!_entityManager.IsAlive(entity))
            return false;

        using var lockScope = _structuralChangeLock.EnterScope();

        // Handle lazy initialization - location may not exist yet
        if (entity.Id < _entityLocations.Length)
        {
            ref var location = ref _entityLocations[entity.Id];
            if (location.MatchesEntity(entity))
            {
                RemoveFromCurrentArchetype(ref location);
            }
            location = EntityLocation.Invalid;
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

        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        return _entityManager.IsAlive(entity);
    }

    /// <summary>
    /// Gets a reference to a component on an entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <returns>A ref struct wrapping the component reference.</returns>
    /// <exception cref="InvalidOperationException">Entity doesn't have the component.</exception>
    public ComponentRef<T, TConfig> GetComponent<T>(Entity entity) where T : unmanaged, IComponent
    {
        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        ref var location = ref GetValidatedLocation(entity);
        var archetype = _archetypeRegistry.GetById(location.ArchetypeId)
            ?? throw new InvalidOperationException($"Entity {entity} has no archetype.");

        var layout = archetype.Layout;
        if (!layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}.");

        var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);
        int offset = layout.GetEntityComponentOffset<T>(indexInChunk);
        var chunk = _chunkManager.Get(chunkHandle);

        return new ComponentRef<T, TConfig>(chunk, offset);
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
        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        ref var location = ref GetValidatedLocation(entity);
        var archetype = _archetypeRegistry.GetById(location.ArchetypeId)
            ?? throw new InvalidOperationException($"Entity {entity} has no archetype.");

        var layout = archetype.Layout;
        if (!layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}.");

        var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);
        int offset = layout.GetEntityComponentOffset<T>(indexInChunk);
        using var chunk = _chunkManager.Get(chunkHandle);
        var span = chunk.GetSpan<T>(offset, 1);
        span[0] = value;
    }

    /// <summary>
    /// Checks if an entity has a specific component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <returns>True if the entity has the component.</returns>
    public bool HasComponent<T>(Entity entity) where T : unmanaged, IComponent
    {
        if (!entity.IsValid)
            return false;

        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        if (!TryGetLocation(entity, out var location))
            return false;

        if (!location.IsValid)
            return false;

        var archetype = _archetypeRegistry.GetById(location.ArchetypeId);
        return archetype?.Layout.HasComponent<T>() ?? false;
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
        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        using var lockScope = _structuralChangeLock.EnterScope();

        ref var location = ref GetValidatedLocation(entity);

        if (!location.IsValid)
        {
            // Entity has no archetype yet - create one with just this component
            var mask = ImmutableBitSet<TBits>.Empty.Set(T.TypeId);
            var archetype = _archetypeRegistry.GetOrCreate((HashedKey<ImmutableBitSet<TBits>>)mask);

            int globalIndex = archetype.AllocateEntity(entity);

            location.ArchetypeId = archetype.Id;
            location.GlobalIndex = globalIndex;

            // Write component value
            var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(globalIndex);
            var chunkHandle = archetype.GetChunk(chunkIndex);
            int offset = archetype.Layout.GetEntityComponentOffset<T>(indexInChunk);
            using var chunk = _chunkManager.Get(chunkHandle);
            var span = chunk.GetSpan<T>(offset, 1);
            span[0] = value;
            return;
        }

        var sourceArchetype = _archetypeRegistry.GetById(location.ArchetypeId)!;

        // Check if already has component
        if (sourceArchetype.Layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T).Name}.");

        // Get target archetype using O(1) edge cache
        var targetArchetype = _archetypeRegistry.GetOrCreateWithAdd(sourceArchetype, T.TypeId);

        // Move entity to target archetype
        MoveEntity(entity, ref location, sourceArchetype, targetArchetype);

        // Write the new component value
        var (newChunkIndex, newIndexInChunk) = targetArchetype.GetChunkLocation(location.GlobalIndex);
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
        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        using var lockScope = _structuralChangeLock.EnterScope();

        ref var location = ref GetValidatedLocation(entity);

        if (!location.IsValid)
            throw new InvalidOperationException($"Entity {entity} has no components.");

        var sourceArchetype = _archetypeRegistry.GetById(location.ArchetypeId)!;

        // Check if has component
        if (!sourceArchetype.Layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}.");

        // Get target archetype using O(1) edge cache
        var targetArchetype = _archetypeRegistry.GetOrCreateWithRemove(sourceArchetype, T.TypeId);

        if (targetArchetype.Layout.ComponentMask.IsEmpty)
        {
            // Removing the last component - remove from archetype entirely
            RemoveFromCurrentArchetype(ref location);
            location = EntityLocation.Invalid with { Version = location.Version };
            return;
        }

        // Move entity to target archetype
        MoveEntity(entity, ref location, sourceArchetype, targetArchetype);
    }

    /// <summary>
    /// Creates a query builder for this world.
    /// </summary>
    /// <returns>A new query builder.</returns>
    public static QueryBuilder<TBits, TRegistry, TConfig> Query()
    {
        return new QueryBuilder<TBits, TRegistry, TConfig>();
    }

    private void MoveEntity(
        Entity entity,
        ref EntityLocation location,
        Archetype<TBits, TRegistry, TConfig> source,
        Archetype<TBits, TRegistry, TConfig> target)
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
            ref var movedLocation = ref _entityLocations[movedEntityId];
            movedLocation.GlobalIndex = oldGlobalIndex;
        }

        // Update the entity's location to the new archetype
        location.ArchetypeId = target.Id;
        location.GlobalIndex = newGlobalIndex;
    }

    private void CopySharedComponents(
        Archetype<TBits, TRegistry, TConfig> source,
        Archetype<TBits, TRegistry, TConfig> target,
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

    private ref EntityLocation GetValidatedLocation(Entity entity)
    {
        if (!entity.IsValid)
            throw new InvalidOperationException("Invalid entity handle.");

        if (!_entityManager.IsAlive(entity))
            throw new InvalidOperationException($"Entity {entity} is not alive.");

        // Ensure location array has capacity (lazy initialization)
        EnsureEntityLocationCapacity(entity.Id);

        ref var location = ref _entityLocations[entity.Id];

        // Initialize location if it was never set or was from a previous entity at this ID
        if (location.Version == 0)
        {
            // Never initialized or was despawned - initialize with current entity's version
            location = EntityLocation.Invalid with { Version = entity.Version };
        }
        else if (!location.MatchesEntity(entity))
        {
            throw new InvalidOperationException($"Entity {entity} location version mismatch (stale handle).");
        }

        return ref location;
    }

    private bool TryGetLocation(Entity entity, out EntityLocation location)
    {
        if (!entity.IsValid || entity.Id >= _entityLocations.Length)
        {
            location = EntityLocation.Invalid;
            return false;
        }

        location = _entityLocations[entity.Id];
        return location.MatchesEntity(entity);
    }

    private void EnsureEntityLocationCapacity(int entityId)
    {
        if (entityId < _entityLocations.Length)
            return;

        int newCapacity = Math.Max(_entityLocations.Length * 2, entityId + 1);
        var newLocations = new EntityLocation[newCapacity];
        Array.Copy(_entityLocations, newLocations, _entityLocations.Length);
        _entityLocations = newLocations;
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Wait for all in-flight operations to complete
        _operationGuard.WaitForCompletion();

        _archetypeRegistry.Dispose();
        _entityManager.Dispose();

        _entityLocations = [];
    }
}
