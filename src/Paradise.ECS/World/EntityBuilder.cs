namespace Paradise.ECS;

/// <summary>
/// Interface for component builders used in fluent entity creation.
/// Each builder collects component types and writes component data.
/// </summary>
public interface IComponentsBuilder
{
    /// <summary>
    /// Collects component type IDs into the component mask.
    /// </summary>
    /// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
    /// <param name="mask">The mask to add component types to.</param>
    void CollectTypes<TBits>(ref ImmutableBitSet<TBits> mask)
        where TBits : unmanaged, IStorage;

    /// <summary>
    /// Writes component data to the entity's chunk location.
    /// </summary>
    /// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
    /// <typeparam name="TRegistry">The component registry type.</typeparam>
    /// <param name="chunkManager">The chunk manager for memory access.</param>
    /// <param name="layout">The archetype layout with component offsets.</param>
    /// <param name="chunkHandle">The chunk where data should be written.</param>
    /// <param name="indexInChunk">The entity's index within the chunk.</param>
    void WriteComponents<TBits, TRegistry>(
        ChunkManager chunkManager,
        ImmutableArchetypeLayout<TBits, TRegistry> layout,
        ChunkHandle chunkHandle,
        int indexInChunk)
        where TBits : unmanaged, IStorage
        where TRegistry : IComponentRegistry;
}

/// <summary>
/// Base builder for creating entities with no initial components.
/// Start entity creation with <see cref="Create"/>.
/// </summary>
public readonly struct EntityBuilder : IComponentsBuilder
{
    /// <summary>
    /// Creates a new empty entity builder.
    /// </summary>
    /// <returns>A new entity builder.</returns>
    public static EntityBuilder Create() => default;

    /// <inheritdoc/>
    public void CollectTypes<TBits>(ref ImmutableBitSet<TBits> mask)
        where TBits : unmanaged, IStorage
    {
        // No components to add
    }

    /// <inheritdoc/>
    public void WriteComponents<TBits, TRegistry>(
        ChunkManager chunkManager,
        ImmutableArchetypeLayout<TBits, TRegistry> layout,
        ChunkHandle chunkHandle,
        int indexInChunk)
        where TBits : unmanaged, IStorage
        where TRegistry : IComponentRegistry
    {
        // No components to write
    }
}

/// <summary>
/// Builder that wraps an inner builder and adds a component value.
/// Created by calling the Add extension method on a builder.
/// </summary>
/// <typeparam name="TComponent">The component type to add.</typeparam>
/// <typeparam name="TInnerBuilder">The wrapped builder type.</typeparam>
public readonly struct WithComponent<TComponent, TInnerBuilder> : IComponentsBuilder
    where TComponent : unmanaged, IComponent
    where TInnerBuilder : unmanaged, IComponentsBuilder
{
    /// <summary>
    /// The component value to add.
    /// </summary>
    public TComponent Value { get; init; }

    /// <summary>
    /// The inner builder that this wraps.
    /// </summary>
    public TInnerBuilder InnerBuilder { get; init; }

    /// <inheritdoc/>
    public void CollectTypes<TBits>(ref ImmutableBitSet<TBits> mask)
        where TBits : unmanaged, IStorage
    {
        InnerBuilder.CollectTypes(ref mask);
        mask = mask.Set(TComponent.TypeId);
    }

    /// <inheritdoc/>
    public void WriteComponents<TBits, TRegistry>(
        ChunkManager chunkManager,
        ImmutableArchetypeLayout<TBits, TRegistry> layout,
        ChunkHandle chunkHandle,
        int indexInChunk)
        where TBits : unmanaged, IStorage
        where TRegistry : IComponentRegistry
    {
        // Write inner components first
        InnerBuilder.WriteComponents(chunkManager, layout, chunkHandle, indexInChunk);

        // Write this component
        int offset = layout.GetEntityComponentOffset<TComponent>(indexInChunk);
        using var chunk = chunkManager.Get(chunkHandle);
        chunk.GetRef<TComponent>(offset) = Value;
    }
}

/// <summary>
/// Extension providing fluent Add methods for component builders.
/// </summary>
public static class ComponentsBuilderExtensions
{
    extension<TBuilder>(TBuilder builder)
        where TBuilder : unmanaged, IComponentsBuilder
    {
        /// <summary>
        /// Adds a component to the entity being built.
        /// </summary>
        /// <typeparam name="TComponent">The component type to add.</typeparam>
        /// <param name="value">The component value.</param>
        /// <returns>A new builder with the component added.</returns>
        public WithComponent<TComponent, TBuilder> Add<TComponent>(TComponent value = default)
            where TComponent : unmanaged, IComponent
        {
            return new WithComponent<TComponent, TBuilder>
            {
                Value = value,
                InnerBuilder = builder
            };
        }

        /// <summary>
        /// Builds the entity in the specified world.
        /// </summary>
        /// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
        /// <typeparam name="TRegistry">The component registry type.</typeparam>
        /// <param name="world">The world to create the entity in.</param>
        /// <returns>The created entity.</returns>
        public Entity Build<TBits, TRegistry>(World<TBits, TRegistry> world)
            where TBits : unmanaged, IStorage
            where TRegistry : IComponentRegistry
        {
            return world.CreateEntity(builder);
        }

        /// <summary>
        /// Overwrites all components on an existing entity with the builder's components.
        /// Any existing components are discarded. The entity must already exist and be alive.
        /// Used for deserialization or network synchronization.
        /// </summary>
        /// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
        /// <typeparam name="TRegistry">The component registry type.</typeparam>
        /// <param name="entity">The existing entity handle.</param>
        /// <param name="world">The world containing the entity.</param>
        /// <returns>The entity.</returns>
        public Entity Overwrite<TBits, TRegistry>(Entity entity, World<TBits, TRegistry> world)
            where TBits : unmanaged, IStorage
            where TRegistry : IComponentRegistry
        {
            return world.OverwriteEntity(entity, builder);
        }

        /// <summary>
        /// Adds the builder's components to an existing entity, preserving its current components.
        /// This is a structural change that moves the entity to a new archetype.
        /// </summary>
        /// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
        /// <typeparam name="TRegistry">The component registry type.</typeparam>
        /// <param name="entity">The existing entity handle.</param>
        /// <param name="world">The world containing the entity.</param>
        /// <returns>The entity.</returns>
        /// <exception cref="InvalidOperationException">Entity already has one of the components being added.</exception>
        public Entity AddTo<TBits, TRegistry>(Entity entity, World<TBits, TRegistry> world)
            where TBits : unmanaged, IStorage
            where TRegistry : IComponentRegistry
        {
            return world.AddComponents(entity, builder);
        }
    }
}
