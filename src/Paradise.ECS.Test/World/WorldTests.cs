namespace Paradise.ECS.Test;

/// <summary>
/// Tests for World lifecycle and basic operations.
/// </summary>
public class WorldTests
{
    [Test]
    public async Task Constructor_Default_CreatesValidWorld()
    {
        using var world = new World<Bit64, ComponentRegistry>();

        await Assert.That(world).IsNotNull();
        await Assert.That(world.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithCapacity_CreatesValidWorld()
    {
        using var world = new World<Bit64, ComponentRegistry>(initialEntityCapacity: 512);

        await Assert.That(world).IsNotNull();
        await Assert.That(world.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithChunkManager_CreatesValidWorld()
    {
        using var chunkManager = new ChunkManager();
        using var world = new World<Bit64, ComponentRegistry>(chunkManager);

        await Assert.That(world).IsNotNull();
        await Assert.That(world.ChunkManager).IsSameReferenceAs(chunkManager);
    }

    [Test]
    public async Task Constructor_NullChunkManager_Throws()
    {
        await Assert.That(() => new World<Bit64, ComponentRegistry>(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_InvalidCapacity_Throws()
    {
        await Assert.That(() => new World<Bit64, ComponentRegistry>(initialEntityCapacity: 0))
            .Throws<ArgumentOutOfRangeException>();

        await Assert.That(() => new World<Bit64, ComponentRegistry>(initialEntityCapacity: -1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Dispose_MultipleTimes_DoesNotThrow()
    {
        var world = new World<Bit64, ComponentRegistry>();

        await Assert.That(() =>
        {
            world.Dispose();
            world.Dispose();
        }).ThrowsNothing();
    }

    [Test]
    public async Task Dispose_PreventsNewOperations()
    {
        var world = new World<Bit64, ComponentRegistry>();
        world.Dispose();

        await Assert.That(world.Spawn).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_OwnsChunkManager_DisposesIt()
    {
        // Create world with its own ChunkManager
        var world = new World<Bit64, ComponentRegistry>();
        var chunkManager = world.ChunkManager;

        world.Dispose();

        // The ChunkManager owned by World should be disposed
        await Assert.That(chunkManager.Allocate).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_SharedChunkManager_DoesNotDisposeIt()
    {
        using var chunkManager = new ChunkManager();
        var world = new World<Bit64, ComponentRegistry>(chunkManager);

        world.Dispose();

        // The shared ChunkManager should still be usable
        var handle = chunkManager.Allocate();
        await Assert.That(handle.IsValid).IsTrue();
    }
}
