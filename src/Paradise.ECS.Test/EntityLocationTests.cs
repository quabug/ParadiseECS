namespace Paradise.ECS.Test;

/// <summary>
/// Tests for EntityLocation 64-bit packed struct.
/// </summary>
public sealed class EntityLocationTests
{
    #region Constructor and Property Tests

    [Test]
    public async Task Constructor_DefaultValues_CreatesValidPacking()
    {
        var location = new EntityLocation(1, 0, 0);

        await Assert.That(location.Version).IsEqualTo(1u);
        await Assert.That(location.ArchetypeId).IsEqualTo(0);
        await Assert.That(location.GlobalIndex).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_InvalidArchetype_PacksCorrectly()
    {
        var location = new EntityLocation(1, -1, -1);

        await Assert.That(location.Version).IsEqualTo(1u);
        await Assert.That(location.ArchetypeId).IsEqualTo(-1);
        await Assert.That(location.GlobalIndex).IsEqualTo(-1);
        await Assert.That(location.IsValid).IsFalse();
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
    public async Task Constructor_MaxValues_PacksCorrectly()
    {
        var location = new EntityLocation(
            EntityLocation.MaxVersion,
            EntityLocation.MaxArchetypeId,
            EntityLocation.MaxGlobalIndex);

        await Assert.That(location.Version).IsEqualTo(EntityLocation.MaxVersion);
        await Assert.That(location.ArchetypeId).IsEqualTo(EntityLocation.MaxArchetypeId);
        await Assert.That(location.GlobalIndex).IsEqualTo(EntityLocation.MaxGlobalIndex);
    }

    [Test]
    public async Task Constructor_TypicalGameValues_PacksCorrectly()
    {
        // Typical game: version 1-100, archetype 0-1000, index 0-100000
        var location = new EntityLocation(42, 500, 50000);

        await Assert.That(location.Version).IsEqualTo(42u);
        await Assert.That(location.ArchetypeId).IsEqualTo(500);
        await Assert.That(location.GlobalIndex).IsEqualTo(50000);
    }

    [Test]
    public async Task Default_HasZeroValues()
    {
        var location = default(EntityLocation);

        // default(EntityLocation) has packed=0, which unpacks to:
        // Version=0, ArchetypeId=-1 (0-1), GlobalIndex=-1 (0-1)
        await Assert.That(location.Version).IsEqualTo(0u);
        await Assert.That(location.ArchetypeId).IsEqualTo(-1);
        await Assert.That(location.GlobalIndex).IsEqualTo(-1);
        await Assert.That(location.IsValid).IsFalse();
    }

    #endregion

    #region IsValid Tests

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

    #endregion

    #region Invalid Static Property Tests

    [Test]
    public async Task Invalid_HasNegativeArchetypeId()
    {
        var location = EntityLocation.Invalid;

        await Assert.That(location.IsValid).IsFalse();
        await Assert.That(location.ArchetypeId).IsEqualTo(-1);
        await Assert.That(location.GlobalIndex).IsEqualTo(-1);
    }

    [Test]
    public async Task Invalid_HasCorrectPackedValue()
    {
        var invalid = EntityLocation.Invalid;

        await Assert.That(invalid.Version).IsEqualTo(0u);
        await Assert.That(invalid.ArchetypeId).IsEqualTo(-1);
        await Assert.That(invalid.GlobalIndex).IsEqualTo(-1);
    }

    #endregion

    #region FromPacked Tests

    [Test]
    public async Task FromPacked_RoundTrip_PreservesValues()
    {
        var original = new EntityLocation(100, 200, 300);
        ulong packed = original.Packed;
        var restored = EntityLocation.FromPacked(packed);

        await Assert.That(restored.Version).IsEqualTo(original.Version);
        await Assert.That(restored.ArchetypeId).IsEqualTo(original.ArchetypeId);
        await Assert.That(restored.GlobalIndex).IsEqualTo(original.GlobalIndex);
    }

    [Test]
    public async Task FromPacked_Invalid_PreservesInvalidState()
    {
        var original = EntityLocation.Invalid;
        ulong packed = original.Packed;
        var restored = EntityLocation.FromPacked(packed);

        await Assert.That(restored.IsValid).IsFalse();
        await Assert.That(restored.ArchetypeId).IsEqualTo(-1);
        await Assert.That(restored.GlobalIndex).IsEqualTo(-1);
    }

    #endregion

    #region MatchesEntity Tests

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

    #endregion

    #region Equality Tests

    [Test]
    public async Task Equals_SameValues_ReturnsTrue()
    {
        var a = new EntityLocation(1, 2, 3);
        var b = new EntityLocation(1, 2, 3);

        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a == b).IsTrue();
        await Assert.That(a != b).IsFalse();
    }

    [Test]
    public async Task Equals_DifferentValues_ReturnsFalse()
    {
        var a = new EntityLocation(1, 2, 3);
        var b = new EntityLocation(1, 2, 4);

        await Assert.That(a.Equals(b)).IsFalse();
        await Assert.That(a == b).IsFalse();
        await Assert.That(a != b).IsTrue();
    }

    [Test]
    public async Task GetHashCode_SameValues_SameHash()
    {
        var a = new EntityLocation(1, 2, 3);
        var b = new EntityLocation(1, 2, 3);

        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Test]
    public async Task ToString_ValidLocation_IncludesAllFields()
    {
        var location = new EntityLocation(10, 20, 30);
        var str = location.ToString();

        await Assert.That(str).Contains("10");
        await Assert.That(str).Contains("20");
        await Assert.That(str).Contains("30");
    }

    [Test]
    public async Task ToString_InvalidLocation_IndicatesInvalid()
    {
        var location = EntityLocation.Invalid;
        var str = location.ToString();

        await Assert.That(str).Contains("Invalid");
    }

    #endregion

    #region Boundary Tests

    [Test]
    public async Task VersionOverflow_WrapsAround()
    {
        // Version exceeding 24 bits should wrap
        uint overflowVersion = EntityLocation.MaxVersion + 1;
        var location = new EntityLocation(overflowVersion, 0, 0);

        // Should wrap to 0
        await Assert.That(location.Version).IsEqualTo(0u);
    }

    [Test]
    public async Task MaxConstants_MatchBitLimits()
    {
        // MaxVersion should be 2^24 - 1
        uint maxVersion = EntityLocation.MaxVersion;
        await Assert.That(maxVersion).IsEqualTo((1u << 24) - 1);

        // MaxArchetypeId should be 2^20 - 2 (reserving -1 for invalid)
        int maxArchetypeId = EntityLocation.MaxArchetypeId;
        await Assert.That(maxArchetypeId).IsEqualTo((1 << 20) - 2);

        // MaxGlobalIndex should be 2^20 - 2 (reserving -1 for invalid)
        int maxGlobalIndex = EntityLocation.MaxGlobalIndex;
        await Assert.That(maxGlobalIndex).IsEqualTo((1 << 20) - 2);
    }

    #endregion
}
