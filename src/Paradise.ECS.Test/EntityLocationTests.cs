namespace Paradise.ECS.Test;

/// <summary>
/// Tests for EntityLocation.
/// </summary>
public sealed class EntityLocationTests
{
    [Test]
    public async Task Default_HasZeroValues()
    {
        var location = default(EntityLocation);

        // default(EntityLocation) has ArchetypeId=0, which is valid (0 >= 0)
        await Assert.That(location.IsValid).IsTrue();
        await Assert.That(location.Version).IsEqualTo(0u);
        await Assert.That(location.ArchetypeId).IsEqualTo(0);
        await Assert.That(location.GlobalIndex).IsEqualTo(0);
    }

    [Test]
    public async Task Invalid_HasNegativeArchetypeId()
    {
        var location = EntityLocation.Invalid;

        await Assert.That(location.IsValid).IsFalse();
        await Assert.That(location.ArchetypeId).IsEqualTo(-1);
        await Assert.That(location.GlobalIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task Constructor_SetsAllFields()
    {
        var location = new EntityLocation(5, 10, 20);

        await Assert.That(location.Version).IsEqualTo(5u);
        await Assert.That(location.ArchetypeId).IsEqualTo(10);
        await Assert.That(location.GlobalIndex).IsEqualTo(20);
    }

    [Test]
    public async Task IsValid_PositiveArchetypeId_ReturnsTrue()
    {
        var location = new EntityLocation(1, 0, 0);

        await Assert.That(location.IsValid).IsTrue();
    }

    [Test]
    public async Task IsValid_NegativeArchetypeId_ReturnsFalse()
    {
        var location = new EntityLocation(1, -1, 0);

        await Assert.That(location.IsValid).IsFalse();
    }

    [Test]
    public async Task MatchesEntity_SameVersion_ReturnsTrue()
    {
        var location = new EntityLocation(5, 0, 0);
        var entity = new Entity(0, 5);

        await Assert.That(location.MatchesEntity(entity)).IsTrue();
    }

    [Test]
    public async Task MatchesEntity_DifferentVersion_ReturnsFalse()
    {
        var location = new EntityLocation(5, 0, 0);
        var entity = new Entity(0, 6);

        await Assert.That(location.MatchesEntity(entity)).IsFalse();
    }

    [Test]
    public async Task MatchesEntity_ZeroVersion_ReturnsFalse()
    {
        var location = new EntityLocation(0, 0, 0);
        var entity = new Entity(0, 0);

        await Assert.That(location.MatchesEntity(entity)).IsFalse();
    }

    [Test]
    public async Task ToString_Valid_ContainsInfo()
    {
        var location = new EntityLocation(5, 10, 20);
        var str = location.ToString();

        await Assert.That(str).Contains("5");
        await Assert.That(str).Contains("10");
        await Assert.That(str).Contains("20");
    }

    [Test]
    public async Task ToString_Invalid_IndicatesInvalid()
    {
        var location = EntityLocation.Invalid;
        var str = location.ToString();

        await Assert.That(str).Contains("Invalid");
    }

    [Test]
    public async Task Equality_SameValues_ReturnsTrue()
    {
        var loc1 = new EntityLocation(5, 10, 20);
        var loc2 = new EntityLocation(5, 10, 20);

        await Assert.That(loc1).IsEqualTo(loc2);
        await Assert.That(loc1 == loc2).IsTrue();
    }

    [Test]
    public async Task Equality_DifferentValues_ReturnsFalse()
    {
        var loc1 = new EntityLocation(5, 10, 20);
        var loc2 = new EntityLocation(5, 10, 21);

        await Assert.That(loc1).IsNotEqualTo(loc2);
        await Assert.That(loc1 != loc2).IsTrue();
    }

    [Test]
    public async Task GetHashCode_SameValues_ReturnsSame()
    {
        var loc1 = new EntityLocation(5, 10, 20);
        var loc2 = new EntityLocation(5, 10, 20);

        await Assert.That(loc1.GetHashCode()).IsEqualTo(loc2.GetHashCode());
    }

    [Test]
    public async Task With_ModifiesValue()
    {
        var location = new EntityLocation(5, 10, 20);

        var modified = location with { GlobalIndex = 30 };

        await Assert.That(modified.Version).IsEqualTo(5u);
        await Assert.That(modified.ArchetypeId).IsEqualTo(10);
        await Assert.That(modified.GlobalIndex).IsEqualTo(30);
    }
}
