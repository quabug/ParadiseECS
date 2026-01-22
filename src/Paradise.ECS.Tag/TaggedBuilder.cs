using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A builder wrapper that ensures the entity has the EntityTags component with an empty mask.
/// Use this when creating entities via EntityBuilder that need to participate in the tag system.
/// </summary>
/// <typeparam name="TInner">The inner builder type.</typeparam>
/// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
/// <typeparam name="TTagMask">The tag mask type.</typeparam>
public readonly struct EnsureTagsBuilder<TInner, TEntityTags, TTagMask> : IComponentsBuilder
    where TInner : unmanaged, IComponentsBuilder
    where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    private readonly TInner _inner;

    /// <summary>
    /// Creates a new EnsureEntityTags builder wrapping the specified builder.
    /// </summary>
    /// <param name="inner">The inner builder to wrap.</param>
    public EnsureTagsBuilder(TInner inner)
    {
        _inner = inner;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CollectTypes<TBits>(ref ImmutableBitSet<TBits> mask) where TBits : unmanaged, IStorage
    {
        _inner.CollectTypes(ref mask);
        mask = mask.Set(TEntityTags.TypeId);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteComponents<TBits, TRegistry, TConfig, TChunkManager>(
        TChunkManager chunkManager,
        ImmutableArchetypeLayout<TBits, TRegistry, TConfig> layout,
        ChunkHandle chunkHandle,
        int indexInChunk)
        where TBits : unmanaged, IStorage
        where TRegistry : IComponentRegistry
        where TConfig : IConfig, new()
        where TChunkManager : IChunkManager
    {
        // Write inner components first
        _inner.WriteComponents(chunkManager, layout, chunkHandle, indexInChunk);

        // EntityTags with empty mask - chunk memory is already zeroed by allocator,
        // so no explicit write needed for default value
    }
}

/// <summary>
/// Builder that wraps an inner builder and adds a tag to the EntityTags component.
/// Created by calling the AddTag extension method on a builder.
/// Multiple AddTag calls can be chained, each ORing its tag bit into the EntityTags mask.
/// </summary>
/// <typeparam name="TTag">The tag type to add.</typeparam>
/// <typeparam name="TInnerBuilder">The wrapped builder type.</typeparam>
/// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
/// <typeparam name="TTagMask">The tag mask type.</typeparam>
public readonly struct WithTag<TTag, TInnerBuilder, TEntityTags, TTagMask> : IComponentsBuilder
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
    public void CollectTypes<TBits>(ref ImmutableBitSet<TBits> mask) where TBits : unmanaged, IStorage
    {
        InnerBuilder.CollectTypes(ref mask);
        mask = mask.Set(TEntityTags.TypeId);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteComponents<TBits, TRegistry, TConfig, TChunkManager>(
        TChunkManager chunkManager,
        ImmutableArchetypeLayout<TBits, TRegistry, TConfig> layout,
        ChunkHandle chunkHandle,
        int indexInChunk)
        where TBits : unmanaged, IStorage
        where TRegistry : IComponentRegistry
        where TConfig : IConfig, new()
        where TChunkManager : IChunkManager
    {
        // Write inner components first
        InnerBuilder.WriteComponents(chunkManager, layout, chunkHandle, indexInChunk);

        // OR this tag bit into the EntityTags mask
        // Since chunk memory is zeroed on allocation, the first WithTag ORs into zeros,
        // and subsequent WithTag builders OR into the accumulated value.
        int offset = layout.GetEntityComponentOffset<TEntityTags>(indexInChunk);
        ref var entityTags = ref chunkManager.GetBytes(chunkHandle).GetRef<TEntityTags>(offset);
        entityTags.Mask = entityTags.Mask.Set(TTag.TagId);
    }
}

/// <summary>
/// Extension methods for adding tag support to entity builders.
/// </summary>
public static class TaggedBuilderExtensions
{
    /// <summary>
    /// Ensures the entity has the EntityTags component with an empty mask.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
    /// <typeparam name="TTagMask">The tag mask type.</typeparam>
    /// <param name="builder">The builder to wrap.</param>
    /// <returns>An EnsureTagsBuilder that includes the EntityTags component with an empty mask.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EnsureTagsBuilder<TBuilder, TEntityTags, TTagMask> EnsureTags<TBuilder, TEntityTags, TTagMask>(
        this TBuilder builder)
        where TBuilder : unmanaged, IComponentsBuilder
        where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
        where TTagMask : unmanaged, IBitSet<TTagMask>
    {
        return new EnsureTagsBuilder<TBuilder, TEntityTags, TTagMask>(builder);
    }

    /// <summary>
    /// Ensures the entity has the EntityTags component with an empty mask.
    /// Uses the TaggedWorld to infer types - simpler than passing two default values.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
    /// <typeparam name="TRegistry">The component registry type.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
    /// <typeparam name="TTagMask">The tag mask type.</typeparam>
    /// <param name="builder">The builder to wrap.</param>
    /// <param name="world">The TaggedWorld (used only for type inference, not accessed).</param>
    /// <returns>An EnsureTagsBuilder that includes the EntityTags component with an empty mask.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EnsureTagsBuilder<TBuilder, TEntityTags, TTagMask> EnsureTags<TBuilder, TBits, TRegistry, TConfig, TEntityTags, TTagMask>(
        this TBuilder builder,
        TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> world)
        where TBuilder : unmanaged, IComponentsBuilder
        where TBits : unmanaged, IStorage
        where TRegistry : IComponentRegistry
        where TConfig : IConfig, new()
        where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
        where TTagMask : unmanaged, IBitSet<TTagMask>
    {
        return new EnsureTagsBuilder<TBuilder, TEntityTags, TTagMask>(builder);
    }

    /// <summary>
    /// Adds a single tag to the entity being built.
    /// Uses the TaggedWorld to infer EntityTags and TagMask types.
    /// </summary>
    /// <typeparam name="TBuilder">The builder type.</typeparam>
    /// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
    /// <typeparam name="TRegistry">The component registry type.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
    /// <typeparam name="TTagMask">The tag mask type.</typeparam>
    /// <typeparam name="TTag">The tag type to add.</typeparam>
    /// <param name="builder">The builder to wrap.</param>
    /// <param name="tag">A dummy value for type inference (use default).</param>
    /// <param name="world">The TaggedWorld (used only for type inference, not accessed).</param>
    /// <returns>A WithTag builder that includes the EntityTags component with the specified tag.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static WithTag<TTag, TBuilder, TEntityTags, TTagMask> AddTag<TBuilder, TBits, TRegistry, TConfig, TEntityTags, TTagMask, TTag>(
        this TBuilder builder,
        TTag tag,
        TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> world)
        where TBuilder : unmanaged, IComponentsBuilder
        where TBits : unmanaged, IStorage
        where TRegistry : IComponentRegistry
        where TConfig : IConfig, new()
        where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
        where TTagMask : unmanaged, IBitSet<TTagMask>
        where TTag : ITag
    {
        return new WithTag<TTag, TBuilder, TEntityTags, TTagMask> { InnerBuilder = builder };
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
        public Entity Build<TBits, TRegistry, TConfig, TEntityTags, TTagMask>(
            TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> taggedWorld)
            where TBits : unmanaged, IStorage
            where TRegistry : IComponentRegistry
            where TConfig : IConfig, new()
            where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
            where TTagMask : unmanaged, IBitSet<TTagMask>
        {
            var b = builder.EnsureTags<TBuilder, TEntityTags, TTagMask>();
            return taggedWorld.World.CreateEntity(b);
        }

        /// <summary>
        /// Overwrites all components on an existing entity with the builder's components.
        /// Preserves the EntityTags component and its tag mask.
        /// </summary>
        public Entity Overwrite<TBits, TRegistry, TConfig, TEntityTags, TTagMask>(
            Entity entity,
            TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> taggedWorld)
            where TBits : unmanaged, IStorage
            where TRegistry : IComponentRegistry
            where TConfig : IConfig, new()
            where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
            where TTagMask : unmanaged, IBitSet<TTagMask>
        {
            var b = builder.EnsureTags<TBuilder, TEntityTags, TTagMask>();
            return taggedWorld.World.OverwriteEntity(entity, b);
        }

        /// <summary>
        /// Adds the builder's components to an existing entity, preserving its current components.
        /// Delegates to the underlying World.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity AddTo<TBits, TRegistry, TConfig, TEntityTags, TTagMask>(
            Entity entity,
            TaggedWorld<TBits, TRegistry, TConfig, TEntityTags, TTagMask> taggedWorld)
            where TBits : unmanaged, IStorage
            where TRegistry : IComponentRegistry
            where TConfig : IConfig, new()
            where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
            where TTagMask : unmanaged, IBitSet<TTagMask>
        {
            return taggedWorld.World.AddComponents(entity, builder);
        }
    }
}
