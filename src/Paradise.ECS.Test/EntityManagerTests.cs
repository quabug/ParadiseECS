namespace Paradise.ECS.Test;

/// <summary>
/// Tests for EntityManager.
/// </summary>
public sealed class EntityManagerTests
{
    [Test]
    public async Task Create_ReturnsValidEntity()
    {
        var manager = new EntityManager(1024);

        var entity = manager.Create();

        await Assert.That(entity.IsValid).IsTrue();
        await Assert.That(entity.Id).IsGreaterThanOrEqualTo(0);
        await Assert.That(entity.Version).IsEqualTo(1u);
    }

    [Test]
    public async Task Create_MultipleEntities_HaveUniqueIds()
    {
        var manager = new EntityManager(1024);

        var entity1 = manager.Create();
        var entity2 = manager.Create();
        var entity3 = manager.Create();

        await Assert.That(entity1.Id).IsNotEqualTo(entity2.Id);
        await Assert.That(entity2.Id).IsNotEqualTo(entity3.Id);
        await Assert.That(entity1.Id).IsNotEqualTo(entity3.Id);
    }

    [Test]
    public async Task Create_ConsecutiveIds()
    {
        var manager = new EntityManager(1024);

        var e1 = manager.Create();
        var e2 = manager.Create();
        var e3 = manager.Create();

        await Assert.That(e1.Id).IsEqualTo(0);
        await Assert.That(e2.Id).IsEqualTo(1);
        await Assert.That(e3.Id).IsEqualTo(2);
    }

    [Test]
    public async Task IsAlive_NewEntity_ReturnsTrue()
    {
        var manager = new EntityManager(1024);
        var entity = manager.Create();

        var isAlive = manager.IsAlive(entity);

        await Assert.That(isAlive).IsTrue();
    }

    [Test]
    public async Task IsAlive_InvalidEntity_ReturnsFalse()
    {
        var manager = new EntityManager(1024);

        var isAlive = manager.IsAlive(Entity.Invalid);

        await Assert.That(isAlive).IsFalse();
    }

    [Test]
    public async Task IsAlive_DefaultEntity_ReturnsFalse()
    {
        var manager = new EntityManager(1024);

        var isAlive = manager.IsAlive(default);

        await Assert.That(isAlive).IsFalse();
    }

    [Test]
    public async Task Destroy_MakesEntityNotAlive()
    {
        var manager = new EntityManager(1024);
        var entity = manager.Create();

        manager.Destroy(entity);

        var isAlive = manager.IsAlive(entity);
        await Assert.That(isAlive).IsFalse();
    }

    [Test]
    public async Task Destroy_InvalidEntity_DoesNotThrow()
    {
        var manager = new EntityManager(1024);

        manager.Destroy(Entity.Invalid);

        await Assert.That(manager.AliveCount).IsEqualTo(0);
    }

    [Test]
    public async Task Destroy_AlreadyDestroyedEntity_DoesNotThrow()
    {
        var manager = new EntityManager(1024);
        var entity = manager.Create();
        manager.Destroy(entity);

        manager.Destroy(entity); // Double destroy should be safe

        await Assert.That(manager.AliveCount).IsEqualTo(0);
    }

    [Test]
    public async Task Destroy_IncreasesVersion()
    {
        var manager = new EntityManager(1024);
        var entity1 = manager.Create();
        int firstId = entity1.Id;
        uint firstVersion = entity1.Version;

        manager.Destroy(entity1);
        var entity2 = manager.Create(); // Should reuse the slot

        // Same ID (recycled), version should be incremented
        await Assert.That(entity2.Id).IsEqualTo(firstId);
        await Assert.That(entity2.Version).IsGreaterThan(firstVersion);
    }

    [Test]
    public async Task Create_AfterDestroy_ReusesSlot()
    {
        var manager = new EntityManager(1024);
        var entity1 = manager.Create();
        int id1 = entity1.Id;

        manager.Destroy(entity1);
        var entity2 = manager.Create();

        await Assert.That(entity2.Id).IsEqualTo(id1);
        await Assert.That(entity2.Version).IsGreaterThan(entity1.Version);
    }

    [Test]
    public async Task AliveCount_IncrementsOnCreate()
    {
        var manager = new EntityManager(1024);
        int initialCount = manager.AliveCount;

        manager.Create();
        manager.Create();

        await Assert.That(manager.AliveCount).IsEqualTo(initialCount + 2);
    }

    [Test]
    public async Task AliveCount_DecrementsOnDestroy()
    {
        var manager = new EntityManager(1024);
        var entity1 = manager.Create();
        _ = manager.Create();
        int countAfterCreate = manager.AliveCount;

        manager.Destroy(entity1);

        await Assert.That(manager.AliveCount).IsEqualTo(countAfterCreate - 1);
    }

    [Test]
    public async Task AliveCount_ConsistentAfterCreateAndDestroy()
    {
        var manager = new EntityManager(1024);
        int initialCount = manager.AliveCount;

        var entity = manager.Create();
        manager.Destroy(entity);

        await Assert.That(manager.AliveCount).IsEqualTo(initialCount);
    }

    [Test]
    public async Task Create_BeyondInitialCapacity_ExpandsAutomatically()
    {
        var manager = new EntityManager(initialCapacity: 4);

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
        var manager = new EntityManager(initialCapacity: 4);
        int initialCapacity = manager.Capacity;

        for (int i = 0; i < 100; i++)
        {
            manager.Create();
        }

        await Assert.That(manager.Capacity).IsGreaterThan(initialCapacity);
        await Assert.That(manager.AliveCount).IsEqualTo(100);
    }

    [Test]
    public async Task IsAlive_StaleHandle_ReturnsFalse()
    {
        var manager = new EntityManager(1024);
        var entity1 = manager.Create();
        manager.Destroy(entity1);
        var entity2 = manager.Create();

        await Assert.That(manager.IsAlive(entity1)).IsFalse();
        await Assert.That(manager.IsAlive(entity2)).IsTrue();
    }

    [Test]
    public async Task Create_ManyEntities_AllValid()
    {
        var manager = new EntityManager(1024);
        const int count = 1000;
        var entities = new Entity[count];

        for (int i = 0; i < count; i++)
        {
            entities[i] = manager.Create();
        }

        for (int i = 0; i < count; i++)
        {
            await Assert.That(entities[i].IsValid).IsTrue();
            await Assert.That(manager.IsAlive(entities[i])).IsTrue();
        }
    }

    [Test]
    public async Task Destroy_ManyEntities_AllDestroyed()
    {
        var manager = new EntityManager(1024);
        const int count = 100;
        var entities = new Entity[count];

        for (int i = 0; i < count; i++)
        {
            entities[i] = manager.Create();
        }

        for (int i = 0; i < count; i++)
        {
            manager.Destroy(entities[i]);
        }

        for (int i = 0; i < count; i++)
        {
            await Assert.That(manager.IsAlive(entities[i])).IsFalse();
        }
        await Assert.That(manager.AliveCount).IsEqualTo(0);
    }

    [Test]
    public async Task Clear_ResetsState()
    {
        var manager = new EntityManager(1024);
        manager.Create();
        manager.Create();

        manager.Clear();

        await Assert.That(manager.AliveCount).IsEqualTo(0);
        await Assert.That(manager.Capacity).IsEqualTo(0);
    }

    [Test]
    public async Task GetLocation_ReturnsStoredLocation()
    {
        var manager = new EntityManager(1024);
        var entity = manager.Create();
        var location = new EntityLocation(entity.Version, 5, 10);

        manager.SetLocation(entity.Id, location);
        var retrieved = manager.GetLocation(entity.Id);

        await Assert.That(retrieved.Version).IsEqualTo(entity.Version);
        await Assert.That(retrieved.ArchetypeId).IsEqualTo(5);
        await Assert.That(retrieved.GlobalIndex).IsEqualTo(10);
    }

    [Test]
    public async Task SetLocation_UpdatesLocation()
    {
        var manager = new EntityManager(1024);
        var entity = manager.Create();

        manager.SetLocation(entity.Id, new EntityLocation(entity.Version, 1, 2));
        manager.SetLocation(entity.Id, new EntityLocation(entity.Version, 3, 4));

        var location = manager.GetLocation(entity.Id);
        await Assert.That(location.ArchetypeId).IsEqualTo(3);
        await Assert.That(location.GlobalIndex).IsEqualTo(4);
    }

    [Test]
    public async Task Constructor_InvalidCapacity_Throws()
    {
        await Assert.That(() => new EntityManager(0)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => new EntityManager(-1)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Destroy_EntityIdBeyondRange_DoesNotThrow()
    {
        var manager = new EntityManager(1024);
        var fakeEntity = new Entity(9999, 1);

        manager.Destroy(fakeEntity);

        await Assert.That(manager.AliveCount).IsEqualTo(0);
    }

    [Test]
    public async Task IsAlive_EntityIdBeyondRange_ReturnsFalse()
    {
        var manager = new EntityManager(1024);
        var fakeEntity = new Entity(9999, 1);

        await Assert.That(manager.IsAlive(fakeEntity)).IsFalse();
    }
}
