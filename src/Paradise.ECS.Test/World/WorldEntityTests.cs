namespace Paradise.ECS.Test;

/// <summary>
/// Tests for World entity lifecycle operations.
/// </summary>
public sealed class WorldEntityTests : IDisposable
{
    private readonly World<Bit64, ComponentRegistry> _world;

    public WorldEntityTests()
    {
        _world = new World<Bit64, ComponentRegistry>();
    }

    public void Dispose()
    {
        _world.Dispose();
    }

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
    public async Task Spawn_ExceedsInitialCapacity_ExpandsAutomatically()
    {
        // Create world with small capacity
        using var world = new World<Bit64, ComponentRegistry>(initialEntityCapacity: 4);

        // Spawn more entities than initial capacity
        var entities = new Entity[10];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = world.Spawn();
        }

        await Assert.That(world.EntityCount).IsEqualTo(10);

        // All entities should be alive
        foreach (var entity in entities)
        {
            await Assert.That(world.IsAlive(entity)).IsTrue();
        }
    }
}
