using System.Collections.Immutable;

namespace Paradise.ECS;

/// <summary>
/// Manages shared resources (ChunkManager, SharedArchetypeMetadata, ChunkTagRegistry) that can be used across multiple tagged worlds.
/// Disposing this instance will dispose all owned resources.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
/// <typeparam name="TEntityTags">The EntityTags component type.</typeparam>
/// <typeparam name="TTagMask">The tag mask type.</typeparam>
public sealed class SharedTaggedWorld<TMask, TConfig, TEntityTags, TTagMask> : IDisposable
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
    where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
    where TTagMask : unmanaged, IBitSet<TTagMask>
{
    private readonly List<TaggedWorld<TMask, TConfig, TEntityTags, TTagMask>> _worlds = new();
    private readonly TConfig _config;
    private readonly ChunkManager _chunkManager;
    private readonly SharedArchetypeMetadata<TMask, TConfig> _sharedMetadata;
    private readonly ChunkTagRegistry<TTagMask> _chunkTagRegistry;
    private bool _disposed;

    /// <summary>
    /// Gets the chunk manager shared across all worlds.
    /// </summary>
    public ChunkManager ChunkManager => _chunkManager;

    /// <summary>
    /// Gets the shared archetype metadata.
    /// </summary>
    public SharedArchetypeMetadata<TMask, TConfig> SharedMetadata => _sharedMetadata;

    /// <summary>
    /// Gets the chunk tag registry shared across all worlds.
    /// </summary>
    public ChunkTagRegistry<TTagMask> ChunkTagRegistry => _chunkTagRegistry;

    /// <summary>
    /// Gets the configuration instance.
    /// </summary>
    public TConfig Config => _config;

    /// <summary>
    /// Creates a new SharedTaggedWorld with the specified type information and default configuration.
    /// </summary>
    /// <param name="typeInfos">The component type information array from IComponentRegistry.TypeInfos.</param>
    public SharedTaggedWorld(ImmutableArray<ComponentTypeInfo> typeInfos)
        : this(typeInfos, new TConfig())
    {
    }

    /// <summary>
    /// Creates a new SharedTaggedWorld with the specified type information and configuration.
    /// </summary>
    /// <param name="typeInfos">The component type information array from IComponentRegistry.TypeInfos.</param>
    /// <param name="config">The configuration instance.</param>
    public SharedTaggedWorld(ImmutableArray<ComponentTypeInfo> typeInfos, TConfig config)
    {
        _config = config;
        _chunkManager = ChunkManager.Create(config);
        _sharedMetadata = new SharedArchetypeMetadata<TMask, TConfig>(typeInfos, config);
        _chunkTagRegistry = new ChunkTagRegistry<TTagMask>(
            config.ChunkAllocator,
            TConfig.MaxMetaBlocks,
            TConfig.ChunkSize);
    }

    /// <summary>
    /// Creates a new TaggedWorld instance that uses this SharedTaggedWorld's resources.
    /// The created World does not own the shared resources - dispose the SharedTaggedWorld when done.
    /// </summary>
    /// <returns>A new TaggedWorld instance.</returns>
    public TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> CreateWorld()
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        var world = new TaggedWorld<TMask, TConfig, TEntityTags, TTagMask>(
            _config,
            _chunkManager,
            _sharedMetadata,
            _chunkTagRegistry);
        _worlds.Add(world);
        return world;
    }

    /// <summary>
    /// Clears all created worlds and disposes all owned resources including the ChunkManager, SharedArchetypeMetadata, and ChunkTagRegistry.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var world in _worlds)
            world.Clear();
        _worlds.Clear();
        _chunkTagRegistry.Dispose();
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
    }
}
