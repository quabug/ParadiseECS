using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

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
    public async Task Allocate_AfterDispose_ThrowsObjectDisposedException()
    {
        var manager = new ChunkManager(initialCapacity: 4);
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

public class ChunkManagerConcurrencyTests : IDisposable
{
    private readonly ChunkManager _manager;

    public ChunkManagerConcurrencyTests()
    {
        _manager = new ChunkManager(initialCapacity: 64);
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }

    [Test]
    public async Task ConcurrentAllocate_AllHandlesAreUnique()
    {
        const int threadCount = 8;
        const int allocationsPerThread = 500;
        var allHandles = new ConcurrentBag<ChunkHandle>();
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < allocationsPerThread; i++)
                {
                    var handle = _manager.Allocate();
                    allHandles.Add(handle);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
        await Assert.That(allHandles.Count).IsEqualTo(threadCount * allocationsPerThread);

        // Verify all handles are unique (no duplicate ids)
        var uniqueIds = allHandles.Select(h => h.Id).Distinct().ToList();
        await Assert.That(uniqueIds.Count).IsEqualTo(allHandles.Count);
    }

    [Test]
    public async Task ConcurrentAllocateAndFree_NoDataCorruption()
    {
        const int threadCount = 8;
        const int operationsPerThread = 200;
        var exceptions = new ConcurrentBag<Exception>();
        var activeHandles = new ConcurrentDictionary<int, ChunkHandle>();

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                var localHandles = new List<ChunkHandle>();

                for (int i = 0; i < operationsPerThread; i++)
                {
                    // Allocate
                    var handle = _manager.Allocate();
                    localHandles.Add(handle);

                    // Write thread-specific data
                    {
                        using var chunk = _manager.Get(handle);
                        chunk.GetSpan<int>(0, 4)[0] = threadId;
                        chunk.GetSpan<int>(0, 4)[1] = i;
                        chunk.GetSpan<int>(0, 4)[2] = handle.Id;
                    }

                    // Periodically free some handles
                    if (localHandles.Count > 10 && i % 3 == 0)
                    {
                        var toFree = localHandles[0];
                        localHandles.RemoveAt(0);

                        // Verify data before freeing
                        {
                            using var chunk = _manager.Get(toFree);
                            var data = chunk.GetSpan<int>(0, 4);
                            if (data[2] != toFree.Id)
                            {
                                throw new Exception($"Data corruption: expected id {toFree.Id}, got {data[2]}");
                            }
                        }

                        _manager.Free(toFree);
                    }
                }

                // Clean up remaining handles
                foreach (var handle in localHandles)
                {
                    _manager.Free(handle);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
    }

    [Test]
    public async Task ConcurrentGetAndRelease_BorrowCountCorrect()
    {
        const int threadCount = 8;
        const int getsPerThread = 100;
        var exceptions = new ConcurrentBag<Exception>();

        // Pre-allocate a handle that all threads will access
        var sharedHandle = _manager.Allocate();
        {
            using var chunk = _manager.Get(sharedHandle);
            chunk.GetSpan<int>(0, 1)[0] = 42;
        }

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < getsPerThread; i++)
                {
                    using var chunk = _manager.Get(sharedHandle);
                    var value = chunk.GetSpan<int>(0, 1)[0];
                    if (value != 42)
                    {
                        throw new Exception($"Data corruption: expected 42, got {value}");
                    }
                    // Small delay to increase contention
                    Thread.SpinWait(10);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();

        // After all threads complete, we should be able to free
        _manager.Free(sharedHandle);

        // Verify handle is now invalid by checking version changed after reallocation
        var newHandle = _manager.Allocate();
        await Assert.That(newHandle.Id).IsEqualTo(sharedHandle.Id);
        await Assert.That(newHandle.Version).IsNotEqualTo(sharedHandle.Version);
        _manager.Free(newHandle);
    }

    [Test]
    public async Task ConcurrentAllocate_TriggersGrowth_Succeeds()
    {
        // Use small initial capacity to force growth
        using var smallManager = new ChunkManager(initialCapacity: 4);

        const int threadCount = 8;
        const int allocationsPerThread = 300; // Will exceed initial capacity
        var allHandles = new ConcurrentBag<ChunkHandle>();
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < allocationsPerThread; i++)
                {
                    var handle = smallManager.Allocate();
                    allHandles.Add(handle);

                    // Verify we can use the handle
                    {
                        using var chunk = smallManager.Get(handle);
                        chunk.GetSpan<long>(0, 1)[0] = handle.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
        await Assert.That(allHandles.Count).IsEqualTo(threadCount * allocationsPerThread);

        // Verify all data is correct
        int verifiedCount = 0;
        foreach (var handle in allHandles)
        {
            using var chunk = smallManager.Get(handle);
            var storedId = chunk.GetSpan<long>(0, 1)[0];
            if (storedId == handle.Id)
                verifiedCount++;
        }
        await Assert.That(verifiedCount).IsEqualTo(allHandles.Count);
    }

    [Test]
    [SuppressMessage("Security", "CA5394:Do not use insecure randomizer")]
    public async Task ConcurrentMixedOperations_StressTest()
    {
        const int threadCount = 8;
        const int operationsPerThread = 500;
        var exceptions = new ConcurrentBag<Exception>();
        var totalAllocations = 0;
        var totalFrees = 0;

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                var random = new Random(threadId * 1000);
                var localHandles = new List<ChunkHandle>();

                for (int i = 0; i < operationsPerThread; i++)
                {
                    int op = random.Next(100);

                    if (op < 40 || localHandles.Count == 0)
                    {
                        // 40% chance: Allocate
                        var handle = _manager.Allocate();
                        localHandles.Add(handle);
                        Interlocked.Increment(ref totalAllocations);

                        // Write marker data
                        {
                            using var chunk = _manager.Get(handle);
                            chunk.GetSpan<int>(0, 2)[0] = threadId;
                            chunk.GetSpan<int>(0, 2)[1] = handle.Id;
                        }
                    }
                    else if (op < 70)
                    {
                        // 30% chance: Get and verify
                        var idx = random.Next(localHandles.Count);
                        var handle = localHandles[idx];

                        using var chunk = _manager.Get(handle);
                        var data = chunk.GetSpan<int>(0, 2);
                        if (data[1] != handle.Id)
                        {
                            throw new Exception($"Data mismatch: expected {handle.Id}, got {data[1]}");
                        }
                    }
                    else
                    {
                        // 30% chance: Free
                        var idx = random.Next(localHandles.Count);
                        var handle = localHandles[idx];
                        localHandles.RemoveAt(idx);

                        _manager.Free(handle);
                        Interlocked.Increment(ref totalFrees);
                    }
                }

                // Clean up remaining handles
                foreach (var handle in localHandles)
                {
                    _manager.Free(handle);
                    Interlocked.Increment(ref totalFrees);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
        await Assert.That(totalAllocations).IsEqualTo(totalFrees);
    }
}
