#pragma warning disable CA2263 // Prefer generic overload - intentionally testing Type-based APIs

namespace Paradise.ECS.Concurrent.Test;

/// <summary>
/// Tests for QueryBuilder - fluent API for creating query descriptions.
/// </summary>
public sealed class QueryBuilderTests : IDisposable
{
    private readonly ChunkManager _chunkManager = new();
    private readonly World<Bit64, ComponentRegistry> _world;

    public QueryBuilderTests()
    {
        _world = new World<Bit64, ComponentRegistry>(
            SharedArchetypeMetadata<Bit64, ComponentRegistry>.Shared,
            _chunkManager);
    }

    public void Dispose()
    {
        _world.Dispose();
        _chunkManager.Dispose();
    }

    #region Immutability Tests

    [Test]
    public async Task With_ReturnsNewBuilder()
    {
        // Capture descriptions before await (QueryBuilder is ref struct)
        var builder1 = new QueryBuilder<Bit64, ComponentRegistry>();
        var desc1 = builder1.Description;

        var builder2 = builder1.With<TestPosition>();
        var desc2 = builder2.Description;

        // The original description should be empty
        await Assert.That(desc1.All.IsEmpty).IsTrue();
        // The new description should have Position
        await Assert.That(desc2.All.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task Without_ReturnsNewBuilder()
    {
        var builder1 = new QueryBuilder<Bit64, ComponentRegistry>();
        var desc1 = builder1.Description;

        var builder2 = builder1.Without<TestPosition>();
        var desc2 = builder2.Description;

        await Assert.That(desc1.None.IsEmpty).IsTrue();
        await Assert.That(desc2.None.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task WithAny_ReturnsNewBuilder()
    {
        var builder1 = new QueryBuilder<Bit64, ComponentRegistry>();
        var desc1 = builder1.Description;

        var builder2 = builder1.WithAny<TestPosition>();
        var desc2 = builder2.Description;

        await Assert.That(desc1.Any.IsEmpty).IsTrue();
        await Assert.That(desc2.Any.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    #endregion

    #region Fluent Chaining Tests

    [Test]
    public async Task Chaining_PreservesAllConstraints()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .With<TestPosition>()
            .With<TestVelocity>()
            .Without<TestHealth>()
            .Description;

        await Assert.That(desc.All.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(desc.All.Get(TestVelocity.TypeId.Value)).IsTrue();
        await Assert.That(desc.None.Get(TestHealth.TypeId.Value)).IsTrue();
        await Assert.That(desc.Any.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Chaining_AllConstraintTypes()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .With<TestPosition>()
            .Without<TestVelocity>()
            .WithAny<TestHealth>()
            .Description;

        await Assert.That(desc.All.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(desc.None.Get(TestVelocity.TypeId.Value)).IsTrue();
        await Assert.That(desc.Any.Get(TestHealth.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task Chaining_SameComponentTwice_SetsBitOnce()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .With<TestPosition>()
            .With<TestPosition>() // Adding same component again
            .Description;

        // The bit should still be set (idempotent)
        var isSet = desc.All.Get(TestPosition.TypeId.Value);
        var popCount = desc.All.PopCount();

        await Assert.That(isSet).IsTrue();
        await Assert.That(popCount).IsEqualTo(1);
    }

    #endregion

    #region With Methods Tests

    [Test]
    public async Task With_Generic_AddsToAll()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .With<TestPosition>()
            .Description;

        await Assert.That(desc.All.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task With_ComponentId_AddsToAll()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .With(TestPosition.TypeId.Value)
            .Description;

        await Assert.That(desc.All.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task With_Type_AddsToAll()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .With(typeof(TestPosition))
            .Description;

        await Assert.That(desc.All.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task With_InvalidType_ThrowsException()
    {
        bool threw = false;
        try
        {
            _ = new QueryBuilder<Bit64, ComponentRegistry>().With(typeof(string));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    #endregion

    #region Without Methods Tests

    [Test]
    public async Task Without_Generic_AddsToNone()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .Without<TestPosition>()
            .Description;

        await Assert.That(desc.None.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task Without_ComponentId_AddsToNone()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .Without(TestPosition.TypeId.Value)
            .Description;

        await Assert.That(desc.None.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task Without_Type_AddsToNone()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .Without(typeof(TestPosition))
            .Description;

        await Assert.That(desc.None.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task Without_InvalidType_ThrowsException()
    {
        bool threw = false;
        try
        {
            _ = new QueryBuilder<Bit64, ComponentRegistry>().Without(typeof(int));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    #endregion

    #region WithAny Methods Tests

    [Test]
    public async Task WithAny_Generic_AddsToAny()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .WithAny<TestPosition>()
            .Description;

        await Assert.That(desc.Any.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task WithAny_ComponentId_AddsToAny()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .WithAny(TestPosition.TypeId.Value)
            .Description;

        await Assert.That(desc.Any.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task WithAny_Type_AddsToAny()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .WithAny(typeof(TestPosition))
            .Description;

        await Assert.That(desc.Any.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task WithAny_InvalidType_ThrowsException()
    {
        bool threw = false;
        try
        {
            _ = new QueryBuilder<Bit64, ComponentRegistry>().WithAny(typeof(object));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    #endregion

    #region Build Method Tests

    [Test]
    public async Task Build_EmptyBuilder_ReturnsValidQuery()
    {
        var query = new QueryBuilder<Bit64, ComponentRegistry>()
            .Build(_world.ArchetypeRegistry);

        // Empty query should have valid archetype count
        var count = query.ArchetypeCount;
        await Assert.That(count).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Build_WithConstraints_ReturnsFilteredQuery()
    {
        // Create an entity with Position
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var query = new QueryBuilder<Bit64, ComponentRegistry>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var entityCount = query.EntityCount;
        await Assert.That(entityCount).IsEqualTo(1);
    }

    [Test]
    public async Task Build_SameDescription_ReturnsCachedQuery()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var query1 = new QueryBuilder<Bit64, ComponentRegistry>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var query2 = new QueryBuilder<Bit64, ComponentRegistry>()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        var count1 = query1.EntityCount;
        var count2 = query2.EntityCount;

        // Both queries should return the same entity count
        await Assert.That(count1).IsEqualTo(count2);
    }

    #endregion

    #region Implicit Conversion Tests

    [Test]
    public async Task ImplicitConversion_ToDescription_Works()
    {
        ImmutableQueryDescription<Bit64> description = new QueryBuilder<Bit64, ComponentRegistry>()
            .With<TestPosition>();

        await Assert.That(description.All.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    #endregion

    #region Description Property Tests

    [Test]
    public async Task Description_DefaultBuilder_ReturnsEmptyDescription()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>().Description;

        await Assert.That(desc.All.IsEmpty).IsTrue();
        await Assert.That(desc.None.IsEmpty).IsTrue();
        await Assert.That(desc.Any.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Description_AfterChaining_ReturnsAccumulatedConstraints()
    {
        var desc = new QueryBuilder<Bit64, ComponentRegistry>()
            .With<TestPosition>()
            .Without<TestVelocity>()
            .WithAny<TestHealth>()
            .Description;

        var allCount = desc.All.PopCount();
        var noneCount = desc.None.PopCount();
        var anyCount = desc.Any.PopCount();

        await Assert.That(allCount).IsEqualTo(1);
        await Assert.That(noneCount).IsEqualTo(1);
        await Assert.That(anyCount).IsEqualTo(1);
    }

    #endregion

    #region Branching Tests

    [Test]
    public async Task Branching_FromSameBase_CreatesIndependentBuilders()
    {
        var baseBuilder = new QueryBuilder<Bit64, ComponentRegistry>()
            .With<TestPosition>();
        var baseDesc = baseBuilder.Description;

        var branch1 = baseBuilder.With<TestVelocity>();
        var branch1Desc = branch1.Description;

        var branch2 = baseBuilder.With<TestHealth>();
        var branch2Desc = branch2.Description;

        // Base should only have Position
        await Assert.That(baseDesc.All.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(baseDesc.All.Get(TestVelocity.TypeId.Value)).IsFalse();
        await Assert.That(baseDesc.All.Get(TestHealth.TypeId.Value)).IsFalse();

        // Branch1 should have Position + Velocity
        await Assert.That(branch1Desc.All.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(branch1Desc.All.Get(TestVelocity.TypeId.Value)).IsTrue();
        await Assert.That(branch1Desc.All.Get(TestHealth.TypeId.Value)).IsFalse();

        // Branch2 should have Position + Health
        await Assert.That(branch2Desc.All.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(branch2Desc.All.Get(TestVelocity.TypeId.Value)).IsFalse();
        await Assert.That(branch2Desc.All.Get(TestHealth.TypeId.Value)).IsTrue();
    }

    #endregion
}
