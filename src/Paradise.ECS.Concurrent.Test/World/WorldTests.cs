namespace Paradise.ECS.Concurrent.Test;

/// <summary>
/// Tests for World lifecycle and basic operations.
/// </summary>
public sealed class WorldTests
{
    [Test]
    public async Task Constructor_CreatesValidWorld()
    {
        var config = new DefaultConfig();
        using var chunkManager = ChunkManager.Create(config);
        using var sharedMetadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        using var world = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, chunkManager);

        await Assert.That(world).IsNotNull();
        await Assert.That(world.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithCapacity_CreatesValidWorld()
    {
        var config = new DefaultConfig { DefaultEntityCapacity = 512 };
        using var chunkManager = ChunkManager.Create(config);
        using var sharedMetadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        using var world = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, chunkManager);

        await Assert.That(world).IsNotNull();
        await Assert.That(world.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithChunkManager_CreatesValidWorld()
    {
        var config = new DefaultConfig();
        using var chunkManager = ChunkManager.Create(config);
        using var sharedMetadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        using var world = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, chunkManager);

        await Assert.That(world).IsNotNull();
        await Assert.That(world.ChunkManager).IsSameReferenceAs(chunkManager);
    }

    [Test]
    public async Task Constructor_NullChunkManager_Throws()
    {
        var config = new DefaultConfig();
        using var sharedMetadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);

        await Assert.That(() => new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_ZeroOrNegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        // With hybrid config design, invalid capacities throw rather than using defaults
        var config0 = new DefaultConfig { DefaultEntityCapacity = 0 };
        using var chunkManager1 = ChunkManager.Create(config0);
        using var sharedMetadata1 = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config0);
        await Assert.That(() =>
        {
            using var world = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config0, sharedMetadata1, chunkManager1);
        }).Throws<ArgumentOutOfRangeException>();

        var configNeg = new DefaultConfig { DefaultEntityCapacity = -1 };
        using var chunkManager2 = ChunkManager.Create(configNeg);
        using var sharedMetadata2 = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(configNeg);
        await Assert.That(() =>
        {
            using var world = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(configNeg, sharedMetadata2, chunkManager2);
        }).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Dispose_MultipleTimes_DoesNotThrow()
    {
        var config = new DefaultConfig();
        using var chunkManager = ChunkManager.Create(config);
        using var sharedMetadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        using var world = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, chunkManager);

        await Assert.That(() =>
        {
            world.Dispose();
            world.Dispose();
        }).ThrowsNothing();
    }

    [Test]
    public async Task Dispose_PreventsNewOperations()
    {
        var config = new DefaultConfig();
        using var chunkManager = ChunkManager.Create(config);
        using var sharedMetadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        var world = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, chunkManager);
        world.Dispose();

        await Assert.That(world.Spawn).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_DoesNotDisposeChunkManager()
    {
        var config = new DefaultConfig();
        using var chunkManager = ChunkManager.Create(config);
        using var sharedMetadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        var world = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, chunkManager);

        world.Dispose();

        // ChunkManager should still be usable after World is disposed
        var handle = chunkManager.Allocate();
        await Assert.That(handle.IsValid).IsTrue();
    }
}

/// <summary>
/// Tests for multi-world sharing with SharedArchetypeMetadata.
/// </summary>
public sealed class WorldSharedMetadataTests
{
    [Test]
    public async Task Constructor_WithSharedMetadata_UsesProvidedMetadata()
    {
        var config = new DefaultConfig();
        using var customMetadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        using var chunkManager = ChunkManager.Create(config);
        using var world = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, customMetadata, chunkManager);

        await Assert.That(world.SharedMetadata).IsSameReferenceAs(customMetadata);
    }

    [Test]
    public async Task Constructor_NullSharedMetadata_Throws()
    {
        var config = new DefaultConfig();
        using var chunkManager = ChunkManager.Create(config);

        await Assert.That(() => new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, null!, chunkManager))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task MultipleWorlds_SameSharedMetadata_ShareMetadata()
    {
        var config = new DefaultConfig();
        using var sharedMetadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        using var chunkManager1 = ChunkManager.Create(config);
        using var chunkManager2 = ChunkManager.Create(config);
        using var world1 = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, chunkManager1);
        using var world2 = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, chunkManager2);

        await Assert.That(world1.SharedMetadata).IsSameReferenceAs(world2.SharedMetadata);
    }

    [Test]
    public async Task MultipleWorlds_SameArchetypeMask_GetSameArchetypeId()
    {
        // Use isolated metadata to avoid interference from other tests
        var config = new DefaultConfig();
        using var sharedMetadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        using var chunkManager1 = ChunkManager.Create(config);
        using var chunkManager2 = ChunkManager.Create(config);
        using var world1 = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, chunkManager1);
        using var world2 = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, chunkManager2);

        // Create entity with Position in world1
        var entity1 = world1.Spawn();
        world1.AddComponent<TestPosition>(entity1);

        // Create entity with Position in world2
        var entity2 = world2.Spawn();
        world2.AddComponent<TestPosition>(entity2);

        // Both archetypes should have the same ID since they share metadata
        var archetype1 = world1.ArchetypeRegistry.GetById(0);
        var archetype2 = world2.ArchetypeRegistry.GetById(0);

        await Assert.That(archetype1).IsNotNull();
        await Assert.That(archetype2).IsNotNull();
        await Assert.That(archetype1!.Id).IsEqualTo(archetype2!.Id);
        await Assert.That(archetype1.Layout.ComponentMask).IsEqualTo(archetype2.Layout.ComponentMask);
    }

    [Test]
    public async Task MultipleWorlds_IsolatedMetadata_HaveIndependentArchetypeIds()
    {
        var config = new DefaultConfig();
        using var metadata1 = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        using var metadata2 = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        using var chunkManager1 = ChunkManager.Create(config);
        using var chunkManager2 = ChunkManager.Create(config);
        using var world1 = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, metadata1, chunkManager1);
        using var world2 = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, metadata2, chunkManager2);

        // Both worlds get archetype ID 0 for Position since they have separate metadata
        var entity1 = world1.Spawn();
        world1.AddComponent<TestPosition>(entity1);

        var entity2 = world2.Spawn();
        world2.AddComponent<TestVelocity>(entity2);

        // World1 has Position at ID 0, World2 has Velocity at ID 0
        var archetype1 = world1.ArchetypeRegistry.GetById(0);
        var archetype2 = world2.ArchetypeRegistry.GetById(0);

        await Assert.That(archetype1).IsNotNull();
        await Assert.That(archetype2).IsNotNull();

        // Both have ID 0 but different component masks
        await Assert.That(archetype1!.Id).IsEqualTo(0);
        await Assert.That(archetype2!.Id).IsEqualTo(0);
        await Assert.That(archetype1.Layout.ComponentMask).IsNotEqualTo(archetype2.Layout.ComponentMask);
    }

    [Test]
    public async Task Dispose_DoesNotDisposeSharedMetadata()
    {
        var config = new DefaultConfig();
        using var metadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        using var chunkManager = ChunkManager.Create(config);

        var world = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, metadata, chunkManager);
        world.Dispose();

        // The metadata should still be usable after World is disposed
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var id = metadata.GetOrCreateArchetypeId(mask);
        await Assert.That(id).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task SharedMetadata_GraphEdges_SharedAcrossWorlds()
    {
        var config = new DefaultConfig();
        using var sharedMetadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config);
        using var chunkManager1 = ChunkManager.Create(config);
        using var chunkManager2 = ChunkManager.Create(config);
        using var world1 = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, chunkManager1);
        using var world2 = new World<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(config, sharedMetadata, chunkManager2);

        // Create {Position} archetype in world1
        var entity1 = world1.Spawn();
        world1.AddComponent<TestPosition>(entity1);

        // Add Velocity to get {Position, Velocity} in world1 - this creates edge
        world1.AddComponent<TestVelocity>(entity1);

        // In world2, the edge should already exist
        var entity2 = world2.Spawn();
        world2.AddComponent<TestPosition>(entity2);
        world2.AddComponent<TestVelocity>(entity2);

        // Both entities should now be in archetypes with the same ID
        await Assert.That(world1.HasComponent<TestPosition>(entity1)).IsTrue();
        await Assert.That(world1.HasComponent<TestVelocity>(entity1)).IsTrue();
        await Assert.That(world2.HasComponent<TestPosition>(entity2)).IsTrue();
        await Assert.That(world2.HasComponent<TestVelocity>(entity2)).IsTrue();

        // Verify the archetype count reflects edge reuse
        await Assert.That(sharedMetadata.ArchetypeCount).IsEqualTo(2); // {Position} and {Position, Velocity}
    }
}
