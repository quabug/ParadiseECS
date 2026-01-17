namespace Paradise.ECS.Test;

/// <summary>
/// Tests for World component operations.
/// </summary>
public sealed class WorldComponentTests : IDisposable
{
    private readonly ChunkManager _chunkManager = new(NativeMemoryAllocator.Shared);
    private readonly SharedArchetypeMetadata<Bit64, ComponentRegistry> _sharedMetadata = new(NativeMemoryAllocator.Shared);
    private readonly World<Bit64, ComponentRegistry> _world;

    public WorldComponentTests()
    {
        _world = new World<Bit64, ComponentRegistry>(_sharedMetadata, _chunkManager);
    }

    public void Dispose()
    {
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
    }

    #region HasComponent Tests

    [Test]
    public async Task HasComponent_EntityWithNoComponents_ReturnsFalse()
    {
        var entity = _world.Spawn();

        var has = _world.HasComponent<TestPosition>(entity);

        await Assert.That(has).IsFalse();
    }

    [Test]
    public async Task HasComponent_EntityWithComponent_ReturnsTrue()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 1 });

        var has = _world.HasComponent<TestPosition>(entity);

        await Assert.That(has).IsTrue();
    }

    [Test]
    public async Task HasComponent_InvalidEntity_ReturnsFalse()
    {
        var invalid = default(Entity);

        var has = _world.HasComponent<TestPosition>(invalid);

        await Assert.That(has).IsFalse();
    }

    [Test]
    public async Task HasComponent_DeadEntity_ReturnsFalse()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 1 });
        _world.Despawn(entity);

        var has = _world.HasComponent<TestPosition>(entity);

        await Assert.That(has).IsFalse();
    }

    [Test]
    public async Task HasComponent_DifferentComponent_ReturnsFalse()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 1 });

        var has = _world.HasComponent<TestVelocity>(entity);

        await Assert.That(has).IsFalse();
    }

    #endregion

    #region AddComponent Tests

    [Test]
    public async Task AddComponent_ToEmptyEntity_AddsComponent()
    {
        var entity = _world.Spawn();

        _world.AddComponent(entity, new TestPosition { X = 10, Y = 20, Z = 30 });

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsTrue();
    }

    [Test]
    public async Task AddComponent_ToEntityWithOtherComponent_AddsComponent()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });

        _world.AddComponent(entity, new TestVelocity { X = 1, Y = 2 });

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestVelocity>(entity)).IsTrue();
    }

    [Test]
    public async Task AddComponent_DuplicateComponent_Throws()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });

        await Assert.That(() => _world.AddComponent(entity, new TestPosition { X = 20 }))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task AddComponent_ToDeadEntity_Throws()
    {
        var entity = _world.Spawn();
        _world.Despawn(entity);

        await Assert.That(() => _world.AddComponent(entity, new TestPosition { X = 10 }))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task AddComponent_ToInvalidEntity_Throws()
    {
        var invalid = default(Entity);

        await Assert.That(() => _world.AddComponent(invalid, new TestPosition { X = 10 }))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task AddComponent_PreservesExistingComponentValues()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10, Y = 20, Z = 30 });

        _world.AddComponent(entity, new TestVelocity { X = 1 });

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }
        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
        await Assert.That(pos.Z).IsEqualTo(30f);
    }

    [Test]
    public async Task AddComponent_TagComponent_Succeeds()
    {
        var entity = _world.Spawn();

        _world.AddComponent<TestTag>(entity);

        await Assert.That(_world.HasComponent<TestTag>(entity)).IsTrue();
    }

    [Test]
    public async Task AddComponent_MultipleComponents_AllPresent()
    {
        var entity = _world.Spawn();

        _world.AddComponent(entity, new TestPosition { X = 1 });
        _world.AddComponent(entity, new TestVelocity { Y = 2 });
        _world.AddComponent(entity, new TestHealth { Current = 100, Max = 100 });

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestVelocity>(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestHealth>(entity)).IsTrue();
    }

    #endregion

    #region RemoveComponent Tests

    [Test]
    public async Task RemoveComponent_ExistingComponent_RemovesIt()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });

        _world.RemoveComponent<TestPosition>(entity);

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsFalse();
    }

    [Test]
    public async Task RemoveComponent_NonexistentComponent_Throws()
    {
        var entity = _world.Spawn();

        await Assert.That(() => _world.RemoveComponent<TestPosition>(entity))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task RemoveComponent_PreservesOtherComponents()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });
        _world.AddComponent(entity, new TestVelocity { X = 5 });

        _world.RemoveComponent<TestVelocity>(entity);

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestVelocity>(entity)).IsFalse();

        float posX;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            posX = posRef.Value.X;
        }
        await Assert.That(posX).IsEqualTo(10f);
    }

    [Test]
    public async Task RemoveComponent_LastComponent_EntityStaysAlive()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });

        _world.RemoveComponent<TestPosition>(entity);

        await Assert.That(_world.IsAlive(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsFalse();
    }

    [Test]
    public async Task RemoveComponent_FromDeadEntity_Throws()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });
        _world.Despawn(entity);

        await Assert.That(() => _world.RemoveComponent<TestPosition>(entity))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task RemoveComponent_TagComponent_Succeeds()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestTag>(entity);

        _world.RemoveComponent<TestTag>(entity);

        await Assert.That(_world.HasComponent<TestTag>(entity)).IsFalse();
    }

    #endregion

    #region GetComponent Tests

    [Test]
    public async Task GetComponent_ExistingComponent_ReturnsRef()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10, Y = 20, Z = 30 });

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }

        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
        await Assert.That(pos.Z).IsEqualTo(30f);
    }

    [Test]
    public async Task GetComponent_NonexistentComponent_Throws()
    {
        var entity = _world.Spawn();

        await Assert.That(() =>
        {
            using var _ = _world.GetComponent<TestPosition>(entity);
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetComponent_ModifyViaRef_PersistsChange()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });

        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            posRef.Value.X = 999;
        }

        float posX;
        using (var posRef2 = _world.GetComponent<TestPosition>(entity))
        {
            posX = posRef2.Value.X;
        }
        await Assert.That(posX).IsEqualTo(999f);
    }

    [Test]
    public async Task GetComponent_DeadEntity_Throws()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });
        _world.Despawn(entity);

        await Assert.That(() =>
        {
            using var _ = _world.GetComponent<TestPosition>(entity);
        }).Throws<InvalidOperationException>();
    }

    #endregion

    #region SetComponent Tests

    [Test]
    public async Task SetComponent_ExistingComponent_UpdatesValue()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });

        _world.SetComponent(entity, new TestPosition { X = 50, Y = 60, Z = 70 });

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }
        await Assert.That(pos.X).IsEqualTo(50f);
        await Assert.That(pos.Y).IsEqualTo(60f);
        await Assert.That(pos.Z).IsEqualTo(70f);
    }

    [Test]
    public async Task SetComponent_NonexistentComponent_Throws()
    {
        var entity = _world.Spawn();

        await Assert.That(() => _world.SetComponent(entity, new TestPosition { X = 10 }))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task SetComponent_DeadEntity_Throws()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });
        _world.Despawn(entity);

        await Assert.That(() => _world.SetComponent(entity, new TestPosition { X = 20 }))
            .ThrowsExactly<InvalidOperationException>();
    }

    #endregion

    #region Multiple Entity Tests

    [Test]
    public async Task MultipleEntities_SameArchetype_IndependentComponents()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();

        _world.AddComponent(e1, new TestPosition { X = 100 });
        _world.AddComponent(e2, new TestPosition { X = 200 });

        float posX1, posX2;
        using (var ref1 = _world.GetComponent<TestPosition>(e1))
        {
            posX1 = ref1.Value.X;
        }
        using (var ref2 = _world.GetComponent<TestPosition>(e2))
        {
            posX2 = ref2.Value.X;
        }

        await Assert.That(posX1).IsEqualTo(100f);
        await Assert.That(posX2).IsEqualTo(200f);
    }

    [Test]
    public async Task DespawnEntity_DoesNotAffectOtherEntities()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();

        _world.AddComponent(e1, new TestPosition { X = 100 });
        _world.AddComponent(e2, new TestPosition { X = 200 });

        _world.Despawn(e1);

        await Assert.That(_world.IsAlive(e2)).IsTrue();
        float posX2;
        using (var ref2 = _world.GetComponent<TestPosition>(e2))
        {
            posX2 = ref2.Value.X;
        }
        await Assert.That(posX2).IsEqualTo(200f);
    }

    [Test]
    public async Task ManyEntities_AllComponentsAccessible()
    {
        const int count = 100;
        var entities = new Entity[count];

        for (int i = 0; i < count; i++)
        {
            entities[i] = _world.Spawn();
            _world.AddComponent(entities[i], new TestPosition { X = i });
        }

        for (int i = 0; i < count; i++)
        {
            float posX;
            using (var posRef = _world.GetComponent<TestPosition>(entities[i]))
            {
                posX = posRef.Value.X;
            }
            await Assert.That(posX).IsEqualTo((float)i);
        }
    }

    #endregion
}
