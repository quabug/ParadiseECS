namespace Paradise.ECS.Test;

/// <summary>
/// Tests for generated Queryable types and QueryableRegistry.
/// </summary>
public sealed class QueryableRegistryTests
{
    [Test]
    public async Task TestMovableEntity_HasCorrectQueryableId()
    {
        // Auto-assigned ID should be 0 (first alphabetically)
        await Assert.That(TestMovableEntity.QueryableId).IsEqualTo(0);
    }

    [Test]
    public async Task TestProjectile_HasExplicitQueryableId()
    {
        // Explicit ID = 5
        await Assert.That(TestProjectile.QueryableId).IsEqualTo(5);
    }

    [Test]
    public async Task TestHealthEntity_HasExplicitQueryableId()
    {
        // Explicit ID = 10
        await Assert.That(TestHealthEntity.QueryableId).IsEqualTo(10);
    }

    [Test]
    public async Task QueryableRegistry_Count_ReturnsCorrectCount()
    {
        // We have 3 queryable types: TestMovableEntity, TestProjectile, TestHealthEntity
        await Assert.That(QueryableRegistry<ImmutableBitSet<Bit64>>.Count).IsEqualTo(3);
    }

    [Test]
    public async Task QueryableRegistry_Initialize_PopulatesDescriptions()
    {
        var descriptions = QueryableRegistry<ImmutableBitSet<Bit64>>.Descriptions;

        await Assert.That(descriptions.IsDefault).IsFalse();
        // Array size should accommodate max ID (10) + 1 = 11
        await Assert.That(descriptions.Length).IsEqualTo(11);
    }

    [Test]
    public async Task QueryableRegistry_TestMovableEntity_HasCorrectAllMask()
    {
        var allMask = QueryableRegistry<ImmutableBitSet<Bit64>>.Descriptions[TestMovableEntity.QueryableId].Value.All;

        // TestMovableEntity requires TestPosition and TestHealth
        await Assert.That(allMask.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(allMask.Get(TestHealth.TypeId.Value)).IsTrue();
        // Should not have TestVelocity in All mask
        await Assert.That(allMask.Get(TestVelocity.TypeId.Value)).IsFalse();
    }

    [Test]
    public async Task QueryableRegistry_TestMovableEntity_HasCorrectNoneMask()
    {
        var noneMask = QueryableRegistry<ImmutableBitSet<Bit64>>.Descriptions[TestMovableEntity.QueryableId].Value.None;

        // TestMovableEntity excludes TestVelocity
        await Assert.That(noneMask.Get(TestVelocity.TypeId.Value)).IsTrue();
        // Should not exclude other components
        await Assert.That(noneMask.Get(TestPosition.TypeId.Value)).IsFalse();
        await Assert.That(noneMask.Get(TestHealth.TypeId.Value)).IsFalse();
    }

    [Test]
    public async Task QueryableRegistry_TestMovableEntity_HasEmptyAnyMask()
    {
        var anyMask = QueryableRegistry<ImmutableBitSet<Bit64>>.Descriptions[TestMovableEntity.QueryableId].Value.Any;

        // TestMovableEntity has no Any constraints
        await Assert.That(anyMask.IsEmpty).IsTrue();
    }

    [Test]
    public async Task QueryableRegistry_TestProjectile_HasCorrectAllMask()
    {
        var allMask = QueryableRegistry<ImmutableBitSet<Bit64>>.Descriptions[TestProjectile.QueryableId].Value.All;

        // TestProjectile requires TestPosition and TestVelocity
        await Assert.That(allMask.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(allMask.Get(TestVelocity.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryableRegistry_TestProjectile_HasEmptyNoneMask()
    {
        var noneMask = QueryableRegistry<ImmutableBitSet<Bit64>>.Descriptions[TestProjectile.QueryableId].Value.None;

        // TestProjectile has no Without constraints
        await Assert.That(noneMask.IsEmpty).IsTrue();
    }

    [Test]
    public async Task QueryableRegistry_TestProjectile_HasCorrectAnyMask()
    {
        var anyMask = QueryableRegistry<ImmutableBitSet<Bit64>>.Descriptions[TestProjectile.QueryableId].Value.Any;

        // TestProjectile optionally has TestDamage
        await Assert.That(anyMask.Get(TestDamage.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task QueryableRegistry_TestHealthEntity_HasCorrectAllMask()
    {
        var allMask = QueryableRegistry<ImmutableBitSet<Bit64>>.Descriptions[TestHealthEntity.QueryableId].Value.All;

        // TestHealthEntity requires only TestHealth
        await Assert.That(allMask.Get(TestHealth.TypeId.Value)).IsTrue();
        // Should not have other components
        await Assert.That(allMask.Get(TestPosition.TypeId.Value)).IsFalse();
        await Assert.That(allMask.Get(TestVelocity.TypeId.Value)).IsFalse();
    }

    [Test]
    public async Task QueryableRegistry_Description_MatchesCorrectArchetypes()
    {
        var description = QueryableRegistry<ImmutableBitSet<Bit64>>.Descriptions[TestMovableEntity.QueryableId];

        // Create masks for testing
        var positionAndHealthMask = ImmutableBitSet<Bit64>.Empty
            .Set(TestPosition.TypeId)
            .Set(TestHealth.TypeId);

        var positionHealthVelocityMask = ImmutableBitSet<Bit64>.Empty
            .Set(TestPosition.TypeId)
            .Set(TestHealth.TypeId)
            .Set(TestVelocity.TypeId);

        var onlyPositionMask = ImmutableBitSet<Bit64>.Empty
            .Set(TestPosition.TypeId);

        // Should match: has Position and Health, no Velocity
        await Assert.That(description.Value.Matches(positionAndHealthMask)).IsTrue();

        // Should NOT match: has Velocity (excluded by Without<TestVelocity>)
        await Assert.That(description.Value.Matches(positionHealthVelocityMask)).IsFalse();

        // Should NOT match: missing Health
        await Assert.That(description.Value.Matches(onlyPositionMask)).IsFalse();
    }

    [Test]
    public async Task QueryableRegistry_MultipleAccesses_ReturnsSameData()
    {
        // Access registry multiple times
        var descriptions1 = QueryableRegistry<ImmutableBitSet<Bit64>>.Descriptions;
        var descriptions2 = QueryableRegistry<ImmutableBitSet<Bit64>>.Descriptions;

        // Should be the same immutable array (initialized once via static constructor)
        // ImmutableArray equality checks the underlying array reference
        await Assert.That(descriptions1 == descriptions2).IsTrue();
    }
}
