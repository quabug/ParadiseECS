namespace Paradise.ECS.IntegrationTest.Tests;

/// <summary>
/// Integration tests for chunk-level query operations.
/// </summary>
public sealed class ChunkQueryIntegrationTests : IntegrationTestBase
{
    [Before(Test)]
    public void SetupEntities()
    {
        // Create diverse entities across different archetypes
        EntityBuilder.Create()
            .Add(new Position(10, 20))
            .Add(new Velocity(1, 2))
            .Add(new Health(100))
            .Build(World);

        EntityBuilder.Create()
            .Add(new Position(30, 40))
            .Add(new Health(80))
            .Build(World);

        EntityBuilder.Create()
            .Add(new Position(50, 60))
            .Add(new Velocity(3, 4))
            .Add(new Health(50))
            .Build(World);

        EntityBuilder.Create()
            .Add(new Position(70, 80))
            .Add(new Health(40))
            .Build(World);

        EntityBuilder.Create()
            .Add(new Position(90, 100))
            .Add(new Health(25))
            .Add(new Name("Named"))
            .Build(World);
    }

    [Test]
    public async Task WorldChunkQuery_IteratesChunks()
    {
        var chunkQuery = QueryBuilder.Create()
            .With<Position>()
            .With<Health>()
            .BuildChunk(World);

        var entityCount = chunkQuery.EntityCount;
        await Assert.That(entityCount).IsEqualTo(5);

        int totalFromChunks = 0;
        bool allHavePosition = true;
        bool allHaveHealth = true;
        foreach (var chunk in chunkQuery)
        {
            totalFromChunks += chunk.EntityCount;
            if (!chunk.Has<Position>()) allHavePosition = false;
            if (!chunk.Has<Health>()) allHaveHealth = false;
        }

        await Assert.That(totalFromChunks).IsEqualTo(5);
        await Assert.That(allHavePosition).IsTrue();
        await Assert.That(allHaveHealth).IsTrue();
    }

    [Test]
    public async Task WorldChunkQuery_GetSpan_ReturnsBatchData()
    {
        var chunkQuery = QueryBuilder.Create()
            .With<Position>()
            .With<Health>()
            .BuildChunk(World);

        bool spansMatchEntityCount = true;
        foreach (var chunk in chunkQuery)
        {
            var positionsLength = chunk.GetSpan<Position>().Length;
            var healthsLength = chunk.GetSpan<Health>().Length;
            if (positionsLength != chunk.EntityCount || healthsLength != chunk.EntityCount)
            {
                spansMatchEntityCount = false;
            }
        }

        await Assert.That(spansMatchEntityCount).IsTrue();
    }

    [Test]
    public async Task WorldChunkQuery_TryGetSpan_ReturnsFalseForMissingComponent()
    {
        var chunkQuery = QueryBuilder.Create()
            .With<Position>()
            .With<Health>()
            .BuildChunk(World);

        bool foundChunkWithoutVelocity = false;
        bool foundChunkWithVelocity = false;
        bool velocitySpanLengthCorrect = true;

        foreach (var chunk in chunkQuery)
        {
            if (chunk.TryGetSpan<Velocity>(out var velocities))
            {
                foundChunkWithVelocity = true;
                if (velocities.Length != chunk.EntityCount)
                {
                    velocitySpanLengthCorrect = false;
                }
            }
            else
            {
                foundChunkWithoutVelocity = true;
            }
        }

        await Assert.That(foundChunkWithVelocity).IsTrue();
        await Assert.That(foundChunkWithoutVelocity).IsTrue();
        await Assert.That(velocitySpanLengthCorrect).IsTrue();
    }

    [Test]
    public async Task PlayerChunkQuery_TypeSafeAccessors()
    {
        // Create entities that match Player query
        EntityBuilder.Create()
            .Add(new Position(100, 200))
            .Add(new Velocity(5, 0))
            .Add(new Health(100))
            .Build(World);

        EntityBuilder.Create()
            .Add(new Position(200, 300))
            .Add(new Health(80))
            .Build(World);

        var chunkQuery = Player.ChunkQuery.Build(World.World);
        var entityCount = chunkQuery.EntityCount;
        await Assert.That(entityCount).IsGreaterThan(0);

        bool spansMatchEntityCount = true;
        foreach (var chunk in chunkQuery)
        {
            // Type-safe span accessors
            var positionsLength = chunk.Positions.Length;
            var hpsLength = chunk.Hps.Length;

            if (positionsLength != chunk.EntityCount || hpsLength != chunk.EntityCount)
            {
                spansMatchEntityCount = false;
            }
        }

        await Assert.That(spansMatchEntityCount).IsTrue();
    }

    [Test]
    public async Task PlayerChunkQuery_BatchModification()
    {
        EntityBuilder.Create()
            .Add(new Position(100, 200))
            .Add(new Velocity(5, 0))
            .Add(new Health(100))
            .Build(World);

        var chunkQuery = Player.ChunkQuery.Build(World.World);

        foreach (var chunk in chunkQuery)
        {
            var positions = chunk.Positions;
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = new Position(positions[i].X + 1000, positions[i].Y + 1000);
            }
        }

        // Verify modifications persisted
        bool allPositionsModified = true;
        foreach (var p in Player.Query.Build(World.World))
        {
            if (p.Position.X < 1000f)
            {
                allPositionsModified = false;
            }
        }

        await Assert.That(allPositionsModified).IsTrue();
    }
}
