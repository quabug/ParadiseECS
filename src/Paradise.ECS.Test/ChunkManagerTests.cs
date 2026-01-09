namespace Paradise.ECS.Test;

public class ChunkManagerTests : IDisposable
{
    private readonly ChunkManager _manager;

    public ChunkManagerTests()
    {
        _manager = new ChunkManager(initialCapacity: 16);
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }

    [Test]
    public async Task Allocate_ReturnsValidHandle()
    {
        var handle = _manager.Allocate();
        await Assert.That(handle.IsValid).IsTrue();
        await Assert.That(handle.Id).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Allocate_ReuseSlotAfterFree()
    {
        var handle1 = _manager.Allocate();
        int id1 = handle1.Id;

        _manager.Free(handle1);

        var handle2 = _manager.Allocate();

        // Should reuse the slot
        await Assert.That(handle2.Id).IsEqualTo(id1);

        // But version should be different
        await Assert.That(handle2.Version).IsNotEqualTo(handle1.Version);
    }

    [Test]
    public async Task Free_InvalidatesHandle()
    {
        var handle = _manager.Allocate();

        // Should work before free
        {
            using var chunk = _manager.Get(handle); // Borrow and release
        }
        await Assert.That(handle.IsValid).IsTrue();

        _manager.Free(handle);

        // After free, reallocating should give a new version for the same slot
        var handle2 = _manager.Allocate();
        await Assert.That(handle2.Id).IsEqualTo(handle.Id);
        await Assert.That(handle2.Version).IsNotEqualTo(handle.Version);

        _manager.Free(handle2);
    }

    [Test]
    public async Task Allocate_ReusesMemoryAfterFree()
    {
        var handle1 = _manager.Allocate();
        {
            using var chunk1 = _manager.Get(handle1);
            // Write some data
            chunk1.GetSpan<int>(0, 10)[0] = 12345;
        }

        _manager.Free(handle1);

        // Reallocate - should reuse the same slot and memory
        var handle2 = _manager.Allocate();
        int value;
        {
            using var chunk2 = _manager.Get(handle2);
            // Memory should be cleared (zeroed)
            value = chunk2.GetSpan<int>(0, 10)[0];
        }
        await Assert.That(value).IsEqualTo(0);
    }
}

public class ChunkManagerExceptionTests : IDisposable
{
    private readonly ChunkManager _manager;

    public ChunkManagerExceptionTests()
    {
        _manager = new ChunkManager(initialCapacity: 16);
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }

    [Test]
    public async Task Get_WithInvalidHandleId_ReturnsDefaultChunk()
    {
        var invalidHandle = new ChunkHandle(99999, 0); // Id way out of range

        // Should not throw - returns default chunk
        bool noException = true;
        try
        {
            using var chunk = _manager.Get(invalidHandle);
            // Default chunk's Dispose() is safe (checks for null manager)
        }
        catch
        {
            noException = false;
        }

        await Assert.That(noException).IsTrue();
    }

    [Test]
    public async Task Get_WithStaleHandle_ReturnsDefaultChunk()
    {
        var handle = _manager.Allocate();
        _manager.Free(handle);

        // Should not throw - returns default chunk
        bool noException = true;
        try
        {
            using var chunk = _manager.Get(handle);
            // Default chunk's Dispose() is safe (checks for null manager)
        }
        catch
        {
            noException = false;
        }

        await Assert.That(noException).IsTrue();
    }

    [Test]
    public async Task Allocate_AfterDispose_ReturnsInvalidHandle()
    {
        var manager = new ChunkManager(initialCapacity: 4);
        manager.Dispose();

        var handle = manager.Allocate();
        await Assert.That(handle.IsValid).IsFalse();
    }

    [Test]
    public async Task Free_WhileBorrowed_ThrowsInvalidOperationException()
    {
        var handle = _manager.Allocate();
        // Note: Cannot use fluent assertion here because Chunk is a ref struct
        // that cannot be preserved across await boundaries
        bool threw = false;
        try
        {
            using var chunk = _manager.Get(handle);
            _manager.Free(handle);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task Free_WithInvalidHandle_DoesNotThrow()
    {
        // Free with invalid handle should be a no-op
        await Assert.That(() => _manager.Free(ChunkHandle.Invalid)).ThrowsNothing();
    }

    [Test]
    public async Task Free_WithOutOfRangeId_DoesNotThrow()
    {
        var outOfRangeHandle = new ChunkHandle(99999, 0);
        await Assert.That(() => _manager.Free(outOfRangeHandle)).ThrowsNothing();
    }

    [Test]
    public async Task Free_SameHandleTwice_SecondIsNoOp()
    {
        var handle = _manager.Allocate();
        _manager.Free(handle);

        // Second free should be no-op (version mismatch)
        await Assert.That(() => _manager.Free(handle)).ThrowsNothing();
    }

    [Test]
    public async Task Get_WithNegativeId_ReturnsDefaultChunk()
    {
        var negativeHandle = new ChunkHandle(-1, 0);

        // Should not throw - returns default chunk
        // Note: ChunkHandle.Invalid is (-1, 0), so this is the invalid handle
        bool noException = true;
        try
        {
            using var chunk = _manager.Get(negativeHandle);
            // Default chunk's Dispose() is safe (checks for null manager)
        }
        catch
        {
            noException = false;
        }

        await Assert.That(noException).IsTrue();
    }

    [Test]
    public async Task Dispose_MultipleTimes_DoesNotThrow()
    {
        var manager = new ChunkManager(initialCapacity: 4);
        manager.Allocate(); // Allocate something

        await Assert.That(() =>
        {
            manager.Dispose();
            manager.Dispose(); // Second dispose should be no-op
            manager.Dispose(); // Third dispose should also be no-op
        }).ThrowsNothing();
    }

    [Test]
    public async Task Free_AfterDispose_DoesNotThrow()
    {
        var manager = new ChunkManager(initialCapacity: 4);
        var handle = manager.Allocate();
        manager.Dispose();

        // Free after dispose should be a no-op (not throw)
        await Assert.That(() => manager.Free(handle)).ThrowsNothing();
    }
}
