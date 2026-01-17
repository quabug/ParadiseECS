namespace Paradise.ECS;

/// <summary>
/// Static configuration interface for World parameters.
/// Uses static abstract members for compile-time resolution and JIT optimization.
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
    /// Initial capacity for entity storage.
    /// Default: 1024.
    /// </summary>
    static abstract int DefaultEntityCapacity { get; }

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
    /// <inheritdoc />
    public static int ChunkSize { get; } = 16 * 1024;

    /// <inheritdoc />
    public static int DefaultEntityCapacity { get; } = 1024;

    /// <inheritdoc />
    public static int MaxMetaBlocks { get; } = 1024;

    /// <inheritdoc />
    public static int EntityIdByteSize { get; } = sizeof(int);
}
