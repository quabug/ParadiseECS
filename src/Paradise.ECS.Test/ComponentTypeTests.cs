namespace Paradise.ECS.Test;

/// <summary>
/// Test components for unit testing.
/// IDs are assigned by the source generator based on alphabetical ordering.
/// </summary>
[Component("5B9313BE-CB77-4C8B-A0E4-82A3B369C717")]
public partial struct TestHealth
{
    public int Current;
    public int Max;
}

[Component("B6170E3B-FEE1-4C16-85C9-B5130A253BAC")]
public partial struct TestPosition
{
    public float X, Y, Z;
}

[Component("1040E96A-7D4A-4241-BDE1-36D4DBFCF7C0")]
public partial struct TestVelocity
{
    public float X, Y, Z;
}

public partial class ComponentTypeTests
{
    [Component]
    public partial struct TestNested
    {
    }

    // IDs are now automatically assigned by the source generator based on alphabetical ordering:
    // - ComponentTypeTests.A: ID 0 (Paradise.ECS.Test.ComponentTypeTests.A)
    // - TestHealth: ID 1 (Paradise.ECS.Test.TestHealth)
    // - TestPosition: ID 2 (Paradise.ECS.Test.TestPosition)
    // - TestVelocity: ID 3 (Paradise.ECS.Test.TestVelocity)

    [Test]
    public async Task ComponentId_Default_IsValid()
    {
        // default(ComponentId) has Value=0, which is a valid component ID (first component)
        var id = default(ComponentId);

        await Assert.That(id.IsValid).IsTrue();
        await Assert.That(id.Value).IsEqualTo(0);
    }

    [Test]
    public async Task ComponentId_Invalid_HasNegativeValue()
    {
        var id = ComponentId.Invalid;

        await Assert.That(id.IsValid).IsFalse();
        await Assert.That(id.Value).IsEqualTo(-1);
    }

    [Test]
    public async Task ComponentId_Valid_HasNonNegativeValue()
    {
        var id = new ComponentId(5);

        await Assert.That(id.IsValid).IsTrue();
        await Assert.That(id.Value).IsEqualTo(5);
    }

    [Test]
    public async Task ComponentId_ImplicitConversion_ReturnsValue()
    {
        var id = new ComponentId(42);

        int value = id;

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Component_TypeId_ReturnsAssignedId()
    {
        await Assert.That(TestNested.TypeId.Value).IsEqualTo(0);
        await Assert.That(TestHealth.TypeId.Value).IsEqualTo(1);
        await Assert.That(TestPosition.TypeId.Value).IsEqualTo(2);
        await Assert.That(TestVelocity.TypeId.Value).IsEqualTo(3);
    }

    [Test]
    public async Task Component_Size_ReturnsCorrectSize()
    {
        await Assert.That(TestNested.Size).IsEqualTo(0);    // empty struct
        await Assert.That(TestPosition.Size).IsEqualTo(12); // 3 floats
        await Assert.That(TestVelocity.Size).IsEqualTo(12); // 3 floats
        await Assert.That(TestHealth.Size).IsEqualTo(8);    // 2 ints
    }

    [Test]
    public async Task ComponentId_Equality_WorksCorrectly()
    {
        var id1 = new ComponentId(5);
        var id2 = new ComponentId(5);
        var id3 = new ComponentId(10);

        await Assert.That(id1).IsEqualTo(id2);
        await Assert.That(id1).IsNotEqualTo(id3);
    }

    [Test]
    public async Task ComponentId_ToString_FormatsCorrectly()
    {
        var valid = new ComponentId(42);
        var invalid = ComponentId.Invalid;

        await Assert.That(valid.ToString()).IsEqualTo("ComponentId(42)");
        await Assert.That(invalid.ToString()).IsEqualTo("ComponentId(Invalid)");
    }

    [Test]
    public async Task Component_TypeId_AccessibleViaGenericConstraint()
    {
        // TypeId is accessible via generic constraint
        await Assert.That(GetTypeId<TestNested>().Value).IsEqualTo(0);
        await Assert.That(GetTypeId<TestHealth>().Value).IsEqualTo(1);
        await Assert.That(GetTypeId<TestPosition>().Value).IsEqualTo(2);
        await Assert.That(GetTypeId<TestVelocity>().Value).IsEqualTo(3);
    }

    // Helper to get TypeId via generic constraint
    private static ComponentId GetTypeId<T>() where T : unmanaged, IComponent => T.TypeId;

    [Test]
    public async Task Component_Size_AccessibleViaGenericConstraint()
    {
        // Size is accessible via generic constraint
        await Assert.That(GetSize<TestNested>()).IsEqualTo(0);   // empty struct
        await Assert.That(GetSize<TestHealth>()).IsEqualTo(8);   // 2 ints
        await Assert.That(GetSize<TestPosition>()).IsEqualTo(12);  // 3 floats
        await Assert.That(GetSize<TestVelocity>()).IsEqualTo(12);  // 3 floats
    }

    // Helper to get Size via generic constraint
    private static int GetSize<T>() where T : unmanaged, IComponent => T.Size;

    [Test]
    public async Task Component_Alignment_ReturnsCorrectAlignment()
    {
        // Alignment is a static property generated on each component struct
        // int has alignment 4, float has alignment 4
        await Assert.That(TestNested.Alignment).IsEqualTo(0);   // empty struct
        await Assert.That(TestHealth.Alignment).IsEqualTo(4);   // int alignment
        await Assert.That(TestPosition.Alignment).IsEqualTo(4);  // float alignment
        await Assert.That(TestVelocity.Alignment).IsEqualTo(4);  // float alignment
    }

    // Helper to get Alignment via generic constraint
    private static int GetAlignment<T>() where T : unmanaged, IComponent => T.Alignment;

    [Test]
    public async Task Component_Alignment_AccessibleViaGenericConstraint()
    {
        await Assert.That(GetAlignment<TestNested>()).IsEqualTo(0);  // empty struct
        await Assert.That(GetAlignment<TestHealth>()).IsEqualTo(4);
        await Assert.That(GetAlignment<TestPosition>()).IsEqualTo(4);
        await Assert.That(GetAlignment<TestVelocity>()).IsEqualTo(4);
    }
}
