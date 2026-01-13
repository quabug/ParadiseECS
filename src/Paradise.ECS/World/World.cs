namespace Paradise.ECS;

/// <summary>
/// The central ECS world that coordinates entities, components, and systems.
/// Owns all subsystems and provides a unified API for entity manipulation.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
public sealed class World<TBits, TRegistry> : IDisposable
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private const int DefaultEntityCapacity = 1024;

    private readonly ChunkManager _chunkManager;
    private readonly ArchetypeRegistry<TBits, TRegistry> _archetypeRegistry;
    private readonly EntityManager _entityManager;
    private readonly Lock _structuralChangeLock = new();

    private EntityLocation[] _entityLocations;
    private int _disposed;
    private int _activeOperations;

    /// <summary>
    /// Gets the chunk manager for memory allocation.
    /// </summary>
    public ChunkManager ChunkManager => _chunkManager;

    /// <summary>
    /// Gets the archetype registry.
    /// </summary>
    public ArchetypeRegistry<TBits, TRegistry> ArchetypeRegistry => _archetypeRegistry;

    /// <summary>
    /// Gets the number of currently alive entities.
    /// </summary>
    public int EntityCount => _entityManager.AliveCount;

    /// <summary>
    /// Creates a new ECS world with a new ChunkManager.
    /// </summary>
    /// <param name="initialEntityCapacity">Initial capacity for entity storage.</param>
    public World(int initialEntityCapacity = DefaultEntityCapacity)
        : this(new ChunkManager(), initialEntityCapacity, ownsChunkManager: true)
    {
    }

    /// <summary>
    /// Creates a new ECS world using an existing ChunkManager.
    /// </summary>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    /// <param name="initialEntityCapacity">Initial capacity for entity storage.</param>
    public World(ChunkManager chunkManager, int initialEntityCapacity = DefaultEntityCapacity)
        : this(chunkManager, initialEntityCapacity, ownsChunkManager: false)
    {
    }

    private World(ChunkManager chunkManager, int initialEntityCapacity, bool ownsChunkManager)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialEntityCapacity, 0);

        _chunkManager = chunkManager;
        _ownsChunkManager = ownsChunkManager;
        _archetypeRegistry = new ArchetypeRegistry<TBits, TRegistry>(chunkManager);
        _entityManager = new EntityManager(initialEntityCapacity);
        _entityLocations = new EntityLocation[initialEntityCapacity];
    }

    private readonly bool _ownsChunkManager;

    private OperationGuard BeginOperation() => new(ref _activeOperations);

    /// <summary>
    /// Creates a new entity with no components.
    /// Location is lazily initialized when components are first added.
    /// </summary>
    /// <returns>The created entity handle.</returns>
    public Entity Spawn()
    {
        using var _ = BeginOperation();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

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
        using var _ = BeginOperation();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        // Collect component mask
        var mask = ImmutableBitSet<TBits>.Empty;
        builder.CollectTypes(ref mask);

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
        using var _ = BeginOperation();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        using var lockScope = _structuralChangeLock.EnterScope();

        ref var location = ref GetValidatedLocation(entity);

        // Collect component mask
        var mask = ImmutableBitSet<TBits>.Empty;
        builder.CollectTypes(ref mask);

        if (mask.IsEmpty)
            return entity;

        // Remove from old archetype if it had one
        RemoveFromCurrentArchetype(ref location);

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
        using var _ = BeginOperation();
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
        var (newChunkHandle, newIndexInChunk) = targetArchetype.AllocateEntity();

        // Copy existing components and remove from source
        if (location.IsValid)
        {
            var sourceArchetype = _archetypeRegistry.GetById(location.ArchetypeId)!;
            CopySharedComponents(sourceArchetype, targetArchetype, location.ChunkHandle, location.IndexInChunk, newChunkHandle, newIndexInChunk);
            RemoveFromCurrentArchetype(ref location);
        }

        // Update location
        location = new EntityLocation
        {
            Version = entity.Version,
            ArchetypeId = targetArchetype.Id,
            ChunkHandle = newChunkHandle,
            IndexInChunk = newIndexInChunk
        };

        // Write new component data
        builder.WriteComponents(_chunkManager, targetArchetype.Layout, newChunkHandle, newIndexInChunk);

        return entity;
    }

    /// <summary>
    /// Places an entity in the specified archetype and writes component data from the builder.
    /// </summary>
    private void PlaceEntityWithComponents<TBuilder>(
        Entity entity,
        ref EntityLocation location,
        Archetype<TBits, TRegistry> archetype,
        TBuilder builder)
        where TBuilder : IComponentsBuilder
    {
        var (chunkHandle, indexInChunk) = archetype.AllocateEntity();

        location = new EntityLocation
        {
            Version = entity.Version,
            ArchetypeId = archetype.Id,
            ChunkHandle = chunkHandle,
            IndexInChunk = indexInChunk
        };

        builder.WriteComponents(_chunkManager, archetype.Layout, chunkHandle, indexInChunk);
    }

    /// <summary>
    /// Removes an entity from its current archetype if it has one.
    /// </summary>
    private void RemoveFromCurrentArchetype(ref EntityLocation location)
    {
        if (!location.IsValid)
            return;

        var archetype = _archetypeRegistry.GetById(location.ArchetypeId)!;
        int globalIndex = archetype.GetGlobalIndex(
            GetChunkIndex(archetype, location.ChunkHandle),
            location.IndexInChunk);
        archetype.RemoveEntity(globalIndex);
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

        using var _ = BeginOperation();
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

        using var _ = BeginOperation();
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
    public ComponentRef<T> GetComponent<T>(Entity entity) where T : unmanaged, IComponent
    {
        using var _ = BeginOperation();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        ref var location = ref GetValidatedLocation(entity);
        var archetype = _archetypeRegistry.GetById(location.ArchetypeId)
            ?? throw new InvalidOperationException($"Entity {entity} has no archetype.");

        var layout = archetype.Layout;
        if (!layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}.");

        int offset = layout.GetEntityComponentOffset<T>(location.IndexInChunk);
        var chunk = _chunkManager.Get(location.ChunkHandle);

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
        using var _ = BeginOperation();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        ref var location = ref GetValidatedLocation(entity);
        var archetype = _archetypeRegistry.GetById(location.ArchetypeId)
            ?? throw new InvalidOperationException($"Entity {entity} has no archetype.");

        var layout = archetype.Layout;
        if (!layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}.");

        int offset = layout.GetEntityComponentOffset<T>(location.IndexInChunk);
        using var chunk = _chunkManager.Get(location.ChunkHandle);
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

        using var _ = BeginOperation();
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
        using var _ = BeginOperation();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        using var lockScope = _structuralChangeLock.EnterScope();

        ref var location = ref GetValidatedLocation(entity);

        if (!location.IsValid)
        {
            // Entity has no archetype yet - create one with just this component
            var mask = ImmutableBitSet<TBits>.Empty.Set(T.TypeId);
            var archetype = _archetypeRegistry.GetOrCreate((HashedKey<ImmutableBitSet<TBits>>)mask);

            var (chunkHandle, indexInChunk) = archetype.AllocateEntity();

            location.ArchetypeId = archetype.Id;
            location.ChunkHandle = chunkHandle;
            location.IndexInChunk = indexInChunk;

            // Write component value
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
        MoveEntity(ref location, sourceArchetype, targetArchetype);

        // Write the new component value
        int newOffset = targetArchetype.Layout.GetEntityComponentOffset<T>(location.IndexInChunk);
        using var newChunk = _chunkManager.Get(location.ChunkHandle);
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
        using var _ = BeginOperation();
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
        MoveEntity(ref location, sourceArchetype, targetArchetype);
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
        ref EntityLocation location,
        Archetype<TBits, TRegistry> source,
        Archetype<TBits, TRegistry> target)
    {
        // Remember old location
        int oldGlobalIndex = source.GetGlobalIndex(
            GetChunkIndex(source, location.ChunkHandle),
            location.IndexInChunk);

        // Allocate in target archetype
        var (newChunkHandle, newIndexInChunk) = target.AllocateEntity();

        // Copy shared component data
        CopySharedComponents(source, target, location.ChunkHandle, location.IndexInChunk, newChunkHandle, newIndexInChunk);

        // Remove from source archetype (swap-remove)
        source.RemoveEntity(oldGlobalIndex);

        // Update the moved entity's location
        location.ArchetypeId = target.Id;
        location.ChunkHandle = newChunkHandle;
        location.IndexInChunk = newIndexInChunk;
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

    private static int GetChunkIndex(Archetype<TBits, TRegistry> archetype, ChunkHandle chunkHandle)
    {
        var chunks = archetype.GetChunks();
        for (int i = 0; i < chunks.Length; i++)
        {
            if (chunks[i].Id == chunkHandle.Id)
                return i;
        }
        throw new InvalidOperationException("Chunk not found in archetype.");
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
        OperationGuard.WaitForCompletion(ref _activeOperations);

        _archetypeRegistry.Dispose();
        _entityManager.Dispose();

        if (_ownsChunkManager)
            _chunkManager.Dispose();

        _entityLocations = [];
    }
}
