namespace Paradise.ECS.Test;

/// <summary>
/// Configuration with 1-byte entity IDs (max 255 entities).
/// </summary>
public readonly struct ByteEntityIdConfig : IConfig
{
    public ByteEntityIdConfig() { }

    // Static abstract implementations (compile-time constraints)
    public static int ChunkSize => 4 * 1024; // 4KB for smaller test footprint
    public static int MaxMetaBlocks => 16;
    public static int EntityIdByteSize => sizeof(byte);

    // Instance members (runtime configuration)
    public int DefaultEntityCapacity { get; init; } = 64;
    public int DefaultChunkCapacity { get; init; } = 16;
    public IAllocator ChunkAllocator { get; init; } = NativeMemoryAllocator.Shared;
    public IAllocator MetadataAllocator { get; init; } = NativeMemoryAllocator.Shared;
    public IAllocator LayoutAllocator { get; init; } = NativeMemoryAllocator.Shared;
}

/// <summary>
/// Configuration with 2-byte entity IDs (max 65535 entities).
/// </summary>
public readonly struct ShortEntityIdConfig : IConfig
{
    public ShortEntityIdConfig() { }

    // Static abstract implementations (compile-time constraints)
    public static int ChunkSize => 4 * 1024;
    public static int MaxMetaBlocks => 64;
    public static int EntityIdByteSize => sizeof(ushort);

    // Instance members (runtime configuration)
    public int DefaultEntityCapacity { get; init; } = 256;
    public int DefaultChunkCapacity { get; init; } = 32;
    public IAllocator ChunkAllocator { get; init; } = NativeMemoryAllocator.Shared;
    public IAllocator MetadataAllocator { get; init; } = NativeMemoryAllocator.Shared;
    public IAllocator LayoutAllocator { get; init; } = NativeMemoryAllocator.Shared;
}

/// <summary>
/// Tests for different EntityIdByteSize configurations.
/// </summary>
public sealed class EntityIdByteSizeTests
{
    #region MaxEntityId Computation Tests

    [Test]
    public async Task MaxEntityId_ByteConfig_Returns255()
    {
        var maxEntityId = Config<ByteEntityIdConfig>.MaxEntityId;

        await Assert.That(maxEntityId).IsEqualTo(255);
    }

    [Test]
    public async Task MaxEntityId_ShortConfig_Returns65535()
    {
        var maxEntityId = Config<ShortEntityIdConfig>.MaxEntityId;

        await Assert.That(maxEntityId).IsEqualTo(65535);
    }

    [Test]
    public async Task MaxEntityId_DefaultConfig_ReturnsIntMaxValue()
    {
        var maxEntityId = Config<DefaultConfig>.MaxEntityId;

        await Assert.That(maxEntityId).IsEqualTo(int.MaxValue);
    }

    #endregion

    #region ByteEntityIdConfig Tests

    [Test]
    public async Task ByteConfig_SpawnUpToLimit_Succeeds()
    {
        using var chunkManager = new ChunkManager<ByteEntityIdConfig>(new ByteEntityIdConfig());
        using var sharedMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry, ByteEntityIdConfig>(new ByteEntityIdConfig());
        var world = new World<Bit64, ComponentRegistry, ByteEntityIdConfig>(new ByteEntityIdConfig(), sharedMetadata, chunkManager);

        // Spawn 256 entities (IDs 0-255)
        var entities = new Entity[256];
        for (int i = 0; i < 256; i++)
        {
            entities[i] = world.Spawn();
        }

        await Assert.That(world.EntityCount).IsEqualTo(256);
        await Assert.That(entities[255].Id).IsEqualTo(255);
    }

    [Test]
    public async Task ByteConfig_SpawnBeyondLimit_ThrowsInvalidOperationException()
    {
        using var chunkManager = new ChunkManager<ByteEntityIdConfig>(new ByteEntityIdConfig());
        using var sharedMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry, ByteEntityIdConfig>(new ByteEntityIdConfig());
        var world = new World<Bit64, ComponentRegistry, ByteEntityIdConfig>(new ByteEntityIdConfig(), sharedMetadata, chunkManager);

        // Spawn 256 entities (IDs 0-255) - this should succeed
        for (int i = 0; i < 256; i++)
        {
            world.Spawn();
        }

        // Spawning the 257th entity (ID 256) should throw
        await Assert.That(world.Spawn).Throws<InvalidOperationException>()
            .WithMessageContaining("Entity ID 256 exceeds maximum of 255", StringComparison.Ordinal);
    }

    [Test]
    public async Task ByteConfig_SpawnAfterDespawn_ReusesIdWithinLimit()
    {
        using var chunkManager = new ChunkManager<ByteEntityIdConfig>(new ByteEntityIdConfig());
        using var sharedMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry, ByteEntityIdConfig>(new ByteEntityIdConfig());
        var world = new World<Bit64, ComponentRegistry, ByteEntityIdConfig>(new ByteEntityIdConfig(), sharedMetadata, chunkManager);

        // Spawn 256 entities
        var entities = new Entity[256];
        for (int i = 0; i < 256; i++)
        {
            entities[i] = world.Spawn();
        }

        // Despawn one entity
        world.Despawn(entities[100]);

        // Spawning another should reuse the freed ID, not exceed the limit
        var newEntity = world.Spawn();

        await Assert.That(newEntity.Id).IsEqualTo(100);
        await Assert.That(newEntity.Version).IsGreaterThan(entities[100].Version);
        await Assert.That(world.EntityCount).IsEqualTo(256);
    }

    #endregion

    #region ShortEntityIdConfig Tests

    [Test]
    public async Task ShortConfig_SpawnWithinLimit_Succeeds()
    {
        using var chunkManager = new ChunkManager<ShortEntityIdConfig>(new ShortEntityIdConfig());
        using var sharedMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry, ShortEntityIdConfig>(new ShortEntityIdConfig());
        var world = new World<Bit64, ComponentRegistry, ShortEntityIdConfig>(new ShortEntityIdConfig(), sharedMetadata, chunkManager);

        // Spawn 1000 entities - well within the 65535 limit
        for (int i = 0; i < 1000; i++)
        {
            world.Spawn();
        }

        await Assert.That(world.EntityCount).IsEqualTo(1000);
    }

    #endregion

    #region CreateEntity with Builder Tests

    [Test]
    public async Task ByteConfig_CreateEntityBeyondLimit_ThrowsInvalidOperationException()
    {
        using var chunkManager = new ChunkManager<ByteEntityIdConfig>(new ByteEntityIdConfig());
        using var sharedMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry, ByteEntityIdConfig>(new ByteEntityIdConfig());
        var world = new World<Bit64, ComponentRegistry, ByteEntityIdConfig>(new ByteEntityIdConfig(), sharedMetadata, chunkManager);

        // Spawn 256 entities (IDs 0-255)
        for (int i = 0; i < 256; i++)
        {
            world.Spawn();
        }

        // Creating entity with builder should also throw
        var builder = EntityBuilder.Create().Add(new TestPosition());
        await Assert.That(() => builder.Build(world))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("Entity ID 256 exceeds maximum of 255", StringComparison.Ordinal);
    }

    #endregion

    #region Archetype EntitiesPerChunk Tests

    [Test]
    public async Task EmptyArchetype_ByteConfig_EntitiesPerChunkEqualsChunkSizeDividedByEntityIdByteSize()
    {
        // ByteEntityIdConfig: ChunkSize=4096, EntityIdByteSize=1
        // Expected: 4096 / 1 = 4096 entities per chunk
        var layoutData = ImmutableArchetypeLayout<Bit64, ComponentRegistry, ByteEntityIdConfig>.Create(
            NativeMemoryAllocator.Shared,
            ImmutableBitSet<Bit64>.Empty);
        try
        {
            var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry, ByteEntityIdConfig>(layoutData);
            int entitiesPerChunk = layout.EntitiesPerChunk;
            int expected = ByteEntityIdConfig.ChunkSize / ByteEntityIdConfig.EntityIdByteSize;

            await Assert.That(entitiesPerChunk).IsEqualTo(expected);
            await Assert.That(entitiesPerChunk).IsEqualTo(4096);
        }
        finally
        {
            ImmutableArchetypeLayout<Bit64, ComponentRegistry, ByteEntityIdConfig>.Free(NativeMemoryAllocator.Shared, layoutData);
        }
    }

    [Test]
    public async Task EmptyArchetype_ShortConfig_EntitiesPerChunkEqualsChunkSizeDividedByEntityIdByteSize()
    {
        // ShortEntityIdConfig: ChunkSize=4096, EntityIdByteSize=2
        // Expected: 4096 / 2 = 2048 entities per chunk
        var layoutData = ImmutableArchetypeLayout<Bit64, ComponentRegistry, ShortEntityIdConfig>.Create(
            NativeMemoryAllocator.Shared,
            ImmutableBitSet<Bit64>.Empty);
        try
        {
            var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry, ShortEntityIdConfig>(layoutData);
            int entitiesPerChunk = layout.EntitiesPerChunk;
            int expected = ShortEntityIdConfig.ChunkSize / ShortEntityIdConfig.EntityIdByteSize;

            await Assert.That(entitiesPerChunk).IsEqualTo(expected);
            await Assert.That(entitiesPerChunk).IsEqualTo(2048);
        }
        finally
        {
            ImmutableArchetypeLayout<Bit64, ComponentRegistry, ShortEntityIdConfig>.Free(NativeMemoryAllocator.Shared, layoutData);
        }
    }

    [Test]
    public async Task EmptyArchetype_DefaultConfig_EntitiesPerChunkEqualsChunkSizeDividedByEntityIdByteSize()
    {
        // DefaultConfig: ChunkSize=16384, EntityIdByteSize=4
        // Expected: 16384 / 4 = 4096 entities per chunk
        var layoutData = ImmutableArchetypeLayout<Bit64, ComponentRegistry, DefaultConfig>.Create(
            NativeMemoryAllocator.Shared,
            ImmutableBitSet<Bit64>.Empty);
        try
        {
            var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry, DefaultConfig>(layoutData);
            int entitiesPerChunk = layout.EntitiesPerChunk;
            int expected = DefaultConfig.ChunkSize / DefaultConfig.EntityIdByteSize;

            await Assert.That(entitiesPerChunk).IsEqualTo(expected);
            await Assert.That(entitiesPerChunk).IsEqualTo(4096);
        }
        finally
        {
            ImmutableArchetypeLayout<Bit64, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, layoutData);
        }
    }

    [Test]
    public async Task ArchetypeWithComponent_ByteConfigFitsMoreEntitiesThanShortConfig()
    {
        // With same ChunkSize but smaller EntityIdByteSize, more entities should fit
        // TestPosition is 12 bytes (3 floats)
        // ByteConfig: per entity = 1 + 12 = 13 bytes -> 4096 / 13 = 315 entities
        // ShortConfig: per entity = 2 + 12 = 14 bytes -> 4096 / 14 = 292 entities
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId.Value);

        var byteLayoutData = ImmutableArchetypeLayout<Bit64, ComponentRegistry, ByteEntityIdConfig>.Create(
            NativeMemoryAllocator.Shared, mask);
        var shortLayoutData = ImmutableArchetypeLayout<Bit64, ComponentRegistry, ShortEntityIdConfig>.Create(
            NativeMemoryAllocator.Shared, mask);
        try
        {
            var byteLayout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry, ByteEntityIdConfig>(byteLayoutData);
            var shortLayout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry, ShortEntityIdConfig>(shortLayoutData);
            int byteEntitiesPerChunk = byteLayout.EntitiesPerChunk;
            int shortEntitiesPerChunk = shortLayout.EntitiesPerChunk;

            // ByteConfig should fit more entities due to smaller entity ID storage
            await Assert.That(byteEntitiesPerChunk).IsGreaterThan(shortEntitiesPerChunk);
        }
        finally
        {
            ImmutableArchetypeLayout<Bit64, ComponentRegistry, ByteEntityIdConfig>.Free(NativeMemoryAllocator.Shared, byteLayoutData);
            ImmutableArchetypeLayout<Bit64, ComponentRegistry, ShortEntityIdConfig>.Free(NativeMemoryAllocator.Shared, shortLayoutData);
        }
    }

    #endregion
}
