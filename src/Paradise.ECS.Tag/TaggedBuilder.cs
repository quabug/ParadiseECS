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
