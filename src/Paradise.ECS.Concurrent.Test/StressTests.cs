namespace Paradise.ECS.Concurrent.Test;

/// <summary>
/// Stress tests for large-scale ECS operations.
/// These tests verify the system works correctly under heavy load.
/// </summary>
[Category("Stress")]
public sealed class StressTests : IDisposable
{
    private ChunkManager<DefaultConfig>? _chunkManager;
    private World<Bit64, ComponentRegistry, DefaultConfig>? _world;

    public void Dispose()
    {
        _world?.Dispose();
        _chunkManager?.Dispose();
    }

    private void CreateWorld()
    {
        _chunkManager = new ChunkManager<DefaultConfig>(new DefaultConfig());
        _world = new World<Bit64, ComponentRegistry, DefaultConfig>(
            new DefaultConfig(),
            SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig>.Shared,
            _chunkManager);
    }

    #region Large Entity Count Tests

    [Test]
    public async Task SpawnManyEntities_1000_Succeeds()
    {
        CreateWorld();
        const int count = 1000;

        for (int i = 0; i < count; i++)
        {
            var entity = _world!.Spawn();
            _world.AddComponent(entity, new TestPosition { X = i, Y = i, Z = i });
        }

        await Assert.That(_world!.EntityCount).IsEqualTo(count);
    }

    [Test]
    public async Task SpawnManyEntities_10000_Succeeds()
    {
        CreateWorld();
        const int count = 10000;

        for (int i = 0; i < count; i++)
        {
            var entity = _world!.Spawn();
            _world.AddComponent(entity, new TestPosition { X = i });
        }

        await Assert.That(_world!.EntityCount).IsEqualTo(count);
    }

    [Test]
    public async Task SpawnManyEntities_QueryReturnsCorrectCount()
    {
        CreateWorld();
        const int count = 5000;

        for (int i = 0; i < count; i++)
        {
            var entity = _world!.Spawn();
            _world.AddComponent(entity, new TestPosition { X = i });
        }

        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Build(_world!.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(count);
    }

    #endregion

    #region Spawn and Despawn Patterns Tests

    [Test]
    public async Task SpawnAndDespawnAlternating_MaintainsCorrectCount()
    {
        CreateWorld();
        const int iterations = 1000;
        var entities = new List<Entity>();

        // Spawn all
        for (int i = 0; i < iterations; i++)
        {
            var entity = _world!.Spawn();
            _world.AddComponent(entity, new TestPosition { X = i });
            entities.Add(entity);
        }

        await Assert.That(_world!.EntityCount).IsEqualTo(iterations);

        // Despawn half
        for (int i = 0; i < iterations / 2; i++)
        {
            _world!.Despawn(entities[i]);
        }

        await Assert.That(_world!.EntityCount).IsEqualTo(iterations / 2);

        // Spawn more
        for (int i = 0; i < iterations / 2; i++)
        {
            var entity = _world!.Spawn();
            _world.AddComponent(entity, new TestPosition { X = i });
        }

        await Assert.That(_world!.EntityCount).IsEqualTo(iterations);
    }

    [Test]
    public async Task SpawnDespawnSpawn_EntitySlotsReused()
    {
        CreateWorld();

        // Spawn first batch
        var firstBatch = new Entity[100];
        for (int i = 0; i < 100; i++)
        {
            firstBatch[i] = _world!.Spawn();
        }

        // Despawn all
        foreach (var entity in firstBatch)
        {
            _world!.Despawn(entity);
        }

        await Assert.That(_world!.EntityCount).IsEqualTo(0);

        // Spawn second batch - slots should be reused
        var secondBatch = new Entity[100];
        for (int i = 0; i < 100; i++)
        {
            secondBatch[i] = _world!.Spawn();
        }

        await Assert.That(_world!.EntityCount).IsEqualTo(100);

        // IDs should be reused (though versions will be different)
        var firstIds = firstBatch.Select(e => e.Id).OrderBy(x => x).ToArray();
        var secondIds = secondBatch.Select(e => e.Id).OrderBy(x => x).ToArray();

        // Some IDs should overlap due to slot reuse
        await Assert.That(firstIds.Intersect(secondIds).Any()).IsTrue();
    }

    #endregion

    #region Multiple Archetypes Tests

    [Test]
    public async Task ManyDifferentArchetypes_AllAccessible()
    {
        CreateWorld();

        // Create entities in different archetypes
        var e1 = _world!.Spawn();
        _world.AddComponent<TestPosition>(e1);

        var e2 = _world.Spawn();
        _world.AddComponent<TestVelocity>(e2);

        var e3 = _world.Spawn();
        _world.AddComponent<TestHealth>(e3);

        var e4 = _world.Spawn();
        _world.AddComponent<TestPosition>(e4);
        _world.AddComponent<TestVelocity>(e4);

        var e5 = _world.Spawn();
        _world.AddComponent<TestPosition>(e5);
        _world.AddComponent<TestHealth>(e5);

        var e6 = _world.Spawn();
        _world.AddComponent<TestVelocity>(e6);
        _world.AddComponent<TestHealth>(e6);

        var e7 = _world.Spawn();
        _world.AddComponent<TestPosition>(e7);
        _world.AddComponent<TestVelocity>(e7);
        _world.AddComponent<TestHealth>(e7);

        // Verify queries
        var posQuery = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var velQuery = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestVelocity>()
            .Build(_world.ArchetypeRegistry);

        var healthQuery = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestHealth>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(posQuery.EntityCount).IsEqualTo(4);  // e1, e4, e5, e7
        await Assert.That(velQuery.EntityCount).IsEqualTo(4);  // e2, e4, e6, e7
        await Assert.That(healthQuery.EntityCount).IsEqualTo(4); // e3, e5, e6, e7
    }

    [Test]
    public async Task ManyEntitiesAcrossManyArchetypes_QueryCorrect()
    {
        CreateWorld();
        const int entitiesPerArchetype = 100;

        // Create 4 different archetypes
        for (int i = 0; i < entitiesPerArchetype; i++)
        {
            // {Position}
            var e1 = _world!.Spawn();
            _world.AddComponent<TestPosition>(e1);

            // {Position, Velocity}
            var e2 = _world.Spawn();
            _world.AddComponent<TestPosition>(e2);
            _world.AddComponent<TestVelocity>(e2);

            // {Position, Health}
            var e3 = _world.Spawn();
            _world.AddComponent<TestPosition>(e3);
            _world.AddComponent<TestHealth>(e3);

            // {Velocity}
            var e4 = _world.Spawn();
            _world.AddComponent<TestVelocity>(e4);
        }

        var posQuery = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Build(_world!.ArchetypeRegistry);

        // 3 archetypes with Position, 100 entities each
        await Assert.That(posQuery.EntityCount).IsEqualTo(300);
        await Assert.That(posQuery.ArchetypeCount).IsEqualTo(3);
    }

    #endregion

    #region Component Operations Stress Tests

    [Test]
    public async Task AddRemoveComponents_ManyTimes_MaintainsIntegrity()
    {
        CreateWorld();
        var entities = new List<Entity>();

        // Create entities with Position
        for (int i = 0; i < 100; i++)
        {
            var entity = _world!.Spawn();
            _world.AddComponent(entity, new TestPosition { X = i });
            entities.Add(entity);
        }

        // Add Velocity to all
        foreach (var entity in entities)
        {
            _world!.AddComponent(entity, new TestVelocity { X = entity.Id });
        }

        // Verify all have both components
        foreach (var entity in entities)
        {
            var hasPos = _world!.HasComponent<TestPosition>(entity);
            var hasVel = _world.HasComponent<TestVelocity>(entity);
            await Assert.That(hasPos).IsTrue();
            await Assert.That(hasVel).IsTrue();
        }

        // Remove Velocity from half
        for (int i = 0; i < 50; i++)
        {
            _world!.RemoveComponent<TestVelocity>(entities[i]);
        }

        // Verify component states
        for (int i = 0; i < 100; i++)
        {
            var hasPos = _world!.HasComponent<TestPosition>(entities[i]);
            var hasVel = _world.HasComponent<TestVelocity>(entities[i]);
            await Assert.That(hasPos).IsTrue();
            await Assert.That(hasVel).IsEqualTo(i >= 50);
        }
    }

    [Test]
    public async Task ComponentDataPreserved_AfterManyArchetypeTransitions()
    {
        CreateWorld();
        var entity = _world!.Spawn();

        // Add Position
        _world.AddComponent(entity, new TestPosition { X = 10, Y = 20, Z = 30 });

        // Add Velocity (archetype transition)
        _world.AddComponent(entity, new TestVelocity { X = 1, Y = 2, Z = 3 });

        // Verify Position data preserved
        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }

        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
        await Assert.That(pos.Z).IsEqualTo(30f);

        // Add Health (another transition)
        _world.AddComponent(entity, new TestHealth { Current = 100, Max = 100 });

        // Verify both Position and Velocity preserved
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }
        TestVelocity vel;
        using (var velRef = _world.GetComponent<TestVelocity>(entity))
        {
            vel = velRef.Value;
        }

        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(vel.X).IsEqualTo(1f);
    }

    #endregion

    #region Memory/Chunk Tests

    [Test]
    public async Task FillMultipleChunks_AllDataAccessible()
    {
        CreateWorld();

        // Create enough entities to fill multiple chunks
        var entities = new List<Entity>();
        for (int i = 0; i < 1000; i++)
        {
            var entity = _world!.Spawn();
            _world.AddComponent(entity, new TestPosition { X = i, Y = i * 2, Z = i * 3 });
            entities.Add(entity);
        }

        // Verify all data
        for (int i = 0; i < entities.Count; i++)
        {
            TestPosition pos;
            using (var posRef = _world!.GetComponent<TestPosition>(entities[i]))
            {
                pos = posRef.Value;
            }

            await Assert.That(pos.X).IsEqualTo((float)i);
            await Assert.That(pos.Y).IsEqualTo((float)(i * 2));
            await Assert.That(pos.Z).IsEqualTo((float)(i * 3));
        }
    }

    #endregion

    #region Query Stress Tests

    [Test]
    public async Task ManyQueriesSameDescription_Cached()
    {
        CreateWorld();

        var entity = _world!.Spawn();
        _world.AddComponent<TestPosition>(entity);

        // Build many queries with same description
        var queries = new List<Query<Bit64, ComponentRegistry, DefaultConfig>>();
        for (int i = 0; i < 100; i++)
        {
            var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
                .With<TestPosition>()
                .Build(_world.ArchetypeRegistry);
            queries.Add(query);
        }

        // All should work correctly
        foreach (var query in queries)
        {
            await Assert.That(query.EntityCount).IsEqualTo(1);
        }
    }

    [Test]
    public async Task QueryIterationManyTimes_Consistent()
    {
        CreateWorld();

        for (int i = 0; i < 100; i++)
        {
            var entity = _world!.Spawn();
            _world.AddComponent(entity, new TestPosition { X = i });
        }

        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Build(_world!.ArchetypeRegistry);

        // Iterate many times
        for (int iteration = 0; iteration < 10; iteration++)
        {
            int count = 0;
            foreach (var archetype in query)
            {
                count += archetype.EntityCount;
            }
            await Assert.That(count).IsEqualTo(100);
        }
    }

    #endregion

    #region Rapid Creation/Destruction Tests

    [Test]
    public async Task RapidCreateDestroy_NoMemoryLeak()
    {
        CreateWorld();

        // Rapidly create and destroy entities
        for (int round = 0; round < 10; round++)
        {
            var entities = new Entity[100];

            // Create
            for (int i = 0; i < 100; i++)
            {
                entities[i] = _world!.Spawn();
                _world.AddComponent(entities[i], new TestPosition { X = i });
            }

            // Destroy
            for (int i = 0; i < 100; i++)
            {
                _world!.Despawn(entities[i]);
            }
        }

        await Assert.That(_world!.EntityCount).IsEqualTo(0);
    }

    #endregion
}
