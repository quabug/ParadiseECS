namespace Paradise.ECS.Test;

/// <summary>
/// Tests for Query and QueryBuilder.
/// </summary>
public sealed class QueryTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly ChunkManager _chunkManager = ChunkManager.Create(s_config);
    private readonly SharedArchetypeMetadata<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig> _sharedMetadata = new(s_config);
    private readonly ArchetypeRegistry<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig> _registry;

    public QueryTests()
    {
        _registry = new ArchetypeRegistry<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>(_sharedMetadata, _chunkManager);
    }

    public void Dispose()
    {
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
    }

    #region QueryBuilder Tests

    [Test]
    public async Task QueryBuilder_Default_IsEmpty()
    {
        var builder = new QueryBuilder<SmallBitSet<ulong>>();
        var description = builder.Description;

        await Assert.That(description.All.IsEmpty).IsTrue();
        await Assert.That(description.None.IsEmpty).IsTrue();
        await Assert.That(description.Any.IsEmpty).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_With_SetsAllMask()
    {
        var builder = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>();

        var description = builder.Description;

        await Assert.That(description.All.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_WithMultiple_SetsAllMask()
    {
        var builder = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .With<TestVelocity>();

        var description = builder.Description;

        await Assert.That(description.All.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(description.All.Get(TestVelocity.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_Without_SetsNoneMask()
    {
        var builder = new QueryBuilder<SmallBitSet<ulong>>()
            .Without<TestPosition>();

        var description = builder.Description;

        await Assert.That(description.None.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_WithAny_SetsAnyMask()
    {
        var builder = new QueryBuilder<SmallBitSet<ulong>>()
            .WithAny<TestPosition>();

        var description = builder.Description;

        await Assert.That(description.Any.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_Combined_SetsAllMasks()
    {
        var builder = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Without<TestVelocity>()
            .WithAny<TestHealth>();

        var description = builder.Description;

        await Assert.That(description.All.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(description.None.Get(TestVelocity.TypeId.Value)).IsTrue();
        await Assert.That(description.Any.Get(TestHealth.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_WithComponentId_SetsAllMask()
    {
        var builder = new QueryBuilder<SmallBitSet<ulong>>()
            .With(TestPosition.TypeId.Value);

        var description = builder.Description;

        await Assert.That(description.All.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_WithoutComponentId_SetsNoneMask()
    {
        var builder = new QueryBuilder<SmallBitSet<ulong>>()
            .Without(TestPosition.TypeId.Value);

        var description = builder.Description;

        await Assert.That(description.None.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_WithAnyComponentId_SetsAnyMask()
    {
        var builder = new QueryBuilder<SmallBitSet<ulong>>()
            .WithAny(TestPosition.TypeId.Value);

        var description = builder.Description;

        await Assert.That(description.Any.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    #endregion

    #region Query Matching Tests

    [Test]
    public async Task Query_MatchesArchetypeWithRequiredComponents()
    {
        // Create archetype with Position
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));

        // Create archetype with Velocity
        var velocityMask = SmallBitSet<ulong>.Empty.Set(TestVelocity.TypeId);
        var velocityArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)velocityMask);
        velocityArchetype.AllocateEntity(new Entity(2, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        int count = 0;
        foreach (var archetype in query.Archetypes)
        {
            count += archetype.EntityCount;
        }

        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Query_ExcludesArchetypeWithoutRequiredComponents()
    {
        // Create archetype with Velocity only
        var velocityMask = SmallBitSet<ulong>.Empty.Set(TestVelocity.TypeId);
        var velocityArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)velocityMask);
        velocityArchetype.AllocateEntity(new Entity(1, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        int count = 0;
        foreach (var archetype in query.Archetypes)
        {
            count += archetype.EntityCount;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Query_Without_ExcludesArchetypeWithComponent()
    {
        // Create archetype with Position only
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));

        // Create archetype with Position + Velocity
        var bothMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var bothArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)bothMask);
        bothArchetype.AllocateEntity(new Entity(2, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Without<TestVelocity>()
            .Build(_registry);

        int count = 0;
        foreach (var archetype in query.Archetypes)
        {
            count += archetype.EntityCount;
        }

        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Query_WithMultipleRequired_OnlyMatchesComplete()
    {
        // Create archetype with Position only
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));

        // Create archetype with Position + Velocity
        var bothMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var bothArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)bothMask);
        bothArchetype.AllocateEntity(new Entity(2, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .With<TestVelocity>()
            .Build(_registry);

        int count = 0;
        foreach (var archetype in query.Archetypes)
        {
            count += archetype.EntityCount;
        }

        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Query_EmptyRegistry_ReturnsZero()
    {
        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        int count = 0;
        foreach (var archetype in query.Archetypes)
        {
            count += archetype.EntityCount;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Query_ArchetypeCount_ReturnsCorrectCount()
    {
        // Create archetype with Position only
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));

        // Create archetype with Position + Velocity
        var bothMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var bothArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)bothMask);
        bothArchetype.AllocateEntity(new Entity(2, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        await Assert.That(query.ArchetypeCount).IsEqualTo(2);
    }

    [Test]
    public async Task Query_EntityCount_ReturnsCorrectCount()
    {
        // Create archetype with Position only and add 2 entities
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));
        positionArchetype.AllocateEntity(new Entity(2, 1));

        // Create archetype with Velocity only and add 1 entity
        var velocityMask = SmallBitSet<ulong>.Empty.Set(TestVelocity.TypeId);
        var velocityArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)velocityMask);
        velocityArchetype.AllocateEntity(new Entity(3, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        await Assert.That(query.EntityCount).IsEqualTo(2);
    }

    [Test]
    public async Task Query_IsEmpty_ReturnsCorrectValue()
    {
        var emptyQuery = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        await Assert.That(emptyQuery.IsEmpty).IsTrue();

        // Create archetype with Position and add entity
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        await Assert.That(query.IsEmpty).IsFalse();
    }

    #endregion

    #region Query Caching Tests

    [Test]
    public async Task Query_SameDescription_ReturnsSameQuery()
    {
        // Create archetype with Position
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));

        var query1 = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        var query2 = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        // Both queries should reference the same archetype count
        await Assert.That(query1.ArchetypeCount).IsEqualTo(query2.ArchetypeCount);
    }

    #endregion

    #region ImmutableQueryDescription Tests

    [Test]
    public async Task ImmutableQueryDescription_Equality_SameDescriptions()
    {
        var desc1 = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Description;

        var desc2 = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Description;

        await Assert.That(desc1).IsEqualTo(desc2);
        await Assert.That(desc1.GetHashCode()).IsEqualTo(desc2.GetHashCode());
    }

    [Test]
    public async Task ImmutableQueryDescription_Inequality_DifferentDescriptions()
    {
        var desc1 = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Description;

        var desc2 = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestVelocity>()
            .Description;

        await Assert.That(desc1).IsNotEqualTo(desc2);
    }

    [Test]
    public async Task ImmutableQueryDescription_Matches_CorrectMasks()
    {
        var desc = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Without<TestVelocity>()
            .Description;

        // Should match mask with Position but not Velocity
        var matchingMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var nonMatchingMask1 = SmallBitSet<ulong>.Empty.Set(TestVelocity.TypeId);
        var nonMatchingMask2 = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);

        await Assert.That(desc.Matches(matchingMask)).IsTrue();
        await Assert.That(desc.Matches(nonMatchingMask1)).IsFalse();
        await Assert.That(desc.Matches(nonMatchingMask2)).IsFalse();
    }

    #endregion

    #region ChunkEnumerator Tests

    [Test]
    public async Task Chunks_EmptyQuery_ReturnsNoChunks()
    {
        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        int chunkCount = 0;
        foreach (var _ in query.Chunks)
        {
            chunkCount++;
        }

        await Assert.That(chunkCount).IsEqualTo(0);
    }

    [Test]
    public async Task Chunks_SingleArchetypeSingleChunk_ReturnsOneChunk()
    {
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var archetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);
        archetype.AllocateEntity(new Entity(1, 1));
        archetype.AllocateEntity(new Entity(2, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        int chunkCount = 0;
        int totalEntities = 0;
        foreach (var chunkInfo in query.Chunks)
        {
            chunkCount++;
            totalEntities += chunkInfo.EntityCount;
        }

        await Assert.That(chunkCount).IsEqualTo(1);
        await Assert.That(totalEntities).IsEqualTo(2);
    }

    [Test]
    public async Task Chunks_MultipleArchetypes_ReturnsAllChunks()
    {
        // Create two archetypes that match the query
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var archetype1 = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);
        archetype1.AllocateEntity(new Entity(1, 1));

        var bothMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var archetype2 = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)bothMask);
        archetype2.AllocateEntity(new Entity(2, 1));
        archetype2.AllocateEntity(new Entity(3, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        int chunkCount = 0;
        int totalEntities = 0;
        foreach (var chunkInfo in query.Chunks)
        {
            chunkCount++;
            totalEntities += chunkInfo.EntityCount;
        }

        await Assert.That(chunkCount).IsEqualTo(2);
        await Assert.That(totalEntities).IsEqualTo(3);
    }

    [Test]
    public async Task Chunks_ChunkInfo_ContainsCorrectArchetype()
    {
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var archetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);
        archetype.AllocateEntity(new Entity(1, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        int expectedId = archetype.Id;
        int foundArchetypeId = -1;
        int foundEntityCount = 0;
        foreach (var chunkInfo in query.Chunks)
        {
            foundArchetypeId = chunkInfo.Archetype.Id;
            foundEntityCount = chunkInfo.EntityCount;
        }

        await Assert.That(foundArchetypeId).IsEqualTo(expectedId);
        await Assert.That(foundEntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task Chunks_EmptyArchetype_SkipsArchetype()
    {
        // Create archetype with Position but no entities
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        _ = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);

        // Create archetype with Position + Velocity with entities
        var bothMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var archetype2 = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)bothMask);
        archetype2.AllocateEntity(new Entity(1, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        int expectedId = archetype2.Id;
        int chunkCount = 0;
        int foundArchetypeId = -1;
        foreach (var chunkInfo in query.Chunks)
        {
            chunkCount++;
            foundArchetypeId = chunkInfo.Archetype.Id;
        }

        await Assert.That(chunkCount).IsEqualTo(1);
        await Assert.That(foundArchetypeId).IsEqualTo(expectedId);
    }

    #endregion

    #region EntityIdEnumerator Tests

    [Test]
    public async Task EntityIds_EmptyQuery_ReturnsNoEntities()
    {
        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        int entityCount = 0;
        foreach (var _ in query)
        {
            entityCount++;
        }

        await Assert.That(entityCount).IsEqualTo(0);
    }

    [Test]
    public async Task EntityIds_SingleArchetype_ReturnsAllEntityIds()
    {
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var archetype = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);
        archetype.AllocateEntity(new Entity(10, 1));
        archetype.AllocateEntity(new Entity(20, 1));
        archetype.AllocateEntity(new Entity(30, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        var entityIds = new List<int>();
        foreach (var entityId in query)
        {
            entityIds.Add(entityId);
        }

        await Assert.That(entityIds.Count).IsEqualTo(3);
        await Assert.That(entityIds).Contains(10);
        await Assert.That(entityIds).Contains(20);
        await Assert.That(entityIds).Contains(30);
    }

    [Test]
    public async Task EntityIds_MultipleArchetypes_ReturnsAllEntityIds()
    {
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var archetype1 = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);
        archetype1.AllocateEntity(new Entity(1, 1));
        archetype1.AllocateEntity(new Entity(2, 1));

        var bothMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var archetype2 = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)bothMask);
        archetype2.AllocateEntity(new Entity(3, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        var entityIds = new List<int>();
        foreach (var entityId in query)
        {
            entityIds.Add(entityId);
        }

        await Assert.That(entityIds.Count).IsEqualTo(3);
        await Assert.That(entityIds).Contains(1);
        await Assert.That(entityIds).Contains(2);
        await Assert.That(entityIds).Contains(3);
    }

    [Test]
    public async Task EntityIds_EmptyArchetype_SkipsToNextArchetype()
    {
        // Create empty archetype
        var positionMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        _ = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)positionMask);

        // Create archetype with entities
        var bothMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var archetype2 = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)bothMask);
        archetype2.AllocateEntity(new Entity(100, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        var entityIds = new List<int>();
        foreach (var entityId in query)
        {
            entityIds.Add(entityId);
        }

        await Assert.That(entityIds.Count).IsEqualTo(1);
        await Assert.That(entityIds[0]).IsEqualTo(100);
    }

    [Test]
    public async Task EntityIds_ConsecutiveEmptyArchetypes_SkipsAll()
    {
        // Create multiple empty archetypes that match
        var mask1 = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        _ = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)mask1);

        var mask2 = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        _ = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)mask2);

        // Create archetype with entity at the end
        var mask3 = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestHealth.TypeId);
        var archetype3 = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)mask3);
        archetype3.AllocateEntity(new Entity(42, 1));

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        var entityIds = new List<int>();
        foreach (var entityId in query)
        {
            entityIds.Add(entityId);
        }

        await Assert.That(entityIds.Count).IsEqualTo(1);
        await Assert.That(entityIds[0]).IsEqualTo(42);
    }

    [Test]
    public async Task EntityIds_AllEmptyArchetypes_ReturnsNoEntities()
    {
        // Create multiple empty archetypes that match
        var mask1 = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        _ = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)mask1);

        var mask2 = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        _ = _registry.GetOrCreate((HashedKey<SmallBitSet<ulong>>)mask2);

        var query = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Build(_registry);

        var entityIds = new List<int>();
        foreach (var entityId in query)
        {
            entityIds.Add(entityId);
        }

        await Assert.That(entityIds.Count).IsEqualTo(0);
    }

    #endregion
}
