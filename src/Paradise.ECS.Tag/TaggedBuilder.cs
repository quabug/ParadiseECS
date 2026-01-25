using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Builder that wraps an inner builder and adds a tag to the EntityTags component.
/// Created by calling the AddTag extension method on a builder.
/// Multiple AddTag calls can be chained, each ORing its tag bit into the EntityTags mask.
/// </summary>
/// <typeparam name="TTag">The tag type to add.</typeparam>
/// <typeparam name="TInnerBuilder">The wrapped builder type.</typeparam>
/// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
/// <typeparam name="TTagMask">The tag mask type.</typeparam>
public readonly struct WithTagBuilder<TTag, TInnerBuilder, TEntityTags, TTagMask> : IComponentsBuilder
    where TTag : ITag
    where TInnerBuilder : unmanaged, IComponentsBuilder
    where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    /// <summary>
    /// The inner builder that this wraps.
    /// </summary>
    public TInnerBuilder InnerBuilder
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        init;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CollectTypes<TMask>(ref TMask mask) where TMask : unmanaged, IBitSet<TMask>
    {
        InnerBuilder.CollectTypes(ref mask);
        mask = mask.Set(TEntityTags.TypeId);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteComponents<TMask, TConfig, TChunkManager>(
        TChunkManager chunkManager,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        ChunkHandle chunkHandle,
        int indexInChunk)
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
        where TChunkManager : IChunkManager
    {
        // Write inner components first
        InnerBuilder.WriteComponents(chunkManager, layout, chunkHandle, indexInChunk);

        // OR this tag bit into the EntityTags mask
        // Since chunk memory is zeroed on allocation, the first WithTagBuilder ORs into zeros,
        // and subsequent WithTagBuilder builders OR into the accumulated value.
        int offset = layout.GetBaseOffset(TEntityTags.TypeId) + indexInChunk * TEntityTags.Size;
        ref var entityTags = ref chunkManager.GetBytes(chunkHandle).GetRef<TEntityTags>(offset);
        entityTags.Mask = entityTags.Mask.Set(TTag.TagId);
    }

    /// <summary>
    /// Adds another tag to the entity being built.
    /// </summary>
    /// <typeparam name="TNewTag">The new tag type to add.</typeparam>
    /// <param name="tag">A dummy value for type inference (use default).</param>
    /// <returns>A new WithTagBuilder that includes both tags.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WithTagBuilder<TNewTag, WithTagBuilder<TTag, TInnerBuilder, TEntityTags, TTagMask>, TEntityTags, TTagMask> AddTag<TNewTag>(TNewTag tag)
        where TNewTag : ITag
    {
        return new WithTagBuilder<TNewTag, WithTagBuilder<TTag, TInnerBuilder, TEntityTags, TTagMask>, TEntityTags, TTagMask>
        {
            InnerBuilder = this
        };
    }
}

/// <summary>
/// Extension methods for adding tag support to entity builders.
/// </summary>
public static class TaggedBuilderExtensions
{
    /// <param name="builder">The builder to wrap.</param>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    extension<TBuilder>(TBuilder builder) where TBuilder : unmanaged, IComponentsBuilder
    {
        /// <summary>
        /// Adds a single tag to the entity being built.
        /// Uses the TaggedWorld to infer EntityTags and TagMask types.
        /// </summary>
        /// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
        /// <typeparam name="TConfig">The world configuration type.</typeparam>
        /// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
        /// <typeparam name="TTagMask">The tag mask type.</typeparam>
        /// <typeparam name="TTag">The tag type to add.</typeparam>
        /// <param name="tag">A dummy value for type inference (use default).</param>
        /// <param name="world">The TaggedWorld (used only for type inference, not accessed).</param>
        /// <returns>A WithTagBuilder that includes the EntityTags component with the specified tag.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WithTagBuilder<TTag, TBuilder, TEntityTags, TTagMask> AddTag<TMask, TConfig, TEntityTags, TTagMask, TTag>(
            TTag tag,
            TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> world)
            where TMask : unmanaged, IBitSet<TMask>
            where TConfig : IConfig, new()
            where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
            where TTagMask : unmanaged, IBitSet<TTagMask>
            where TTag : ITag
        {
            return new WithTagBuilder<TTag, TBuilder, TEntityTags, TTagMask> { InnerBuilder = builder };
        }
    }
}

/// <summary>
/// Extension methods for building entities in a TaggedWorld.
/// </summary>
public static class ComponentsBuilderTaggedWorldExtensions
{
    extension<TBuilder>(TBuilder builder) where TBuilder : unmanaged, IComponentsBuilder
    {
        /// <summary>
        /// Builds the entity in the specified TaggedWorld.
        /// Delegates to the underlying World.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Build<TMask, TConfig, TEntityTags, TTagMask>(
            TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> taggedWorld)
            where TMask : unmanaged, IBitSet<TMask>
            where TConfig : IConfig, new()
            where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
            where TTagMask : unmanaged, IBitSet<TTagMask>
        {
            return taggedWorld.World.CreateEntity(builder);
        }

        /// <summary>
        /// Overwrites all components on an existing entity with the builder's components.
        /// </summary>
        public Entity Overwrite<TMask, TConfig, TEntityTags, TTagMask>(
            Entity entity,
            TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> taggedWorld)
            where TMask : unmanaged, IBitSet<TMask>
            where TConfig : IConfig, new()
            where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
            where TTagMask : unmanaged, IBitSet<TTagMask>
        {
            return taggedWorld.World.OverwriteEntity(entity, builder);
        }

        /// <summary>
        /// Adds the builder's components to an existing entity, preserving its current components.
        /// Delegates to the underlying World.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity AddTo<TMask, TConfig, TEntityTags, TTagMask>(
            Entity entity,
            TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> taggedWorld)
            where TMask : unmanaged, IBitSet<TMask>
            where TConfig : IConfig, new()
            where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
            where TTagMask : unmanaged, IBitSet<TTagMask>
        {
            return taggedWorld.World.AddComponents(entity, builder);
        }
    }
}
