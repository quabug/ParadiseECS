namespace Paradise.ECS.Test;

/// <summary>
/// Tests for <see cref="ChunkArray{T}"/>.
/// </summary>
public sealed class ChunkArrayTests
{
    private const int BlockByteSize = 16384; // 16KB blocks
    private const int MaxBlocks = 16;

    [Test]
    public async Task Constructor_CreatesWithCorrectProperties()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks);

        await Assert.That(list.MaxBlocks).IsEqualTo(MaxBlocks);
        await Assert.That(list.EntriesPerBlock).IsEqualTo(BlockByteSize / sizeof(int));
        await Assert.That(list.MaxCapacity).IsEqualTo(MaxBlocks * (BlockByteSize / sizeof(int)));
    }

    [Test]
    public async Task Constructor_WithInitialBlocks_PreAllocatesBlocks()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 2);

        await Assert.That(list.IsBlockAllocated(0)).IsTrue();
        await Assert.That(list.IsBlockAllocated(1)).IsTrue();
        await Assert.That(list.IsBlockAllocated(2)).IsFalse();
    }

    [Test]
    public async Task Constructor_ZeroInitialBlocks_NoBlocksAllocated()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 0);

        await Assert.That(list.IsBlockAllocated(0)).IsFalse();
    }

    [Test]
    public async Task GetOrCreateRef_AllocatesBlockLazily()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 0);

        await Assert.That(list.IsBlockAllocated(0)).IsFalse();

        ref var value = ref list.GetOrCreateRef(0);
        value = 42;

        await Assert.That(list.IsBlockAllocated(0)).IsTrue();
    }

    [Test]
    public async Task GetRef_ReturnsCorrectReference()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 1);

        list.GetOrCreateRef(5) = 123;
        ref var value = ref list.GetRef(5);

        await Assert.That(value).IsEqualTo(123);
    }

    [Test]
    public async Task GetRef_ModificationPersists()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 1);

        ref var value1 = ref list.GetRef(10);
        value1 = 999;

        var value2 = list.GetRef(10);

        await Assert.That(value2).IsEqualTo(999);
    }

    [Test]
    public async Task GetValueOrDefault_AllocatedBlock_ReturnsValue()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 1);

        list.SetValue(7, 777);
        var value = list.GetValueOrDefault(7);

        await Assert.That(value).IsEqualTo(777);
    }

    [Test]
    public async Task GetValueOrDefault_UnallocatedBlock_ReturnsDefault()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 0);

        var value = list.GetValueOrDefault(0);

        await Assert.That(value).IsEqualTo(default(int));
    }

    [Test]
    public async Task SetValue_AllocatesBlockIfNeeded()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 0);

        await Assert.That(list.IsBlockAllocated(0)).IsFalse();

        list.SetValue(0, 42);

        await Assert.That(list.IsBlockAllocated(0)).IsTrue();
        await Assert.That(list.GetValueOrDefault(0)).IsEqualTo(42);
    }

    [Test]
    public async Task EnsureCapacity_AllocatesBlockForIndex()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 0);
        int entriesPerBlock = BlockByteSize / sizeof(int);

        // Index in second block
        int indexInSecondBlock = entriesPerBlock + 100;

        await Assert.That(list.IsBlockAllocated(1)).IsFalse();

        list.EnsureCapacity(indexInSecondBlock);

        await Assert.That(list.IsBlockAllocated(1)).IsTrue();
    }

    [Test]
    public async Task EnsureBlockAllocated_AllocatesSpecificBlock()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 0);

        await Assert.That(list.IsBlockAllocated(5)).IsFalse();

        list.EnsureBlockAllocated(5);

        await Assert.That(list.IsBlockAllocated(5)).IsTrue();
    }

    [Test]
    public async Task MultipleBlocks_IndependentStorage()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 0);
        int entriesPerBlock = BlockByteSize / sizeof(int);

        // Write to different blocks
        list.SetValue(0, 100);                    // Block 0
        list.SetValue(entriesPerBlock, 200);      // Block 1
        list.SetValue(entriesPerBlock * 2, 300);  // Block 2

        var v0 = list.GetValueOrDefault(0);
        var v1 = list.GetValueOrDefault(entriesPerBlock);
        var v2 = list.GetValueOrDefault(entriesPerBlock * 2);

        await Assert.That(v0).IsEqualTo(100);
        await Assert.That(v1).IsEqualTo(200);
        await Assert.That(v2).IsEqualTo(300);
    }

    [Test]
    public async Task IndexCalculation_CorrectForBlockBoundaries()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 0);
        int entriesPerBlock = BlockByteSize / sizeof(int);

        // Write to last entry of first block and first entry of second block
        int lastIndexInBlock0 = entriesPerBlock - 1;
        int firstIndexInBlock1 = entriesPerBlock;

        list.SetValue(lastIndexInBlock0, 111);
        list.SetValue(firstIndexInBlock1, 222);

        var v0 = list.GetValueOrDefault(lastIndexInBlock0);
        var v1 = list.GetValueOrDefault(firstIndexInBlock1);

        await Assert.That(v0).IsEqualTo(111);
        await Assert.That(v1).IsEqualTo(222);
    }

    [Test]
    public async Task Clear_ZeroesAllAllocatedBlocks()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 2);

        list.SetValue(0, 100);
        list.SetValue(list.EntriesPerBlock, 200);

        list.Clear();

        var v0 = list.GetValueOrDefault(0);
        var v1 = list.GetValueOrDefault(list.EntriesPerBlock);

        await Assert.That(v0).IsEqualTo(0);
        await Assert.That(v1).IsEqualTo(0);
    }

    [Test]
    public async Task Clear_DoesNotDeallocateBlocks()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 2);

        list.Clear();

        await Assert.That(list.IsBlockAllocated(0)).IsTrue();
        await Assert.That(list.IsBlockAllocated(1)).IsTrue();
    }

    [Test]
    public async Task Dispose_MultipleCallsSafe()
    {
        var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 1);

        list.Dispose();
        list.Dispose(); // Should not throw

        await Assert.That(list).IsNotNull();
    }

    [Test]
    public async Task StructType_WorksCorrectly()
    {
        using var list = new ChunkArray<TestStruct>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 1);

        ref var data = ref list.GetOrCreateRef(0);
        data.X = 1.5f;
        data.Y = 2.5f;
        data.Value = 42;

        var result = list.GetValueOrDefault(0);

        await Assert.That(result.X).IsEqualTo(1.5f);
        await Assert.That(result.Y).IsEqualTo(2.5f);
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task LargeIndex_WorksWithMultipleBlocks()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 0);
        int entriesPerBlock = BlockByteSize / sizeof(int);

        // Access an index in the last possible block
        int largeIndex = (MaxBlocks - 1) * entriesPerBlock + 100;

        list.SetValue(largeIndex, 12345);
        var value = list.GetValueOrDefault(largeIndex);

        await Assert.That(value).IsEqualTo(12345);
        await Assert.That(list.IsBlockAllocated(MaxBlocks - 1)).IsTrue();
    }

    [Test]
    public async Task EntriesPerBlock_CalculatedCorrectlyForDifferentTypes()
    {
        using var intList = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks);
        using var longList = new ChunkArray<long>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks);
        using var structList = new ChunkArray<TestStruct>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks);

        await Assert.That(intList.EntriesPerBlock).IsEqualTo(BlockByteSize / sizeof(int));
        await Assert.That(longList.EntriesPerBlock).IsEqualTo(BlockByteSize / sizeof(long));
        await Assert.That(structList.EntriesPerBlock).IsEqualTo(BlockByteSize / 12); // TestStruct is 12 bytes
    }

    [Test]
    public async Task SequentialWrites_AllPersist()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 1);
        const int count = 1000;

        for (int i = 0; i < count; i++)
        {
            list.SetValue(i, i * 10);
        }

        bool[] allCorrect = new bool[count];
        for (int i = 0; i < count; i++)
        {
            allCorrect[i] = list.GetValueOrDefault(i) == i * 10;
        }

        foreach (var correct in allCorrect)
        {
            await Assert.That(correct).IsTrue();
        }
    }

    [Test]
    public async Task RandomAccessPattern_WorksCorrectly()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 0);

        int[] indices = [0, 1000, 500, 2000, 100, 1500];
        int[] values = [10, 20, 30, 40, 50, 60];

        for (int i = 0; i < indices.Length; i++)
        {
            list.SetValue(indices[i], values[i]);
        }

        for (int i = 0; i < indices.Length; i++)
        {
            var value = list.GetValueOrDefault(indices[i]);
            await Assert.That(value).IsEqualTo(values[i]);
        }
    }

    [Test]
    public async Task GetOrCreateRef_SameIndex_ReturnsSameLocation()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks, initialBlocks: 1);

        ref var ref1 = ref list.GetOrCreateRef(42);
        ref1 = 100;

        // Capture value before await (ref2 cannot cross await boundary)
        var value1 = list.GetOrCreateRef(42);
        await Assert.That(value1).IsEqualTo(100);

        // Write via new ref and verify
        list.GetOrCreateRef(42) = 200;
        var value2 = list.GetValueOrDefault(42);

        await Assert.That(value2).IsEqualTo(200);
    }

    [Test]
    public async Task MaxCapacity_CalculatedCorrectly()
    {
        using var list = new ChunkArray<long>(NativeMemoryAllocator.Shared, BlockByteSize, MaxBlocks);

        int expectedEntriesPerBlock = BlockByteSize / sizeof(long);
        int expectedMaxCapacity = MaxBlocks * expectedEntriesPerBlock;

        await Assert.That(list.MaxCapacity).IsEqualTo(expectedMaxCapacity);
    }

    [Test]
    public async Task ZeroSizedInitialBlocks_LimitedToMaxBlocks()
    {
        using var list = new ChunkArray<int>(NativeMemoryAllocator.Shared, BlockByteSize, maxBlocks: 3, initialBlocks: 10);

        // Should only allocate up to MaxBlocks
        await Assert.That(list.IsBlockAllocated(0)).IsTrue();
        await Assert.That(list.IsBlockAllocated(1)).IsTrue();
        await Assert.That(list.IsBlockAllocated(2)).IsTrue();
    }

    private struct TestStruct
    {
        public float X;
        public float Y;
        public int Value;
    }
}
