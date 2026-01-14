namespace Paradise.ECS.Test;

public sealed class ConcurrentAppendOnlyListTests
{
    [Test]
    public async Task Add_SingleElement_ReturnsCorrectIndex()
    {
        using var list = new ConcurrentAppendOnlyList<int>();

        var index = list.Add(42);

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(list.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo(42);
    }

    [Test]
    public async Task Add_MultipleElements_ReturnsSequentialIndices()
    {
        using var list = new ConcurrentAppendOnlyList<int>();

        var i0 = list.Add(10);
        var i1 = list.Add(20);
        var i2 = list.Add(30);

        await Assert.That(i0).IsEqualTo(0);
        await Assert.That(i1).IsEqualTo(1);
        await Assert.That(i2).IsEqualTo(2);
        await Assert.That(list.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Indexer_ValidIndex_ReturnsValue()
    {
        using var list = new ConcurrentAppendOnlyList<int>();
        list.Add(100);
        list.Add(200);
        list.Add(300);

        await Assert.That(list[0]).IsEqualTo(100);
        await Assert.That(list[1]).IsEqualTo(200);
        await Assert.That(list[2]).IsEqualTo(300);
    }

    [Test]
    public async Task Indexer_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        using var list = new ConcurrentAppendOnlyList<int>();
        list.Add(42);

        await Assert.That(() => _ = list[-1]).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Indexer_IndexEqualsCount_ThrowsArgumentOutOfRangeException()
    {
        using var list = new ConcurrentAppendOnlyList<int>();
        list.Add(42);

        await Assert.That(() => _ = list[1]).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Indexer_IndexGreaterThanCount_ThrowsArgumentOutOfRangeException()
    {
        using var list = new ConcurrentAppendOnlyList<int>();
        list.Add(42);

        await Assert.That(() => _ = list[100]).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Add_BeyondInitialCapacity_GrowsAutomatically()
    {
        using var list = new ConcurrentAppendOnlyList<int>(initialCapacity: 4);

        for (int i = 0; i < 100; i++)
        {
            list.Add(i);
        }

        await Assert.That(list.Count).IsEqualTo(100);
        await Assert.That(list.Capacity).IsGreaterThanOrEqualTo(100);

        // Verify all values
        for (int i = 0; i < 100; i++)
        {
            await Assert.That(list[i]).IsEqualTo(i);
        }
    }

    [Test]
    public async Task AsSpan_ReturnsCorrectSnapshot()
    {
        using var list = new ConcurrentAppendOnlyList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);

        // Capture values before await boundary
        var span = list.AsSpan();
        int length = span.Length;
        int v0 = span[0];
        int v1 = span[1];
        int v2 = span[2];

        await Assert.That(length).IsEqualTo(3);
        await Assert.That(v0).IsEqualTo(1);
        await Assert.That(v1).IsEqualTo(2);
        await Assert.That(v2).IsEqualTo(3);
    }

    [Test]
    public async Task AsSpan_EmptyList_ReturnsEmptySpan()
    {
        using var list = new ConcurrentAppendOnlyList<int>();

        var span = list.AsSpan();
        int length = span.Length;

        await Assert.That(length).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_SubsequentAddThrows()
    {
        var list = new ConcurrentAppendOnlyList<int>();
        list.Add(42);
        list.Dispose();

        await Assert.That(() => list.Add(100)).ThrowsExactly<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var list = new ConcurrentAppendOnlyList<int>();
        list.Add(42);

        await Assert.That(() =>
        {
            list.Dispose();
            list.Dispose();
        }).ThrowsNothing();
    }

    [Test]
    public async Task Constructor_CapacityBelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => new ConcurrentAppendOnlyList<int>(3)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Count_AfterAdds_ReturnsCorrectValue()
    {
        using var list = new ConcurrentAppendOnlyList<int>();

        await Assert.That(list.Count).IsEqualTo(0);

        list.Add(1);
        await Assert.That(list.Count).IsEqualTo(1);

        list.Add(2);
        await Assert.That(list.Count).IsEqualTo(2);

        list.Add(3);
        await Assert.That(list.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Capacity_InitialValue_MatchesConstructor()
    {
        using var list = new ConcurrentAppendOnlyList<int>(32);

        await Assert.That(list.Capacity).IsEqualTo(32);
    }

    [Test]
    public async Task Capacity_AfterGrowth_IsAtLeastDoubled()
    {
        using var list = new ConcurrentAppendOnlyList<int>(4);
        int initialCapacity = list.Capacity;

        // Add enough elements to trigger growth
        for (int i = 0; i < 5; i++)
        {
            list.Add(i);
        }

        await Assert.That(list.Capacity).IsGreaterThanOrEqualTo(initialCapacity * 2);
    }

    [Test]
    public async Task Add_LargeStruct_StoresCorrectly()
    {
        using var list = new ConcurrentAppendOnlyList<LargeStruct>(4);

        var value1 = new LargeStruct { A = 1, B = 2, C = 3, D = 4 };
        var value2 = new LargeStruct { A = 10, B = 20, C = 30, D = 40 };

        list.Add(value1);
        list.Add(value2);

        var retrieved1 = list[0];
        var retrieved2 = list[1];

        await Assert.That(retrieved1.A).IsEqualTo(1L);
        await Assert.That(retrieved1.B).IsEqualTo(2L);
        await Assert.That(retrieved1.C).IsEqualTo(3L);
        await Assert.That(retrieved1.D).IsEqualTo(4L);

        await Assert.That(retrieved2.A).IsEqualTo(10L);
        await Assert.That(retrieved2.B).IsEqualTo(20L);
        await Assert.That(retrieved2.C).IsEqualTo(30L);
        await Assert.That(retrieved2.D).IsEqualTo(40L);
    }

    private struct LargeStruct
    {
        public long A;
        public long B;
        public long C;
        public long D;
    }
}
