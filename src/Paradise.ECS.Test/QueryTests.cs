namespace Paradise.ECS.Test;

/// <summary>
/// Tests for Query and QueryBuilder.
/// </summary>
public sealed class QueryTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly ChunkManager<DefaultConfig> _chunkManager = new(s_config);
    private readonly SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig> _sharedMetadata = new(s_config);
    private readonly ArchetypeRegistry<Bit64, ComponentRegistry, DefaultConfig> _registry;

    public QueryTests()
    {
        _registry = new ArchetypeRegistry<Bit64, ComponentRegistry, DefaultConfig>(_sharedMetadata, _chunkManager);
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
        var builder = World<Bit64, ComponentRegistry, DefaultConfig>.Query();
        var description = builder.Description;

        await Assert.That(description.All.IsEmpty).IsTrue();
        await Assert.That(description.None.IsEmpty).IsTrue();
        await Assert.That(description.Any.IsEmpty).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_With_SetsAllMask()
    {
        var builder = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>();

        var description = builder.Description;

        await Assert.That(description.All.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_WithMultiple_SetsAllMask()
    {
        var builder = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .With<TestVelocity>();

        var description = builder.Description;

        await Assert.That(description.All.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(description.All.Get(TestVelocity.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_Without_SetsNoneMask()
    {
        var builder = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .Without<TestPosition>();

        var description = builder.Description;

        await Assert.That(description.None.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_WithAny_SetsAnyMask()
    {
        var builder = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .WithAny<TestPosition>();

        var description = builder.Description;

        await Assert.That(description.Any.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_Combined_SetsAllMasks()
    {
        var builder = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
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
        var builder = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With(TestPosition.TypeId.Value);

        var description = builder.Description;

        await Assert.That(description.All.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_WithoutComponentId_SetsNoneMask()
    {
        var builder = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .Without(TestPosition.TypeId.Value);

        var description = builder.Description;

        await Assert.That(description.None.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryBuilder_WithAnyComponentId_SetsAnyMask()
    {
        var builder = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
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
        var positionMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));

        // Create archetype with Velocity
        var velocityMask = ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);
        var velocityArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)velocityMask);
        velocityArchetype.AllocateEntity(new Entity(2, 1));

        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Build(_registry);

        int count = 0;
        foreach (var archetype in query)
        {
            count += archetype.EntityCount;
        }

        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Query_ExcludesArchetypeWithoutRequiredComponents()
    {
        // Create archetype with Velocity only
        var velocityMask = ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);
        var velocityArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)velocityMask);
        velocityArchetype.AllocateEntity(new Entity(1, 1));

        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Build(_registry);

        int count = 0;
        foreach (var archetype in query)
        {
            count += archetype.EntityCount;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Query_Without_ExcludesArchetypeWithComponent()
    {
        // Create archetype with Position only
        var positionMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));

        // Create archetype with Position + Velocity
        var bothMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var bothArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)bothMask);
        bothArchetype.AllocateEntity(new Entity(2, 1));

        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Without<TestVelocity>()
            .Build(_registry);

        int count = 0;
        foreach (var archetype in query)
        {
            count += archetype.EntityCount;
        }

        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Query_WithMultipleRequired_OnlyMatchesComplete()
    {
        // Create archetype with Position only
        var positionMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));

        // Create archetype with Position + Velocity
        var bothMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var bothArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)bothMask);
        bothArchetype.AllocateEntity(new Entity(2, 1));

        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .With<TestVelocity>()
            .Build(_registry);

        int count = 0;
        foreach (var archetype in query)
        {
            count += archetype.EntityCount;
        }

        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Query_EmptyRegistry_ReturnsZero()
    {
        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Build(_registry);

        int count = 0;
        foreach (var archetype in query)
        {
            count += archetype.EntityCount;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task Query_ArchetypeCount_ReturnsCorrectCount()
    {
        // Create archetype with Position only
        var positionMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));

        // Create archetype with Position + Velocity
        var bothMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var bothArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)bothMask);
        bothArchetype.AllocateEntity(new Entity(2, 1));

        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Build(_registry);

        await Assert.That(query.ArchetypeCount).IsEqualTo(2);
    }

    [Test]
    public async Task Query_EntityCount_ReturnsCorrectCount()
    {
        // Create archetype with Position only and add 2 entities
        var positionMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));
        positionArchetype.AllocateEntity(new Entity(2, 1));

        // Create archetype with Velocity only and add 1 entity
        var velocityMask = ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);
        var velocityArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)velocityMask);
        velocityArchetype.AllocateEntity(new Entity(3, 1));

        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Build(_registry);

        await Assert.That(query.EntityCount).IsEqualTo(2);
    }

    [Test]
    public async Task Query_IsEmpty_ReturnsCorrectValue()
    {
        var emptyQuery = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Build(_registry);

        await Assert.That(emptyQuery.IsEmpty).IsTrue();

        // Create archetype with Position and add entity
        var positionMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));

        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
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
        var positionMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var positionArchetype = _registry.GetOrCreateArchetype((HashedKey<ImmutableBitSet<Bit64>>)positionMask);
        positionArchetype.AllocateEntity(new Entity(1, 1));

        var query1 = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Build(_registry);

        var query2 = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
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
        var desc1 = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Description;

        var desc2 = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Description;

        await Assert.That(desc1).IsEqualTo(desc2);
        await Assert.That(desc1.GetHashCode()).IsEqualTo(desc2.GetHashCode());
    }

    [Test]
    public async Task ImmutableQueryDescription_Inequality_DifferentDescriptions()
    {
        var desc1 = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Description;

        var desc2 = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestVelocity>()
            .Description;

        await Assert.That(desc1).IsNotEqualTo(desc2);
    }

    [Test]
    public async Task ImmutableQueryDescription_Matches_CorrectMasks()
    {
        var desc = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Without<TestVelocity>()
            .Description;

        // Should match mask with Position but not Velocity
        var matchingMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var nonMatchingMask1 = ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);
        var nonMatchingMask2 = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);

        await Assert.That(desc.Matches(matchingMask)).IsTrue();
        await Assert.That(desc.Matches(nonMatchingMask1)).IsFalse();
        await Assert.That(desc.Matches(nonMatchingMask2)).IsFalse();
    }

    #endregion
}
