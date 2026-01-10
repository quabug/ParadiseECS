namespace Paradise.ECS.Test;

public class EntityTests
{
    [Test]
    public async Task Entity_DefaultIsInvalid()
    {
        var entity = default(Entity);
        await Assert.That(entity.IsValid).IsFalse();
    }

    [Test]
    public async Task Entity_InvalidConstantIsInvalid()
    {
        var entity = Entity.Invalid;
        await Assert.That(entity.IsValid).IsFalse();
        await Assert.That(entity).IsEqualTo(default(Entity));
    }

    [Test]
    public async Task Entity_WithValidIdIsValid()
    {
        var entity = new Entity(42, 1);
        await Assert.That(entity.IsValid).IsTrue();
        await Assert.That(entity.Id).IsEqualTo(42);
        await Assert.That(entity.Version).IsEqualTo(1u);
    }

    [Test]
    public async Task Entity_ToString_ShowsIdAndVersion()
    {
        var entity = new Entity(123, 5);
        var str = entity.ToString();
        await Assert.That(str).Contains("123");
        await Assert.That(str).Contains("5");
    }

    [Test]
    public async Task Entity_ToString_InvalidShowsInvalid()
    {
        var entity = Entity.Invalid;
        var str = entity.ToString();
        await Assert.That(str).Contains("Invalid");
    }

    [Test]
    public async Task Entity_RecordEquality()
    {
        var entity1 = new Entity(10, 2);
        var entity2 = new Entity(10, 2);
        var entity3 = new Entity(10, 3);
        var entity4 = new Entity(11, 2);

        await Assert.That(entity1).IsEqualTo(entity2);
        await Assert.That(entity1).IsNotEqualTo(entity3);
        await Assert.That(entity1).IsNotEqualTo(entity4);
    }
}
