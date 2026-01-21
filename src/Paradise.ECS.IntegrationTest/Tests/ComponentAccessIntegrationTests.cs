namespace Paradise.ECS.IntegrationTest.Tests;

/// <summary>
/// Integration tests for component access and modification.
/// </summary>
public sealed class ComponentAccessIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task GetComponent_ReturnsCorrectValues()
    {
        var entity = CreatePlayer(100, 200, 100, "Hero");

        var pos = World.GetComponent<Position>(entity);
        var health = World.GetComponent<Health>(entity);
        var name = World.GetComponent<Name>(entity);

        await Assert.That(pos.X).IsEqualTo(100f);
        await Assert.That(pos.Y).IsEqualTo(200f);
        await Assert.That(health.Current).IsEqualTo(100);
        await Assert.That(health.Max).IsEqualTo(100);
        await Assert.That(name.ToString()).IsEqualTo("Hero");
    }

    [Test]
    public async Task SetComponent_UpdatesValue()
    {
        var entity = CreatePlayer(100, 200);

        World.SetComponent(entity, new Position(150, 250));
        var pos = World.GetComponent<Position>(entity);

        await Assert.That(pos.X).IsEqualTo(150f);
        await Assert.That(pos.Y).IsEqualTo(250f);
    }

    [Test]
    public async Task HasComponent_ReturnsTrueForExistingComponent()
    {
        var entity = CreatePlayer();

        await Assert.That(World.HasComponent<Position>(entity)).IsTrue();
        await Assert.That(World.HasComponent<Velocity>(entity)).IsTrue();
        await Assert.That(World.HasComponent<Health>(entity)).IsTrue();
    }

    [Test]
    public async Task HasTag_ReturnsTrueForPlayerTag()
    {
        var player = CreatePlayer();
        var enemy = CreateEnemy();

        await Assert.That(World.HasTag<PlayerTag>(player)).IsTrue();
        await Assert.That(World.HasTag<EnemyTag>(player)).IsFalse();
        await Assert.That(World.HasTag<EnemyTag>(enemy)).IsTrue();
        await Assert.That(World.HasTag<PlayerTag>(enemy)).IsFalse();
    }
}
