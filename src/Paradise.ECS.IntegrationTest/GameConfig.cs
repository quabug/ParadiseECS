namespace Paradise.ECS.IntegrationTest;

/// <summary>
/// Game-specific ECS configuration.
/// Marked with [WorldDefault] to generate the World type alias.
/// </summary>
[DefaultConfig]
public readonly struct GameConfig : IConfig
{
    /// <summary>
    /// Creates default game configuration.
    /// </summary>
    public GameConfig() { }

    /// <inheritdoc />
    public static int ChunkSize => 16 * 1024; // 16KB chunks

    /// <inheritdoc />
    public static int MaxMetaBlocks => 1024;

    /// <inheritdoc />
    public static int EntityIdByteSize => sizeof(int);

    /// <inheritdoc />
    public int DefaultEntityCapacity { get; init; } = 1024;

    /// <inheritdoc />
    public int DefaultChunkCapacity { get; init; } = 256;

    /// <inheritdoc />
    public IAllocator ChunkAllocator { get; init; } = NativeMemoryAllocator.Shared;

    /// <inheritdoc />
    public IAllocator MetadataAllocator { get; init; } = NativeMemoryAllocator.Shared;

    /// <inheritdoc />
    public IAllocator LayoutAllocator { get; init; } = NativeMemoryAllocator.Shared;
}
