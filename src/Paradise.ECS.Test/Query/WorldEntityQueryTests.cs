namespace Paradise.ECS.Test;

/// <summary>
/// Tests for WorldEntity and WorldEntityChunk query data types.
/// </summary>
public sealed class WorldEntityQueryTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly ChunkManager _chunkManager = ChunkManager.Create(s_config);
    private readonly SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig> _sharedMetadata = new(ComponentRegistry.Shared.TypeInfos, s_config);
    private readonly World<SmallBitSet<ulong>, DefaultConfig> _world;

    public WorldEntityQueryTests()
    {
        _world = new World<SmallBitSet<ulong>, DefaultConfig>(s_config, _sharedMetadata, _chunkManager);
    }

    public void Dispose()
    {
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
    }

    #region WorldEntity Tests

    [Test]
    public async Task WorldEntity_Entity_ReturnsCorrectEntity()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 10, Y = 20, Z = 30 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .Build(_world);

        Entity foundEntity = default;
        foreach (var worldEntity in query)
        {
            foundEntity = worldEntity.Entity;
        }

        await Assert.That(foundEntity).IsEqualTo(spawned);
    }

    [Test]
    public async Task WorldEntity_Get_ReturnsComponentReference()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 100, Y = 200, Z = 300 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .Build(_world);

        TestPosition pos = default;
        foreach (var worldEntity in query)
        {
            pos = worldEntity.Get<TestPosition>();
        }

        await Assert.That(pos.X).IsEqualTo(100f);
        await Assert.That(pos.Y).IsEqualTo(200f);
        await Assert.That(pos.Z).IsEqualTo(300f);
    }

    [Test]
    public async Task WorldEntity_Get_ModifiesComponent()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1, Y = 2, Z = 3 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .Build(_world);

        foreach (var worldEntity in query)
        {
            ref var pos = ref worldEntity.Get<TestPosition>();
            pos.X = 999;
        }

        var updated = _world.GetComponent<TestPosition>(spawned);
        await Assert.That(updated.X).IsEqualTo(999f);
    }

    [Test]
    public async Task WorldEntity_Has_ExistingComponent_ReturnsTrue()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });
        _world.AddComponent(spawned, new TestHealth { Current = 50, Max = 100 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .Build(_world);

        bool hasPosition = false;
        bool hasHealth = false;
        foreach (var worldEntity in query)
        {
            hasPosition = worldEntity.Has<TestPosition>();
            hasHealth = worldEntity.Has<TestHealth>();
        }

        await Assert.That(hasPosition).IsTrue();
        await Assert.That(hasHealth).IsTrue();
    }

    [Test]
    public async Task WorldEntity_Has_NonExistentComponent_ReturnsFalse()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .Build(_world);

        bool hasVelocity = true;
        foreach (var worldEntity in query)
        {
            hasVelocity = worldEntity.Has<TestVelocity>();
        }

        await Assert.That(hasVelocity).IsFalse();
    }

    [Test]
    public async Task WorldEntity_ImplicitConversionToEntity_Works()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .Build(_world);

        Entity converted = default;
        foreach (var worldEntity in query)
        {
            converted = worldEntity; // implicit conversion
        }

        await Assert.That(converted).IsEqualTo(spawned);
    }

    [Test]
    public async Task WorldEntity_MultipleEntities_IteratesAll()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        var e3 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 1 });
        _world.AddComponent(e2, new TestPosition { X = 2 });
        _world.AddComponent(e3, new TestPosition { X = 3 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .Build(_world);

        var entities = new List<Entity>();
        var positions = new List<float>();
        foreach (var worldEntity in query)
        {
            entities.Add(worldEntity.Entity);
            positions.Add(worldEntity.Get<TestPosition>().X);
        }

        await Assert.That(entities.Count).IsEqualTo(3);
        await Assert.That(entities).Contains(e1);
        await Assert.That(entities).Contains(e2);
        await Assert.That(entities).Contains(e3);
        await Assert.That(positions).Contains(1f);
        await Assert.That(positions).Contains(2f);
        await Assert.That(positions).Contains(3f);
    }

    #endregion

    #region WorldEntityChunk Tests

    [Test]
    public async Task WorldEntityChunk_EntityCount_ReturnsCorrectCount()
    {
        _world.Spawn();
        _world.Spawn();
        _world.Spawn();

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .BuildChunk(_world);

        int totalCount = 0;
        foreach (var chunk in query)
        {
            totalCount += chunk.EntityCount;
        }

        await Assert.That(totalCount).IsEqualTo(3);
    }

    [Test]
    public async Task WorldEntityChunk_Get_ReturnsComponentSpan()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 10 });
        _world.AddComponent(e2, new TestPosition { X = 20 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        var allPositions = new List<float>();
        foreach (var chunk in query)
        {
            var positions = chunk.Get<TestPosition>();
            for (int i = 0; i < chunk.EntityCount; i++)
            {
                allPositions.Add(positions[i].X);
            }
        }

        await Assert.That(allPositions.Count).IsEqualTo(2);
        await Assert.That(allPositions).Contains(10f);
        await Assert.That(allPositions).Contains(20f);
    }

    [Test]
    public async Task WorldEntityChunk_Get_ModifiesComponents()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 1 });
        _world.AddComponent(e2, new TestPosition { X = 2 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        foreach (var chunk in query)
        {
            var positions = chunk.Get<TestPosition>();
            for (int i = 0; i < chunk.EntityCount; i++)
            {
                positions[i].X *= 100;
            }
        }

        await Assert.That(_world.GetComponent<TestPosition>(e1).X).IsEqualTo(100f);
        await Assert.That(_world.GetComponent<TestPosition>(e2).X).IsEqualTo(200f);
    }

    [Test]
    public async Task WorldEntityChunk_Has_ExistingComponent_ReturnsTrue()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        bool hasPosition = false;
        foreach (var chunk in query)
        {
            hasPosition = chunk.Has<TestPosition>();
        }

        await Assert.That(hasPosition).IsTrue();
    }

    [Test]
    public async Task WorldEntityChunk_Has_NonExistentComponent_ReturnsFalse()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        bool hasVelocity = true;
        foreach (var chunk in query)
        {
            hasVelocity = chunk.Has<TestVelocity>();
        }

        await Assert.That(hasVelocity).IsFalse();
    }

    [Test]
    public async Task WorldEntityChunk_Handle_ReturnsValidHandle()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        ChunkHandle handle = default;
        foreach (var chunk in query)
        {
            handle = chunk.Handle;
        }

        await Assert.That(handle.Id).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task WorldEntityChunk_GetEntityAt_ReturnsWorldEntity()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 10 });
        _world.AddComponent(e2, new TestPosition { X = 20 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        var entities = new List<Entity>();
        var positions = new List<float>();
        foreach (var chunk in query)
        {
            for (int i = 0; i < chunk.EntityCount; i++)
            {
                var worldEntity = chunk.GetEntityAt(i);
                entities.Add(worldEntity.Entity);
                positions.Add(worldEntity.Get<TestPosition>().X);
            }
        }

        await Assert.That(entities.Count).IsEqualTo(2);
        await Assert.That(entities).Contains(e1);
        await Assert.That(entities).Contains(e2);
        await Assert.That(positions).Contains(10f);
        await Assert.That(positions).Contains(20f);
    }

    [Test]
    public async Task WorldEntityChunk_GetEntityAt_ModifiesComponentViaWorldEntity()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 5 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        foreach (var chunk in query)
        {
            for (int i = 0; i < chunk.EntityCount; i++)
            {
                var worldEntity = chunk.GetEntityAt(i);
                ref var pos = ref worldEntity.Get<TestPosition>();
                pos.X = 555;
            }
        }

        await Assert.That(_world.GetComponent<TestPosition>(spawned).X).IsEqualTo(555f);
    }

    #endregion

    #region QueryResult Tests

    [Test]
    public async Task QueryResult_EntityCount_ReturnsCorrectCount()
    {
        _world.Spawn();
        _world.Spawn();
        _world.Spawn();

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .Build(_world);

        await Assert.That(query.EntityCount).IsEqualTo(3);
    }

    [Test]
    public async Task QueryResult_IsEmpty_NoEntities_ReturnsTrue()
    {
        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .Build(_world);

        await Assert.That(query.IsEmpty).IsTrue();
    }

    [Test]
    public async Task QueryResult_IsEmpty_WithEntities_ReturnsFalse()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .Build(_world);

        await Assert.That(query.IsEmpty).IsFalse();
    }

    [Test]
    public async Task QueryResult_WithFilter_ReturnsMatchingEntities()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        var e3 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 1 });
        _world.AddComponent(e2, new TestPosition { X = 2 });
        _world.AddComponent(e2, new TestVelocity { X = 5 });
        _world.AddComponent(e3, new TestVelocity { X = 10 });

        // Query for entities with Position but without Velocity
        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .Without<TestVelocity>()
            .Build(_world);

        var entities = new List<Entity>();
        foreach (var worldEntity in query)
        {
            entities.Add(worldEntity.Entity);
        }

        await Assert.That(entities.Count).IsEqualTo(1);
        await Assert.That(entities).Contains(e1);
    }

    #endregion

    #region ChunkQueryResult Tests

    [Test]
    public async Task ChunkQueryResult_EntityCount_ReturnsCorrectCount()
    {
        _world.Spawn();
        _world.Spawn();
        _world.Spawn();

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .BuildChunk(_world);

        await Assert.That(query.EntityCount).IsEqualTo(3);
    }

    [Test]
    public async Task ChunkQueryResult_IsEmpty_NoEntities_ReturnsTrue()
    {
        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        await Assert.That(query.IsEmpty).IsTrue();
    }

    [Test]
    public async Task ChunkQueryResult_IsEmpty_WithEntities_ReturnsFalse()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        await Assert.That(query.IsEmpty).IsFalse();
    }

    [Test]
    public async Task ChunkQueryResult_IteratesAllChunks()
    {
        // Create entities in different archetypes to potentially get multiple chunks
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 1 });
        _world.AddComponent(e2, new TestPosition { X = 2 });
        _world.AddComponent(e2, new TestVelocity { X = 5 });

        var queryWithPosition = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        int chunkCount = 0;
        int totalEntities = 0;
        foreach (var chunk in queryWithPosition)
        {
            chunkCount++;
            totalEntities += chunk.EntityCount;
        }

        // Two archetypes: Position only, Position+Velocity
        await Assert.That(chunkCount).IsEqualTo(2);
        await Assert.That(totalEntities).IsEqualTo(2);
    }

    #endregion

    #region Query Across Multiple Archetypes

    [Test]
    public async Task WorldEntity_MultipleArchetypes_IteratesAll()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        var e3 = _world.Spawn();

        _world.AddComponent(e1, new TestPosition { X = 1 });
        _world.AddComponent(e2, new TestPosition { X = 2 });
        _world.AddComponent(e2, new TestVelocity { X = 20 });
        _world.AddComponent(e3, new TestPosition { X = 3 });
        _world.AddComponent(e3, new TestHealth { Current = 100, Max = 100 });

        // Query for all entities with Position (3 different archetypes)
        var query = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .Build(_world);

        var entities = new List<Entity>();
        foreach (var worldEntity in query)
        {
            entities.Add(worldEntity.Entity);
        }

        await Assert.That(entities.Count).IsEqualTo(3);
        await Assert.That(entities).Contains(e1);
        await Assert.That(entities).Contains(e2);
        await Assert.That(entities).Contains(e3);
    }

    #endregion
}
