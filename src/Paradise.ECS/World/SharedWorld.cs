using System.Collections.Immutable;

namespace Paradise.ECS;

/// <summary>
/// Manages shared resources (ChunkManager, SharedArchetypeMetadata) that can be used across multiple worlds.
/// Disposing this instance will dispose all owned resources.
/// This type is not thread-safe; all operations must be called from the same thread.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
public sealed class SharedWorld<TMask, TConfig> : IDisposable
    where TMask : unmanaged, IBitSet<TMask>
    where TConfig : IConfig, new()
{
    private readonly List<World<TMask, TConfig>> _worlds = new();
    private readonly TConfig _config;
    private readonly ChunkManager _chunkManager;
    private readonly SharedArchetypeMetadata<TMask, TConfig> _sharedMetadata;
    private ThreadAffinity _threadAffinity;
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
    /// Gets the configuration instance.
    /// </summary>
    public TConfig Config => _config;

    /// <summary>
    /// Creates a new SharedWorld with the specified type information and default configuration.
    /// </summary>
    /// <param name="typeInfos">The component type information array from IComponentRegistry.TypeInfos.</param>
    public SharedWorld(ImmutableArray<ComponentTypeInfo> typeInfos)
        : this(typeInfos, new TConfig())
    {
    }

    /// <summary>
    /// Creates a new SharedWorld with the specified type information and configuration.
    /// </summary>
    /// <param name="typeInfos">The component type information array from IComponentRegistry.TypeInfos.</param>
    /// <param name="config">The configuration instance.</param>
    public SharedWorld(ImmutableArray<ComponentTypeInfo> typeInfos, TConfig config)
    {
        _config = config;
        _chunkManager = ChunkManager.Create(config);
        _sharedMetadata = new SharedArchetypeMetadata<TMask, TConfig>(typeInfos, config);
    }

    /// <summary>
    /// Creates a new World instance that uses this SharedWorld's resources.
    /// The created World does not own the shared resources - dispose the SharedWorld when done.
    /// </summary>
    /// <returns>A new World instance.</returns>
    public World<TMask, TConfig> CreateWorld()
    {
        _threadAffinity.Assert();
        ThrowHelper.ThrowIfDisposed(_disposed, this);
        var world = new World<TMask, TConfig>(_config, _sharedMetadata, _chunkManager);
        _worlds.Add(world);
        return world;
    }

    /// <summary>
    /// Disposes all owned resources including the ChunkManager and SharedArchetypeMetadata.
    /// </summary>
    public void Dispose()
    {
        _threadAffinity.Assert();
        if (_disposed)
            return;

        _disposed = true;
        foreach (var world in _worlds)
            world.Clear();
        _worlds.Clear();
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
    }
}
