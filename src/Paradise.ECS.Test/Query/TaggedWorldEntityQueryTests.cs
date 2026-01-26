namespace Paradise.ECS.Test;

/// <summary>
/// Tests for TaggedWorldEntity and TaggedWorldEntityChunk query data types.
/// </summary>
public sealed class TaggedWorldEntityQueryTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly ChunkManager _chunkManager = ChunkManager.Create(s_config);
    private readonly SharedArchetypeMetadata _sharedMetadata = new(ComponentRegistry.Shared.TypeInfos, s_config);
    private readonly ChunkTagRegistry<TagMask> _chunkTagRegistry = new(s_config.ChunkAllocator, DefaultConfig.MaxMetaBlocks, DefaultConfig.ChunkSize);
    private readonly World _world;

    public TaggedWorldEntityQueryTests()
    {
        _world = new World(s_config, _chunkManager, _sharedMetadata, _chunkTagRegistry);
    }

    public void Dispose()
    {
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
        _chunkTagRegistry.Dispose();
    }

    #region TaggedWorldEntity Tests

    [Test]
    public async Task TaggedWorldEntity_Entity_ReturnsCorrectEntity()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 10 });
        _world.AddTag<TestIsActive>(spawned);

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        Entity foundEntity = default;
        foreach (var taggedEntity in query)
        {
            foundEntity = taggedEntity.Entity;
        }

        await Assert.That(foundEntity).IsEqualTo(spawned);
    }

    [Test]
    public async Task TaggedWorldEntity_Get_ReturnsComponentReference()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 100, Y = 200, Z = 300 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        TestPosition pos = default;
        foreach (var taggedEntity in query)
        {
            pos = taggedEntity.Get<TestPosition>();
        }

        await Assert.That(pos.X).IsEqualTo(100f);
        await Assert.That(pos.Y).IsEqualTo(200f);
        await Assert.That(pos.Z).IsEqualTo(300f);
    }

    [Test]
    public async Task TaggedWorldEntity_Get_ModifiesComponent()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        foreach (var taggedEntity in query)
        {
            ref var pos = ref taggedEntity.Get<TestPosition>();
            pos.X = 999;
        }

        var updated = _world.GetComponent<TestPosition>(spawned);
        await Assert.That(updated.X).IsEqualTo(999f);
    }

    [Test]
    public async Task TaggedWorldEntity_Has_ExistingComponent_ReturnsTrue()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        bool hasPosition = false;
        foreach (var taggedEntity in query)
        {
            hasPosition = taggedEntity.Has<TestPosition>();
        }

        await Assert.That(hasPosition).IsTrue();
    }

    [Test]
    public async Task TaggedWorldEntity_Has_NonExistentComponent_ReturnsFalse()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        bool hasVelocity = true;
        foreach (var taggedEntity in query)
        {
            hasVelocity = taggedEntity.Has<TestVelocity>();
        }

        await Assert.That(hasVelocity).IsFalse();
    }

    [Test]
    public async Task TaggedWorldEntity_HasTag_ExistingTag_ReturnsTrue()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });
        _world.AddTag<TestIsActive>(spawned);

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        bool hasTag = false;
        foreach (var taggedEntity in query)
        {
            hasTag = taggedEntity.HasTag<TestIsActive>();
        }

        await Assert.That(hasTag).IsTrue();
    }

    [Test]
    public async Task TaggedWorldEntity_HasTag_NonExistentTag_ReturnsFalse()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        bool hasTag = true;
        foreach (var taggedEntity in query)
        {
            hasTag = taggedEntity.HasTag<TestIsActive>();
        }

        await Assert.That(hasTag).IsFalse();
    }

    [Test]
    public async Task TaggedWorldEntity_SetTag_SetsTagOnEntity()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        foreach (var taggedEntity in query)
        {
            taggedEntity.SetTag<TestIsActive>(true);
        }

        await Assert.That(_world.HasTag<TestIsActive>(spawned)).IsTrue();
    }

    [Test]
    public async Task TaggedWorldEntity_SetTag_ClearsTagOnEntity()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });
        _world.AddTag<TestIsActive>(spawned);

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        foreach (var taggedEntity in query)
        {
            taggedEntity.SetTag<TestIsActive>(false);
        }

        await Assert.That(_world.HasTag<TestIsActive>(spawned)).IsFalse();
    }

    [Test]
    public async Task TaggedWorldEntity_TagMask_ReturnsCorrectMask()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });
        _world.AddTag<TestIsActive>(spawned);
        _world.AddTag<TestIsEnemy>(spawned);

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        TagMask mask = default;
        foreach (var taggedEntity in query)
        {
            mask = taggedEntity.TagMask;
        }

        await Assert.That(mask.Get(TestIsActive.TagId.Value)).IsTrue();
        await Assert.That(mask.Get(TestIsEnemy.TagId.Value)).IsTrue();
        await Assert.That(mask.Get(TestIsPlayer.TagId.Value)).IsFalse();
    }

    [Test]
    public async Task TaggedWorldEntity_ImplicitConversionToEntity_Works()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        Entity converted = default;
        foreach (var taggedEntity in query)
        {
            converted = taggedEntity; // implicit conversion
        }

        await Assert.That(converted).IsEqualTo(spawned);
    }

    [Test]
    public async Task TaggedWorldEntity_MultipleEntities_IteratesAll()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        var e3 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 1 });
        _world.AddComponent(e2, new TestPosition { X = 2 });
        _world.AddComponent(e3, new TestPosition { X = 3 });
        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsEnemy>(e2);

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        var entities = new List<Entity>();
        foreach (var taggedEntity in query)
        {
            entities.Add(taggedEntity.Entity);
        }

        await Assert.That(entities.Count).IsEqualTo(3);
        await Assert.That(entities).Contains(e1);
        await Assert.That(entities).Contains(e2);
        await Assert.That(entities).Contains(e3);
    }

    #endregion

    #region TaggedWorldEntityChunk Tests

    [Test]
    public async Task TaggedWorldEntityChunk_EntityCount_ReturnsCorrectCount()
    {
        _world.Spawn();
        _world.Spawn();
        _world.Spawn();

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .BuildChunk(_world);

        int totalCount = 0;
        foreach (var chunk in query)
        {
            totalCount += chunk.EntityCount;
        }

        await Assert.That(totalCount).IsEqualTo(3);
    }

    [Test]
    public async Task TaggedWorldEntityChunk_Get_ReturnsComponentSpan()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 10 });
        _world.AddComponent(e2, new TestPosition { X = 20 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
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
    public async Task TaggedWorldEntityChunk_Get_ModifiesComponents()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 1 });
        _world.AddComponent(e2, new TestPosition { X = 2 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
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
    public async Task TaggedWorldEntityChunk_Has_ExistingComponent_ReturnsTrue()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
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
    public async Task TaggedWorldEntityChunk_Has_NonExistentComponent_ReturnsFalse()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
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
    public async Task TaggedWorldEntityChunk_Handle_ReturnsValidHandle()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
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
    public async Task TaggedWorldEntityChunk_Handle_CanQueryChunkTagRegistry()
    {
        var e1 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 1 });
        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsEnemy>(e1);

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        ChunkHandle handle = default;
        foreach (var chunk in query)
        {
            handle = chunk.Handle;
        }

        // Use the handle to query the ChunkTagRegistry
        var chunkMask = _world.ChunkTagRegistry.GetChunkMask(handle);
        await Assert.That(chunkMask.Get(TestIsActive.TagId.Value)).IsTrue();
        await Assert.That(chunkMask.Get(TestIsEnemy.TagId.Value)).IsTrue();
    }

    [Test]
    public async Task TaggedWorldEntityChunk_TagMasks_ReturnsEntityTagsSpan()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 1 });
        _world.AddComponent(e2, new TestPosition { X = 2 });
        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsEnemy>(e2);

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        int activeCount = 0;
        int enemyCount = 0;
        foreach (var chunk in query)
        {
            var tagMasks = chunk.TagMasks;
            for (int i = 0; i < chunk.EntityCount; i++)
            {
                if (tagMasks[i].Mask.Get(TestIsActive.TagId.Value)) activeCount++;
                if (tagMasks[i].Mask.Get(TestIsEnemy.TagId.Value)) enemyCount++;
            }
        }

        await Assert.That(activeCount).IsEqualTo(1);
        await Assert.That(enemyCount).IsEqualTo(1);
    }

    [Test]
    public async Task TaggedWorldEntityChunk_GetEntityAt_ReturnsTaggedWorldEntity()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 10 });
        _world.AddComponent(e2, new TestPosition { X = 20 });
        _world.AddTag<TestIsActive>(e1);

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        var entities = new List<Entity>();
        int activeCount = 0;
        foreach (var chunk in query)
        {
            for (int i = 0; i < chunk.EntityCount; i++)
            {
                var taggedEntity = chunk.GetEntityAt(i);
                entities.Add(taggedEntity.Entity);
                if (taggedEntity.HasTag<TestIsActive>()) activeCount++;
            }
        }

        await Assert.That(entities.Count).IsEqualTo(2);
        await Assert.That(entities).Contains(e1);
        await Assert.That(entities).Contains(e2);
        await Assert.That(activeCount).IsEqualTo(1);
    }

    [Test]
    public async Task TaggedWorldEntityChunk_GetEntityAt_ModifiesComponentViaTaggedWorldEntity()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 5 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        foreach (var chunk in query)
        {
            for (int i = 0; i < chunk.EntityCount; i++)
            {
                var taggedEntity = chunk.GetEntityAt(i);
                ref var pos = ref taggedEntity.Get<TestPosition>();
                pos.X = 555;
            }
        }

        await Assert.That(_world.GetComponent<TestPosition>(spawned).X).IsEqualTo(555f);
    }

    [Test]
    public async Task TaggedWorldEntityChunk_GetEntityAt_SetTagViaTaggedWorldEntity()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        foreach (var chunk in query)
        {
            for (int i = 0; i < chunk.EntityCount; i++)
            {
                var taggedEntity = chunk.GetEntityAt(i);
                taggedEntity.SetTag<TestIsPlayer>(true);
            }
        }

        await Assert.That(_world.HasTag<TestIsPlayer>(spawned)).IsTrue();
    }

    #endregion

    #region QueryResult Tests for TaggedWorld

    [Test]
    public async Task QueryResult_TaggedWorld_EntityCount_ReturnsCorrectCount()
    {
        _world.Spawn();
        _world.Spawn();
        _world.Spawn();

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .Build(_world);

        await Assert.That(query.EntityCount).IsEqualTo(3);
    }

    [Test]
    public async Task QueryResult_TaggedWorld_IsEmpty_NoEntities_ReturnsTrue()
    {
        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        await Assert.That(query.IsEmpty).IsTrue();
    }

    [Test]
    public async Task QueryResult_TaggedWorld_IsEmpty_WithEntities_ReturnsFalse()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .Build(_world);

        await Assert.That(query.IsEmpty).IsFalse();
    }

    #endregion

    #region ChunkQueryResult Tests for TaggedWorld

    [Test]
    public async Task ChunkQueryResult_TaggedWorld_EntityCount_ReturnsCorrectCount()
    {
        _world.Spawn();
        _world.Spawn();
        _world.Spawn();

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .BuildChunk(_world);

        await Assert.That(query.EntityCount).IsEqualTo(3);
    }

    [Test]
    public async Task ChunkQueryResult_TaggedWorld_IsEmpty_NoEntities_ReturnsTrue()
    {
        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        await Assert.That(query.IsEmpty).IsTrue();
    }

    [Test]
    public async Task ChunkQueryResult_TaggedWorld_IsEmpty_WithEntities_ReturnsFalse()
    {
        var spawned = _world.Spawn();
        _world.AddComponent(spawned, new TestPosition { X = 1 });

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .BuildChunk(_world);

        await Assert.That(query.IsEmpty).IsFalse();
    }

    #endregion
}
