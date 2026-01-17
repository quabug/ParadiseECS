namespace Paradise.ECS.Test;

/// <summary>
/// Tests for <see cref="AppendOnlyList{T}"/>.
/// </summary>
public sealed class AppendOnlyListTests
{
    [Test]
    public async Task NewList_HasZeroCount()
    {
        var list = new AppendOnlyList<int>();

        await Assert.That(list.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Add_IncreasesCount()
    {
        var list = new AppendOnlyList<int>();

        list.Add(42);

        await Assert.That(list.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Add_ReturnsIndex()
    {
        var list = new AppendOnlyList<int>();

        var index0 = list.Add(10);
        var index1 = list.Add(20);
        var index2 = list.Add(30);

        await Assert.That(index0).IsEqualTo(0);
        await Assert.That(index1).IsEqualTo(1);
        await Assert.That(index2).IsEqualTo(2);
    }

    [Test]
    public async Task Indexer_ReturnsCorrectValue()
    {
        var list = new AppendOnlyList<int>();
        list.Add(10);
        list.Add(20);
        list.Add(30);

        await Assert.That(list[0]).IsEqualTo(10);
        await Assert.That(list[1]).IsEqualTo(20);
        await Assert.That(list[2]).IsEqualTo(30);
    }

    [Test]
    public async Task Indexer_ThrowsForNegativeIndex()
    {
        var list = new AppendOnlyList<int>();
        list.Add(42);

        await Assert.That(() => _ = list[-1]).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Indexer_ThrowsForIndexAtCount()
    {
        var list = new AppendOnlyList<int>();
        list.Add(42);

        await Assert.That(() => _ = list[1]).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task GetRef_ReturnsReference()
    {
        var list = new AppendOnlyList<int>();
        list.Add(10);

        ref int value = ref list.GetRef(0);
        value = 99;

        await Assert.That(list[0]).IsEqualTo(99);
    }

    [Test]
    public async Task GetRef_ThrowsForInvalidIndex()
    {
        var list = new AppendOnlyList<int>();

        await Assert.That(() => list.GetRef(0)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AddRange_AddsMultipleElements()
    {
        var list = new AppendOnlyList<int>();

        ReadOnlySpan<int> values = [1, 2, 3, 4, 5];
        var startIndex = list.AddRange(values);

        await Assert.That(startIndex).IsEqualTo(0);
        await Assert.That(list.Count).IsEqualTo(5);
        await Assert.That(list[0]).IsEqualTo(1);
        await Assert.That(list[4]).IsEqualTo(5);
    }

    [Test]
    public async Task AddRange_ReturnsStartIndex()
    {
        var list = new AppendOnlyList<int>();
        list.Add(0);

        ReadOnlySpan<int> values = [1, 2, 3];
        var startIndex = list.AddRange(values);

        await Assert.That(startIndex).IsEqualTo(1);
    }

    [Test]
    public async Task AddRange_EmptySpan_ReturnsCurrentCount()
    {
        var list = new AppendOnlyList<int>();
        list.Add(42);

        var startIndex = list.AddRange(ReadOnlySpan<int>.Empty);

        await Assert.That(startIndex).IsEqualTo(1);
        await Assert.That(list.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ChunkCount_IncreasesAsNeeded()
    {
        // Use small chunk size (shift=2 means 4 elements per chunk)
        var list = new AppendOnlyList<int>(2);

        await Assert.That(list.ChunkCount).IsEqualTo(0);

        list.Add(1);
        await Assert.That(list.ChunkCount).IsEqualTo(1);

        list.Add(2);
        list.Add(3);
        list.Add(4);
        await Assert.That(list.ChunkCount).IsEqualTo(1);

        list.Add(5);
        await Assert.That(list.ChunkCount).IsEqualTo(2);
    }

    [Test]
    public async Task Capacity_ReflectsAllocatedChunks()
    {
        // Use small chunk size (shift=2 means 4 elements per chunk)
        var list = new AppendOnlyList<int>(2);

        list.Add(1);
        await Assert.That(list.Capacity).IsEqualTo(4);

        list.Add(2);
        list.Add(3);
        list.Add(4);
        list.Add(5);
        await Assert.That(list.Capacity).IsEqualTo(8);
    }

    [Test]
    public async Task Enumerator_IteratesAllElements()
    {
        var list = new AppendOnlyList<int>();
        for (int i = 0; i < 10; i++)
        {
            list.Add(i * 10);
        }

        var collected = new List<int>();
        foreach (var item in list)
        {
            collected.Add(item);
        }

        await Assert.That(collected.Count).IsEqualTo(10);
        await Assert.That(collected[0]).IsEqualTo(0);
        await Assert.That(collected[9]).IsEqualTo(90);
    }

    [Test]
    public async Task IEnumerable_IteratesAllElements()
    {
        var list = new AppendOnlyList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);

#pragma warning disable CA1859
        IReadOnlyList<int> readOnlyList = list;
#pragma warning restore CA1859

        await Assert.That(readOnlyList.Count).IsEqualTo(3);
        await Assert.That(readOnlyList[0]).IsEqualTo(1);
        await Assert.That(readOnlyList[2]).IsEqualTo(3);
    }

    [Test]
    public async Task Constructor_ThrowsForInvalidChunkShift()
    {
        await Assert.That(() => new AppendOnlyList<int>(1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new AppendOnlyList<int>(21)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Constructor_AcceptsValidChunkShift()
    {
        var list = new AppendOnlyList<int>(10);

        list.Add(42);

        await Assert.That(list[0]).IsEqualTo(42);
    }

    [Test]
    public async Task LargeDataSet_WorksCorrectly()
    {
        var list = new AppendOnlyList<int>();

        for (int i = 0; i < 10000; i++)
        {
            list.Add(i);
        }

        await Assert.That(list.Count).IsEqualTo(10000);
        await Assert.That(list[0]).IsEqualTo(0);
        await Assert.That(list[5000]).IsEqualTo(5000);
        await Assert.That(list[9999]).IsEqualTo(9999);
    }

    [Test]
    public async Task ReferenceType_WorksCorrectly()
    {
        var list = new AppendOnlyList<string>();

        list.Add("hello");
        list.Add("world");

        await Assert.That(list[0]).IsEqualTo("hello");
        await Assert.That(list[1]).IsEqualTo("world");
    }
}
