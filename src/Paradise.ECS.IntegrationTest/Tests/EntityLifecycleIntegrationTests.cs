namespace Paradise.ECS.IntegrationTest.Tests;

/// <summary>
/// Integration tests for entity lifecycle (spawn, despawn, clear).
/// </summary>
public sealed class EntityLifecycleIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task Despawn_RemovesEntity()
    {
        var entity = CreateEnemy();
        await Assert.That(World.IsAlive(entity)).IsTrue();

        World.Despawn(entity);

        await Assert.That(World.IsAlive(entity)).IsFalse();
        await Assert.That(World.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task Despawn_TwiceOnSameEntity_ReturnsFalseSecondTime()
    {
        var entity = CreateEnemy();
        World.Despawn(entity);

        var despawnedAgain = World.Despawn(entity);

        await Assert.That(despawnedAgain).IsFalse();
    }

    [Test]
    public async Task Clear_RemovesAllEntities()
    {
        var player = CreatePlayer();
        for (int i = 0; i < 5; i++)
        {
            CreateEnemy(i * 50, 300);
        }
        await Assert.That(World.EntityCount).IsEqualTo(6);

        World.Clear();

        await Assert.That(World.EntityCount).IsEqualTo(0);
        await Assert.That(World.IsAlive(player)).IsFalse();
    }

    [Test]
    public async Task EntityOverwrite_ReplacesComponents()
    {
        var entity = CreatePlayer(100, 200);
        await Assert.That(World.HasComponent<Velocity>(entity)).IsTrue();

        EntityBuilder.Create()
            .Add(new Position(0, 0))
            .Add(new Health(200))
            .AddTag(default(PlayerTag), World)
            .Overwrite(entity, World);

        await Assert.That(World.HasComponent<Velocity>(entity)).IsFalse();
        var pos = World.GetComponent<Position>(entity);
        await Assert.That(pos.X).IsEqualTo(0f);
        await Assert.That(pos.Y).IsEqualTo(0f);
        var health = World.GetComponent<Health>(entity);
        await Assert.That(health.Max).IsEqualTo(200);
    }
}
