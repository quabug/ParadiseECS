using System.Runtime.InteropServices;

namespace Paradise.ECS.Concurrent.Test;

public class ChunkManagerTests : IDisposable
{
    private readonly ChunkManager _manager;

    public ChunkManagerTests()
    {
        _manager = ChunkManager.Create(new DefaultConfig { DefaultChunkCapacity = 16 });
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
        var bytes = _manager.GetBytes(handle);
        var isEmpty = bytes.IsEmpty;
        await Assert.That(handle.IsValid).IsTrue();
        await Assert.That(isEmpty).IsFalse();

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
        var bytes1 = _manager.GetBytes(handle1);
        MemoryMarshal.Cast<byte, int>(bytes1.Slice(0, sizeof(int) * 10))[0] = 12345;

        _manager.Free(handle1);

        // Reallocate - should reuse the same slot and memory
        var handle2 = _manager.Allocate();
        var bytes2 = _manager.GetBytes(handle2);
        // Memory should be cleared (zeroed)
        var value = MemoryMarshal.Cast<byte, int>(bytes2.Slice(0, sizeof(int) * 10))[0];
        await Assert.That(value).IsEqualTo(0);
    }
}

public class ChunkManagerExceptionTests : IDisposable
{
    private readonly ChunkManager _manager;

    public ChunkManagerExceptionTests()
    {
        _manager = ChunkManager.Create(new DefaultConfig { DefaultChunkCapacity = 16 });
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }

    [Test]
    public async Task GetBytes_WithInvalidHandleId_ReturnsEmptySpan()
    {
        // Version 0 makes this handle invalid; Id is also out of range
        var invalidHandle = new ChunkHandle(99999, 0);

        var bytes = _manager.GetBytes(invalidHandle);

        await Assert.That(bytes.IsEmpty).IsTrue();
    }

    [Test]
    public async Task GetBytes_WithStaleHandle_ReturnsEmptySpan()
    {
        var handle = _manager.Allocate();
        _manager.Free(handle);

        var bytes = _manager.GetBytes(handle);

        await Assert.That(bytes.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Allocate_AfterDispose_ThrowsObjectDisposedException()
    {
        var manager = ChunkManager.Create(new DefaultConfig { DefaultChunkCapacity = 4 });
        manager.Dispose();

        await Assert.That(manager.Allocate).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Free_WhileAcquired_ThrowsInvalidOperationException()
    {
        var handle = _manager.Allocate();
        _manager.Acquire(handle);

        bool threw = false;
        try
        {
            _manager.Free(handle);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        finally
        {
            _manager.Release(handle);
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
    public async Task GetBytes_WithNegativeId_ReturnsEmptySpan()
    {
        // With packed representation, -1 becomes 0xFFFFFF (max 24-bit value) for Id
        // Version 0 means invalid, so this handle is invalid
        var negativeHandle = new ChunkHandle(-1, 0);

        var bytes = _manager.GetBytes(negativeHandle);

        await Assert.That(bytes.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Dispose_MultipleTimes_DoesNotThrow()
    {
        using var manager = ChunkManager.Create(new DefaultConfig { DefaultChunkCapacity = 4 });
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
        var manager = ChunkManager.Create(new DefaultConfig { DefaultChunkCapacity = 4 });
        var handle = manager.Allocate();
        manager.Dispose();

        await Assert.That(() => manager.Free(handle)).ThrowsNothing();
    }

    [Test]
    public async Task GetBytes_AfterDispose_ThrowsObjectDisposedException()
    {
        var manager = ChunkManager.Create(new DefaultConfig { DefaultChunkCapacity = 4 });
        var handle = manager.Allocate();
        manager.Dispose();

        bool threw = false;
        try
        {
            _ = manager.GetBytes(handle);
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
        var manager = ChunkManager.Create(new DefaultConfig { DefaultChunkCapacity = 4 });

        // Get a handle, then use a stale handle
        var handle = manager.Allocate();
        manager.Free(handle);

        // GetBytes with stale handle returns empty span
        var bytes = manager.GetBytes(handle);
        await Assert.That(bytes.IsEmpty).IsTrue();

        // Release with out of range should not throw
        await Assert.That(() => manager.Release(new ChunkHandle(99999, 1))).ThrowsNothing();

        manager.Dispose();
    }

    [Test]
    public async Task Release_DirectCallWithOutOfRangeId_DoesNotThrow()
    {
        // Directly test the Release path when id >= _nextSlotId
        var manager = ChunkManager.Create(new DefaultConfig { DefaultChunkCapacity = 4 });

        // Only allocate 1 chunk, so _nextSlotId is 1
        _ = manager.Allocate();

        // Call Release with a handle that's out of range (>= _nextSlotId)
        await Assert.That(() => manager.Release(new ChunkHandle(9999, 1))).ThrowsNothing();

        manager.Dispose();
    }

    [Test]
    public async Task Release_AfterDispose_DoesNotThrow()
    {
        var manager = ChunkManager.Create(new DefaultConfig { DefaultChunkCapacity = 4 });
        var handle = manager.Allocate();
        manager.Acquire(handle); // Increment share count
        manager.Release(handle); // Release it

        manager.Dispose();

        await Assert.That(() => manager.Release(handle)).ThrowsNothing();
    }

    [Test]
    public async Task Acquire_ValidHandle_ReturnsTrue()
    {
        var handle = _manager.Allocate();

        var result = _manager.Acquire(handle);
        _manager.Release(handle);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Acquire_StaleHandle_ReturnsFalse()
    {
        var handle = _manager.Allocate();
        _manager.Free(handle);

        var result = _manager.Acquire(handle);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Acquire_Release_AllowsFree()
    {
        var handle = _manager.Allocate();

        // Acquire and release
        _manager.Acquire(handle);
        _manager.Release(handle);

        // Should be able to free after release
        bool threw = false;
        try
        {
            _manager.Free(handle);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        await Assert.That(threw).IsFalse();
    }
}

public class ChunkManagerConstructorTests
{
    [Test]
    public async Task Constructor_WithNullChunkAllocator_ThrowsArgumentNullException()
    {
        var configWithNullAllocator = new DefaultConfig { DefaultChunkCapacity = 16, ChunkAllocator = null! };
        await Assert.That(() => ChunkManager.Create(configWithNullAllocator)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithZeroInitialCapacity_UsesMinimumCapacity()
    {
        using var manager = ChunkManager.Create(new DefaultConfig { DefaultChunkCapacity = 0 });
        var handle = manager.Allocate();
        await Assert.That(handle.IsValid).IsTrue();
    }

    [Test]
    public async Task Constructor_WithLargeInitialCapacity_CapsToMaxBlocks()
    {
        // This tests the path where metaBlocksNeeded > MaxMetaBlocks
        // MaxMetaBlocks * EntriesPerMetaBlock = 1024 * 1024 = 1M
        // We can't actually allocate that much, but we can request a large capacity
        using var manager = ChunkManager.Create(new DefaultConfig { DefaultChunkCapacity = 2_000_000 });
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
