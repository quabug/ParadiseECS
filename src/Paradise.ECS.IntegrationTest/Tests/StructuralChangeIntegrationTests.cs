namespace Paradise.ECS.IntegrationTest.Tests;

/// <summary>
/// Integration tests for structural changes (adding/removing components).
/// </summary>
public sealed class StructuralChangeIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task AddComponent_ToEmptyEntity_ComponentPresent()
    {
        var entity = World.Spawn();

        World.AddComponent(entity, new Position(10, 20));

        await Assert.That(World.HasComponent<Position>(entity)).IsTrue();
        var pos = World.GetComponent<Position>(entity);
        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
    }

    [Test]
    public async Task RemoveComponent_FromEntity_ComponentRemoved()
    {
        var entity = World.Spawn();
        World.AddComponent(entity, new Position(10, 20));

        World.RemoveComponent<Position>(entity);

        await Assert.That(World.HasComponent<Position>(entity)).IsFalse();
    }

    [Test]
    public async Task AddAndRemoveMultipleComponents_WorksCorrectly()
    {
        var entity = World.Spawn();

        World.AddComponent(entity, new Position(10, 20));
        World.AddComponent(entity, new Velocity(1, 2));
        World.AddComponent(entity, new Health(100));

        await Assert.That(World.HasComponent<Position>(entity)).IsTrue();
        await Assert.That(World.HasComponent<Velocity>(entity)).IsTrue();
        await Assert.That(World.HasComponent<Health>(entity)).IsTrue();

        World.RemoveComponent<Velocity>(entity);

        await Assert.That(World.HasComponent<Position>(entity)).IsTrue();
        await Assert.That(World.HasComponent<Velocity>(entity)).IsFalse();
        await Assert.That(World.HasComponent<Health>(entity)).IsTrue();
    }
}
