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
    /// </summary>
    /// <returns>The created entity handle.</returns>
    public Entity Spawn()
    {
        using var _ = BeginOperation();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        var entity = _entityManager.Create();
        EnsureEntityLocationCapacity(entity.Id);

        // Initialize location as invalid (no archetype yet)
        ref var location = ref _entityLocations[entity.Id];
        location = new EntityLocation
        {
            Version = entity.Version,
            ArchetypeId = -1,
            ChunkHandle = ChunkHandle.Invalid,
            IndexInChunk = -1
        };

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
        using var _ = BeginOperation();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        // Collect component mask
        var mask = ImmutableBitSet<TBits>.Empty;
        builder.CollectTypes(ref mask);

        // Create entity
        var entity = _entityManager.Create();
        EnsureEntityLocationCapacity(entity.Id);

        ref var location = ref _entityLocations[entity.Id];

        if (mask.IsEmpty)
        {
            // No components - just mark as spawned with invalid archetype
            location = new EntityLocation
            {
                Version = entity.Version,
                ArchetypeId = -1,
                ChunkHandle = ChunkHandle.Invalid,
                IndexInChunk = -1
            };
            return entity;
        }

        // Get or create archetype
        var archetype = _archetypeRegistry.GetOrCreate((HashedKey<ImmutableBitSet<TBits>>)mask);

        // Allocate in archetype
        var (chunkHandle, indexInChunk) = archetype.AllocateEntity();

        // Store location
        location = new EntityLocation
        {
            Version = entity.Version,
            ArchetypeId = archetype.Id,
            ChunkHandle = chunkHandle,
            IndexInChunk = indexInChunk
        };

        // Write component data
        builder.WriteComponents(_chunkManager, archetype.Layout, chunkHandle, indexInChunk);

        return entity;
    }

    /// <summary>
    /// Sets up components for an existing entity using the provided builder.
    /// The entity must already exist in this world.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <param name="entity">The existing entity handle.</param>
    /// <param name="builder">The component builder with initial components.</param>
    /// <returns>The entity handle.</returns>
    internal Entity SetupEntity<TBuilder>(Entity entity, TBuilder builder)
        where TBuilder : IComponentsBuilder
    {
        using var _ = BeginOperation();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        if (!IsAlive(entity))
            ThrowHelper.ThrowArgumentException("Entity is not alive", nameof(entity));

        // Collect component mask
        var mask = ImmutableBitSet<TBits>.Empty;
        builder.CollectTypes(ref mask);

        if (mask.IsEmpty)
            return entity;

        ref var location = ref _entityLocations[entity.Id];

        // Get or create archetype
        var archetype = _archetypeRegistry.GetOrCreate((HashedKey<ImmutableBitSet<TBits>>)mask);

        // Allocate in archetype
        var (chunkHandle, indexInChunk) = archetype.AllocateEntity();

        // Store location
        location = new EntityLocation
        {
            Version = entity.Version,
            ArchetypeId = archetype.Id,
            ChunkHandle = chunkHandle,
            IndexInChunk = indexInChunk
        };

        // Write component data
        builder.WriteComponents(_chunkManager, archetype.Layout, chunkHandle, indexInChunk);

        return entity;
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

        if (entity.Id >= _entityLocations.Length)
            return false;

        using var lockScope = _structuralChangeLock.EnterScope();

        ref var location = ref _entityLocations[entity.Id];
        if (!location.MatchesEntity(entity))
            return false;

        // Remove from archetype if it has one
        if (location.IsValid)
        {
            var archetype = _archetypeRegistry.GetById(location.ArchetypeId);
            if (archetype != null)
            {
                int globalIndex = archetype.GetGlobalIndex(
                    GetChunkIndex(archetype, location.ChunkHandle),
                    location.IndexInChunk);

                archetype.RemoveEntity(globalIndex);
                // Note: We don't need to update the swapped entity's location here
                // because we're just destroying, not tracking which entity moved.
                // The Archetype now stores Entity handles and we can update in RemoveEntity.
            }
        }

        // Mark location as invalid
        location = EntityLocation.Invalid;

        // Destroy entity handle (increments version)
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
            int globalIndex = sourceArchetype.GetGlobalIndex(
                GetChunkIndex(sourceArchetype, location.ChunkHandle),
                location.IndexInChunk);
            sourceArchetype.RemoveEntity(globalIndex);

            location.ArchetypeId = -1;
            location.ChunkHandle = ChunkHandle.Invalid;
            location.IndexInChunk = -1;
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

        if (entity.Id >= _entityLocations.Length)
            throw new InvalidOperationException($"Entity {entity} ID out of range.");

        ref var location = ref _entityLocations[entity.Id];
        if (!location.MatchesEntity(entity))
            throw new InvalidOperationException($"Entity {entity} location version mismatch (stale handle).");

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
