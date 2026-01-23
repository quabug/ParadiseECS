namespace Paradise.ECS;

/// <summary>
/// Configuration interface for World parameters.
/// Uses static abstract members for compile-time structural constraints,
/// and instance members for runtime configuration like initial capacities.
/// </summary>
public interface IConfig
{
    /// <summary>
    /// Maximum supported archetype ID (20 bits = 1,048,575).
    /// </summary>
    public const int MaxArchetypeId = (1 << EdgeKey.ArchetypeBits) - 1;

    /// <summary>
    /// Maximum supported component type ID (11 bits = 2,047).
    /// </summary>
    public const int MaxComponentTypeId = (1 << EdgeKey.ComponentBits) - 1;

    /// <summary>
    /// Chunk memory block size in bytes.
    /// Should be a power of 2 for optimal memory alignment.
    /// Default: 16KB (optimized for L1 cache).
    /// </summary>
    static abstract int ChunkSize { get; }

    /// <summary>
    /// Maximum number of metadata blocks for chunk management.
    /// Each block can track 1024 chunk entries.
    /// Default: 1024 (supports up to ~1M chunks).
    /// </summary>
    static abstract int MaxMetaBlocks { get; }

    /// <summary>
    /// Size in bytes of an entity ID stored in chunk memory.
    /// Only the Entity.Id is stored, not the full Entity struct.
    /// Default: 4 bytes (sizeof(int)).
    /// </summary>
    static abstract int EntityIdByteSize { get; }

    /// <summary>
    /// Initial capacity for chunk storage (number of chunk slots to pre-allocate meta blocks for).
    /// This is a runtime hint that can vary per instance.
    /// Default: 256.
    /// </summary>
    int DefaultChunkCapacity { get; }

    /// <summary>
    /// Initial capacity for entity storage.
    /// This is a runtime hint that can vary per instance.
    /// Default: 1024.
    /// </summary>
    int DefaultEntityCapacity { get; }

    /// <summary>
    /// Memory allocator for chunk memory operations in <see cref="ChunkManager"/>.
    /// This is a runtime configuration that can vary per instance.
    /// Default: <see cref="NativeMemoryAllocator.Shared"/>.
    /// </summary>
    IAllocator ChunkAllocator { get; }

    /// <summary>
    /// Memory allocator for archetype metadata operations in <see cref="SharedArchetypeMetadata{TMask,TRegistry,TConfig}"/>.
    /// This is a runtime configuration that can vary per instance.
    /// Default: <see cref="NativeMemoryAllocator.Shared"/>.
    /// </summary>
    IAllocator MetadataAllocator { get; }

    /// <summary>
    /// Memory allocator for archetype layout data in <see cref="ImmutableArchetypeLayout{TMask,TRegistry,TConfig}"/>.
    /// This is a runtime configuration that can vary per instance.
    /// Default: <see cref="NativeMemoryAllocator.Shared"/>.
    /// </summary>
    IAllocator LayoutAllocator { get; }
}

/// <summary>
/// Computed configuration values derived from <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The world configuration type.</typeparam>
public static class Config<T> where T : IConfig
{
    /// <summary>
    /// Maximum entity ID that can be stored in EntityIdByteSize bytes.
    /// </summary>
    // ReSharper disable once StaticMemberInGenericType
    public static int MaxEntityId { get; } = T.EntityIdByteSize >= sizeof(int)
        ? int.MaxValue
        : (1 << (T.EntityIdByteSize * 8)) - 1;
}

/// <summary>
/// Default configuration optimized for typical game scenarios.
/// 16KB chunks sized for L1 cache, 1024 initial entity capacity.
/// </summary>
public readonly struct DefaultConfig : IConfig
{
    /// <summary>
    /// Creates a default configuration with standard settings.
    /// </summary>
    public DefaultConfig() { }

    /// <inheritdoc />
    public static int ChunkSize => 16 * 1024;

    /// <inheritdoc />
    public static int MaxMetaBlocks => 1024;

    /// <inheritdoc />
    public static int EntityIdByteSize => sizeof(int);

    /// <summary>
    /// Initial capacity for entity storage. Default: 1024.
    /// </summary>
    public int DefaultEntityCapacity { get; init; } = 1024;

    /// <summary>
    /// Initial capacity for chunk storage. Default: 256.
    /// </summary>
    public int DefaultChunkCapacity { get; init; } = 256;

    /// <summary>
    /// Memory allocator for chunk memory operations. Default: <see cref="NativeMemoryAllocator.Shared"/>.
    /// </summary>
    public IAllocator ChunkAllocator { get; init; } = NativeMemoryAllocator.Shared;

    /// <summary>
    /// Memory allocator for archetype metadata operations. Default: <see cref="NativeMemoryAllocator.Shared"/>.
    /// </summary>
    public IAllocator MetadataAllocator { get; init; } = NativeMemoryAllocator.Shared;

    /// <summary>
    /// Memory allocator for archetype layout data. Default: <see cref="NativeMemoryAllocator.Shared"/>.
    /// </summary>
    public IAllocator LayoutAllocator { get; init; } = NativeMemoryAllocator.Shared;
}
