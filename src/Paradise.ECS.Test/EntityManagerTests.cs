namespace Paradise.ECS.Test;

public class EntityManagerTests : IDisposable
{
    private readonly EntityManager _manager;

    public EntityManagerTests()
    {
        _manager = new EntityManager();
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }

    [Test]
    public async Task Create_ReturnsValidEntity()
    {
        var entity = _manager.Create();
        await Assert.That(entity.IsValid).IsTrue();
        await Assert.That(entity.Id).IsGreaterThanOrEqualTo(0);
        await Assert.That(entity.Version).IsGreaterThanOrEqualTo(0u);
    }

    [Test]
    public async Task Create_MultipleEntities_HaveUniqueIds()
    {
        var entity1 = _manager.Create();
        var entity2 = _manager.Create();
        var entity3 = _manager.Create();

        await Assert.That(entity1.Id).IsNotEqualTo(entity2.Id);
        await Assert.That(entity2.Id).IsNotEqualTo(entity3.Id);
        await Assert.That(entity1.Id).IsNotEqualTo(entity3.Id);
    }

    [Test]
    public async Task IsAlive_NewEntity_ReturnsTrue()
    {
        var entity = _manager.Create();
        var isAlive = _manager.IsAlive(entity);
        await Assert.That(isAlive).IsTrue();
    }

    [Test]
    public async Task IsAlive_InvalidEntity_ReturnsFalse()
    {
        var isAlive = _manager.IsAlive(Entity.Invalid);
        await Assert.That(isAlive).IsFalse();
    }

    [Test]
    public async Task Destroy_MakesEntityNotAlive()
    {
        var entity = _manager.Create();
        _manager.Destroy(entity);
        var isAlive = _manager.IsAlive(entity);
        await Assert.That(isAlive).IsFalse();
    }

    [Test]
    public async Task Destroy_InvalidEntity_DoesNotThrow()
    {
        // Should not throw
        _manager.Destroy(Entity.Invalid);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    [Test]
    public async Task Destroy_AlreadyDestroyedEntity_DoesNotThrow()
    {
        var entity = _manager.Create();
        _manager.Destroy(entity);
        // Destroy again - should not throw
        _manager.Destroy(entity);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    [Test]
    public async Task Destroy_IncreasesVersion()
    {
        var entity1 = _manager.Create();
        int firstId = entity1.Id;
        uint firstVersion = entity1.Version;

        _manager.Destroy(entity1);
        var entity2 = _manager.Create(); // Should reuse the slot

        // If same ID (recycled), version should be incremented
        if (entity2.Id == firstId)
        {
            await Assert.That(entity2.Version).IsGreaterThan(firstVersion);
        }
    }

    [Test]
    public async Task Create_AfterDestroy_ReusesSlot()
    {
        var entity1 = _manager.Create();
        int id1 = entity1.Id;

        _manager.Destroy(entity1);
        var entity2 = _manager.Create();

        // Should reuse the freed slot
        await Assert.That(entity2.Id).IsEqualTo(id1);
        await Assert.That(entity2.Version).IsGreaterThan(entity1.Version);
    }

    [Test]
    public async Task AliveCount_IncrementsOnCreate()
    {
        int initialCount = _manager.AliveCount;
        _manager.Create();
        _manager.Create();
        int newCount = _manager.AliveCount;

        await Assert.That(newCount).IsEqualTo(initialCount + 2);
    }

    [Test]
    public async Task AliveCount_DecrementsOnDestroy()
    {
        var entity1 = _manager.Create();
        _ = _manager.Create(); // entity2 - just to have multiple entities
        int countAfterCreate = _manager.AliveCount;

        _manager.Destroy(entity1);
        int countAfterDestroy = _manager.AliveCount;

        await Assert.That(countAfterDestroy).IsEqualTo(countAfterCreate - 1);
    }

    [Test]
    public async Task AliveCount_ConsistentAfterCreateAndDestroy()
    {
        int initialCount = _manager.AliveCount;

        var entity = _manager.Create();
        _manager.Destroy(entity);
        int finalCount = _manager.AliveCount;

        await Assert.That(finalCount).IsEqualTo(initialCount);
    }

    [Test]
    public async Task Create_BeyondInitialCapacity_ExpandsAutomatically()
    {
        using var manager = new EntityManager();

        // Create many entities to test expansion
        for (int i = 0; i < 100; i++)
        {
            var entity = manager.Create();
            await Assert.That(entity.IsValid).IsTrue();
        }

        await Assert.That(manager.AliveCount).IsEqualTo(100);
    }

    [Test]
    public async Task Capacity_GrowsAsNeeded()
    {
        using var manager = new EntityManager();

        int initialCapacity = manager.Capacity;

        // Create entities beyond initial capacity (1024 default)
        for (int i = 0; i < 5000; i++)
        {
            manager.Create();
        }

        int newCapacity = manager.Capacity;
        await Assert.That(newCapacity).IsGreaterThan(initialCapacity);
        await Assert.That(manager.AliveCount).IsEqualTo(5000);
    }

    [Test]
    public async Task IsAlive_StaleHandle_ReturnsFalse()
    {
        var entity1 = _manager.Create();
        _manager.Destroy(entity1);
        var entity2 = _manager.Create(); // Reuses slot, increments version

        // entity1 is now stale
        var isAlive1 = _manager.IsAlive(entity1);
        var isAlive2 = _manager.IsAlive(entity2);

        await Assert.That(isAlive1).IsFalse();
        await Assert.That(isAlive2).IsTrue();
    }

    [Test]
    public async Task Create_ManyEntities_AllValid()
    {
        const int count = 1000;
        var entities = new Entity[count];

        for (int i = 0; i < count; i++)
        {
            entities[i] = _manager.Create();
        }

        // Verify all are valid and alive
        for (int i = 0; i < count; i++)
        {
            await Assert.That(entities[i].IsValid).IsTrue();
            await Assert.That(_manager.IsAlive(entities[i])).IsTrue();
        }
    }

    [Test]
    public async Task Destroy_ManyEntities_AllDestroyed()
    {
        const int count = 100;
        var entities = new Entity[count];

        for (int i = 0; i < count; i++)
        {
            entities[i] = _manager.Create();
        }

        for (int i = 0; i < count; i++)
        {
            _manager.Destroy(entities[i]);
        }

        // Verify all are destroyed
        for (int i = 0; i < count; i++)
        {
            await Assert.That(_manager.IsAlive(entities[i])).IsFalse();
        }

        await Assert.That(_manager.AliveCount).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_ClearsState()
    {
        var manager = new EntityManager();
        manager.Create();
        manager.Create();

        manager.Dispose();

        var aliveCount = manager.AliveCount;
        await Assert.That(aliveCount).IsEqualTo(0);
    }

    [Test]
    public async Task Create_AfterDispose_ThrowsObjectDisposedException()
    {
        var manager = new EntityManager();
        manager.Dispose();

        await Assert.That(manager.Create).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Destroy_AfterDispose_ThrowsObjectDisposedException()
    {
        var manager = new EntityManager();
        var entity = manager.Create();
        manager.Dispose();

        await Assert.That(() => manager.Destroy(entity)).Throws<ObjectDisposedException>();
    }
}
