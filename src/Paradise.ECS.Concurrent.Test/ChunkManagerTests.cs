namespace Paradise.ECS.Concurrent.Test;

public class ChunkManagerTests : IDisposable
{
    private readonly ChunkManager<DefaultConfig> _manager;

    public ChunkManagerTests()
    {
        _manager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 16 });
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
    private readonly ChunkManager<DefaultConfig> _manager;

    public ChunkManagerExceptionTests()
    {
        _manager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 16 });
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }

    [Test]
    public async Task Get_WithInvalidHandleId_ReturnsDefaultChunk()
    {
        // Version 0 makes this handle invalid; Id is also out of range
        var invalidHandle = new ChunkHandle(99999, 0);

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
    public async Task Allocate_AfterDispose_ThrowsObjectDisposedException()
    {
        var manager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 4 });
        manager.Dispose();

        await Assert.That(manager.Allocate).Throws<ObjectDisposedException>();
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
        // With packed representation, -1 becomes 0xFFFFFF (max 24-bit value) for Id
        // Version 0 means invalid, so this handle is invalid
        var negativeHandle = new ChunkHandle(-1, 0);

        // Should not throw - returns default chunk
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
        using var manager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 4 });
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
        var manager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 4 });
        var handle = manager.Allocate();
        manager.Dispose();

        await Assert.That(() => manager.Free(handle)).ThrowsNothing();
    }

    [Test]
    public async Task Get_AfterDispose_ThrowsObjectDisposedException()
    {
        var manager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 4 });
        var handle = manager.Allocate();
        manager.Dispose();

        bool threw = false;
        try
        {
            using var chunk = manager.Get(handle);
        }
        catch (ObjectDisposedException)
        {
            threw = true;
        }
        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task Release_WithOutOfRangeId_DoesNotThrow()
    {
        // This tests the Release path when id >= _nextSlotId
        // Internally called by Chunk.Dispose() with default chunk
        var manager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 4 });

        // Get a default chunk by using a stale handle
        var handle = manager.Allocate();
        manager.Free(handle);

        // Get with stale handle returns default chunk
        // When disposed, the default chunk calls Release which should be a no-op
        await Assert.That(() =>
        {
            using var chunk = manager.Get(handle);
            // chunk is default, its Dispose() calls Release with invalid data
        }).ThrowsNothing();

        manager.Dispose();
    }

    [Test]
    public async Task Release_DirectCallWithOutOfRangeId_DoesNotThrow()
    {
        // Directly test the Release path when id >= _nextSlotId
        var manager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 4 });

        // Only allocate 1 chunk, so _nextSlotId is 1
        _ = manager.Allocate();

        // Call Release with an id that's out of range (>= _nextSlotId)
        await Assert.That(() => manager.Release(9999)).ThrowsNothing();

        manager.Dispose();
    }

    [Test]
    public async Task Release_AfterDispose_DoesNotThrow()
    {
        var manager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 4 });
        var handle = manager.Allocate();
        {
            using var chunk = manager.Get(handle); // Borrow to increment share count
        }

        manager.Dispose();

        await Assert.That(() => manager.Release(handle.Id)).ThrowsNothing();
    }
}

public class ChunkManagerConstructorTests
{
    [Test]
    public async Task Constructor_WithNullChunkAllocator_ThrowsArgumentNullException()
    {
        var configWithNullAllocator = new DefaultConfig { DefaultChunkCapacity = 16, ChunkAllocator = null! };
        await Assert.That(() => new ChunkManager<DefaultConfig>(configWithNullAllocator)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithZeroInitialCapacity_UsesMinimumCapacity()
    {
        using var manager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 0 });
        var handle = manager.Allocate();
        await Assert.That(handle.IsValid).IsTrue();
    }

    [Test]
    public async Task Constructor_WithLargeInitialCapacity_CapsToMaxBlocks()
    {
        // This tests the path where metaBlocksNeeded > MaxMetaBlocks
        // MaxMetaBlocks * EntriesPerMetaBlock = 1024 * 1024 = 1M
        // We can't actually allocate that much, but we can request a large capacity
        using var manager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 2_000_000 });
        var handle = manager.Allocate();
        await Assert.That(handle.IsValid).IsTrue();
    }
}

public class ChunkHandleTests
{
    [Test]
    public async Task ToString_ValidHandle_ReturnsFormattedString()
    {
        var handle = new ChunkHandle(42, 123);
        var result = handle.ToString();
        await Assert.That(result).Contains("42");
        await Assert.That(result).Contains("123");
    }

    [Test]
    public async Task ToString_InvalidHandle_ReturnsInvalidString()
    {
        var result = ChunkHandle.Invalid.ToString();
        await Assert.That(result).Contains("Invalid");
    }

    [Test]
    public async Task IsValid_ValidHandle_ReturnsTrue()
    {
        var handle = new ChunkHandle(0, 1); // Version must be >= 1 for valid handle
        await Assert.That(handle.IsValid).IsTrue();
    }

    [Test]
    public async Task IsValid_InvalidHandle_ReturnsFalse()
    {
        await Assert.That(ChunkHandle.Invalid.IsValid).IsFalse();
    }
}
