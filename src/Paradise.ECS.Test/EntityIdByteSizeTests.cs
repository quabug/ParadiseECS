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
        using var chunkManager = ChunkManager.Create(new ByteEntityIdConfig());
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
        using var chunkManager = ChunkManager.Create(new ByteEntityIdConfig());
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
        using var chunkManager = ChunkManager.Create(new ByteEntityIdConfig());
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
        using var chunkManager = ChunkManager.Create(new ShortEntityIdConfig());
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
        using var chunkManager = ChunkManager.Create(new ByteEntityIdConfig());
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

    #region EntityId Storage Correctness Tests (Regression for sizeof(int) overwrite bug)

    [Test]
    public async Task ByteConfig_MultipleEntityIdsStoredCorrectly_NoDataCorruption()
    {
        // This test verifies that entity IDs are stored using EntityIdByteSize bytes,
        // not sizeof(int). If sizeof(int) is used, writing entity ID at index N
        // would overwrite entity ID at index N+1/N+2/N+3, corrupting data.
        using var chunkManager = ChunkManager.Create(new ByteEntityIdConfig());
        using var sharedMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry, ByteEntityIdConfig>(new ByteEntityIdConfig());
        var registry = new ArchetypeRegistry<Bit64, ComponentRegistry, ByteEntityIdConfig>(sharedMetadata, chunkManager);

        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId.Value);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var archetype = registry.GetOrCreate(hashedKey);

        // Allocate multiple entities with consecutive IDs
        var entities = new Entity[10];
        for (int i = 0; i < 10; i++)
        {
            entities[i] = new Entity(i, 1);
            archetype.AllocateEntity(entities[i]);
        }

        // Verify all entity IDs are stored correctly by removing entities
        // and checking returned movedEntityId
        // Remove first entity - entity 9 should be moved to position 0
        int movedId = archetype.RemoveEntity(0);
        await Assert.That(movedId).IsEqualTo(9);

        // Remove second entity (was at index 1, now holds entity 1) - entity 8 should be moved
        movedId = archetype.RemoveEntity(1);
        await Assert.That(movedId).IsEqualTo(8);

        // Verify count
        await Assert.That(archetype.EntityCount).IsEqualTo(8);
    }

    [Test]
    public async Task ShortConfig_MultipleEntityIdsStoredCorrectly_NoDataCorruption()
    {
        // Similar test for 2-byte EntityIdByteSize
        using var chunkManager = ChunkManager.Create(new ShortEntityIdConfig());
        using var sharedMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry, ShortEntityIdConfig>(new ShortEntityIdConfig());
        var registry = new ArchetypeRegistry<Bit64, ComponentRegistry, ShortEntityIdConfig>(sharedMetadata, chunkManager);

        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId.Value);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var archetype = registry.GetOrCreate(hashedKey);

        // Allocate multiple entities with consecutive IDs
        var entities = new Entity[20];
        for (int i = 0; i < 20; i++)
        {
            entities[i] = new Entity(i, 1);
            archetype.AllocateEntity(entities[i]);
        }

        // Remove first entity - entity 19 should be moved to position 0
        int movedId = archetype.RemoveEntity(0);
        await Assert.That(movedId).IsEqualTo(19);

        // Verify correct swap-remove behavior
        movedId = archetype.RemoveEntity(1);
        await Assert.That(movedId).IsEqualTo(18);

        await Assert.That(archetype.EntityCount).IsEqualTo(18);
    }

    [Test]
    public async Task ByteConfig_SwapRemoveCopiesCorrectBytes_NoDataCorruption()
    {
        // Test specifically for the swap-remove copy path using sizeof(int)
        // When entity at middle is removed, last entity's ID should be copied correctly
        using var chunkManager = ChunkManager.Create(new ByteEntityIdConfig());
        using var sharedMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry, ByteEntityIdConfig>(new ByteEntityIdConfig());
        var registry = new ArchetypeRegistry<Bit64, ComponentRegistry, ByteEntityIdConfig>(sharedMetadata, chunkManager);

        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId.Value);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var archetype = registry.GetOrCreate(hashedKey);

        // Allocate 5 entities
        for (int i = 0; i < 5; i++)
        {
            archetype.AllocateEntity(new Entity(i, 1));
        }

        // Remove entity at index 2 (entity ID 2), entity 4 should move to index 2
        int movedId = archetype.RemoveEntity(2);
        await Assert.That(movedId).IsEqualTo(4);

        // Remove entity at index 1 (entity ID 1), entity 3 should move to index 1
        movedId = archetype.RemoveEntity(1);
        await Assert.That(movedId).IsEqualTo(3);

        // Now we have entities [0, 3, 4] at indices [0, 1, 2]
        // Remove entity at index 0, entity at index 2 (entity ID 4) should move
        movedId = archetype.RemoveEntity(0);
        await Assert.That(movedId).IsEqualTo(4);

        await Assert.That(archetype.EntityCount).IsEqualTo(2);
    }

    [Test]
    public async Task ByteConfig_EntityIdOffsetCalculation_IsCorrect()
    {
        // Verify that GetEntityIdOffset uses EntityIdByteSize
        // ByteConfig: EntityIdByteSize = 1
        // Expected offsets: entity 0 -> 0, entity 1 -> 1, entity 2 -> 2, etc.
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId.Value);
        var layoutData = ImmutableArchetypeLayout<Bit64, ComponentRegistry, ByteEntityIdConfig>.Create(
            NativeMemoryAllocator.Shared, mask);
        try
        {
            int offset0 = ImmutableArchetypeLayout<Bit64, ComponentRegistry, ByteEntityIdConfig>.GetEntityIdOffset(0);
            int offset1 = ImmutableArchetypeLayout<Bit64, ComponentRegistry, ByteEntityIdConfig>.GetEntityIdOffset(1);
            int offset2 = ImmutableArchetypeLayout<Bit64, ComponentRegistry, ByteEntityIdConfig>.GetEntityIdOffset(2);

            await Assert.That(offset0).IsEqualTo(0);
            await Assert.That(offset1).IsEqualTo(1); // ByteEntityIdConfig.EntityIdByteSize = 1
            await Assert.That(offset2).IsEqualTo(2);
        }
        finally
        {
            ImmutableArchetypeLayout<Bit64, ComponentRegistry, ByteEntityIdConfig>.Free(NativeMemoryAllocator.Shared, layoutData);
        }
    }

    [Test]
    public async Task ShortConfig_EntityIdOffsetCalculation_IsCorrect()
    {
        // Verify that GetEntityIdOffset uses EntityIdByteSize
        // ShortConfig: EntityIdByteSize = 2
        // Expected offsets: entity 0 -> 0, entity 1 -> 2, entity 2 -> 4, etc.
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId.Value);
        var layoutData = ImmutableArchetypeLayout<Bit64, ComponentRegistry, ShortEntityIdConfig>.Create(
            NativeMemoryAllocator.Shared, mask);
        try
        {
            int offset0 = ImmutableArchetypeLayout<Bit64, ComponentRegistry, ShortEntityIdConfig>.GetEntityIdOffset(0);
            int offset1 = ImmutableArchetypeLayout<Bit64, ComponentRegistry, ShortEntityIdConfig>.GetEntityIdOffset(1);
            int offset2 = ImmutableArchetypeLayout<Bit64, ComponentRegistry, ShortEntityIdConfig>.GetEntityIdOffset(2);

            await Assert.That(offset0).IsEqualTo(0);
            await Assert.That(offset1).IsEqualTo(2); // ShortEntityIdConfig.EntityIdByteSize = 2
            await Assert.That(offset2).IsEqualTo(4);
        }
        finally
        {
            ImmutableArchetypeLayout<Bit64, ComponentRegistry, ShortEntityIdConfig>.Free(NativeMemoryAllocator.Shared, layoutData);
        }
    }

    #endregion
}
