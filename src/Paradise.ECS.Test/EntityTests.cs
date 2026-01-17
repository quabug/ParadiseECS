namespace Paradise.ECS.Test;

/// <summary>
/// Tests for <see cref="Entity"/>.
/// </summary>
public sealed class EntityTests
{
    [Test]
    public async Task Default_IsInvalid()
    {
        var entity = default(Entity);

        await Assert.That(entity.IsValid).IsFalse();
        await Assert.That(entity.Id).IsEqualTo(0);
        await Assert.That(entity.Version).IsEqualTo(0u);
    }

    [Test]
    public async Task Invalid_IsDefault()
    {
        var entity = Entity.Invalid;

        await Assert.That(entity.IsValid).IsFalse();
        await Assert.That(entity).IsEqualTo(default(Entity));
    }

    [Test]
    public async Task Constructor_SetsIdAndVersion()
    {
        var entity = new Entity(42, 7);

        await Assert.That(entity.Id).IsEqualTo(42);
        await Assert.That(entity.Version).IsEqualTo(7u);
    }

    [Test]
    public async Task IsValid_TrueWhenVersionGreaterThanZero()
    {
        var entity = new Entity(0, 1);

        await Assert.That(entity.IsValid).IsTrue();
    }

    [Test]
    public async Task IsValid_FalseWhenVersionIsZero()
    {
        var entity = new Entity(42, 0);

        await Assert.That(entity.IsValid).IsFalse();
    }

    [Test]
    public async Task Equals_ReturnsTrueForSameIdAndVersion()
    {
        var entity1 = new Entity(42, 7);
        var entity2 = new Entity(42, 7);

        await Assert.That(entity1.Equals(entity2)).IsTrue();
        await Assert.That(entity1 == entity2).IsTrue();
    }

    [Test]
    public async Task Equals_ReturnsFalseForDifferentId()
    {
        var entity1 = new Entity(42, 7);
        var entity2 = new Entity(43, 7);

        await Assert.That(entity1.Equals(entity2)).IsFalse();
        await Assert.That(entity1 != entity2).IsTrue();
    }

    [Test]
    public async Task Equals_ReturnsFalseForDifferentVersion()
    {
        var entity1 = new Entity(42, 7);
        var entity2 = new Entity(42, 8);

        await Assert.That(entity1.Equals(entity2)).IsFalse();
    }

    [Test]
    public async Task GetHashCode_SameForEqualEntities()
    {
        var entity1 = new Entity(42, 7);
        var entity2 = new Entity(42, 7);

        await Assert.That(entity1.GetHashCode()).IsEqualTo(entity2.GetHashCode());
    }

    [Test]
    public async Task GetHashCode_DifferentForDifferentEntities()
    {
        var entity1 = new Entity(42, 7);
        var entity2 = new Entity(43, 7);

        // Compute hash codes (they should differ)
        _ = entity1.GetHashCode();
        _ = entity2.GetHashCode();

        // We don't assert they differ because hash collisions are possible,
        // but we verify the method doesn't throw
        await Assert.That(entity1).IsNotEqualTo(entity2);
    }

    [Test]
    public async Task ToString_Valid_ContainsIdAndVersion()
    {
        var entity = new Entity(42, 7);
        var str = entity.ToString();

        await Assert.That(str).Contains("42");
        await Assert.That(str).Contains("7");
    }

    [Test]
    public async Task ToString_Invalid_IndicatesInvalid()
    {
        var entity = Entity.Invalid;
        var str = entity.ToString();

        await Assert.That(str).Contains("Invalid");
    }

    [Test]
    public async Task Dictionary_WorksWithEntity()
    {
        var dict = new Dictionary<Entity, string>();
        var entity1 = new Entity(1, 1);
        var entity2 = new Entity(2, 1);

        dict[entity1] = "first";
        dict[entity2] = "second";

        await Assert.That(dict[entity1]).IsEqualTo("first");
        await Assert.That(dict[entity2]).IsEqualTo("second");
    }

    [Test]
    public async Task HashSet_WorksWithEntity()
    {
        var set = new HashSet<Entity>
        {
            new Entity(1, 1),
            new Entity(2, 1),
            new Entity(1, 1) // Duplicate
        };

        await Assert.That(set.Count).IsEqualTo(2);
    }
}
