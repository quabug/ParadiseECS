namespace Paradise.ECS.Test;

/// <summary>
/// Tests for SharedWorld and multiple worlds sharing resources.
/// </summary>
public sealed class SharedWorldTests : IDisposable
{
    private readonly SharedWorld<SmallBitSet<ulong>, DefaultConfig> _sharedWorld;

    public SharedWorldTests()
    {
        _sharedWorld = new SharedWorld<SmallBitSet<ulong>, DefaultConfig>(ComponentRegistry.Shared.TypeInfos);
    }

    public void Dispose()
    {
        _sharedWorld.Dispose();
    }

    #region Multiple Worlds Creation

    [Test]
    public async Task CreateWorld_ReturnsValidWorld()
    {
        var world = _sharedWorld.CreateWorld();

        await Assert.That(world).IsNotNull();
        await Assert.That(world.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task CreateWorld_MultipleTimes_ReturnsDifferentInstances()
    {
        var world1 = _sharedWorld.CreateWorld();
        var world2 = _sharedWorld.CreateWorld();

        await Assert.That(world1).IsNotEqualTo(world2);
    }

    [Test]
    public async Task CreateWorld_MultipleWorlds_ShareChunkManager()
    {
        var world1 = _sharedWorld.CreateWorld();
        var world2 = _sharedWorld.CreateWorld();

        // Both worlds should use the same ChunkManager
        await Assert.That(world1.ChunkManager).IsEqualTo(world2.ChunkManager);
        await Assert.That(world1.ChunkManager).IsEqualTo(_sharedWorld.ChunkManager);
    }

    [Test]
    public async Task CreateWorld_MultipleWorlds_ShareArchetypeRegistry()
    {
        var world1 = _sharedWorld.CreateWorld();
        var world2 = _sharedWorld.CreateWorld();

        // Both worlds should use archetypes from the same SharedArchetypeMetadata
        // Verified by checking they share the same archetype IDs for identical component masks
        var entity1 = world1.Spawn();
        world1.AddComponent(entity1, new TestPosition { X = 1, Y = 2 });

        var entity2 = world2.Spawn();
        world2.AddComponent(entity2, new TestPosition { X = 3, Y = 4 });

        var loc1 = world1.GetLocation(entity1);
        var loc2 = world2.GetLocation(entity2);

        // Same component combination should result in same archetype ID (shared metadata)
        await Assert.That(loc1.ArchetypeId).IsEqualTo(loc2.ArchetypeId);
    }

    #endregion

    #region Entity Independence

    [Test]
    public async Task MultipleWorlds_EntitiesAreIndependent()
    {
        var world1 = _sharedWorld.CreateWorld();
        var world2 = _sharedWorld.CreateWorld();

        // Create entities in world1
        world1.Spawn();
        world1.Spawn();

        // Create entities in world2
        world2.Spawn();

        await Assert.That(world1.EntityCount).IsEqualTo(2);
        await Assert.That(world2.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleWorlds_EntityValidityIsPerWorld()
    {
        var world1 = _sharedWorld.CreateWorld();
        var world2 = _sharedWorld.CreateWorld();

        var entity1 = world1.Spawn();

        // Entity from world1 is alive in world1
        await Assert.That(world1.IsAlive(entity1)).IsTrue();

        // Entity from world1 is NOT alive in world2 (different EntityManager)
        await Assert.That(world2.IsAlive(entity1)).IsFalse();
    }

    [Test]
    public async Task MultipleWorlds_ComponentsArePerWorld()
    {
        var world1 = _sharedWorld.CreateWorld();
        var world2 = _sharedWorld.CreateWorld();

        // Create entity with Position in world1
        var entity1 = world1.Spawn();
        world1.AddComponent(entity1, new TestPosition { X = 100, Y = 200 });

        // Create entity with different Position in world2
        var entity2 = world2.Spawn();
        world2.AddComponent(entity2, new TestPosition { X = 300, Y = 400 });

        // Verify components are stored independently
        var pos1 = world1.GetComponent<TestPosition>(entity1);
        await Assert.That(pos1.X).IsEqualTo(100);
        await Assert.That(pos1.Y).IsEqualTo(200);

        var pos2 = world2.GetComponent<TestPosition>(entity2);
        await Assert.That(pos2.X).IsEqualTo(300);
        await Assert.That(pos2.Y).IsEqualTo(400);
    }

    [Test]
    public async Task MultipleWorlds_DespawnIsPerWorld()
    {
        var world1 = _sharedWorld.CreateWorld();
        var world2 = _sharedWorld.CreateWorld();

        var entity1 = world1.Spawn();
        var entity2 = world2.Spawn();

        // Despawn entity in world1
        world1.Despawn(entity1);

        await Assert.That(world1.IsAlive(entity1)).IsFalse();
        await Assert.That(world1.EntityCount).IsEqualTo(0);

        // Entity in world2 should still be alive
        await Assert.That(world2.IsAlive(entity2)).IsTrue();
        await Assert.That(world2.EntityCount).IsEqualTo(1);
    }

    #endregion

    #region Shared Archetypes

    [Test]
    public async Task MultipleWorlds_ShareArchetypeDefinitions()
    {
        var world1 = _sharedWorld.CreateWorld();
        var world2 = _sharedWorld.CreateWorld();

        // Create entity with same component types in world1
        var entity1 = world1.Spawn();
        world1.AddComponent(entity1, new TestPosition { X = 1, Y = 2 });
        world1.AddComponent(entity1, new TestVelocity { X = 3, Y = 4 });

        // Create entity with same component types in world2
        var entity2 = world2.Spawn();
        world2.AddComponent(entity2, new TestPosition { X = 5, Y = 6 });
        world2.AddComponent(entity2, new TestVelocity { X = 7, Y = 8 });

        // Get archetype IDs
        var loc1 = world1.GetLocation(entity1);
        var loc2 = world2.GetLocation(entity2);

        // Same component combination should result in same archetype ID
        await Assert.That(loc1.ArchetypeId).IsEqualTo(loc2.ArchetypeId);
    }

    [Test]
    public async Task MultipleWorlds_QueriesWorkIndependently()
    {
        var world1 = _sharedWorld.CreateWorld();
        var world2 = _sharedWorld.CreateWorld();

        // Create 3 entities with Position in world1
        for (int i = 0; i < 3; i++)
        {
            var entity = world1.Spawn();
            world1.AddComponent(entity, new TestPosition { X = i, Y = i });
        }

        // Create 5 entities with Position in world2
        for (int i = 0; i < 5; i++)
        {
            var entity = world2.Spawn();
            world2.AddComponent(entity, new TestPosition { X = i * 10, Y = i * 10 });
        }

        // Query world1
        var query1 = QueryBuilder<SmallBitSet<ulong>>.Create().With<TestPosition>().Build(world1.ArchetypeRegistry);
        int count1 = 0;
        foreach (var _ in query1)
            count1++;

        // Query world2
        var query2 = QueryBuilder<SmallBitSet<ulong>>.Create().With<TestPosition>().Build(world2.ArchetypeRegistry);
        int count2 = 0;
        foreach (var _ in query2)
            count2++;

        await Assert.That(count1).IsEqualTo(3);
        await Assert.That(count2).IsEqualTo(5);
    }

    #endregion

    #region Clear and Dispose

    [Test]
    public async Task ClearWorld_OnlyAffectsThatWorld()
    {
        var world1 = _sharedWorld.CreateWorld();
        var world2 = _sharedWorld.CreateWorld();

        world1.Spawn();
        world1.Spawn();
        world2.Spawn();

        // Clear world1
        world1.Clear();

        await Assert.That(world1.EntityCount).IsEqualTo(0);
        await Assert.That(world2.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task Dispose_ClearsAllWorlds()
    {
        // Create a separate SharedWorld for this test
        var sharedWorld = new SharedWorld<SmallBitSet<ulong>, DefaultConfig>(ComponentRegistry.Shared.TypeInfos);

        var world1 = sharedWorld.CreateWorld();
        var world2 = sharedWorld.CreateWorld();

        world1.Spawn();
        world2.Spawn();

        // Dispose the shared world
        sharedWorld.Dispose();

        // After dispose, entities should no longer be alive (worlds were cleared)
        await Assert.That(world1.EntityCount).IsEqualTo(0);
        await Assert.That(world2.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_PreventsNewWorldCreation()
    {
        // Create a separate SharedWorld for this test
        var sharedWorld = new SharedWorld<SmallBitSet<ulong>, DefaultConfig>(ComponentRegistry.Shared.TypeInfos);
        sharedWorld.Dispose();

        // Attempting to create a world after dispose should throw
        await Assert.That(sharedWorld.CreateWorld).Throws<ObjectDisposedException>();
    }

    #endregion

    #region Stress Tests

    [Test]
    public async Task ManyWorlds_CanCreateAndUseMany()
    {
        const int worldCount = 10;
        const int entitiesPerWorld = 100;

        var worlds = new List<World<SmallBitSet<ulong>, DefaultConfig>>();

        // Create many worlds
        for (int i = 0; i < worldCount; i++)
        {
            worlds.Add(_sharedWorld.CreateWorld());
        }

        // Create entities in each world
        for (int w = 0; w < worldCount; w++)
        {
            for (int e = 0; e < entitiesPerWorld; e++)
            {
                var entity = worlds[w].Spawn();
                worlds[w].AddComponent(entity, new TestPosition { X = w, Y = e });
            }
        }

        // Verify entity counts
        for (int w = 0; w < worldCount; w++)
        {
            await Assert.That(worlds[w].EntityCount).IsEqualTo(entitiesPerWorld);
        }

        // Verify total archetype count is minimal (should share archetypes)
        await Assert.That(_sharedWorld.SharedMetadata.ArchetypeCount).IsLessThanOrEqualTo(2); // Empty + Position
    }

    #endregion
}
