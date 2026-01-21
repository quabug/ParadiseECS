namespace Paradise.ECS.IntegrationTest.Tests;

/// <summary>
/// Integration tests for entity creation functionality.
/// </summary>
public sealed class EntityCreationIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task Spawn_EmptyEntity_CreatesValidEntity()
    {
        var entity = World.Spawn();

        await Assert.That(entity.IsValid).IsTrue();
        await Assert.That(World.IsAlive(entity)).IsTrue();
        await Assert.That(World.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task EntityBuilder_CreatePlayerWithComponents_AllComponentsPresent()
    {
        var playerEntity = CreatePlayer();

        await Assert.That(World.IsAlive(playerEntity)).IsTrue();
        await Assert.That(World.EntityCount).IsEqualTo(1);
        await Assert.That(World.HasComponent<Position>(playerEntity)).IsTrue();
        await Assert.That(World.HasComponent<Velocity>(playerEntity)).IsTrue();
        await Assert.That(World.HasComponent<Health>(playerEntity)).IsTrue();
        await Assert.That(World.HasComponent<Name>(playerEntity)).IsTrue();
        await Assert.That(World.HasTag<PlayerTag>(playerEntity)).IsTrue();
    }

    [Test]
    public async Task EntityBuilder_CreateMultipleEnemies_AllCreatedSuccessfully()
    {
        var enemies = new Entity[5];
        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i] = CreateEnemy(i * 50, 300);
        }

        await Assert.That(World.EntityCount).IsEqualTo(5);
        foreach (var enemy in enemies)
        {
            await Assert.That(World.IsAlive(enemy)).IsTrue();
            await Assert.That(World.HasTag<EnemyTag>(enemy)).IsTrue();
        }
    }

    [Test]
    public async Task EntityBuilder_CreateMixedEntities_CorrectCounts()
    {
        var emptyEntity = World.Spawn();
        var playerEntity = CreatePlayer();
        var enemies = new Entity[5];
        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i] = CreateEnemy(i * 50, 300);
        }

        await Assert.That(World.EntityCount).IsEqualTo(7);
        await Assert.That(World.IsAlive(emptyEntity)).IsTrue();
        await Assert.That(World.IsAlive(playerEntity)).IsTrue();
    }
}
