namespace Paradise.ECS.Test;

/// <summary>
/// Tests for <see cref="QueryBuilder{TBits}"/>.
/// </summary>
public sealed class QueryBuilderTests
{
    [Test]
    public async Task Create_ReturnsEmptyBuilder()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create();
        var description = builder.Description;

        await Assert.That(description.All.IsEmpty).IsTrue();
        await Assert.That(description.None.IsEmpty).IsTrue();
        await Assert.That(description.Any.IsEmpty).IsTrue();
    }

    [Test]
    public async Task With_ByTypeId_AddsToAllMask()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create().With(5);
        var description = builder.Description;

        await Assert.That(description.All.Get(5)).IsTrue();
        await Assert.That(description.All.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task With_ByComponent_AddsToAllMask()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create().With<TestHealth>();
        var description = builder.Description;

        await Assert.That(description.All.Get(TestHealth.TypeId)).IsTrue();
    }

    [Test]
    public async Task With_Multiple_AddsAllToAllMask()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestHealth>()
            .With<TestPosition>();
        var description = builder.Description;

        await Assert.That(description.All.Get(TestHealth.TypeId)).IsTrue();
        await Assert.That(description.All.Get(TestPosition.TypeId)).IsTrue();
        await Assert.That(description.All.PopCount()).IsEqualTo(2);
    }

    [Test]
    public async Task Without_ByTypeId_AddsToNoneMask()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create().Without(5);
        var description = builder.Description;

        await Assert.That(description.None.Get(5)).IsTrue();
        await Assert.That(description.None.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task Without_ByComponent_AddsToNoneMask()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create().Without<TestVelocity>();
        var description = builder.Description;

        await Assert.That(description.None.Get(TestVelocity.TypeId)).IsTrue();
    }

    [Test]
    public async Task Without_Multiple_AddsAllToNoneMask()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create()
            .Without<TestVelocity>()
            .Without<TestDamage>();
        var description = builder.Description;

        await Assert.That(description.None.Get(TestVelocity.TypeId)).IsTrue();
        await Assert.That(description.None.Get(TestDamage.TypeId)).IsTrue();
        await Assert.That(description.None.PopCount()).IsEqualTo(2);
    }

    [Test]
    public async Task WithAny_ByTypeId_AddsToAnyMask()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create().WithAny(5);
        var description = builder.Description;

        await Assert.That(description.Any.Get(5)).IsTrue();
        await Assert.That(description.Any.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task WithAny_ByComponent_AddsToAnyMask()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create().WithAny<TestDamage>();
        var description = builder.Description;

        await Assert.That(description.Any.Get(TestDamage.TypeId)).IsTrue();
    }

    [Test]
    public async Task WithAny_Multiple_AddsAllToAnyMask()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create()
            .WithAny<TestDamage>()
            .WithAny<TestTag>();
        var description = builder.Description;

        await Assert.That(description.Any.Get(TestDamage.TypeId)).IsTrue();
        await Assert.That(description.Any.Get(TestTag.TypeId)).IsTrue();
        await Assert.That(description.Any.PopCount()).IsEqualTo(2);
    }

    [Test]
    public async Task CombinedQuery_HasCorrectMasks()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With<TestPosition>()
            .With<TestHealth>()
            .Without<TestVelocity>()
            .WithAny<TestDamage>();
        var description = builder.Description;

        await Assert.That(description.All.Get(TestPosition.TypeId)).IsTrue();
        await Assert.That(description.All.Get(TestHealth.TypeId)).IsTrue();
        await Assert.That(description.None.Get(TestVelocity.TypeId)).IsTrue();
        await Assert.That(description.Any.Get(TestDamage.TypeId)).IsTrue();

        await Assert.That(description.All.PopCount()).IsEqualTo(2);
        await Assert.That(description.None.PopCount()).IsEqualTo(1);
        await Assert.That(description.Any.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task Builder_IsImmutable()
    {
        var original = QueryBuilder<SmallBitSet<ulong>>.Create();
        var withHealth = original.With<TestHealth>();
        var withPosition = original.With<TestPosition>();

        // Capture values before await (ref structs can't cross await boundaries)
        var originalIsEmpty = original.Description.All.IsEmpty;
        var withHealthHasHealth = withHealth.Description.All.Get(TestHealth.TypeId);
        var withHealthHasPosition = withHealth.Description.All.Get(TestPosition.TypeId);
        var withPositionHasPosition = withPosition.Description.All.Get(TestPosition.TypeId);
        var withPositionHasHealth = withPosition.Description.All.Get(TestHealth.TypeId);

        await Assert.That(originalIsEmpty).IsTrue();
        await Assert.That(withHealthHasHealth).IsTrue();
        await Assert.That(withHealthHasPosition).IsFalse();
        await Assert.That(withPositionHasPosition).IsTrue();
        await Assert.That(withPositionHasHealth).IsFalse();
    }

    [Test]
    public async Task ImplicitConversion_ToDescription()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create().With<TestHealth>();
        ImmutableQueryDescription<SmallBitSet<ulong>> description = builder;

        await Assert.That(description.All.Get(TestHealth.TypeId)).IsTrue();
    }

    [Test]
    public async Task Description_Property_ReturnsCorrectValue()
    {
        var builder = QueryBuilder<SmallBitSet<ulong>>.Create()
            .With(10)
            .Without(20)
            .WithAny(30);

        var description = builder.Description;

        await Assert.That(description.All.Get(10)).IsTrue();
        await Assert.That(description.None.Get(20)).IsTrue();
        await Assert.That(description.Any.Get(30)).IsTrue();
    }

    [Test]
    public async Task Chaining_ReturnsNewBuilder()
    {
        var b1 = QueryBuilder<SmallBitSet<ulong>>.Create();
        var b2 = b1.With(0);
        var b3 = b2.Without(1);
        var b4 = b3.WithAny(2);

        // Capture values before await (ref structs can't cross await boundaries)
        var b1AllEmpty = b1.Description.All.IsEmpty;
        var b2AllHas0 = b2.Description.All.Get(0);
        var b2NoneEmpty = b2.Description.None.IsEmpty;
        var b3NoneHas1 = b3.Description.None.Get(1);
        var b4AnyHas2 = b4.Description.Any.Get(2);

        await Assert.That(b1AllEmpty).IsTrue();
        await Assert.That(b2AllHas0).IsTrue();
        await Assert.That(b2NoneEmpty).IsTrue();
        await Assert.That(b3NoneHas1).IsTrue();
        await Assert.That(b4AnyHas2).IsTrue();
    }
}
