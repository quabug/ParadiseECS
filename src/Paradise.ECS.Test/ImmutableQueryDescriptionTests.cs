#pragma warning disable CA2263 // Prefer generic overload - intentionally testing Type-based APIs

namespace Paradise.ECS.Test;

public class ImmutableQueryDescriptionTests
{
    private static ImmutableBitSet<Bit64> MakeMask(params int[] componentIds)
    {
        var mask = ImmutableBitSet<Bit64>.Empty;
        foreach (var id in componentIds)
        {
            mask = mask.Set(id);
        }
        return mask;
    }

    [Test]
    public async Task Empty_MatchesAllArchetypes()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty;

        await Assert.That(desc.All.IsEmpty).IsTrue();
        await Assert.That(desc.None.IsEmpty).IsTrue();
        await Assert.That(desc.Any.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Empty_MatchesAnyMask()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty;
        var mask = MakeMask(TestPosition.TypeId.Value, TestVelocity.TypeId.Value);

        await Assert.That(desc.Matches(mask)).IsTrue();
        await Assert.That(desc.Matches(ImmutableBitSet<Bit64>.Empty)).IsTrue();
    }

    [Test]
    public async Task With_Generic_AddsToAllConstraint()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .With<TestPosition>();

        await Assert.That(desc.All.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(desc.None.IsEmpty).IsTrue();
        await Assert.That(desc.Any.IsEmpty).IsTrue();
    }

    [Test]
    public async Task With_Type_AddsToAllConstraint()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .With(typeof(TestPosition));

        await Assert.That(desc.All.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task Without_Generic_AddsToNoneConstraint()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .Without<TestPosition>();

        await Assert.That(desc.All.IsEmpty).IsTrue();
        await Assert.That(desc.None.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(desc.Any.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Without_Type_AddsToNoneConstraint()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .Without(typeof(TestPosition));

        await Assert.That(desc.None.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task WithAny_Generic_AddsToAnyConstraint()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .WithAny<TestPosition>();

        await Assert.That(desc.All.IsEmpty).IsTrue();
        await Assert.That(desc.None.IsEmpty).IsTrue();
        await Assert.That(desc.Any.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task WithAny_Type_AddsToAnyConstraint()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .WithAny(typeof(TestPosition));

        await Assert.That(desc.Any.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task With_ComponentId_AddsToAllConstraint()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .With(TestPosition.TypeId.Value);

        await Assert.That(desc.All.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(desc.None.IsEmpty).IsTrue();
        await Assert.That(desc.Any.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Without_ComponentId_AddsToNoneConstraint()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .Without(TestPosition.TypeId.Value);

        await Assert.That(desc.All.IsEmpty).IsTrue();
        await Assert.That(desc.None.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(desc.Any.IsEmpty).IsTrue();
    }

    [Test]
    public async Task WithAny_ComponentId_AddsToAnyConstraint()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .WithAny(TestPosition.TypeId.Value);

        await Assert.That(desc.All.IsEmpty).IsTrue();
        await Assert.That(desc.None.IsEmpty).IsTrue();
        await Assert.That(desc.Any.Get(TestPosition.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task Matches_AllConstraint_RequiresAllComponents()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .With<TestPosition>()
            .With<TestVelocity>();

        var matchingMask = MakeMask(TestPosition.TypeId.Value, TestVelocity.TypeId.Value);
        var partialMask = MakeMask(TestPosition.TypeId.Value);
        var supersetMask = MakeMask(TestPosition.TypeId.Value, TestVelocity.TypeId.Value, TestHealth.TypeId.Value);

        await Assert.That(desc.Matches(matchingMask)).IsTrue();
        await Assert.That(desc.Matches(partialMask)).IsFalse();
        await Assert.That(desc.Matches(supersetMask)).IsTrue();
    }

    [Test]
    public async Task Matches_NoneConstraint_ExcludesComponents()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .With<TestPosition>()
            .Without<TestVelocity>();

        var matchingMask = MakeMask(TestPosition.TypeId.Value);
        var excludedMask = MakeMask(TestPosition.TypeId.Value, TestVelocity.TypeId.Value);

        await Assert.That(desc.Matches(matchingMask)).IsTrue();
        await Assert.That(desc.Matches(excludedMask)).IsFalse();
    }

    [Test]
    public async Task Matches_AnyConstraint_RequiresAtLeastOne()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .WithAny<TestPosition>()
            .WithAny<TestVelocity>();

        var hasFirst = MakeMask(TestPosition.TypeId.Value);
        var hasSecond = MakeMask(TestVelocity.TypeId.Value);
        var hasBoth = MakeMask(TestPosition.TypeId.Value, TestVelocity.TypeId.Value);
        var hasNeither = MakeMask(TestHealth.TypeId.Value);

        await Assert.That(desc.Matches(hasFirst)).IsTrue();
        await Assert.That(desc.Matches(hasSecond)).IsTrue();
        await Assert.That(desc.Matches(hasBoth)).IsTrue();
        await Assert.That(desc.Matches(hasNeither)).IsFalse();
    }

    [Test]
    public async Task Matches_CombinedConstraints_AllMustPass()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .With<TestPosition>()
            .Without<TestHealth>()
            .WithAny<TestVelocity>();

        // Has Position + Velocity, no Health - matches
        var matching = MakeMask(TestPosition.TypeId.Value, TestVelocity.TypeId.Value);
        await Assert.That(desc.Matches(matching)).IsTrue();

        // Has Position but missing any of Velocity - fails
        var missingAny = MakeMask(TestPosition.TypeId.Value);
        await Assert.That(desc.Matches(missingAny)).IsFalse();

        // Has Position + Velocity + Health - fails (has excluded)
        var hasExcluded = MakeMask(TestPosition.TypeId.Value, TestVelocity.TypeId.Value, TestHealth.TypeId.Value);
        await Assert.That(desc.Matches(hasExcluded)).IsFalse();

        // Missing Position - fails
        var missingRequired = MakeMask(TestVelocity.TypeId.Value);
        await Assert.That(desc.Matches(missingRequired)).IsFalse();
    }

    [Test]
    public async Task RecordEquality_SameConstraints_AreEqual()
    {
        var desc1 = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .With<TestPosition>()
            .Without<TestHealth>();

        var desc2 = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .With<TestPosition>()
            .Without<TestHealth>();

        await Assert.That(desc1).IsEqualTo(desc2);
        await Assert.That(desc1.GetHashCode()).IsEqualTo(desc2.GetHashCode());
    }

    [Test]
    public async Task RecordEquality_DifferentConstraints_AreNotEqual()
    {
        var desc1 = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .With<TestPosition>();

        var desc2 = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .With<TestVelocity>();

        await Assert.That(desc1).IsNotEqualTo(desc2);
    }

    [Test]
    public async Task Chaining_PreservesExistingConstraints()
    {
        var desc = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty
            .With<TestPosition>()
            .With<TestVelocity>()
            .Without<TestHealth>();

        await Assert.That(desc.All.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(desc.All.Get(TestVelocity.TypeId.Value)).IsTrue();
        await Assert.That(desc.None.Get(TestHealth.TypeId.Value)).IsTrue();
    }

    [Test]
    public async Task With_InvalidType_ThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty.With(typeof(string));
            await Task.CompletedTask.ConfigureAwait(false);
        });
    }

    [Test]
    public async Task Without_InvalidType_ThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty.Without(typeof(int));
            await Task.CompletedTask.ConfigureAwait(false);
        });
    }

    [Test]
    public async Task WithAny_InvalidType_ThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = ImmutableQueryDescription<Bit64, ComponentRegistry>.Empty.WithAny(typeof(object));
            await Task.CompletedTask.ConfigureAwait(false);
        });
    }

    [Test]
    public async Task SingleArgConstructor_SetsOnlyAllConstraint()
    {
        var all = MakeMask(TestPosition.TypeId.Value);
        var desc = new ImmutableQueryDescription<Bit64, ComponentRegistry>(all);

        await Assert.That(desc.All).IsEqualTo(all);
        await Assert.That(desc.None.IsEmpty).IsTrue();
        await Assert.That(desc.Any.IsEmpty).IsTrue();
    }
}
