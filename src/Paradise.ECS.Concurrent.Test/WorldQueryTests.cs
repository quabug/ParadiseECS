namespace Paradise.ECS.Concurrent.Test;

/// <summary>
/// Integration tests for queries with World operations.
/// </summary>
public sealed class WorldQueryTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly ChunkManager _chunkManager = ChunkManager.Create(s_config);
    private readonly SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig> _sharedMetadata = new(ComponentRegistry.Shared, s_config);
    private readonly World<SmallBitSet<ulong>, DefaultConfig> _world;

    public WorldQueryTests()
    {
        _world = new World<SmallBitSet<ulong>, DefaultConfig>(
            s_config,
            _sharedMetadata,
            _chunkManager);
    }

    public void Dispose()
    {
        _world.Dispose();
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
    }

    #region Basic Query Integration Tests

    [Test]
    public async Task Query_EmptyWorld_ReturnsEmptyQuery()
    {
        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.IsEmpty).IsTrue();
        await Assert.That(query.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task Query_WithMatchingEntities_ReturnsCorrectCount()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        var e3 = _world.Spawn();

        _world.AddComponent<TestPosition>(e1);
        _world.AddComponent<TestPosition>(e2);
        _world.AddComponent<TestVelocity>(e3); // Should not match

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(2);
    }

    #endregion

    #region Query After Entity Operations Tests

    [Test]
    public async Task Query_AfterSpawn_UpdatesCount()
    {
        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var countBefore = query.EntityCount;

        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var countAfter = query.EntityCount;

        await Assert.That(countBefore).IsEqualTo(0);
        await Assert.That(countAfter).IsEqualTo(1);
    }

    [Test]
    public async Task Query_AfterDespawn_UpdatesCount()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var countBefore = query.EntityCount;
        _world.Despawn(entity);
        var countAfter = query.EntityCount;

        await Assert.That(countBefore).IsEqualTo(1);
        await Assert.That(countAfter).IsEqualTo(0);
    }

    [Test]
    public async Task Query_AfterAddComponent_UpdatesIfNowMatches()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestVelocity>(entity);

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var countBefore = query.EntityCount;
        _world.AddComponent<TestPosition>(entity);
        var countAfter = query.EntityCount;

        await Assert.That(countBefore).IsEqualTo(0);
        await Assert.That(countAfter).IsEqualTo(1);
    }

    [Test]
    public async Task Query_AfterRemoveComponent_UpdatesIfNoLongerMatches()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);
        _world.AddComponent<TestVelocity>(entity);

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .With<TestVelocity>()
            .Build(_world.ArchetypeRegistry);

        var countBefore = query.EntityCount;
        _world.RemoveComponent<TestVelocity>(entity);
        var countAfter = query.EntityCount;

        await Assert.That(countBefore).IsEqualTo(1);
        await Assert.That(countAfter).IsEqualTo(0);
    }

    #endregion

    #region Query with Exclusion Tests

    [Test]
    public async Task Query_WithExclusion_ExcludesCorrectEntities()
    {
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        var e2 = _world.Spawn();
        _world.AddComponent<TestPosition>(e2);
        _world.AddComponent<TestVelocity>(e2);

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Without<TestVelocity>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task Query_WithExclusion_AfterAddExcludedComponent_UpdatesCount()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Without<TestVelocity>()
            .Build(_world.ArchetypeRegistry);

        var countBefore = query.EntityCount;
        _world.AddComponent<TestVelocity>(entity);
        var countAfter = query.EntityCount;

        await Assert.That(countBefore).IsEqualTo(1);
        await Assert.That(countAfter).IsEqualTo(0);
    }

    #endregion

    #region Query with Any Tests

    [Test]
    public async Task Query_WithAny_MatchesAnyComponent()
    {
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        var e2 = _world.Spawn();
        _world.AddComponent<TestVelocity>(e2);

        var e3 = _world.Spawn();
        _world.AddComponent<TestHealth>(e3); // Should not match

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .WithAny<TestPosition>()
            .WithAny<TestVelocity>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(2);
    }

    [Test]
    public async Task Query_WithAllAndAny_CombinesCorrectly()
    {
        // Entity with Position only - should not match (missing any of Velocity/Health)
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        // Entity with Position + Velocity - should match
        var e2 = _world.Spawn();
        _world.AddComponent<TestPosition>(e2);
        _world.AddComponent<TestVelocity>(e2);

        // Entity with Position + Health - should match
        var e3 = _world.Spawn();
        _world.AddComponent<TestPosition>(e3);
        _world.AddComponent<TestHealth>(e3);

        // Entity with Velocity only - should not match (missing Position)
        var e4 = _world.Spawn();
        _world.AddComponent<TestVelocity>(e4);

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .WithAny<TestVelocity>()
            .WithAny<TestHealth>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(2);
    }

    #endregion

    #region Complex Query Scenarios

    [Test]
    public async Task Query_AllThreeConstraintTypes_WorksCorrectly()
    {
        // Matches: Position + Velocity, no Health
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);
        _world.AddComponent<TestVelocity>(e1);

        // Does not match: has Health (excluded)
        var e2 = _world.Spawn();
        _world.AddComponent<TestPosition>(e2);
        _world.AddComponent<TestVelocity>(e2);
        _world.AddComponent<TestHealth>(e2);

        // Does not match: missing Position (required)
        var e3 = _world.Spawn();
        _world.AddComponent<TestVelocity>(e3);

        // Does not match: missing any of Velocity/TestTag
        var e4 = _world.Spawn();
        _world.AddComponent<TestPosition>(e4);

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()           // Required
            .Without<TestHealth>()          // Excluded
            .WithAny<TestVelocity>()        // Any of these
            .WithAny<TestTag>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task Query_EmptyDescription_MatchesAllArchetypes()
    {
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        var e2 = _world.Spawn();
        _world.AddComponent<TestVelocity>(e2);

        var e3 = _world.Spawn();
        _world.AddComponent<TestHealth>(e3);

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .Build(_world.ArchetypeRegistry);

        // Empty query should match all archetypes
        await Assert.That(query.EntityCount).IsEqualTo(3);
    }

    #endregion

    #region Query Iteration Tests

    [Test]
    public async Task Query_Iteration_VisitsAllMatchingArchetypes()
    {
        // Create entities in different archetypes
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        var e2 = _world.Spawn();
        _world.AddComponent<TestPosition>(e2);
        _world.AddComponent<TestVelocity>(e2);

        var e3 = _world.Spawn();
        _world.AddComponent<TestPosition>(e3);
        _world.AddComponent<TestHealth>(e3);

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var visitedArchetypes = new List<Archetype<SmallBitSet<ulong>, DefaultConfig>>();
        foreach (var archetype in query.Archetypes)
        {
            visitedArchetypes.Add(archetype);
        }

        await Assert.That(visitedArchetypes.Count).IsEqualTo(3);

        int totalEntities = 0;
        foreach (var archetype in visitedArchetypes)
        {
            totalEntities += archetype.EntityCount;
        }
        await Assert.That(totalEntities).IsEqualTo(3);
    }

    [Test]
    public async Task Query_Iteration_CanAccessComponentData()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10, Y = 20, Z = 30 });

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        float sumX = 0;
        foreach (var archetype in query.Archetypes)
        {
            var layout = archetype.Layout;
            foreach (var chunkHandle in archetype.GetChunks())
            {
                var bytes = _chunkManager.GetBytes(chunkHandle);
                int entitiesInChunk = Math.Min(
                    archetype.EntityCount - archetype.GetGlobalIndex(0, 0),
                    archetype.Layout.EntitiesPerChunk);

                for (int i = 0; i < entitiesInChunk; i++)
                {
                    int offset = layout.GetEntityComponentOffset<TestPosition>(i);
                    var position = System.Runtime.InteropServices.MemoryMarshal.Read<TestPosition>(bytes.Slice(offset));
                    sumX += position.X;
                }
            }
        }

        await Assert.That(sumX).IsEqualTo(10f);
    }

    #endregion

    #region Query Caching Tests

    [Test]
    public async Task Query_SameDescription_ReturnsSameResults()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var query1 = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var query2 = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query1.EntityCount).IsEqualTo(query2.EntityCount);
        await Assert.That(query1.ArchetypeCount).IsEqualTo(query2.ArchetypeCount);
    }

    [Test]
    public async Task Query_DifferentDescriptions_ReturnsDifferentResults()
    {
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        var e2 = _world.Spawn();
        _world.AddComponent<TestVelocity>(e2);

        var queryPosition = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var queryVelocity = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestVelocity>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(queryPosition.EntityCount).IsEqualTo(1);
        await Assert.That(queryVelocity.EntityCount).IsEqualTo(1);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task Query_ForZeroSizeComponent_Works()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestTag>(entity);

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestTag>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task Query_ManyArchetypes_PerformsCorrectly()
    {
        // Create many different archetypes
        for (int i = 0; i < 10; i++)
        {
            var entity = _world.Spawn();
            _world.AddComponent<TestPosition>(entity);

            if (i % 2 == 0) _world.AddComponent<TestVelocity>(entity);
            if (i % 3 == 0) _world.AddComponent<TestHealth>(entity);
        }

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(10);
    }

    #endregion
}
