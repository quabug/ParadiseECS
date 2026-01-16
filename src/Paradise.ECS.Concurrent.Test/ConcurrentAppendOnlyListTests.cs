namespace Paradise.ECS.Concurrent.Test;

public sealed class ConcurrentAppendOnlyListTests
{
    [Test]
    public async Task Add_SingleElement_ReturnsCorrectIndex()
    {
        var list = new ConcurrentAppendOnlyList<int>();

        var index = list.Add(42);

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(list.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo(42);
    }

    [Test]
    public async Task Add_MultipleElements_ReturnsSequentialIndices()
    {
        var list = new ConcurrentAppendOnlyList<int>();

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
        var list = new ConcurrentAppendOnlyList<int>();
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
        var list = new ConcurrentAppendOnlyList<int>();
        list.Add(42);

        await Assert.That(() => _ = list[-1]).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Indexer_IndexEqualsCount_ThrowsArgumentOutOfRangeException()
    {
        var list = new ConcurrentAppendOnlyList<int>();
        list.Add(42);

        await Assert.That(() => _ = list[1]).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Indexer_IndexGreaterThanCount_ThrowsArgumentOutOfRangeException()
    {
        var list = new ConcurrentAppendOnlyList<int>();
        list.Add(42);

        await Assert.That(() => _ = list[100]).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Add_BeyondInitialChunk_GrowsAutomatically()
    {
        // chunkShift=2 means chunk size of 4
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2);

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
    public async Task Constructor_ChunkShiftBelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => new ConcurrentAppendOnlyList<int>(1)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Constructor_ChunkShiftAboveMaximum_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => new ConcurrentAppendOnlyList<int>(21)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Count_AfterAdds_ReturnsCorrectValue()
    {
        var list = new ConcurrentAppendOnlyList<int>();

        await Assert.That(list.Count).IsEqualTo(0);

        list.Add(1);
        await Assert.That(list.Count).IsEqualTo(1);

        list.Add(2);
        await Assert.That(list.Count).IsEqualTo(2);

        list.Add(3);
        await Assert.That(list.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Capacity_InitialValue_IsZero()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 4);

        // No chunks allocated yet
        await Assert.That(list.Capacity).IsEqualTo(0);
    }

    [Test]
    public async Task Capacity_AfterAdd_MatchesChunkSize()
    {
        // chunkShift=4 means chunk size of 16
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 4);

        list.Add(1);

        // One chunk allocated
        await Assert.That(list.Capacity).IsEqualTo(16);
    }

    [Test]
    public async Task ChunkCount_AfterGrowth_IncrementsCorrectly()
    {
        // chunkShift=2 means chunk size of 4
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2);

        await Assert.That(list.ChunkCount).IsEqualTo(0);

        // Add 4 elements - should use 1 chunk
        for (int i = 0; i < 4; i++)
        {
            list.Add(i);
        }
        await Assert.That(list.ChunkCount).IsEqualTo(1);

        // Add 1 more - should allocate second chunk
        list.Add(4);
        await Assert.That(list.ChunkCount).IsEqualTo(2);
    }

    [Test]
    public async Task Add_LargeStruct_StoresCorrectly()
    {
        var list = new ConcurrentAppendOnlyList<LargeStruct>(chunkShift: 2);

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

    [Test]
    public async Task Add_ReferenceType_StoresCorrectly()
    {
        var list = new ConcurrentAppendOnlyList<string>();

        list.Add("hello");
        list.Add("world");

        await Assert.That(list[0]).IsEqualTo("hello");
        await Assert.That(list[1]).IsEqualTo("world");
    }

    [Test]
    public async Task Add_NullReference_StoresCorrectly()
    {
        var list = new ConcurrentAppendOnlyList<string?>();

        list.Add(null);
        list.Add("not null");
        list.Add(null);

        await Assert.That(list[0]).IsNull();
        await Assert.That(list[1]).IsEqualTo("not null");
        await Assert.That(list[2]).IsNull();
    }

    private struct LargeStruct
    {
        public long A;
        public long B;
        public long C;
        public long D;
    }

    // AddRange tests

    [Test]
    public async Task AddRange_EmptySpan_ReturnsCurrentCount()
    {
        var list = new ConcurrentAppendOnlyList<int>();
        list.Add(42);

        var index = list.AddRange(ReadOnlySpan<int>.Empty);

        await Assert.That(index).IsEqualTo(1); // Returns committed count
        await Assert.That(list.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AddRange_SingleElement_ReturnsCorrectIndex()
    {
        var list = new ConcurrentAppendOnlyList<int>();

        var index = list.AddRange([42]);

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(list.Count).IsEqualTo(1);
        await Assert.That(list[0]).IsEqualTo(42);
    }

    [Test]
    public async Task AddRange_MultipleElements_ReturnsStartIndex()
    {
        var list = new ConcurrentAppendOnlyList<int>();

        var index = list.AddRange([10, 20, 30, 40, 50]);

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(list.Count).IsEqualTo(5);
        await Assert.That(list[0]).IsEqualTo(10);
        await Assert.That(list[1]).IsEqualTo(20);
        await Assert.That(list[2]).IsEqualTo(30);
        await Assert.That(list[3]).IsEqualTo(40);
        await Assert.That(list[4]).IsEqualTo(50);
    }

    [Test]
    public async Task AddRange_AfterAdd_ReturnsCorrectStartIndex()
    {
        var list = new ConcurrentAppendOnlyList<int>();
        list.Add(1);
        list.Add(2);

        var index = list.AddRange([10, 20, 30]);

        await Assert.That(index).IsEqualTo(2);
        await Assert.That(list.Count).IsEqualTo(5);
        await Assert.That(list[0]).IsEqualTo(1);
        await Assert.That(list[1]).IsEqualTo(2);
        await Assert.That(list[2]).IsEqualTo(10);
        await Assert.That(list[3]).IsEqualTo(20);
        await Assert.That(list[4]).IsEqualTo(30);
    }

    [Test]
    public async Task AddRange_SpansMultipleChunks_StoresAllValues()
    {
        // chunkShift=2 means chunk size of 4
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2);

        // Add 10 elements spanning 3 chunks
        int[] values = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
        var index = list.AddRange(values);

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(list.Count).IsEqualTo(10);
        await Assert.That(list.ChunkCount).IsEqualTo(3); // 4 + 4 + 2

        for (int i = 0; i < 10; i++)
        {
            await Assert.That(list[i]).IsEqualTo(i);
        }
    }

    [Test]
    public async Task AddRange_LargeRange_GrowsAutomatically()
    {
        // chunkShift=2 means chunk size of 4
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2);

        int[] values = new int[100];
        for (int i = 0; i < 100; i++)
            values[i] = i * 10;

        var index = list.AddRange(values);

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(list.Count).IsEqualTo(100);
        await Assert.That(list.ChunkCount).IsEqualTo(25); // 100 / 4

        for (int i = 0; i < 100; i++)
        {
            await Assert.That(list[i]).IsEqualTo(i * 10);
        }
    }

    [Test]
    public async Task AddRange_CrossesBitmapWordBoundary_MarksAllSlotsReady()
    {
        // Test marking slots that cross the 64-bit word boundary
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 4); // 16 elements per chunk

        // First add 60 elements to get close to word boundary (64)
        int[] first = new int[60];
        for (int i = 0; i < 60; i++)
            first[i] = i;
        list.AddRange(first);

        // Add 10 more elements crossing the boundary (60-69)
        int[] crossing = [60, 61, 62, 63, 64, 65, 66, 67, 68, 69];
        var index = list.AddRange(crossing);

        await Assert.That(index).IsEqualTo(60);
        await Assert.That(list.Count).IsEqualTo(70);

        // Verify all values including those crossing the boundary
        for (int i = 0; i < 70; i++)
        {
            await Assert.That(list[i]).IsEqualTo(i);
        }
    }

    [Test]
    public async Task AddRange_EntireWordRange_MarksAllSlotsReady()
    {
        // Test marking exactly 64 slots (one full bitmap word)
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 6); // 64 elements per chunk

        int[] values = new int[64];
        for (int i = 0; i < 64; i++)
            values[i] = i * 2;

        var index = list.AddRange(values);

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(list.Count).IsEqualTo(64);

        for (int i = 0; i < 64; i++)
        {
            await Assert.That(list[i]).IsEqualTo(i * 2);
        }
    }

    [Test]
    public async Task AddRange_MultipleWordRange_MarksAllSlotsReady()
    {
        // Test marking 200 slots (spanning ~3 bitmap words)
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 6); // 64 elements per chunk

        int[] values = new int[200];
        for (int i = 0; i < 200; i++)
            values[i] = i;

        var index = list.AddRange(values);

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(list.Count).IsEqualTo(200);

        for (int i = 0; i < 200; i++)
        {
            await Assert.That(list[i]).IsEqualTo(i);
        }
    }

    [Test]
    public async Task AddRange_MixedWithAdd_MaintainsCorrectOrder()
    {
        var list = new ConcurrentAppendOnlyList<int>();

        list.Add(1);
        list.AddRange([2, 3, 4]);
        list.Add(5);
        list.AddRange([6, 7]);
        list.Add(8);

        await Assert.That(list.Count).IsEqualTo(8);
        for (int i = 0; i < 8; i++)
        {
            await Assert.That(list[i]).IsEqualTo(i + 1);
        }
    }

    [Test]
    public async Task AddRange_WithReferenceTypes_StoresCorrectly()
    {
        var list = new ConcurrentAppendOnlyList<string>();

        var index = list.AddRange(["hello", "world", "foo", "bar"]);

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(list.Count).IsEqualTo(4);
        await Assert.That(list[0]).IsEqualTo("hello");
        await Assert.That(list[1]).IsEqualTo("world");
        await Assert.That(list[2]).IsEqualTo("foo");
        await Assert.That(list[3]).IsEqualTo("bar");
    }

    [Test]
    public async Task AddRange_WithNullReferences_StoresCorrectly()
    {
        var list = new ConcurrentAppendOnlyList<string?>();

        var index = list.AddRange([null, "not null", null]);

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(list.Count).IsEqualTo(3);
        await Assert.That(list[0]).IsNull();
        await Assert.That(list[1]).IsEqualTo("not null");
        await Assert.That(list[2]).IsNull();
    }

    [Test]
    public async Task AddRange_WithLargeStruct_StoresCorrectly()
    {
        var list = new ConcurrentAppendOnlyList<LargeStruct>(chunkShift: 2);

        LargeStruct[] values =
        [
            new() { A = 1, B = 2, C = 3, D = 4 },
            new() { A = 10, B = 20, C = 30, D = 40 },
            new() { A = 100, B = 200, C = 300, D = 400 }
        ];

        var index = list.AddRange(values);

        await Assert.That(index).IsEqualTo(0);
        await Assert.That(list.Count).IsEqualTo(3);

        await Assert.That(list[0].A).IsEqualTo(1L);
        await Assert.That(list[1].A).IsEqualTo(10L);
        await Assert.That(list[2].A).IsEqualTo(100L);
    }
}
