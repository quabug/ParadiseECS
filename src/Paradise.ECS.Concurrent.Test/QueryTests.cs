namespace Paradise.ECS.Concurrent.Test;

/// <summary>
/// Tests for Query struct - a lightweight view over matching archetypes.
/// </summary>
public sealed class QueryTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly ChunkManager _chunkManager = ChunkManager.Create(s_config);
    private readonly SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig> _sharedMetadata = new(s_config);
    private readonly World<Bit64, ComponentRegistry, DefaultConfig> _world;

    public QueryTests()
    {
        _world = new World<Bit64, ComponentRegistry, DefaultConfig>(
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

    #region EntityCount Tests

    [Test]
    public async Task EntityCount_EmptyWorld_ReturnsZero()
    {
        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task EntityCount_SingleArchetype_ReturnsCorrectCount()
    {
        // Create 5 entities with Position
        for (int i = 0; i < 5; i++)
        {
            var entity = _world.Spawn();
            _world.AddComponent<TestPosition>(entity);
        }

        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(5);
    }

    [Test]
    public async Task EntityCount_MultipleArchetypes_SumsCorrectly()
    {
        // Create 3 entities with Position only
        for (int i = 0; i < 3; i++)
        {
            var entity = _world.Spawn();
            _world.AddComponent<TestPosition>(entity);
        }

        // Create 2 entities with Position + Velocity
        for (int i = 0; i < 2; i++)
        {
            var entity = _world.Spawn();
            _world.AddComponent<TestPosition>(entity);
            _world.AddComponent<TestVelocity>(entity);
        }

        // Query for Position should match both archetypes
        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(5);
    }

    [Test]
    public async Task EntityCount_AfterDespawn_UpdatesCorrectly()
    {
        var entity1 = _world.Spawn();
        var entity2 = _world.Spawn();
        _world.AddComponent<TestPosition>(entity1);
        _world.AddComponent<TestPosition>(entity2);

        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var countBefore = query.EntityCount;
        _world.Despawn(entity1);
        var countAfter = query.EntityCount;

        await Assert.That(countBefore).IsEqualTo(2);
        await Assert.That(countAfter).IsEqualTo(1);
    }

    #endregion

    #region IsEmpty Tests

    [Test]
    public async Task IsEmpty_EmptyWorld_ReturnsTrue()
    {
        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.IsEmpty).IsTrue();
    }

    [Test]
    public async Task IsEmpty_WithMatchingEntities_ReturnsFalse()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.IsEmpty).IsFalse();
    }

    [Test]
    public async Task IsEmpty_NoMatchingEntities_ReturnsTrue()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestVelocity>(entity);

        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.IsEmpty).IsTrue();
    }

    [Test]
    public async Task IsEmpty_AllMatchingEntitiesDespawned_ReturnsTrue()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var emptyBefore = query.IsEmpty;
        _world.Despawn(entity);
        var emptyAfter = query.IsEmpty;

        await Assert.That(emptyBefore).IsFalse();
        await Assert.That(emptyAfter).IsTrue();
    }

    #endregion

    #region ArchetypeCount Tests

    [Test]
    public async Task ArchetypeCount_NoMatches_ReturnsZero()
    {
        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.ArchetypeCount).IsEqualTo(0);
    }

    [Test]
    public async Task ArchetypeCount_SingleArchetype_ReturnsOne()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.ArchetypeCount).IsEqualTo(1);
    }

    [Test]
    public async Task ArchetypeCount_MultipleArchetypes_ReturnsCorrectCount()
    {
        // Create archetype {Position}
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        // Create archetype {Position, Velocity}
        var e2 = _world.Spawn();
        _world.AddComponent<TestPosition>(e2);
        _world.AddComponent<TestVelocity>(e2);

        // Create archetype {Position, Health}
        var e3 = _world.Spawn();
        _world.AddComponent<TestPosition>(e3);
        _world.AddComponent<TestHealth>(e3);

        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.ArchetypeCount).IsEqualTo(3);
    }

    #endregion

    #region Enumeration Tests

    [Test]
    public async Task GetEnumerator_EmptyQuery_NoIterations()
    {
        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        int count = 0;
        foreach (var archetype in query)
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task GetEnumerator_WithEntities_IteratesAllArchetypes()
    {
        // Create entities in different archetypes
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        var e2 = _world.Spawn();
        _world.AddComponent<TestPosition>(e2);
        _world.AddComponent<TestVelocity>(e2);

        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var archetypes = new List<Archetype<Bit64, ComponentRegistry, DefaultConfig>>();
        foreach (var archetype in query)
        {
            archetypes.Add(archetype);
        }

        await Assert.That(archetypes.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetEnumerator_CanIterateMultipleTimes()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        int count1 = 0;
        foreach (var _ in query) count1++;

        int count2 = 0;
        foreach (var _ in query) count2++;

        await Assert.That(count1).IsEqualTo(1);
        await Assert.That(count2).IsEqualTo(1);
    }

    #endregion

    #region Query Caching Tests

    [Test]
    public async Task Build_SameDescription_ReturnsCachedQuery()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var query1 = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var query2 = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        // Both queries should reflect the same entity count
        await Assert.That(query1.EntityCount).IsEqualTo(1);
        await Assert.That(query2.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task Build_NewArchetypeCreatedAfterQuery_IncludesIt()
    {
        // Create first entity before query
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        // Build query
        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var countBefore = query.ArchetypeCount;

        // Create entity in new archetype after query was built
        var e2 = _world.Spawn();
        _world.AddComponent<TestPosition>(e2);
        _world.AddComponent<TestVelocity>(e2);

        var countAfter = query.ArchetypeCount;

        await Assert.That(countBefore).IsEqualTo(1);
        await Assert.That(countAfter).IsEqualTo(2);
    }

    #endregion

    #region Complex Query Tests

    [Test]
    public async Task Query_WithMultipleComponents_MatchesCorrectly()
    {
        // Create {Position} archetype
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        // Create {Position, Velocity} archetype
        var e2 = _world.Spawn();
        _world.AddComponent<TestPosition>(e2);
        _world.AddComponent<TestVelocity>(e2);

        // Create {Velocity} archetype
        var e3 = _world.Spawn();
        _world.AddComponent<TestVelocity>(e3);

        // Query for Position + Velocity
        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .With<TestVelocity>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(1);
        await Assert.That(query.ArchetypeCount).IsEqualTo(1);
    }

    [Test]
    public async Task Query_WithExclusion_ExcludesCorrectly()
    {
        // Create {Position} archetype
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        // Create {Position, Velocity} archetype
        var e2 = _world.Spawn();
        _world.AddComponent<TestPosition>(e2);
        _world.AddComponent<TestVelocity>(e2);

        // Query for Position without Velocity
        var query = new QueryBuilder<Bit64>()
            .With<TestPosition>()
            .Without<TestVelocity>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(1);
        await Assert.That(query.ArchetypeCount).IsEqualTo(1);
    }

    [Test]
    public async Task Query_WithAny_MatchesCorrectly()
    {
        // Create {Position} archetype
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        // Create {Velocity} archetype
        var e2 = _world.Spawn();
        _world.AddComponent<TestVelocity>(e2);

        // Create {Health} archetype - should not match
        var e3 = _world.Spawn();
        _world.AddComponent<TestHealth>(e3);

        // Query for any of Position or Velocity
        var query = new QueryBuilder<Bit64>()
            .WithAny<TestPosition>()
            .WithAny<TestVelocity>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(2);
        await Assert.That(query.ArchetypeCount).IsEqualTo(2);
    }

    #endregion
}
