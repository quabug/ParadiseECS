namespace Paradise.ECS.Test;

/// <summary>
/// Minimal test to verify the test framework is working.
/// </summary>
public sealed class MinimalTest
{
    [Test]
    public async Task Simple_Test_Works()
    {
        var list = new List<int> { 1, 2, 3 };
        await Assert.That(list.Count).IsEqualTo(3);
    }
}
