using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Paradise.ECS.Concurrent.Test;

public sealed class ChunkManagerConcurrencyTests : IDisposable
{
    private readonly ChunkManager<DefaultConfig> _manager;

    public ChunkManagerConcurrencyTests()
    {
        _manager = new ChunkManager<DefaultConfig>(initialCapacity: 64);
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
        using var smallManager = new ChunkManager<DefaultConfig>(initialCapacity: 4);

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
