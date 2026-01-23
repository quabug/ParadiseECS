using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Paradise.ECS.Concurrent;

/// <summary>
/// The central ECS world that coordinates entities, components, and systems.
/// Owns all subsystems and provides a unified API for entity manipulation.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TRegistry">The component registry type.</typeparam>
/// <typeparam name="TConfig">The world configuration type that determines chunk size and limits.</typeparam>
public sealed class World<TMask, TRegistry, TConfig> : IDisposable
    where TMask : unmanaged, IBitSet<TMask>
    where TRegistry : IComponentRegistry
    where TConfig : IConfig, new()
{
    private readonly SharedArchetypeMetadata<TMask, TRegistry, TConfig> _sharedMetadata;
    private readonly ChunkManager _chunkManager;
    private readonly ArchetypeRegistry<TMask, TRegistry, TConfig> _archetypeRegistry;
    private readonly EntityManager _entityManager;
    private readonly Lock _structuralChangeLock = new();

    private readonly OperationGuard _operationGuard = new();
    private int _disposed;

    /// <summary>
    /// Gets the shared archetype metadata used by this world.
    /// </summary>
    public SharedArchetypeMetadata<TMask, TRegistry, TConfig> SharedMetadata => _sharedMetadata;

    /// <summary>
    /// Gets the chunk manager for memory allocation.
    /// </summary>
    public ChunkManager ChunkManager => _chunkManager;

    /// <summary>
    /// Gets the archetype registry.
    /// </summary>
    public ArchetypeRegistry<TMask, TRegistry, TConfig> ArchetypeRegistry => _archetypeRegistry;

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
                 SharedArchetypeMetadata<TMask, TRegistry, TConfig> sharedMetadata,
                 ChunkManager chunkManager)
    {
        ArgumentNullException.ThrowIfNull(sharedMetadata);
        ArgumentNullException.ThrowIfNull(chunkManager);

        _sharedMetadata = sharedMetadata;
        _chunkManager = chunkManager;
        _archetypeRegistry = new ArchetypeRegistry<TMask, TRegistry, TConfig>(sharedMetadata, chunkManager);
        _entityManager = new EntityManager(config.DefaultEntityCapacity);
    }

    /// <summary>
    /// Creates a new ECS world using default configuration and shared archetype metadata.
    /// Uses <c>new TConfig()</c> for configuration with default property values.
    /// The caller is responsible for disposing the shared metadata and chunk manager.
    /// </summary>
    /// <param name="sharedMetadata">The shared archetype metadata to use.</param>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    public World(SharedArchetypeMetadata<TMask, TRegistry, TConfig> sharedMetadata,
                 ChunkManager chunkManager)
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
        ThrowHelper.ThrowIfEntityIdExceedsLimit(_entityManager.PeekNextId(), Config<TConfig>.MaxEntityId, TConfig.EntityIdByteSize);
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
        var mask = TMask.Empty;
        builder.CollectTypes(ref mask);

        // Validate before creating to avoid inconsistent state if limit exceeded
        ThrowHelper.ThrowIfEntityIdExceedsLimit(_entityManager.PeekNextId(), Config<TConfig>.MaxEntityId, TConfig.EntityIdByteSize);

        // Create entity
        var entity = _entityManager.Create();

        if (mask.IsEmpty)
        {
            // No components - entity starts with invalid location
            return entity;
        }

        var archetype = _archetypeRegistry.GetOrCreate((HashedKey<TMask>)mask);
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
        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        using var lockScope = _structuralChangeLock.EnterScope();

        var location = GetValidatedLocation(entity);

        // Collect component mask
        var mask = TMask.Empty;
        builder.CollectTypes(ref mask);

        // Remove from old archetype if it had one
        RemoveFromCurrentArchetype(location);

        if (mask.IsEmpty)
        {
            // No components - entity is now componentless
            _entityManager.SetLocation(entity, EntityLocation.Invalid);
            return entity;
        }

        var archetype = _archetypeRegistry.GetOrCreate((HashedKey<TMask>)mask);
        PlaceEntityWithComponents(entity, archetype, builder);

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

        var location = GetValidatedLocation(entity);

        // Collect new component mask from builder
        var newMask = TMask.Empty;
        builder.CollectTypes(ref newMask);

        if (newMask.IsEmpty)
            return entity;

        // Get current mask (empty if entity has no archetype)
        var currentMask = location.IsValid
            ? _archetypeRegistry.GetById(location.ArchetypeId)!.Layout.ComponentMask
            : TMask.Empty;

        // Check for duplicates
        var overlap = currentMask.And(newMask);
        if (!overlap.IsEmpty)
        {
            int id = overlap.FirstSetBit();
            throw new InvalidOperationException($"Entity {entity} already has component with ID {id}.");
        }

        // Merge masks and get target archetype
        var targetMask = currentMask.Or(newMask);
        var targetArchetype = _archetypeRegistry.GetOrCreate((HashedKey<TMask>)targetMask);

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
            RemoveFromCurrentArchetype(location);
        }

        // Update location
        _entityManager.SetLocation(entity, new EntityLocation(entity.Version, targetArchetype.Id, newGlobalIndex));

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
        Archetype<TMask, TRegistry, TConfig> archetype,
        TBuilder builder)
        where TBuilder : IComponentsBuilder
    {
        int globalIndex = archetype.AllocateEntity(entity);
        _entityManager.SetLocation(entity, new EntityLocation(entity.Version, archetype.Id, globalIndex));

        var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(globalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);
        builder.WriteComponents(_chunkManager, archetype.Layout, chunkHandle, indexInChunk);
    }

    /// <summary>
    /// Removes an entity from its current archetype if it has one.
    /// Updates the moved entity's location if a swap-remove occurred.
    /// </summary>
    private void RemoveFromCurrentArchetype(EntityLocation location)
    {
        if (!location.IsValid)
            return;

        var archetype = _archetypeRegistry.GetById(location.ArchetypeId)!;
        int movedEntityId = archetype.RemoveEntity(location.GlobalIndex);

        // If an entity was moved during swap-remove, update its location
        if (movedEntityId >= 0)
        {
            var movedEntity = GetEntityFromId(movedEntityId);
            var movedLocation = _entityManager.GetLocation(movedEntity);
            _entityManager.SetLocation(movedEntity, new EntityLocation(movedLocation.Version, movedLocation.ArchetypeId, location.GlobalIndex));
        }
    }

    /// <summary>
    /// Gets the Entity handle for a given entity ID by reading its version from storage.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Entity GetEntityFromId(int entityId)
    {
        var location = _entityManager.GetLocation(new Entity(entityId, 1)); // Version doesn't matter for GetLocation
        return new Entity(entityId, location.Version);
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

        if (_entityManager.TryGetLocation(entity, out var location) && location.MatchesEntity(entity))
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
    public T GetComponent<T>(Entity entity) where T : unmanaged, IComponent
    {
        using var _ = _operationGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        var location = GetValidatedLocation(entity);
        var archetype = _archetypeRegistry.GetById(location.ArchetypeId)
            ?? throw new InvalidOperationException($"Entity {entity} has no archetype.");

        var layout = archetype.Layout;
        if (!layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}.");

        var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);
        int offset = layout.GetEntityComponentOffset<T>(indexInChunk);
        return _chunkManager.GetBytes(chunkHandle).GetRef<T>(offset);
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

        var location = GetValidatedLocation(entity);
        var archetype = _archetypeRegistry.GetById(location.ArchetypeId)
            ?? throw new InvalidOperationException($"Entity {entity} has no archetype.");

        var layout = archetype.Layout;
        if (!layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}.");

        var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);
        int offset = layout.GetEntityComponentOffset<T>(indexInChunk);
        System.Runtime.InteropServices.MemoryMarshal.Write(_chunkManager.GetBytes(chunkHandle).Slice(offset), in value);
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

        if (!_entityManager.TryGetLocation(entity, out var location))
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

        var location = GetValidatedLocation(entity);

        if (!location.IsValid)
        {
            // Entity has no archetype yet - create one with just this component
            var mask = TMask.Empty.Set(T.TypeId);
            var archetype = _archetypeRegistry.GetOrCreate((HashedKey<TMask>)mask);

            int globalIndex = archetype.AllocateEntity(entity);
            _entityManager.SetLocation(entity, new EntityLocation(entity.Version, archetype.Id, globalIndex));

            // Write component value
            var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(globalIndex);
            var chunkHandle = archetype.GetChunk(chunkIndex);
            int offset = archetype.Layout.GetEntityComponentOffset<T>(indexInChunk);
            System.Runtime.InteropServices.MemoryMarshal.Write(_chunkManager.GetBytes(chunkHandle).Slice(offset), in value);
            return;
        }

        var sourceArchetype = _archetypeRegistry.GetById(location.ArchetypeId)!;

        // Check if already has component
        if (sourceArchetype.Layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T).Name}.");

        // Get target archetype using O(1) edge cache
        var targetArchetype = _archetypeRegistry.GetOrCreateWithAdd(sourceArchetype, T.TypeId);

        // Move entity to target archetype
        int newGlobalIndex = MoveEntity(entity, location, sourceArchetype, targetArchetype);

        // Write the new component value
        var (newChunkIndex, newIndexInChunk) = targetArchetype.GetChunkLocation(newGlobalIndex);
        var newChunkHandle = targetArchetype.GetChunk(newChunkIndex);
        int newOffset = targetArchetype.Layout.GetEntityComponentOffset<T>(newIndexInChunk);
        System.Runtime.InteropServices.MemoryMarshal.Write(_chunkManager.GetBytes(newChunkHandle).Slice(newOffset), in value);
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

        var location = GetValidatedLocation(entity);

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
            RemoveFromCurrentArchetype(location);
            // Preserve entity version, just mark as having no archetype
            _entityManager.SetLocation(entity, new EntityLocation(entity.Version, -1, -1));
            return;
        }

        // Move entity to target archetype
        MoveEntity(entity, location, sourceArchetype, targetArchetype);
    }

    /// <summary>
    /// Creates a query builder for this world.
    /// </summary>
    /// <returns>A new query builder.</returns>
    public static QueryBuilder<TMask> Query()
    {
        return new QueryBuilder<TMask>();
    }

    private int MoveEntity(
        Entity entity,
        EntityLocation location,
        Archetype<TMask, TRegistry, TConfig> source,
        Archetype<TMask, TRegistry, TConfig> target)
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
            var movedEntity = GetEntityFromId(movedEntityId);
            var movedLocation = _entityManager.GetLocation(movedEntity);
            _entityManager.SetLocation(movedEntity, new EntityLocation(movedLocation.Version, movedLocation.ArchetypeId, oldGlobalIndex));
        }

        // Update the entity's location to the new archetype
        _entityManager.SetLocation(entity, new EntityLocation(location.Version, target.Id, newGlobalIndex));

        return newGlobalIndex;
    }

    private void CopySharedComponents(
        Archetype<TMask, TRegistry, TConfig> source,
        Archetype<TMask, TRegistry, TConfig> target,
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

        var srcBytes = _chunkManager.GetBytes(srcChunkHandle);
        var dstBytes = _chunkManager.GetBytes(dstChunkHandle);

        var action = new CopyComponentsAction<TMask, TRegistry, TConfig>
        {
            TypeInfos = TRegistry.TypeInfos,
            SrcLayout = srcLayout,
            DstLayout = dstLayout,
            SrcBytes = srcBytes,
            DstBytes = dstBytes,
            SrcIndexInChunk = srcIndexInChunk,
            DstIndexInChunk = dstIndexInChunk
        };
        sharedMask.ForEach(ref action);
    }

    private ref struct CopyComponentsAction<TM, TR, TC> : IBitAction
        where TM : unmanaged, IBitSet<TM>
        where TR : IComponentRegistry
        where TC : IConfig, new()
    {
        public ImmutableArray<ComponentTypeInfo> TypeInfos;
        public ImmutableArchetypeLayout<TM, TR, TC> SrcLayout;
        public ImmutableArchetypeLayout<TM, TR, TC> DstLayout;
        public Span<byte> SrcBytes;
        public Span<byte> DstBytes;
        public int SrcIndexInChunk;
        public int DstIndexInChunk;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(int bitIndex)
        {
            var info = TypeInfos[bitIndex];
            if (info.Size == 0)
                return; // Skip tag components

            int srcOffset = SrcLayout.GetEntityComponentOffset(SrcIndexInChunk, new ComponentId(bitIndex));
            int dstOffset = DstLayout.GetEntityComponentOffset(DstIndexInChunk, new ComponentId(bitIndex));

            var srcData = SrcBytes.Slice(srcOffset, info.Size);
            var dstData = DstBytes.Slice(dstOffset, info.Size);
            srcData.CopyTo(dstData);
        }
    }

    private EntityLocation GetValidatedLocation(Entity entity)
    {
        if (!entity.IsValid)
            throw new InvalidOperationException("Invalid entity handle.");

        if (!_entityManager.IsAlive(entity))
            throw new InvalidOperationException($"Entity {entity} is not alive.");

        var location = _entityManager.GetLocation(entity);

        // Initialize location if it was never set (no archetype yet)
        if (location.Version == 0)
        {
            // Never initialized - return invalid location with current entity's version
            return new EntityLocation(entity.Version, -1, -1);
        }

        return location;
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Wait for all in-flight operations to complete
        _operationGuard.WaitForCompletion();

        _archetypeRegistry.Dispose();
        _entityManager.Dispose();
    }
}

public static class ComponentsBuilderWorldExtensions
{
    extension<TBuilder>(TBuilder builder) where TBuilder : unmanaged, IComponentsBuilder
    {
        /// <summary>
        /// Builds the entity in the specified world.
        /// </summary>
        /// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
        /// <typeparam name="TRegistry">The component registry type.</typeparam>
        /// <typeparam name="TConfig">The world configuration type.</typeparam>
        /// <param name="world">The world to create the entity in.</param>
        /// <returns>The created entity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Build<TMask, TRegistry, TConfig>(World<TMask, TRegistry, TConfig> world)
            where TMask : unmanaged, IBitSet<TMask>
            where TRegistry : IComponentRegistry
            where TConfig : IConfig, new()
        {
            return world.CreateEntity(builder);
        }

        /// <summary>
        /// Overwrites all components on an existing entity with the builder's components.
        /// Any existing components are discarded. The entity must already exist and be alive.
        /// Used for deserialization or network synchronization.
        /// </summary>
        /// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
        /// <typeparam name="TRegistry">The component registry type.</typeparam>
        /// <typeparam name="TConfig">The world configuration type.</typeparam>
        /// <param name="entity">The existing entity handle.</param>
        /// <param name="world">The world containing the entity.</param>
        /// <returns>The entity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Overwrite<TMask, TRegistry, TConfig>(Entity entity, World<TMask, TRegistry, TConfig> world)
            where TMask : unmanaged, IBitSet<TMask>
            where TRegistry : IComponentRegistry
            where TConfig : IConfig, new()
        {
            return world.OverwriteEntity(entity, builder);
        }

        /// <summary>
        /// Adds the builder's components to an existing entity, preserving its current components.
        /// This is a structural change that moves the entity to a new archetype.
        /// </summary>
        /// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
        /// <typeparam name="TRegistry">The component registry type.</typeparam>
        /// <typeparam name="TConfig">The world configuration type.</typeparam>
        /// <param name="entity">The existing entity handle.</param>
        /// <param name="world">The world containing the entity.</param>
        /// <returns>The entity.</returns>
        /// <exception cref="InvalidOperationException">Entity already has one of the components being added.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity AddTo<TMask, TRegistry, TConfig>(Entity entity, World<TMask, TRegistry, TConfig> world)
            where TMask : unmanaged, IBitSet<TMask>
            where TRegistry : IComponentRegistry
            where TConfig : IConfig, new()
        {
            return world.AddComponents(entity, builder);
        }
    }
}
