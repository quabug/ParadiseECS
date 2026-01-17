namespace Paradise.ECS.Test;

/// <summary>
/// Tests for EntityBuilder.
/// </summary>
public sealed class EntityBuilderTests : IDisposable
{
    private readonly ChunkManager _chunkManager = new(NativeMemoryAllocator.Shared);
    private readonly SharedArchetypeMetadata<Bit64, ComponentRegistry> _sharedMetadata = new(NativeMemoryAllocator.Shared);
    private readonly World<Bit64, ComponentRegistry> _world;

    public EntityBuilderTests()
    {
        _world = new World<Bit64, ComponentRegistry>(_sharedMetadata, _chunkManager);
    }

    public void Dispose()
    {
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
    }

    [Test]
    public async Task Spawn_NoComponents_CreatesEmptyEntity()
    {
        var entity = _world.Spawn();

        await Assert.That(entity.IsValid).IsTrue();
        await Assert.That(_world.IsAlive(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsFalse();
    }

    [Test]
    public async Task EntityBuilder_SingleComponent_CreatesEntity()
    {
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 10, Y = 20, Z = 30 })
            .Build(_world);

        await Assert.That(entity.IsValid).IsTrue();
        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsTrue();

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
    public async Task EntityBuilder_MultipleComponents_CreatesEntity()
    {
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 10 })
            .Add(new TestVelocity { Y = 20 })
            .Add(new TestHealth { Current = 100, Max = 100 })
            .Build(_world);

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestVelocity>(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestHealth>(entity)).IsTrue();
    }

    [Test]
    public async Task EntityBuilder_ComponentValues_ArePreserved()
    {
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 1, Y = 2, Z = 3 })
            .Add(new TestVelocity { X = 4, Y = 5, Z = 6 })
            .Build(_world);

        TestPosition pos;
        TestVelocity vel;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }
        using (var velRef = _world.GetComponent<TestVelocity>(entity))
        {
            vel = velRef.Value;
        }

        await Assert.That(pos.X).IsEqualTo(1f);
        await Assert.That(pos.Y).IsEqualTo(2f);
        await Assert.That(pos.Z).IsEqualTo(3f);
        await Assert.That(vel.X).IsEqualTo(4f);
        await Assert.That(vel.Y).IsEqualTo(5f);
        await Assert.That(vel.Z).IsEqualTo(6f);
    }

    [Test]
    public async Task EntityBuilder_TagComponent_Works()
    {
        var entity = EntityBuilder.Create()
            .Add(new TestTag())
            .Build(_world);

        await Assert.That(_world.HasComponent<TestTag>(entity)).IsTrue();
    }

    [Test]
    public async Task EntityBuilder_MultipleEntities_Independent()
    {
        var e1 = EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Build(_world);

        var e2 = EntityBuilder.Create()
            .Add(new TestPosition { X = 200 })
            .Build(_world);

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
    public async Task EntityBuilder_EmptyBuilder_CreatesEmptyEntity()
    {
        var entity = EntityBuilder.Create().Build(_world);

        await Assert.That(entity.IsValid).IsTrue();
        await Assert.That(_world.IsAlive(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsFalse();
    }

    [Test]
    public async Task EntityBuilder_AddTo_AddsComponent()
    {
        var entity = _world.Spawn();

        EntityBuilder.Create()
            .Add(new TestPosition { X = 50 })
            .AddTo(entity, _world);

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsTrue();
        float posX;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            posX = posRef.Value.X;
        }
        await Assert.That(posX).IsEqualTo(50f);
    }

    [Test]
    public async Task EntityBuilder_AddTo_MultipleToExisting_AddsAllComponents()
    {
        var entity = _world.Spawn();

        EntityBuilder.Create()
            .Add(new TestPosition { X = 10 })
            .Add(new TestVelocity { Y = 20 })
            .AddTo(entity, _world);

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestVelocity>(entity)).IsTrue();
    }

    [Test]
    public async Task EntityBuilder_AddTo_PreservesExistingComponents()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });

        EntityBuilder.Create()
            .Add(new TestVelocity { Y = 20 })
            .AddTo(entity, _world);

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestVelocity>(entity)).IsTrue();

        float posX;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            posX = posRef.Value.X;
        }
        await Assert.That(posX).IsEqualTo(10f);
    }

    [Test]
    public async Task EntityBuilder_Overwrite_OverwritesAllComponents()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });
        _world.AddComponent(entity, new TestVelocity { Y = 20 });

        EntityBuilder.Create()
            .Add(new TestHealth { Current = 100, Max = 100 })
            .Overwrite(entity, _world);

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsFalse();
        await Assert.That(_world.HasComponent<TestVelocity>(entity)).IsFalse();
        await Assert.That(_world.HasComponent<TestHealth>(entity)).IsTrue();
    }

    [Test]
    public async Task EntityBuilder_Overwrite_EmptyBuilder_RemovesAllComponents()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10 });

        EntityBuilder.Create().Overwrite(entity, _world);

        await Assert.That(_world.IsAlive(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsFalse();
    }
}
