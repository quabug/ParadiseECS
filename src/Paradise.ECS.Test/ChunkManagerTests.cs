namespace Paradise.ECS.Test;

/// <summary>
/// Tests for <see cref="ChunkManager"/> and <see cref="Chunk"/>.
/// </summary>
public sealed class ChunkManagerTests
{
    [Test]
    public async Task Create_WithDefaultCapacity_DisposeSucceeds()
    {
        var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        manager.Dispose();

        await Assert.That(manager).IsNotNull();
    }

    [Test]
    public async Task Create_WithCustomCapacity_DisposeSucceeds()
    {
        var manager = new ChunkManager(NativeMemoryAllocator.Shared, initialCapacity: 16);
        manager.Dispose();

        await Assert.That(manager).IsNotNull();
    }

    [Test]
    public async Task Allocate_ReturnsValidHandle()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);

        var handle = manager.Allocate();

        await Assert.That(handle.IsValid).IsTrue();
    }

    [Test]
    public async Task Allocate_MultipleHandles_ReturnsDistinctIds()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);

        var handle1 = manager.Allocate();
        var handle2 = manager.Allocate();
        var handle3 = manager.Allocate();

        await Assert.That(handle1.Id).IsNotEqualTo(handle2.Id);
        await Assert.That(handle2.Id).IsNotEqualTo(handle3.Id);
        await Assert.That(handle1.Id).IsNotEqualTo(handle3.Id);
    }

    [Test]
    public async Task Get_ValidHandle_ReturnsValidChunk()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        bool isValid;
        {
            using var chunk = manager.Get(handle);
            isValid = chunk.IsValid;
        }

        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task Get_InvalidHandle_ReturnsInvalidChunk()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);

        bool isValid;
        {
            using var chunk = manager.Get(ChunkHandle.Invalid);
            isValid = chunk.IsValid;
        }

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task Get_StaleHandle_ReturnsInvalidChunk()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();
        manager.Free(handle);

        bool isValid;
        {
            using var chunk = manager.Get(handle);
            isValid = chunk.IsValid;
        }

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task Free_ValidHandle_InvalidatesHandle()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        manager.Free(handle);

        bool isValid;
        {
            using var chunk = manager.Get(handle);
            isValid = chunk.IsValid;
        }
        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task Free_InvalidHandle_DoesNotThrow()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);

        manager.Free(ChunkHandle.Invalid);

        await Assert.That(manager).IsNotNull();
    }

    [Test]
    public async Task Free_StaleHandle_DoesNotThrow()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();
        manager.Free(handle);

        manager.Free(handle); // Double free should be safe

        await Assert.That(manager).IsNotNull();
    }

    [Test]
    public async Task Free_WhileBorrowed_ThrowsInvalidOperationException()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        Exception? caught = null;
        {
            using var chunk = manager.Get(handle);
            try
            {
                manager.Free(handle);
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        }

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task Free_AfterBorrowReleased_Succeeds()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        {
            using var chunk = manager.Get(handle);
            // Borrow is released when chunk is disposed
        }

        manager.Free(handle);

        bool isValid;
        {
            using var chunk = manager.Get(handle);
            isValid = chunk.IsValid;
        }
        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task Allocate_AfterFree_ReusesSlot()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle1 = manager.Allocate();
        var id1 = handle1.Id;
        manager.Free(handle1);

        var handle2 = manager.Allocate();

        await Assert.That(handle2.Id).IsEqualTo(id1);
        await Assert.That(handle2.Version).IsGreaterThan(handle1.Version);
    }

    [Test]
    public async Task Chunk_GetSpan_ReturnsWritableMemory()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        int[] captured;
        {
            using var chunk = manager.Get(handle);
            var span = chunk.GetSpan<int>(0, 10);
            for (int i = 0; i < 10; i++)
            {
                span[i] = i * 100;
            }
            captured = span.ToArray();
        }

        await Assert.That(captured[0]).IsEqualTo(0);
        await Assert.That(captured[5]).IsEqualTo(500);
        await Assert.That(captured[9]).IsEqualTo(900);
    }

    [Test]
    public async Task Chunk_GetSpan_PersistsAcrossGet()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        // Write data
        {
            using var chunk = manager.Get(handle);
            var span = chunk.GetSpan<int>(0, 10);
            for (int i = 0; i < 10; i++)
            {
                span[i] = i * 100;
            }
        }

        // Read data back
        int[] captured;
        {
            using var chunk = manager.Get(handle);
            var span = chunk.GetSpan<int>(0, 10);
            captured = span.ToArray();
        }

        await Assert.That(captured[0]).IsEqualTo(0);
        await Assert.That(captured[5]).IsEqualTo(500);
        await Assert.That(captured[9]).IsEqualTo(900);
    }

    [Test]
    public async Task Chunk_GetRef_ReturnsWritableReference()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        int value;
        {
            using var chunk = manager.Get(handle);
            ref int refValue = ref chunk.GetRef<int>(0);
            refValue = 42;
            value = chunk.GetRef<int>(0);
        }

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Chunk_GetDataBytes_ReturnsFullChunkSize()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        int length;
        {
            using var chunk = manager.Get(handle);
            var bytes = chunk.GetDataBytes();
            length = bytes.Length;
        }

        await Assert.That(length).IsEqualTo(Chunk.ChunkSize);
    }

    [Test]
    public async Task Chunk_GetDataBytes_WithSize_ReturnsSpecifiedSize()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        int length;
        {
            using var chunk = manager.Get(handle);
            var bytes = chunk.GetDataBytes(100);
            length = bytes.Length;
        }

        await Assert.That(length).IsEqualTo(100);
    }

    [Test]
    public async Task Chunk_GetRawBytes_ReturnsChunkSize()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        int length;
        {
            using var chunk = manager.Get(handle);
            var bytes = chunk.GetRawBytes();
            length = bytes.Length;
        }

        await Assert.That(length).IsEqualTo(Chunk.ChunkSize);
    }

    [Test]
    public async Task Chunk_GetBytesAt_ReturnsCorrectSlice()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        int length;
        {
            using var chunk = manager.Get(handle);
            var bytes = chunk.GetBytesAt(100, 50);
            length = bytes.Length;
        }

        await Assert.That(length).IsEqualTo(50);
    }

    [Test]
    public async Task MultipleBorrows_AllValid()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        bool valid1, valid2, valid3;
        {
            using var chunk1 = manager.Get(handle);
            using var chunk2 = manager.Get(handle);
            using var chunk3 = manager.Get(handle);
            valid1 = chunk1.IsValid;
            valid2 = chunk2.IsValid;
            valid3 = chunk3.IsValid;
        }

        await Assert.That(valid1).IsTrue();
        await Assert.That(valid2).IsTrue();
        await Assert.That(valid3).IsTrue();
    }

    [Test]
    public async Task ManyAllocations_AllValid()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handles = new ChunkHandle[100];

        for (int i = 0; i < 100; i++)
        {
            handles[i] = manager.Allocate();
        }

        bool[] validities = new bool[100];
        for (int i = 0; i < 100; i++)
        {
            using var chunk = manager.Get(handles[i]);
            validities[i] = chunk.IsValid;
        }

        foreach (var valid in validities)
        {
            await Assert.That(valid).IsTrue();
        }
    }

    [Test]
    public async Task Dispose_InvalidatesFurtherAllocations()
    {
        var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        manager.Dispose();

        var caught = false;
        try
        {
            manager.Allocate();
        }
        catch (ObjectDisposedException)
        {
            caught = true;
        }

        await Assert.That(caught).IsTrue();
    }

    [Test]
    public async Task Dispose_InvalidatesFurtherGet()
    {
        var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();
        manager.Dispose();

        var caught = false;
        try
        {
            using var chunk = manager.Get(handle);
        }
        catch (ObjectDisposedException)
        {
            caught = true;
        }

        await Assert.That(caught).IsTrue();
    }

    [Test]
    public async Task Default_Chunk_IsInvalid()
    {
        // Note: default Chunk has null ChunkManager, so Dispose() is a safe no-op
        bool isValid;
        {
            var chunk = default(Chunk);
            isValid = chunk.IsValid;
            chunk.Dispose();
        }

        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task StructTypes_WorkCorrectly()
    {
        using var manager = new ChunkManager(NativeMemoryAllocator.Shared);
        var handle = manager.Allocate();

        TestStruct captured;
        {
            using var chunk = manager.Get(handle);
            ref var data = ref chunk.GetRef<TestStruct>(0);
            data.X = 1.5f;
            data.Y = 2.5f;
            data.Value = 42;
            captured = chunk.GetRef<TestStruct>(0);
        }

        await Assert.That(captured.X).IsEqualTo(1.5f);
        await Assert.That(captured.Y).IsEqualTo(2.5f);
        await Assert.That(captured.Value).IsEqualTo(42);
    }

    private struct TestStruct
    {
        public float X;
        public float Y;
        public int Value;
    }
}
