namespace Paradise.ECS.IntegrationTest.Tests;

/// <summary>
/// Integration tests verifying generated types work correctly.
/// </summary>
public sealed class GeneratedTypesIntegrationTests
{
    [Test]
    public async Task ComponentMask_TypeIsGenerated()
    {
        var typeName = typeof(ComponentMask).FullName;

        await Assert.That(typeName).Contains("ImmutableBitSet");
    }

    [Test]
    public async Task World_TypeIsTaggedWorld()
    {
        var typeName = typeof(World).FullName;

        await Assert.That(typeName).Contains("TaggedWorld");
    }

    [Test]
    public async Task ComponentTypeIds_AreAssigned()
    {
        await Assert.That(Position.TypeId.Value).IsGreaterThanOrEqualTo(0);
        await Assert.That(Velocity.TypeId.Value).IsGreaterThanOrEqualTo(0);
        await Assert.That(Health.TypeId.Value).IsGreaterThanOrEqualTo(0);
        await Assert.That(Name.TypeId.Value).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task TagIds_AreAssigned()
    {
        await Assert.That(PlayerTag.TagId.Value).IsGreaterThanOrEqualTo(0);
        await Assert.That(EnemyTag.TagId.Value).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task ComponentGuids_AreAssigned()
    {
        await Assert.That(Position.Guid).IsNotEqualTo(Guid.Empty);
        await Assert.That(PlayerTag.Guid).IsNotEqualTo(Guid.Empty);
    }

    [Test]
    public async Task ComponentSizes_AreCorrect()
    {
        await Assert.That(Position.Size).IsEqualTo(8);  // 2 floats
        await Assert.That(Health.Size).IsEqualTo(8);    // 2 ints
        await Assert.That(Name.Size).IsEqualTo(68);     // fixed char[32] + int
    }

    [Test]
    public async Task ComponentRegistry_HasTypeInfos()
    {
        await Assert.That(ComponentRegistry.TypeInfos.Length).IsGreaterThan(0);
    }
}
