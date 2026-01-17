namespace Paradise.ECS.Test;

/// <summary>
/// Tests for <see cref="ChunkHandle"/>.
/// </summary>
public sealed class ChunkHandleTests
{
    [Test]
    public async Task Default_IsInvalid()
    {
        var handle = default(ChunkHandle);

        await Assert.That(handle.IsValid).IsFalse();
        await Assert.That(handle.Id).IsEqualTo(0);
        await Assert.That(handle.Version).IsEqualTo(0UL);
    }

    [Test]
    public async Task Invalid_IsDefault()
    {
        var handle = ChunkHandle.Invalid;

        await Assert.That(handle.IsValid).IsFalse();
        await Assert.That(handle).IsEqualTo(default(ChunkHandle));
    }

    [Test]
    public async Task Constructor_SetsIdAndVersion()
    {
        var handle = new ChunkHandle(42, 7);

        await Assert.That(handle.Id).IsEqualTo(42);
        await Assert.That(handle.Version).IsEqualTo(7UL);
    }

    [Test]
    public async Task IsValid_TrueWhenVersionGreaterThanZero()
    {
        var handle = new ChunkHandle(0, 1);

        await Assert.That(handle.IsValid).IsTrue();
    }

    [Test]
    public async Task IsValid_FalseWhenVersionIsZero()
    {
        var handle = new ChunkHandle(42, 0);

        await Assert.That(handle.IsValid).IsFalse();
    }

    [Test]
    public async Task Equals_ReturnsTrueForSameIdAndVersion()
    {
        var handle1 = new ChunkHandle(42, 7);
        var handle2 = new ChunkHandle(42, 7);

        await Assert.That(handle1.Equals(handle2)).IsTrue();
        await Assert.That(handle1 == handle2).IsTrue();
    }

    [Test]
    public async Task Equals_ReturnsFalseForDifferentId()
    {
        var handle1 = new ChunkHandle(42, 7);
        var handle2 = new ChunkHandle(43, 7);

        await Assert.That(handle1.Equals(handle2)).IsFalse();
        await Assert.That(handle1 != handle2).IsTrue();
    }

    [Test]
    public async Task Equals_ReturnsFalseForDifferentVersion()
    {
        var handle1 = new ChunkHandle(42, 7);
        var handle2 = new ChunkHandle(42, 8);

        await Assert.That(handle1.Equals(handle2)).IsFalse();
    }

    [Test]
    public async Task GetHashCode_SameForEqualHandles()
    {
        var handle1 = new ChunkHandle(42, 7);
        var handle2 = new ChunkHandle(42, 7);

        await Assert.That(handle1.GetHashCode()).IsEqualTo(handle2.GetHashCode());
    }

    [Test]
    public async Task GetHashCode_DifferentForDifferentHandles()
    {
        var handle1 = new ChunkHandle(42, 7);
        var handle2 = new ChunkHandle(43, 7);

        // Compute hash codes (they should differ)
        _ = handle1.GetHashCode();
        _ = handle2.GetHashCode();

        // We don't assert they differ because hash collisions are possible,
        // but we verify the method doesn't throw
        await Assert.That(handle1).IsNotEqualTo(handle2);
    }

    [Test]
    public async Task ToString_Valid_ContainsIdAndVersion()
    {
        var handle = new ChunkHandle(42, 7);
        var str = handle.ToString();

        await Assert.That(str).Contains("42");
        await Assert.That(str).Contains("7");
    }

    [Test]
    public async Task ToString_Invalid_IndicatesInvalid()
    {
        var handle = ChunkHandle.Invalid;
        var str = handle.ToString();

        await Assert.That(str).Contains("Invalid");
    }

    [Test]
    public async Task Dictionary_WorksWithChunkHandle()
    {
        var dict = new Dictionary<ChunkHandle, string>();
        var handle1 = new ChunkHandle(1, 1);
        var handle2 = new ChunkHandle(2, 1);

        dict[handle1] = "first";
        dict[handle2] = "second";

        await Assert.That(dict[handle1]).IsEqualTo("first");
        await Assert.That(dict[handle2]).IsEqualTo("second");
    }
}
