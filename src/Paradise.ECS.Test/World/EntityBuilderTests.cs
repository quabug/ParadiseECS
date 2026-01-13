namespace Paradise.ECS.Test;

/// <summary>
/// Tests for EntityBuilder fluent API.
/// </summary>
public class EntityBuilderTests : IDisposable
{
    private readonly World<Bit64, ComponentRegistry> _world;

    public EntityBuilderTests()
    {
        _world = new World<Bit64, ComponentRegistry>();
    }

    public void Dispose()
    {
        _world.Dispose();
    }

    [Test]
    public async Task Build_EmptyBuilder_CreatesEntityWithNoComponents()
    {
        var entity = EntityBuilder.Create()
            .Build(_world);

        var isValid = entity.IsValid;
        var isAlive = _world.IsAlive(entity);
        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasVel = _world.HasComponent<TestVelocity>(entity);

        await Assert.That(isValid).IsTrue();
        await Assert.That(isAlive).IsTrue();
        await Assert.That(hasPos).IsFalse();
        await Assert.That(hasVel).IsFalse();
    }

    [Test]
    public async Task Build_WithOneComponent_CreatesEntityWithComponent()
    {
        // Simplified API - no need to specify all generic types!
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 10, Y = 20, Z = 30 })
            .Build(_world);

        var isValid = entity.IsValid;
        var hasPos = _world.HasComponent<TestPosition>(entity);

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }

        await Assert.That(isValid).IsTrue();
        await Assert.That(hasPos).IsTrue();
        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
        await Assert.That(pos.Z).IsEqualTo(30f);
    }

    [Test]
    public async Task Build_WithMultipleComponents_CreatesEntityWithAllComponents()
    {
        // Simplified API - chaining works naturally!
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 10, Y = 20, Z = 30 })
            .Add(new TestVelocity { X = 1, Y = 2, Z = 3 })
            .Build(_world);

        var isValid = entity.IsValid;
        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasVel = _world.HasComponent<TestVelocity>(entity);

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

        await Assert.That(isValid).IsTrue();
        await Assert.That(hasPos).IsTrue();
        await Assert.That(hasVel).IsTrue();
        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
        await Assert.That(pos.Z).IsEqualTo(30f);
        await Assert.That(vel.X).IsEqualTo(1f);
        await Assert.That(vel.Y).IsEqualTo(2f);
        await Assert.That(vel.Z).IsEqualTo(3f);
    }

    [Test]
    public async Task Build_WithDefaultComponent_CreatesEntityWithDefaultValue()
    {
        // Simplified API - only need to specify the component type
        var entity = EntityBuilder.Create()
            .Add(default(TestPosition))
            .Build(_world);

        var isValid = entity.IsValid;
        var hasPos = _world.HasComponent<TestPosition>(entity);

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }

        await Assert.That(isValid).IsTrue();
        await Assert.That(hasPos).IsTrue();
        await Assert.That(pos.X).IsEqualTo(0f);
        await Assert.That(pos.Y).IsEqualTo(0f);
        await Assert.That(pos.Z).IsEqualTo(0f);
    }

    [Test]
    public async Task Build_MultipleEntities_IndependentInstances()
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

        await Assert.That(e1.Id).IsNotEqualTo(e2.Id);
        await Assert.That(posX1).IsEqualTo(100f);
        await Assert.That(posX2).IsEqualTo(200f);
    }

    [Test]
    public async Task Build_MultipleEntitiesSameArchetype_SharesArchetype()
    {
        _ = EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Build(_world);

        _ = EntityBuilder.Create()
            .Add(new TestPosition { X = 200 })
            .Build(_world);

        // Both should be in same archetype
        var count = _world.ArchetypeRegistry.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Build_DifferentComponentCombinations_CreatesDifferentArchetypes()
    {
        _ = EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Build(_world);

        _ = EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Add(new TestVelocity { X = 1 })
            .Build(_world);

        // Should be in different archetypes
        var count = _world.ArchetypeRegistry.Count;
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task EntityCount_AfterMultipleBuilds_CorrectCount()
    {
        for (int i = 0; i < 10; i++)
        {
            EntityBuilder.Create()
                .Add(new TestPosition { X = i })
                .Build(_world);
        }

        var count = _world.EntityCount;
        await Assert.That(count).IsEqualTo(10);
    }

    [Test]
    public async Task Overwrite_SpawnedEntity_SetsUpComponents()
    {
        var entity = _world.Spawn();

        var result = EntityBuilder.Create()
            .Add(new TestPosition { X = 10, Y = 20, Z = 30 })
            .Overwrite(entity, _world);

        var isAlive = _world.IsAlive(result);
        var hasPos = _world.HasComponent<TestPosition>(result);

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(result))
        {
            pos = posRef.Value;
        }

        await Assert.That(result.Id).IsEqualTo(entity.Id);
        await Assert.That(result.Version).IsEqualTo(entity.Version);
        await Assert.That(isAlive).IsTrue();
        await Assert.That(hasPos).IsTrue();
        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
        await Assert.That(pos.Z).IsEqualTo(30f);
    }

    [Test]
    public async Task Overwrite_WithMultipleComponents_SetsUpAllComponents()
    {
        var entity = _world.Spawn();

        EntityBuilder.Create()
            .Add(new TestPosition { X = 1, Y = 2, Z = 3 })
            .Add(new TestVelocity { X = 4, Y = 5, Z = 6 })
            .Overwrite(entity, _world);

        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasVel = _world.HasComponent<TestVelocity>(entity);

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

        await Assert.That(hasPos).IsTrue();
        await Assert.That(hasVel).IsTrue();
        await Assert.That(pos.X).IsEqualTo(1f);
        await Assert.That(pos.Y).IsEqualTo(2f);
        await Assert.That(pos.Z).IsEqualTo(3f);
        await Assert.That(vel.X).IsEqualTo(4f);
        await Assert.That(vel.Y).IsEqualTo(5f);
        await Assert.That(vel.Z).IsEqualTo(6f);
    }

    [Test]
    public async Task Overwrite_EmptyBuilder_EntityStaysWithoutComponents()
    {
        var entity = _world.Spawn();

        var result = EntityBuilder.Create()
            .Overwrite(entity, _world);

        var isAlive = _world.IsAlive(result);
        var hasPos = _world.HasComponent<TestPosition>(result);
        var hasVel = _world.HasComponent<TestVelocity>(result);

        await Assert.That(result.Id).IsEqualTo(entity.Id);
        await Assert.That(isAlive).IsTrue();
        await Assert.That(hasPos).IsFalse();
        await Assert.That(hasVel).IsFalse();
    }

    [Test]
    public async Task Overwrite_DeadEntity_ThrowsArgumentException()
    {
        var entity = _world.Spawn();
        _world.Despawn(entity);

        var action = () => EntityBuilder.Create()
            .Add(new TestPosition { X = 10 })
            .Overwrite(entity, _world);

        await Assert.That(action).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task Overwrite_InvalidEntity_ThrowsException()
    {
        var invalidEntity = default(Entity);

        var action = () => EntityBuilder.Create()
            .Add(new TestPosition { X = 10 })
            .Overwrite(invalidEntity, _world);

        await Assert.That(action).ThrowsException();
    }

    [Test]
    public async Task Overwrite_MultipleEntities_IndependentInstances()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();

        EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Overwrite(e1, _world);

        EntityBuilder.Create()
            .Add(new TestPosition { X = 200 })
            .Overwrite(e2, _world);

        float posX1, posX2;
        using (var ref1 = _world.GetComponent<TestPosition>(e1))
        {
            posX1 = ref1.Value.X;
        }
        using (var ref2 = _world.GetComponent<TestPosition>(e2))
        {
            posX2 = ref2.Value.X;
        }

        await Assert.That(e1.Id).IsNotEqualTo(e2.Id);
        await Assert.That(posX1).IsEqualTo(100f);
        await Assert.That(posX2).IsEqualTo(200f);
    }

    [Test]
    public async Task Overwrite_SameArchetypeAsBuiltEntities_SharesArchetype()
    {
        // Create entity using Build
        _ = EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Build(_world);

        // Create entity using Spawn + Overwrite
        var spawned = _world.Spawn();
        EntityBuilder.Create()
            .Add(new TestPosition { X = 200 })
            .Overwrite(spawned, _world);

        // Both should be in same archetype
        var count = _world.ArchetypeRegistry.Count;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Overwrite_EntityWithExistingComponents_ReplacesComponents()
    {
        // Create entity with Position component
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 100, Y = 200, Z = 300 })
            .Build(_world);

        // Overwrite with Velocity - this replaces, not adds
        EntityBuilder.Create()
            .Add(new TestVelocity { X = 1, Y = 2, Z = 3 })
            .Overwrite(entity, _world);

        // Entity now has only Velocity, not Position (Overwrite replaces all components)
        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasVel = _world.HasComponent<TestVelocity>(entity);

        await Assert.That(hasPos).IsFalse();
        await Assert.That(hasVel).IsTrue();

        TestVelocity vel;
        using (var velRef = _world.GetComponent<TestVelocity>(entity))
        {
            vel = velRef.Value;
        }

        await Assert.That(vel.X).IsEqualTo(1f);
        await Assert.That(vel.Y).IsEqualTo(2f);
        await Assert.That(vel.Z).IsEqualTo(3f);
    }
}
