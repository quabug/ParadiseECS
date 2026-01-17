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

    #region Static Abstract Members (Compile-time Structural Constraints)

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
    /// <remarks>
    /// <para>
    /// Each chunk stores entity IDs at the beginning, followed by component data arrays.
    /// The chunk layout is: [EntityIds × N][Component1 × N][Component2 × N]...
    /// </para>
    /// <para>
    /// Smaller values allow more entities per chunk but limit maximum entity count:
    /// <list type="bullet">
    ///   <item>1 byte: max 255 entities, 4KB chunk fits 4096 empty entities</item>
    ///   <item>2 bytes: max 65,535 entities, 4KB chunk fits 2048 empty entities</item>
    ///   <item>4 bytes: max ~2 billion entities, 16KB chunk fits 4096 empty entities</item>
    /// </list>
    /// </para>
    /// <para>
    /// The maximum entity ID is computed as: (1 &lt;&lt; (EntityIdByteSize * 8)) - 1,
    /// available via <see cref="Config{T}.MaxEntityId"/>.
    /// </para>
    /// </remarks>
    static abstract int EntityIdByteSize { get; }

    #endregion

    #region Instance Members (Runtime Configuration)

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

    #endregion
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

    #region Static Abstract Implementations (Compile-time Constraints)

    /// <inheritdoc />
    public static int ChunkSize => 16 * 1024;

    /// <inheritdoc />
    public static int MaxMetaBlocks => 1024;

    /// <inheritdoc />
    public static int EntityIdByteSize => sizeof(int);

    #endregion

    #region Instance Members (Runtime Configuration)

    /// <summary>
    /// Initial capacity for entity storage. Default: 1024.
    /// </summary>
    public int DefaultEntityCapacity { get; init; } = 1024;

    /// <summary>
    /// Initial capacity for chunk storage. Default: 256.
    /// </summary>
    public int DefaultChunkCapacity { get; init; } = 256;

    #endregion
}
