namespace Paradise.ECS.Test;

/// <summary>
/// Tests for World lifecycle and basic operations.
/// </summary>
public sealed class WorldTests
{
    [Test]
    public async Task Constructor_CreatesValidWorld()
    {
        using var chunkManager = new ChunkManager();
        using var world = new World<Bit64, ComponentRegistry>(
            SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
            chunkManager);

        await Assert.That(world).IsNotNull();
        await Assert.That(world.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithCapacity_CreatesValidWorld()
    {
        using var chunkManager = new ChunkManager();
        using var world = new World<Bit64, ComponentRegistry>(
            SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
            chunkManager,
            initialEntityCapacity: 512);

        await Assert.That(world).IsNotNull();
        await Assert.That(world.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithChunkManager_CreatesValidWorld()
    {
        using var chunkManager = new ChunkManager();
        using var world = new World<Bit64, ComponentRegistry>(
            SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
            chunkManager);

        await Assert.That(world).IsNotNull();
        await Assert.That(world.ChunkManager).IsSameReferenceAs(chunkManager);
    }

    [Test]
    public async Task Constructor_NullChunkManager_Throws()
    {
        await Assert.That(() => new World<Bit64, ComponentRegistry>(
                SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
                null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_InvalidCapacity_Throws()
    {
        using var chunkManager1 = new ChunkManager();
        await Assert.That(() => new World<Bit64, ComponentRegistry>(
                SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
                chunkManager1,
                initialEntityCapacity: 0))
            .ThrowsExactly<ArgumentOutOfRangeException>();

        using var chunkManager2 = new ChunkManager();
        await Assert.That(() => new World<Bit64, ComponentRegistry>(
                SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
                chunkManager2,
                initialEntityCapacity: -1))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Dispose_MultipleTimes_DoesNotThrow()
    {
        using var chunkManager = new ChunkManager();
        using var world = new World<Bit64, ComponentRegistry>(
            SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
            chunkManager);

        await Assert.That(() =>
        {
            world.Dispose();
            world.Dispose();
        }).ThrowsNothing();
    }

    [Test]
    public async Task Dispose_PreventsNewOperations()
    {
        using var chunkManager = new ChunkManager();
        var world = new World<Bit64, ComponentRegistry>(
            SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
            chunkManager);
        world.Dispose();

        await Assert.That(world.Spawn).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_DoesNotDisposeChunkManager()
    {
        using var chunkManager = new ChunkManager();
        var world = new World<Bit64, ComponentRegistry>(
            SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
            chunkManager);

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
    public async Task Constructor_WithGlobalSharedMetadata_UsesIt()
    {
        using var chunkManager = new ChunkManager();
        using var world = new World<Bit64, ComponentRegistry>(
            SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
            chunkManager);

        await Assert.That(world.SharedMetadata)
            .IsSameReferenceAs(SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared);
    }

    [Test]
    public async Task Constructor_WithSharedMetadata_UsesProvidedMetadata()
    {
        using var customMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry>();
        using var chunkManager = new ChunkManager();
        using var world = new World<Bit64, ComponentRegistry>(customMetadata, chunkManager);

        await Assert.That(world.SharedMetadata).IsSameReferenceAs(customMetadata);
    }

    [Test]
    public async Task Constructor_NullSharedMetadata_Throws()
    {
        using var chunkManager = new ChunkManager();

        await Assert.That(() => new World<Bit64, ComponentRegistry>(null!, chunkManager))
            .ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task MultipleWorlds_SameSharedMetadata_ShareMetadata()
    {
        using var chunkManager1 = new ChunkManager();
        using var chunkManager2 = new ChunkManager();
        using var world1 = new World<Bit64, ComponentRegistry>(
            SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
            chunkManager1);
        using var world2 = new World<Bit64, ComponentRegistry>(
            SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
            chunkManager2);

        await Assert.That(world1.SharedMetadata).IsSameReferenceAs(world2.SharedMetadata);
    }

    [Test]
    public async Task MultipleWorlds_SameArchetypeMask_GetSameArchetypeId()
    {
        // Use isolated metadata to avoid interference from other tests
        using var sharedMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry>();
        using var chunkManager1 = new ChunkManager();
        using var chunkManager2 = new ChunkManager();
        using var world1 = new World<Bit64, ComponentRegistry>(sharedMetadata, chunkManager1);
        using var world2 = new World<Bit64, ComponentRegistry>(sharedMetadata, chunkManager2);

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
        using var metadata1 = new SharedArchetypeMetadata<Bit64, ComponentRegistry>();
        using var metadata2 = new SharedArchetypeMetadata<Bit64, ComponentRegistry>();
        using var chunkManager1 = new ChunkManager();
        using var chunkManager2 = new ChunkManager();
        using var world1 = new World<Bit64, ComponentRegistry>(metadata1, chunkManager1);
        using var world2 = new World<Bit64, ComponentRegistry>(metadata2, chunkManager2);

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
        using var metadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry>();
        using var chunkManager = new ChunkManager();

        var world = new World<Bit64, ComponentRegistry>(metadata, chunkManager);
        world.Dispose();

        // The metadata should still be usable after World is disposed
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var id = metadata.GetOrCreateArchetypeId(mask);
        await Assert.That(id).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task SharedMetadata_GraphEdges_SharedAcrossWorlds()
    {
        using var sharedMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry>();
        using var chunkManager1 = new ChunkManager();
        using var chunkManager2 = new ChunkManager();
        using var world1 = new World<Bit64, ComponentRegistry>(sharedMetadata, chunkManager1);
        using var world2 = new World<Bit64, ComponentRegistry>(sharedMetadata, chunkManager2);

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
