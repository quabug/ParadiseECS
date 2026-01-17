namespace Paradise.ECS.Test;

/// <summary>
/// Tests for World entity lifecycle operations.
/// </summary>
public sealed class WorldEntityTests : IDisposable
{
    private readonly ChunkManager _chunkManager = new(NativeMemoryAllocator.Shared);
    private readonly SharedArchetypeMetadata<Bit64, ComponentRegistry> _sharedMetadata = new(NativeMemoryAllocator.Shared);
    private readonly World<Bit64, ComponentRegistry> _world;

    public WorldEntityTests()
    {
        _world = new World<Bit64, ComponentRegistry>(_sharedMetadata, _chunkManager);
    }

    public void Dispose()
    {
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
    }

    #region Spawn Tests

    [Test]
    public async Task Spawn_ReturnsValidEntity()
    {
        var entity = _world.Spawn();

        await Assert.That(entity.IsValid).IsTrue();
        await Assert.That(_world.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task Spawn_MultipleEntities_ReturnsUniqueIds()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        var e3 = _world.Spawn();

        await Assert.That(e1.Id).IsNotEqualTo(e2.Id);
        await Assert.That(e2.Id).IsNotEqualTo(e3.Id);
        await Assert.That(e1.Id).IsNotEqualTo(e3.Id);
        await Assert.That(_world.EntityCount).IsEqualTo(3);
    }

    [Test]
    public async Task Spawn_EntityHasVersion1()
    {
        var entity = _world.Spawn();

        await Assert.That(entity.Version).IsEqualTo(1u);
    }

    [Test]
    public async Task Spawn_ExceedsInitialCapacity_ExpandsAutomatically()
    {
        using var chunkManager = new ChunkManager(NativeMemoryAllocator.Shared);
        using var sharedMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry>(NativeMemoryAllocator.Shared);
        var world = new World<Bit64, ComponentRegistry>(sharedMetadata, chunkManager, initialEntityCapacity: 4);

        var entities = new Entity[10];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = world.Spawn();
        }

        await Assert.That(world.EntityCount).IsEqualTo(10);
        foreach (var entity in entities)
        {
            await Assert.That(world.IsAlive(entity)).IsTrue();
        }
    }

    #endregion

    #region IsAlive Tests

    [Test]
    public async Task IsAlive_ValidEntity_ReturnsTrue()
    {
        var entity = _world.Spawn();

        var isAlive = _world.IsAlive(entity);

        await Assert.That(isAlive).IsTrue();
    }

    [Test]
    public async Task IsAlive_InvalidEntity_ReturnsFalse()
    {
        var invalid = default(Entity);

        var isAlive = _world.IsAlive(invalid);

        await Assert.That(isAlive).IsFalse();
    }

    [Test]
    public async Task IsAlive_EntityInvalid_ReturnsFalse()
    {
        var isAlive = _world.IsAlive(Entity.Invalid);

        await Assert.That(isAlive).IsFalse();
    }

    #endregion

    #region Despawn Tests

    [Test]
    public async Task Despawn_ValidEntity_ReturnsTrue()
    {
        var entity = _world.Spawn();

        var result = _world.Despawn(entity);

        await Assert.That(result).IsTrue();
        await Assert.That(_world.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task Despawn_SameEntityTwice_SecondReturnsFalse()
    {
        var entity = _world.Spawn();

        var first = _world.Despawn(entity);
        var second = _world.Despawn(entity);

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
    }

    [Test]
    public async Task Despawn_InvalidEntity_ReturnsFalse()
    {
        var invalid = default(Entity);

        var result = _world.Despawn(invalid);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Despawn_MakesEntityNotAlive()
    {
        var entity = _world.Spawn();
        _world.Despawn(entity);

        var isAlive = _world.IsAlive(entity);

        await Assert.That(isAlive).IsFalse();
    }

    [Test]
    public async Task Spawn_AfterDespawn_ReusesId()
    {
        var e1 = _world.Spawn();
        var id1 = e1.Id;
        _world.Despawn(e1);

        var e2 = _world.Spawn();

        await Assert.That(e2.Id).IsEqualTo(id1);
        await Assert.That(e2.Version).IsGreaterThan(e1.Version);
    }

    [Test]
    public async Task Despawn_EntityWithComponents_Succeeds()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10, Y = 20, Z = 30 });

        var result = _world.Despawn(entity);

        await Assert.That(result).IsTrue();
        await Assert.That(_world.EntityCount).IsEqualTo(0);
    }

    #endregion

    #region Swap-Remove Tests

    [Test]
    public async Task Despawn_SwapRemove_UpdatesMovedEntityLocation()
    {
        // Create 3 entities in the same archetype
        var entityA = _world.Spawn();
        _world.AddComponent(entityA, new TestPosition { X = 100 });

        var entityB = _world.Spawn();
        _world.AddComponent(entityB, new TestPosition { X = 200 });

        var entityC = _world.Spawn();
        _world.AddComponent(entityC, new TestPosition { X = 300 });

        // Despawn entityA (index 0) - entityC should be swapped to index 0
        _world.Despawn(entityA);

        // Add a new entity that will take the old slot
        var entityD = _world.Spawn();
        _world.AddComponent(entityD, new TestPosition { X = 400 });

        // entityB, entityC, and entityD should all be alive
        await Assert.That(_world.IsAlive(entityB)).IsTrue();
        await Assert.That(_world.IsAlive(entityC)).IsTrue();
        await Assert.That(_world.IsAlive(entityD)).IsTrue();

        // The critical test: entityC's location should be updated after swap-remove
        float posB, posC, posD;
        using (var refB = _world.GetComponent<TestPosition>(entityB))
        {
            posB = refB.Value.X;
        }
        using (var refC = _world.GetComponent<TestPosition>(entityC))
        {
            posC = refC.Value.X;
        }
        using (var refD = _world.GetComponent<TestPosition>(entityD))
        {
            posD = refD.Value.X;
        }

        await Assert.That(posB).IsEqualTo(200f);
        await Assert.That(posC).IsEqualTo(300f);
        await Assert.That(posD).IsEqualTo(400f);
    }

    #endregion

    #region Clear Tests

    [Test]
    public async Task Clear_RemovesAllEntities()
    {
        var e1 = _world.Spawn();
        _ = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 1 });

        _world.Clear();

        await Assert.That(_world.EntityCount).IsEqualTo(0);
    }

    #endregion
}
