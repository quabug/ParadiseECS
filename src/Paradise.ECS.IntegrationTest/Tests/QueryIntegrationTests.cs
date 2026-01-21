namespace Paradise.ECS.IntegrationTest.Tests;

/// <summary>
/// Integration tests for query functionality.
/// </summary>
public sealed class QueryIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task Query_WithPositionAndVelocity_ReturnsCorrectCount()
    {
        CreatePlayer();  // Has Position and Velocity
        for (int i = 0; i < 4; i++)
        {
            CreateEnemy(i * 50, 300);  // Has Position and Velocity
        }

        var query = QueryBuilder
            .Create()
            .With<Position>()
            .With<Velocity>()
            .Build(World);

        await Assert.That(query.EntityCount).IsEqualTo(5);
        await Assert.That(query.IsEmpty).IsFalse();
    }

    [Test]
    public async Task Query_IteratesAllMatchingEntities()
    {
        CreatePlayer(100, 200);
        CreateEnemy(50, 300);
        CreateEnemy(100, 300);

        var query = QueryBuilder
            .Create()
            .With<Position>()
            .With<Velocity>()
            .Build(World);

        var positions = new List<(float X, float Y)>();
        foreach (var entity in query)
        {
            var pos = entity.Get<Position>();
            positions.Add((pos.X, pos.Y));
        }

        await Assert.That(positions.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Query_GetRef_ModifiesComponent()
    {
        var entity = CreatePlayer(100, 200);

        var query = QueryBuilder
            .Create()
            .With<Position>()
            .Build(World);

        foreach (var e in query)
        {
            ref var pos = ref e.Get<Position>();
            pos = new Position(pos.X + 50, pos.Y + 50);
        }

        var updatedPos = World.GetComponent<Position>(entity);
        await Assert.That(updatedPos.X).IsEqualTo(150f);
        await Assert.That(updatedPos.Y).IsEqualTo(250f);
    }

    [Test]
    public async Task Query_FilterByTag_WorksManually()
    {
        CreatePlayer();
        for (int i = 0; i < 4; i++)
        {
            CreateEnemy(i * 50, 300);
        }

        var query = QueryBuilder
            .Create()
            .With<Position>()
            .With<Velocity>()
            .Build(World);

        var nonPlayerCount = 0;
        foreach (var entity in query)
        {
            if (!World.HasTag<PlayerTag>(entity.Entity))
            {
                nonPlayerCount++;
            }
        }

        await Assert.That(nonPlayerCount).IsEqualTo(4);
    }

    [Test]
    public async Task Query_HealthQuery_ReturnsAllDamageableEntities()
    {
        CreatePlayer();
        for (int i = 0; i < 4; i++)
        {
            CreateEnemy(i * 50, 300);
        }

        var healthQuery = QueryBuilder
            .Create()
            .With<Health>()
            .Build(World);

        await Assert.That(healthQuery.EntityCount).IsEqualTo(5);
    }

    [Test]
    public async Task Query_ArchetypeIteration_WorksCorrectly()
    {
        CreatePlayer();
        for (int i = 0; i < 4; i++)
        {
            CreateEnemy(i * 50, 300);
        }

        var query = QueryBuilder
            .Create()
            .With<Position>()
            .With<Velocity>()
            .Build(World);

        var archetypeCount = 0;
        foreach (var archetype in query.Query.Archetypes)
        {
            archetypeCount++;
            await Assert.That(archetype.EntityCount).IsGreaterThan(0);
        }

        await Assert.That(archetypeCount).IsGreaterThan(0);
    }

    [Test]
    public async Task Query_ChunkIteration_WorksCorrectly()
    {
        CreatePlayer();
        for (int i = 0; i < 4; i++)
        {
            CreateEnemy(i * 50, 300);
        }

        var query = QueryBuilder
            .Create()
            .With<Position>()
            .With<Velocity>()
            .Build(World);

        var totalEntitiesFromChunks = 0;
        foreach (var chunkInfo in query.Query.Chunks)
        {
            totalEntitiesFromChunks += chunkInfo.EntityCount;
        }

        await Assert.That(totalEntitiesFromChunks).IsEqualTo(5);
    }
}
