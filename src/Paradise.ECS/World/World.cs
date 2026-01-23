using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// The central ECS world that coordinates entities, components, and systems.
/// Owns all subsystems and provides a unified API for entity manipulation.
/// Single-threaded version without concurrent access support.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public sealed class World<TMask, TConfig>
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    private readonly ChunkManager _chunkManager;
    private readonly ArchetypeRegistry<TMask, TConfig> _archetypeRegistry;
    private readonly EntityManager _entityManager;
    private readonly ImmutableArray<ComponentTypeInfo> _typeInfos;
    private Archetype<TMask, TConfig> _emptyArchetype;

    /// <summary>
    /// Gets the number of currently alive entities.
    /// </summary>
    public int EntityCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _entityManager.AliveCount;
    }

    /// <summary>
    /// Gets all archetypes in this world.
    /// </summary>
    internal IReadOnlyList<Archetype<TMask, TConfig>?> Archetypes => _archetypeRegistry.Archetypes;

    /// <summary>
    /// Creates a new ECS world using the specified configuration and shared archetype metadata.
    /// The caller is responsible for disposing the shared metadata and chunk manager.
    /// </summary>
    /// <param name="config">The configuration instance with runtime settings.</param>
    /// <param name="sharedMetadata">The shared archetype metadata to use.</param>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    public World(TConfig config,
                 SharedArchetypeMetadata<TMask, TConfig> sharedMetadata,
                 ChunkManager chunkManager)
    {
        ArgumentNullException.ThrowIfNull(sharedMetadata);
        ArgumentNullException.ThrowIfNull(chunkManager);

        _chunkManager = chunkManager;
        _typeInfos = sharedMetadata.TypeInfos;
        _archetypeRegistry = new ArchetypeRegistry<TMask, TConfig>(sharedMetadata, chunkManager);
        _entityManager = new EntityManager(config.DefaultEntityCapacity);

        // Create the empty archetype for componentless entities
        _emptyArchetype = _archetypeRegistry.GetOrCreate(
            (HashedKey<TMask>)TMask.Empty);
    }

    /// <summary>
    /// Creates a new entity with no components.
    /// The entity is placed in the empty archetype.
    /// </summary>
    /// <returns>The created entity handle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity Spawn()
    {
        // Validate before creating to avoid inconsistent state if limit exceeded
        ThrowHelper.ThrowIfEntityIdExceedsLimit(_entityManager.PeekNextId(), Config<TConfig>.MaxEntityId, TConfig.EntityIdByteSize);
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
        var mask = TMask.Empty;
        builder.CollectTypes(ref mask);

        // Validate before creating to avoid inconsistent state if limit exceeded
        ThrowHelper.ThrowIfEntityIdExceedsLimit(_entityManager.PeekNextId(), Config<TConfig>.MaxEntityId, TConfig.EntityIdByteSize);

        // Create entity and place in target archetype (returns empty archetype if mask is empty)
        var entity = _entityManager.Create();
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
        var location = GetValidatedLocation(entity);

        // Collect component mask
        var mask = TMask.Empty;
        builder.CollectTypes(ref mask);

        // Remove from old archetype
        RemoveFromCurrentArchetype(location);

        // Get target archetype (returns empty archetype if mask is empty)
        var archetype = _archetypeRegistry.GetOrCreate((HashedKey<TMask>)mask);

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
        var newMask = TMask.Empty;
        builder.CollectTypes(ref newMask);

        if (newMask.IsEmpty)
            return entity;

        // Get current mask from entity's archetype
        var sourceArchetype = _archetypeRegistry.GetById(location.ArchetypeId)!;
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

        var targetArchetype = _archetypeRegistry.GetOrCreate((HashedKey<TMask>)targetMask);

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
        Archetype<TMask, TConfig> archetype,
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
        var archetype = _archetypeRegistry.GetById(location.ArchetypeId)!;
        int movedEntityId = archetype.RemoveEntity(location.GlobalIndex);

        // If an entity was moved during swap-remove, update its location
        if (movedEntityId >= 0)
        {
            var movedLocation = _entityManager.GetLocation(movedEntityId);
            _entityManager.SetLocation(movedEntityId, new EntityLocation(movedLocation.Version, movedLocation.ArchetypeId, location.GlobalIndex));
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAlive(Entity entity)
    {
        if (!entity.IsValid)
            return false;

        return _entityManager.IsAlive(entity);
    }

    /// <summary>
    /// Gets a component value from an entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <returns>The component value.</returns>
    /// <exception cref="InvalidOperationException">Entity doesn't have the component.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetComponent<T>(Entity entity) where T : unmanaged, IComponent
    {
        var (handle, offset) = GetComponentLocation<T>(entity);
        return _chunkManager.GetBytes(handle).GetRef<T>(offset);
    }

    /// <summary>
    /// Gets a reference to a component on an entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <returns>A reference to the component.</returns>
    /// <exception cref="InvalidOperationException">Entity doesn't have the component.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetComponentRef<T>(Entity entity) where T : unmanaged, IComponent
    {
        var (handle, offset) = GetComponentLocation<T>(entity);
        return ref _chunkManager.GetBytes(handle).GetRef<T>(offset);
    }

    /// <summary>
    /// Sets a component value on an entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <param name="value">The component value.</param>
    /// <exception cref="InvalidOperationException">Entity doesn't have the component.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetComponent<T>(Entity entity, T value) where T : unmanaged, IComponent
    {
        var (handle, offset) = GetComponentLocation<T>(entity);
        _chunkManager.GetBytes(handle).GetRef<T>(offset) = value;
    }

    /// <summary>
    /// Gets the chunk handle and offset for accessing a component on an entity.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <returns>A tuple containing the chunk handle and the byte offset of the component.</returns>
    /// <exception cref="InvalidOperationException">Entity is not alive or doesn't have the component.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (ChunkHandle Handle, int Offset) GetComponentLocation<T>(Entity entity) where T : unmanaged, IComponent
    {
        var location = GetValidatedLocation(entity);
        var archetype = _archetypeRegistry.GetById(location.ArchetypeId)
            ?? throw new InvalidOperationException($"Entity {entity} has no archetype.");

        var layout = archetype.Layout;
        if (!layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}.");

        var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);
        int offset = layout.GetEntityComponentOffset<T>(indexInChunk);
        return (chunkHandle, offset);
    }

    /// <summary>
    /// Checks if an entity has a specific component.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <returns>True if the entity has the component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasComponent<T>(Entity entity) where T : unmanaged, IComponent
    {
        if (!IsAlive(entity))
            return false;

        var location = _entityManager.GetLocation(entity.Id);
        var archetype = _archetypeRegistry.GetById(location.ArchetypeId)!;
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
        var sourceArchetype = _archetypeRegistry.GetById(location.ArchetypeId)!;

        // Check if already has component
        if (sourceArchetype.Layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T).Name}.");

        // Get target archetype using O(1) edge cache
        var targetArchetype = _archetypeRegistry.GetOrCreateWithAdd(sourceArchetype, T.TypeId);

        // Move entity to target archetype
        MoveEntity(entity, location, sourceArchetype, targetArchetype);

        // Write the new component value - get updated location
        var updatedLocation = _entityManager.GetLocation(entity.Id);
        var (newChunkIndex, newIndexInChunk) = targetArchetype.GetChunkLocation(updatedLocation.GlobalIndex);
        var newChunkHandle = targetArchetype.GetChunk(newChunkIndex);
        int newOffset = targetArchetype.Layout.GetEntityComponentOffset<T>(newIndexInChunk);
        _chunkManager.GetBytes(newChunkHandle).GetRef<T>(newOffset) = value;
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
        var sourceArchetype = _archetypeRegistry.GetById(location.ArchetypeId)!;

        // Check if has component
        if (!sourceArchetype.Layout.HasComponent<T>())
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T).Name}.");

        // Get target archetype using O(1) edge cache (returns empty archetype if removing last component)
        var targetArchetype = _archetypeRegistry.GetOrCreateWithRemove(sourceArchetype, T.TypeId);

        // Move entity to target archetype
        MoveEntity(entity, location, sourceArchetype, targetArchetype);
    }

    /// <summary>
    /// Gets the archetype registry for this world.
    /// Used for building queries via <see cref="QueryBuilder{TMask}"/>.
    /// </summary>
    public ArchetypeRegistry<TMask, TConfig> ArchetypeRegistry
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _archetypeRegistry;
    }

    /// <summary>
    /// Gets the component type information array for this world.
    /// </summary>
    public ImmutableArray<ComponentTypeInfo> TypeInfos
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _typeInfos;
    }

    /// <summary>
    /// Gets the chunk manager for this world.
    /// Used by generated queryable types for direct component access.
    /// </summary>
    public ChunkManager ChunkManager
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunkManager;
    }

    /// <summary>
    /// Gets the Entity handle for a given entity ID.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <returns>The Entity handle with current version.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Entity GetEntity(int entityId)
    {
        var location = _entityManager.GetLocation(entityId);
        return new Entity(entityId, location.Version);
    }

    private void MoveEntity(
        Entity entity,
        EntityLocation location,
        Archetype<TMask, TConfig> source,
        Archetype<TMask, TConfig> target)
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
            _entityManager.SetLocation(movedEntityId, new EntityLocation(movedLocation.Version, movedLocation.ArchetypeId, oldGlobalIndex));
        }

        // Update the entity's location to the new archetype
        _entityManager.SetLocation(entity.Id, new EntityLocation(location.Version, target.Id, newGlobalIndex));
    }

    private void CopySharedComponents(
        Archetype<TMask, TConfig> source,
        Archetype<TMask, TConfig> target,
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

        var typeInfos = _typeInfos;

        var action = new CopyComponentsAction<TMask, TConfig>
        {
            TypeInfos = typeInfos,
            SrcLayout = srcLayout,
            DstLayout = dstLayout,
            SrcBytes = srcBytes,
            DstBytes = dstBytes,
            SrcIndexInChunk = srcIndexInChunk,
            DstIndexInChunk = dstIndexInChunk
        };
        sharedMask.ForEach(ref action);
    }

    private ref struct CopyComponentsAction<TM, TC> : IBitAction
        where TM : unmanaged, IBitSet<TM>
        where TC : IConfig, new()
    {
        public ImmutableArray<ComponentTypeInfo> TypeInfos;
        public ImmutableArchetypeLayout<TM, TC> SrcLayout;
        public ImmutableArchetypeLayout<TM, TC> DstLayout;
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

            var componentId = new ComponentId(bitIndex);
            int srcBaseOffset = SrcLayout.GetBaseOffset(componentId);
            int dstBaseOffset = DstLayout.GetBaseOffset(componentId);

            int srcOffset = srcBaseOffset + SrcIndexInChunk * info.Size;
            int dstOffset = dstBaseOffset + DstIndexInChunk * info.Size;

            var srcData = SrcBytes.GetBytesAt(srcOffset, info.Size);
            var dstData = DstBytes.GetBytesAt(dstOffset, info.Size);
            srcData.CopyTo(dstData);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EntityLocation GetValidatedLocation(Entity entity)
    {
        if (!entity.IsValid)
            throw new InvalidOperationException("Invalid entity handle.");

        if (!_entityManager.IsAlive(entity))
            throw new InvalidOperationException($"Entity {entity} is not alive.");

        return _entityManager.GetLocation(entity.Id);
    }

    /// <summary>
    /// Gets the storage location of an entity.
    /// </summary>
    /// <param name="entity">The entity to get the location of.</param>
    /// <returns>The entity's location containing archetype ID and global index.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the entity is invalid or not alive.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityLocation GetLocation(Entity entity) => GetValidatedLocation(entity);

    /// <summary>
    /// Removes all entities from this world.
    /// After calling this method, all previously created entities are invalid.
    /// </summary>
    public void Clear()
    {
        _archetypeRegistry.Clear();
        _entityManager.Clear();

        // Re-create the empty archetype for componentless entities
        _emptyArchetype = _archetypeRegistry.GetOrCreate(
            (HashedKey<TMask>)TMask.Empty);
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
        /// <typeparam name="TConfig">The world configuration type.</typeparam>
        /// <param name="world">The world to create the entity in.</param>
        /// <returns>The created entity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Build<TMask, TConfig>(World<TMask, TConfig> world)
            where TMask : unmanaged, IBitSet<TMask>
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
        /// <typeparam name="TConfig">The world configuration type.</typeparam>
        /// <param name="entity">The existing entity handle.</param>
        /// <param name="world">The world containing the entity.</param>
        /// <returns>The entity.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Overwrite<TMask, TConfig>(Entity entity, World<TMask, TConfig> world)
            where TMask : unmanaged, IBitSet<TMask>
            where TConfig : IConfig, new()
        {
            return world.OverwriteEntity(entity, builder);
        }

        /// <summary>
        /// Adds the builder's components to an existing entity, preserving its current components.
        /// This is a structural change that moves the entity to a new archetype.
        /// </summary>
        /// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
        /// <typeparam name="TConfig">The world configuration type.</typeparam>
        /// <param name="entity">The existing entity handle.</param>
        /// <param name="world">The world containing the entity.</param>
        /// <returns>The entity.</returns>
        /// <exception cref="InvalidOperationException">Entity already has one of the components being added.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity AddTo<TMask, TConfig>(Entity entity, World<TMask, TConfig> world)
            where TMask : unmanaged, IBitSet<TMask>
            where TConfig : IConfig, new()
        {
            return world.AddComponents(entity, builder);
        }
    }
}

public static class QueryBuilderWorldExtensions
{
    extension<TMask>(QueryBuilder<TMask> builder) where TMask : unmanaged, IBitSet<TMask>
    {
        /// <summary>
        /// Builds a WorldQuery from this description, enabling WorldEntity enumeration.
        /// </summary>
        /// <typeparam name="TConfig">The world configuration type.</typeparam>
        /// <param name="world">The world to query.</param>
        /// <returns>A WorldQuery that iterates over WorldEntity handles.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WorldQuery<TMask, TConfig> Build<TConfig>(
            World<TMask, TConfig> world)
            where TConfig : IConfig, new()
        {
            var query = world.ArchetypeRegistry.GetOrCreateQuery(
                (HashedKey<ImmutableQueryDescription<TMask>>)builder.Description);
            return new WorldQuery<TMask, TConfig>(world, query);
        }

        /// <summary>
        /// Builds a WorldChunkQuery from this description, enabling chunk-level iteration with batch component access.
        /// </summary>
        /// <typeparam name="TConfig">The world configuration type.</typeparam>
        /// <param name="world">The world to query.</param>
        /// <returns>A WorldChunkQuery that iterates over WorldChunk instances.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WorldChunkQuery<TMask, TConfig> BuildChunk<TConfig>(
            World<TMask, TConfig> world)
            where TConfig : IConfig, new()
        {
            var query = world.ArchetypeRegistry.GetOrCreateQuery(
                (HashedKey<ImmutableQueryDescription<TMask>>)builder.Description);
            return new WorldChunkQuery<TMask, TConfig>(world, query);
        }
    }
}
