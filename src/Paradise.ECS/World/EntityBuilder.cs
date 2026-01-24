using System.Runtime.CompilerServices;

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
    /// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
    /// <param name="mask">The mask to add component types to.</param>
    void CollectTypes<TMask>(ref TMask mask)
        where TMask : unmanaged, IBitSet<TMask>;

    /// <summary>
    /// Writes component data to the entity's chunk location.
    /// </summary>
    /// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <typeparam name="TChunkManager">The chunk manager type.</typeparam>
    /// <param name="chunkManager">The chunk manager for memory access.</param>
    /// <param name="layout">The archetype layout with component offsets.</param>
    /// <param name="chunkHandle">The chunk where data should be written.</param>
    /// <param name="indexInChunk">The entity's index within the chunk.</param>
    void WriteComponents<TMask, TConfig, TChunkManager>(
        TChunkManager chunkManager,
        ImmutableArchetypeLayout<TMask, TConfig> layout,
        ChunkHandle chunkHandle,
        int indexInChunk)
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
        where TChunkManager : IChunkManager;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityBuilder Create() => new();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CollectTypes<TMask>(ref TMask mask)
        where TMask : unmanaged, IBitSet<TMask>
    {
        // No components to add
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
    public TComponent Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        init;
    }

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
    public void CollectTypes<TMask>(ref TMask mask)
        where TMask : unmanaged, IBitSet<TMask>
    {
        InnerBuilder.CollectTypes(ref mask);
        mask = mask.Set(TComponent.TypeId);
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

        // Skip writes for zero-size tag components to avoid corrupting memory at offset 0.
        // Empty structs have sizeof=1 in C#, so writing default(TagComponent) would write
        // 1 byte at offset 0 (since GetEntityComponentOffset returns 0 for size-0 components).
        if (TComponent.Size == 0)
            return;

        // Write this component
        int offset = layout.GetBaseOffset(TComponent.TypeId) + indexInChunk * TComponent.Size;
        chunkManager.GetBytes(chunkHandle).GetRef<TComponent>(offset) = Value;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WithComponent<TComponent, TBuilder> Add<TComponent>(TComponent value = default)
            where TComponent : unmanaged, IComponent
        {
            return new WithComponent<TComponent, TBuilder>
            {
                Value = value,
                InnerBuilder = builder
            };
        }
    }
}
