namespace Paradise.ECS.Test;

public class ArchetypeTests
{
    // IDs are automatically assigned by the source generator

    [Test]
    public async Task Empty_HasNoComponents()
    {
        var archetype = Archetype<Bit256>.Empty;

        await Assert.That(archetype.IsEmpty).IsTrue();
        await Assert.That(archetype.Count).IsEqualTo(0);
    }

    [Test]
    public async Task With_AddsComponent()
    {
        var archetype = Archetype<Bit256>.Empty
            .With<TestPosition>();

        await Assert.That(archetype.Has<TestPosition>()).IsTrue();
        await Assert.That(archetype.Has<TestVelocity>()).IsFalse();
        await Assert.That(archetype.Count).IsEqualTo(1);
    }

    [Test]
    public async Task With_MultipleComponents_AddsAll()
    {
        var archetype = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>()
            .With<TestHealth>();

        await Assert.That(archetype.Has<TestPosition>()).IsTrue();
        await Assert.That(archetype.Has<TestVelocity>()).IsTrue();
        await Assert.That(archetype.Has<TestHealth>()).IsTrue();
        await Assert.That(archetype.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Without_RemovesComponent()
    {
        var archetype = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>()
            .Without<TestPosition>();

        await Assert.That(archetype.Has<TestPosition>()).IsFalse();
        await Assert.That(archetype.Has<TestVelocity>()).IsTrue();
        await Assert.That(archetype.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ContainsAll_WithSubset_ReturnsTrue()
    {
        var full = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>()
            .With<TestHealth>();

        var subset = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>();

        await Assert.That(full.ContainsAll(subset)).IsTrue();
    }

    [Test]
    public async Task ContainsAll_WithSuperset_ReturnsFalse()
    {
        var partial = Archetype<Bit256>.Empty
            .With<TestPosition>();

        var full = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>();

        await Assert.That(partial.ContainsAll(full)).IsFalse();
    }

    [Test]
    public async Task ContainsAny_WithOverlap_ReturnsTrue()
    {
        var archetype1 = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestHealth>();

        var archetype2 = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>();

        await Assert.That(archetype1.ContainsAny(archetype2)).IsTrue();
    }

    [Test]
    public async Task ContainsAny_WithNoOverlap_ReturnsFalse()
    {
        var archetype1 = Archetype<Bit256>.Empty
            .With<TestPosition>();

        var archetype2 = Archetype<Bit256>.Empty
            .With<TestVelocity>();

        await Assert.That(archetype1.ContainsAny(archetype2)).IsFalse();
    }

    [Test]
    public async Task ContainsNone_WithNoOverlap_ReturnsTrue()
    {
        var archetype1 = Archetype<Bit256>.Empty
            .With<TestPosition>();

        var archetype2 = Archetype<Bit256>.Empty
            .With<TestVelocity>();

        await Assert.That(archetype1.ContainsNone(archetype2)).IsTrue();
    }

    [Test]
    public async Task ContainsNone_WithOverlap_ReturnsFalse()
    {
        var archetype1 = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>();

        var archetype2 = Archetype<Bit256>.Empty
            .With<TestPosition>();

        await Assert.That(archetype1.ContainsNone(archetype2)).IsFalse();
    }

    [Test]
    public async Task OrOperator_CombinesArchetypes()
    {
        var archetype1 = Archetype<Bit256>.Empty.With<TestPosition>();
        var archetype2 = Archetype<Bit256>.Empty.With<TestVelocity>();

        var combined = archetype1 | archetype2;

        await Assert.That(combined.Has<TestPosition>()).IsTrue();
        await Assert.That(combined.Has<TestVelocity>()).IsTrue();
        await Assert.That(combined.Count).IsEqualTo(2);
    }

    [Test]
    public async Task AndOperator_IntersectsArchetypes()
    {
        var archetype1 = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>();

        var archetype2 = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestHealth>();

        var intersection = archetype1 & archetype2;

        await Assert.That(intersection.Has<TestPosition>()).IsTrue();
        await Assert.That(intersection.Has<TestVelocity>()).IsFalse();
        await Assert.That(intersection.Has<TestHealth>()).IsFalse();
        await Assert.That(intersection.Count).IsEqualTo(1);
    }

    [Test]
    public async Task XorOperator_SymmetricDifference()
    {
        var archetype1 = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>();

        var archetype2 = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestHealth>();

        var diff = archetype1 ^ archetype2;

        await Assert.That(diff.Has<TestPosition>()).IsFalse(); // Common, excluded
        await Assert.That(diff.Has<TestVelocity>()).IsTrue();  // Only in archetype1
        await Assert.That(diff.Has<TestHealth>()).IsTrue();    // Only in archetype2
        await Assert.That(diff.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Equality_SameComponents_AreEqual()
    {
        var archetype1 = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>();

        var archetype2 = Archetype<Bit256>.Empty
            .With<TestVelocity>()  // Different order
            .With<TestPosition>();

        await Assert.That(archetype1).IsEqualTo(archetype2);
    }

    [Test]
    public async Task Equality_DifferentComponents_AreNotEqual()
    {
        var archetype1 = Archetype<Bit256>.Empty.With<TestPosition>();
        var archetype2 = Archetype<Bit256>.Empty.With<TestVelocity>();

        await Assert.That(archetype1).IsNotEqualTo(archetype2);
    }

    [Test]
    public async Task Capacity_MatchesBitStorage()
    {
        await Assert.That(Archetype<Bit64>.Capacity).IsEqualTo(64);
        await Assert.That(Archetype<Bit128>.Capacity).IsEqualTo(128);
        await Assert.That(Archetype<Bit256>.Capacity).IsEqualTo(256);
        await Assert.That(Archetype<Bit512>.Capacity).IsEqualTo(512);
        await Assert.That(Archetype<Bit1024>.Capacity).IsEqualTo(1024);
    }

    [Test]
    public async Task Mask_ExposesUnderlyingBitSet()
    {
        var archetype = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>();

        var mask = archetype.Mask;

        await Assert.That(mask.Get(TestPosition.TypeId.Value)).IsTrue();
        await Assert.That(mask.Get(TestVelocity.TypeId.Value)).IsTrue();
        await Assert.That(mask.Get(TestHealth.TypeId.Value)).IsFalse();
    }

    [Test]
    public async Task ToString_ContainsComponentCount()
    {
        var archetype = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>();

        var str = archetype.ToString();

        await Assert.That(str).Contains("2 components");
        await Assert.That(str).Contains("Bit256");
    }

    [Test]
    public async Task With_Type_AddsComponent()
    {
        var archetype = Archetype<Bit256>.Empty
            .With(typeof(TestPosition));

        await Assert.That(archetype.Has<TestPosition>()).IsTrue();
        await Assert.That(archetype.Has<TestVelocity>()).IsFalse();
    }

    [Test]
    public async Task Without_Type_RemovesComponent()
    {
        var archetype = Archetype<Bit256>.Empty
            .With<TestPosition>()
            .With<TestVelocity>()
            .Without(typeof(TestPosition));

        await Assert.That(archetype.Has<TestPosition>()).IsFalse();
        await Assert.That(archetype.Has<TestVelocity>()).IsTrue();
    }

    [Test]
    public async Task Has_Type_ChecksComponent()
    {
        var archetype = Archetype<Bit256>.Empty
            .With<TestPosition>();

        await Assert.That(archetype.Has(typeof(TestPosition))).IsTrue();
        await Assert.That(archetype.Has(typeof(TestVelocity))).IsFalse();
    }

    [Test]
    public async Task ComponentRegistry_GetId_ReturnsCorrectId()
    {
        await Assert.That(ComponentRegistry.GetId(typeof(TestHealth)).Value).IsEqualTo(0);
        await Assert.That(ComponentRegistry.GetId(typeof(TestPosition)).Value).IsEqualTo(1);
        await Assert.That(ComponentRegistry.GetId(typeof(TestVelocity)).Value).IsEqualTo(2);
    }

    [Test]
    public async Task ComponentRegistry_GetId_UnknownType_ReturnsInvalid()
    {
        var id = ComponentRegistry.GetId(typeof(string));

        await Assert.That(id.IsValid).IsFalse();
    }

    [Test]
    public async Task ComponentRegistry_TryGetId_ReturnsTrue_WhenFound()
    {
        var found = ComponentRegistry.TryGetId(typeof(TestPosition), out var id);

        await Assert.That(found).IsTrue();
        await Assert.That(id.Value).IsEqualTo(1);
    }

    [Test]
    public async Task ComponentRegistry_TryGetId_ReturnsFalse_WhenNotFound()
    {
        var found = ComponentRegistry.TryGetId(typeof(string), out _);

        await Assert.That(found).IsFalse();
    }

    [Test]
    public async Task ComponentRegistry_GetId_ByGuid_ReturnsCorrectId()
    {
        var guid = new System.Guid("5B9313BE-CB77-4C8B-A0E4-82A3B369C717"); // TestHealth's GUID
        var id = ComponentRegistry.GetId(guid);

        await Assert.That(id.Value).IsEqualTo(0);
    }

    [Test]
    public async Task ComponentRegistry_GetId_UnknownGuid_ReturnsInvalid()
    {
        var guid = System.Guid.NewGuid();
        var id = ComponentRegistry.GetId(guid);

        await Assert.That(id.IsValid).IsFalse();
    }
}
