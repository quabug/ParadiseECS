namespace Paradise.ECS.Concurrent.Test;

public class ComponentAttributeTests
{
    [Test]
    public async Task Constructor_WithNullGuid_SetsGuidToNull()
    {
        var attr = new ComponentAttribute();
        await Assert.That(attr.Guid).IsNull();
    }

    [Test]
    public async Task Constructor_WithGuid_SetsGuidProperty()
    {
        const string testGuid = "12345678-1234-1234-1234-123456789012";
        var attr = new ComponentAttribute(testGuid);
        await Assert.That(attr.Guid).IsEqualTo(testGuid);
    }

    [Test]
    public async Task Constructor_WithExplicitNull_SetsGuidToNull()
    {
        var attr = new ComponentAttribute(null);
        await Assert.That(attr.Guid).IsNull();
    }
}
