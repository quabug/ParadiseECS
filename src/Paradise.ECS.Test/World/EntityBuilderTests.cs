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
}
