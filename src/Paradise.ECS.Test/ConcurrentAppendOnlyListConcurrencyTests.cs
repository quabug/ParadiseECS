using System.Collections.Concurrent;

namespace Paradise.ECS.Test;

public sealed class ConcurrentAppendOnlyListConcurrencyTests
{
    [Test]
    public async Task ConcurrentAdd_AllValuesStored()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 4); // 16 elements per chunk
        const int threadCount = 8;
        const int additionsPerThread = 500;
        var allIndices = new ConcurrentBag<int>();
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < additionsPerThread; i++)
                {
                    int value = threadId * 10000 + i;
                    int index = list.Add(value);
                    allIndices.Add(index);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
        await Assert.That(list.Count).IsEqualTo(threadCount * additionsPerThread);

        // Verify all indices are unique
        var uniqueIndices = allIndices.Distinct().ToList();
        await Assert.That(uniqueIndices.Count).IsEqualTo(allIndices.Count);

        // Verify indices are 0 to N-1
        var sortedIndices = uniqueIndices.OrderBy(x => x).ToList();
        for (int i = 0; i < sortedIndices.Count; i++)
        {
            await Assert.That(sortedIndices[i]).IsEqualTo(i);
        }
    }

    [Test]
    public async Task ConcurrentAddAndRead_NoDataCorruption()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 4);
        const int writerCount = 4;
        const int readerCount = 4;
        const int additionsPerWriter = 500;
        const int readsPerReader = 2000;
        var exceptions = new ConcurrentBag<Exception>();

        var writers = Enumerable.Range(0, writerCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < additionsPerWriter; i++)
                {
                    int value = threadId * 10000 + i;
                    list.Add(value);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }));

        var readers = Enumerable.Range(0, readerCount).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < readsPerReader; i++)
                {
                    int count = list.Count;
                    if (count > 0)
                    {
                        // Read a valid index
                        int index = i % count;
                        if (index < list.Count) // Double-check
                        {
                            _ = list[index]; // Should not throw
                        }
                    }
                    Thread.SpinWait(10);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }));

        await Task.WhenAll(writers.Concat(readers)).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
        await Assert.That(list.Count).IsEqualTo(writerCount * additionsPerWriter);
    }

    [Test]
    public async Task ConcurrentAdd_TriggersGrowth_NoDataLoss()
    {
        // Start with small chunk size to force many chunk allocations
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2); // 4 elements per chunk
        const int threadCount = 8;
        const int additionsPerThread = 1000;
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < additionsPerThread; i++)
                {
                    int value = threadId * 100000 + i;
                    int index = list.Add(value);

                    // Immediately verify the value was stored correctly
                    int stored = list[index];
                    if (stored != value)
                    {
                        throw new Exception($"Data corruption: stored {stored}, expected {value}");
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
        await Assert.That(list.Count).IsEqualTo(threadCount * additionsPerThread);
        await Assert.That(list.Capacity).IsGreaterThanOrEqualTo(threadCount * additionsPerThread);
    }

    [Test]
    public async Task ConcurrentRead_WhileGrowing_ReturnsConsistentValues()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2); // 4 elements per chunk
        const int totalAdditions = 5000;
        var exceptions = new ConcurrentBag<Exception>();
        var addComplete = false;

        // Writer task - adds sequential values where value == index
        var writer = Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < totalAdditions; i++)
                {
                    list.Add(i);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
            finally
            {
                Volatile.Write(ref addComplete, true);
            }
        });

        // Reader tasks - verify value at index i equals i
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            try
            {
                while (!Volatile.Read(ref addComplete) || list.Count > 0)
                {
                    int count = list.Count;
                    for (int i = 0; i < count && i < list.Count; i++)
                    {
                        int value = list[i];
                        // Value at index i should be i (since we add sequentially)
                        if (value != i)
                        {
                            throw new Exception($"Data inconsistency at index {i}: got {value}");
                        }
                    }

                    if (Volatile.Read(ref addComplete))
                        break;

                    Thread.SpinWait(100);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await writer.ConfigureAwait(false);
        await Task.WhenAll(readers).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
        await Assert.That(list.Count).IsEqualTo(totalAdditions);
    }

    [Test]
    public async Task ConcurrentAdd_HighContention_MaintainsOrderedCommit()
    {
        // This tests that sequential commit (via spin-wait) works correctly
        // under high contention where many threads try to commit simultaneously
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 6); // 64 elements per chunk
        const int threadCount = 16;
        const int additionsPerThread = 200;
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < additionsPerThread; i++)
                {
                    list.Add(i);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();

        int expectedCount = threadCount * additionsPerThread;
        await Assert.That(list.Count).IsEqualTo(expectedCount);

        // Verify all indices are readable (no holes in committed data)
        for (int i = 0; i < expectedCount; i++)
        {
            var _ = list[i]; // Should not throw
        }
    }

    [Test]
    public async Task ConcurrentAdd_WithLargeStruct_NoDataCorruption()
    {
        var list = new ConcurrentAppendOnlyList<LargeStruct>(chunkShift: 3); // 8 elements per chunk
        const int threadCount = 4;
        const int additionsPerThread = 250;
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < additionsPerThread; i++)
                {
                    var value = new LargeStruct
                    {
                        A = threadId,
                        B = i,
                        C = threadId * 1000 + i,
                        D = ~(threadId * 1000 + i)
                    };
                    int index = list.Add(value);

                    // Verify the value was stored correctly
                    var stored = list[index];
                    if (stored.A != value.A || stored.B != value.B ||
                        stored.C != value.C || stored.D != value.D)
                    {
                        throw new Exception($"Data corruption at index {index}");
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
        await Assert.That(list.Count).IsEqualTo(threadCount * additionsPerThread);
    }

    [Test]
    public async Task ConcurrentAdd_WithReferenceType_NoDataCorruption()
    {
        var list = new ConcurrentAppendOnlyList<string>(chunkShift: 4);
        const int threadCount = 4;
        const int additionsPerThread = 250;
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < additionsPerThread; i++)
                {
                    string value = $"thread{threadId}_item{i}";
                    int index = list.Add(value);

                    // Verify the value was stored correctly
                    string stored = list[index];
                    if (stored != value)
                    {
                        throw new Exception($"Data corruption at index {index}: expected '{value}', got '{stored}'");
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
        await Assert.That(list.Count).IsEqualTo(threadCount * additionsPerThread);
    }

    private struct LargeStruct
    {
        public long A;
        public long B;
        public long C;
        public long D;
    }

    /// <summary>
    /// Regression test for data race in indexer where _chunks array reference was not read with Volatile.Read.
    /// The race condition: a reader could see an updated _committedCount but hold a stale reference to
    /// an old, smaller _chunks array, causing IndexOutOfRangeException or NullReferenceException.
    /// </summary>
    [Test]
    [Repeat(5)] // Repeat to increase chance of hitting the race condition
    public async Task ConcurrentReadDuringGrowth_NoStaleChunksArrayReference()
    {
        // Use minimum chunk size (4 elements) to maximize array growth frequency
        // Start with initial capacity of 4 chunks = 16 elements
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2);
        const int totalAdditions = 10000; // Force many _chunks array growths
        var exceptions = new ConcurrentBag<Exception>();
        var addComplete = false;

        // Writer task - adds elements to force frequent _chunks array reallocations
        var writer = Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < totalAdditions; i++)
                {
                    list.Add(i);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
            finally
            {
                Volatile.Write(ref addComplete, true);
            }
        });

        // Multiple aggressive reader tasks that hammer the indexer during growth
        // This should expose the race where _chunks is read without Volatile.Read
        var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            try
            {
                int lastCheckedIndex = -1;
                while (!Volatile.Read(ref addComplete) || lastCheckedIndex < list.Count - 1)
                {
                    int count = list.Count;
                    if (count > 0)
                    {
                        // Read the last committed index - this is where the race is most likely
                        // because the _chunks array may have just grown
                        int indexToRead = count - 1;
                        if (indexToRead > lastCheckedIndex)
                        {
                            // This read could fail with IndexOutOfRangeException or NullReferenceException
                            // if _chunks is not read with Volatile.Read and we get a stale array reference
                            int value = list[indexToRead];
                            if (value != indexToRead)
                            {
                                throw new Exception($"Data inconsistency at index {indexToRead}: got {value}");
                            }
                            lastCheckedIndex = indexToRead;
                        }
                    }
                    // No spin wait - maximize contention
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await writer.ConfigureAwait(false);
        await Task.WhenAll(readers).ConfigureAwait(false);

        // If the race condition was hit, we'd see IndexOutOfRangeException or NullReferenceException
        await Assert.That(exceptions).IsEmpty();
        await Assert.That(list.Count).IsEqualTo(totalAdditions);
    }
}
