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
    [MethodDataSource(nameof(GetTestTimeout))]
    public async Task ConcurrentRead_WhileGrowing_ReturnsConsistentValues(TimeSpan timeout)
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2); // 4 elements per chunk
        const int totalAdditions = 5000;
        var exceptions = new ConcurrentBag<Exception>();
        var addComplete = false;

        using var cts = new CancellationTokenSource(timeout);

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
                while (!Volatile.Read(ref addComplete))
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

                    Thread.SpinWait(100);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        var allTasks = Task.WhenAll(writer, Task.WhenAll(readers));
        var completedTask = await Task.WhenAny(allTasks, Task.Delay(timeout)).ConfigureAwait(false);

        if (completedTask != allTasks)
        {
            Assert.Fail($"Timeout after {timeout.TotalSeconds}s - test did not complete");
        }

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

    public static IEnumerable<TimeSpan> GetTestTimeout()
    {
        yield return TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Regression test for race condition in MarkSlotReady where bitmap array could be replaced
    /// while a thread holds a reference to the old array.
    ///
    /// The race condition:
    /// 1. Thread A reads _readyBitmap reference in MarkSlotReady
    /// 2. Thread B in EnsureChunkSlow creates new bitmap, copies old data, replaces _readyBitmap
    /// 3. Thread A writes to old bitmap array (now orphaned)
    /// 4. The bit is never set in the new bitmap → slot appears not ready → infinite spin
    ///
    /// The fix: MarkSlotReady re-reads _readyBitmap after the atomic OR and retries if changed.
    /// </summary>
    [Test]
    [Repeat(10)] // Repeat to increase chance of hitting the race condition
    public async Task ConcurrentAdd_BitmapGrowthRace_NoInfiniteSpin()
    {
        // Use chunkShift=2 (4 elements per chunk)
        // Initial bitmap: 4 ulong words = 256 bits = 64 chunks worth
        // Bitmap growth triggers when adding chunk 64+ (element 256+)
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2);

        // Pre-populate to just below bitmap growth threshold
        // This ensures concurrent adds will straddle the growth boundary
        const int prePopulateCount = 250;
        for (int i = 0; i < prePopulateCount; i++)
        {
            list.Add(i);
        }

        const int threadCount = 16;
        const int additionsPerThread = 50;
        var exceptions = new ConcurrentBag<Exception>();
        var completed = new int[threadCount];

        // Use timeout task to detect infinite spin
        using var cts = new CancellationTokenSource();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), cts.Token);

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < additionsPerThread; i++)
                {
                    int value = prePopulateCount + threadId * additionsPerThread + i;
                    int index = list.Add(value);

                    // Verify the value is immediately readable (proves commit succeeded)
                    int stored = list[index];
                    if (stored != value)
                    {
                        throw new Exception($"Data corruption at index {index}: expected {value}, got {stored}");
                    }
                }
                Interlocked.Exchange(ref completed[threadId], 1);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        var allTasks = Task.WhenAll(tasks);
        var finishedTask = await Task.WhenAny(allTasks, timeoutTask).ConfigureAwait(false);

        if (finishedTask == timeoutTask)
        {
            // Timeout - some threads are stuck
            int completedCount = completed.Sum();
            Assert.Fail($"Timeout: only {completedCount}/{threadCount} threads completed - likely infinite spin in Add");
        }

        await cts.CancelAsync().ConfigureAwait(false);
        await Assert.That(exceptions).IsEmpty();

        int expectedCount = prePopulateCount + threadCount * additionsPerThread;
        await Assert.That(list.Count).IsEqualTo(expectedCount);

        // Verify all values are readable
        for (int i = 0; i < expectedCount; i++)
        {
            var _ = list[i];
        }
    }

    /// <summary>
    /// Stress test for bitmap race condition with multiple growth events.
    /// Forces many bitmap reallocations to increase race window exposure.
    /// </summary>
    [Test]
    [Repeat(5)]
    public async Task ConcurrentAdd_MultipleBitmapGrowths_AllSlotsCommitted()
    {
        // chunkShift=2: 4 elements/chunk, bitmap grows every 256 elements
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2);

        const int threadCount = 8;
        const int additionsPerThread = 500; // 4000 total = ~15 bitmap growths
        var exceptions = new ConcurrentBag<Exception>();
        var completed = new int[threadCount];

        using var cts = new CancellationTokenSource();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cts.Token);

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < additionsPerThread; i++)
                {
                    int value = threadId * 1_000_000 + i;
                    int index = list.Add(value);

                    // Verify commit succeeded
                    if (list[index] != value)
                    {
                        throw new Exception($"Data mismatch at index {index}");
                    }
                }
                Interlocked.Exchange(ref completed[threadId], 1);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        var allTasks = Task.WhenAll(tasks);
        var finishedTask = await Task.WhenAny(allTasks, timeoutTask).ConfigureAwait(false);

        if (finishedTask == timeoutTask)
        {
            int completedCount = completed.Sum();
            Assert.Fail($"Timeout: only {completedCount}/{threadCount} threads completed - bitmap race condition");
        }

        await cts.CancelAsync().ConfigureAwait(false);
        await Assert.That(exceptions).IsEmpty();
        await Assert.That(list.Count).IsEqualTo(threadCount * additionsPerThread);
    }

    /// <summary>
    /// Regression test for data race in indexer where _chunks array reference was not read with Volatile.Read.
    /// The race condition: a reader could see an updated _committedCount but hold a stale reference to
    /// an old, smaller _chunks array, causing IndexOutOfRangeException or NullReferenceException.
    /// </summary>
    [Test]
    [Repeat(5)] // Repeat to increase chance of hitting the race condition
    [MethodDataSource(nameof(GetTestTimeout))]
    public async Task ConcurrentReadDuringGrowth_NoStaleChunksArrayReference(TimeSpan timeout)
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
                while (!Volatile.Read(ref addComplete))
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
                    Thread.SpinWait(1); // Minimal yield to prevent thread starvation
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        var allTasks = Task.WhenAll(writer, Task.WhenAll(readers));
        var completedTask = await Task.WhenAny(allTasks, Task.Delay(timeout)).ConfigureAwait(false);

        if (completedTask != allTasks)
        {
            Assert.Fail($"Timeout after {timeout.TotalSeconds}s - test did not complete");
        }

        // If the race condition was hit, we'd see IndexOutOfRangeException or NullReferenceException
        await Assert.That(exceptions).IsEmpty();
        await Assert.That(list.Count).IsEqualTo(totalAdditions);
    }

    /// <summary>
    /// Verifies that Count (committed count) only increases monotonically.
    /// This is a critical invariant: once a value is committed, it must remain visible.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(GetTestTimeout))]
    public async Task CommitOrdering_CountMonotonicallyIncreasing(TimeSpan timeout)
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 3); // 8 elements per chunk

        const int writerCount = 4;
        const int itemsPerWriter = 200;
        var exceptions = new ConcurrentBag<Exception>();
        var writersComplete = false;

        var writerTasks = Enumerable.Range(0, writerCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < itemsPerWriter; i++)
                {
                    list.Add(threadId * 10000 + i);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        // Observer task that watches Count and verifies it never decreases
        var observerTask = Task.Run(() =>
        {
            try
            {
                int lastSeen = 0;
                while (!Volatile.Read(ref writersComplete))
                {
                    int current = list.Count;
                    if (current < lastSeen)
                    {
                        throw new Exception($"Count decreased from {lastSeen} to {current}");
                    }
                    lastSeen = current;
                    Thread.SpinWait(10);
                }

                // Final check after writers complete
                int finalCount = list.Count;
                if (finalCount < lastSeen)
                {
                    throw new Exception($"Count decreased from {lastSeen} to {finalCount}");
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        var allWriters = Task.WhenAll(writerTasks);
        var completedTask = await Task.WhenAny(allWriters, Task.Delay(timeout)).ConfigureAwait(false);

        if (completedTask != allWriters)
        {
            Assert.Fail($"Timeout after {timeout.TotalSeconds}s - writers did not complete");
        }

        Volatile.Write(ref writersComplete, true);
        await observerTask.ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
        await Assert.That(list.Count).IsEqualTo(writerCount * itemsPerWriter);
    }

    /// <summary>
    /// Tests when multiple threads try to trigger chunk allocation simultaneously
    /// by pre-filling to just below the chunk boundary.
    /// </summary>
    [Test]
    [Repeat(10)]
    public async Task ConcurrentAdd_SimultaneousGrowthTrigger_AllValuesAdded()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2); // 4 elements per chunk

        // Pre-fill to just below chunk boundary (3 of 4 slots filled)
        for (int i = 0; i < 3; i++)
        {
            list.Add(i);
        }

        const int threadCount = 16;
        var exceptions = new ConcurrentBag<Exception>();
        var indices = new ConcurrentBag<int>();

        // All threads try to add at the same time, triggering chunk allocation contention
        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                int index = list.Add(100 + threadId);
                indices.Add(index);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
        await Assert.That(list.Count).IsEqualTo(3 + threadCount);

        // Verify all returned indices are unique
        var uniqueIndices = indices.Distinct().ToList();
        await Assert.That(uniqueIndices.Count).IsEqualTo(threadCount);

        // Verify all indices are in valid range [3, 3 + threadCount)
        foreach (int idx in indices)
        {
            await Assert.That(idx).IsGreaterThanOrEqualTo(3);
            await Assert.That(idx).IsLessThan(3 + threadCount);
        }
    }

    /// <summary>
    /// Verifies that the index returned by Add actually contains the added value.
    /// Each thread adds unique values and verifies they're at the returned indices.
    /// </summary>
    [Test]
    public async Task DataIntegrity_ReturnedIndexContainsAddedValue()
    {
        var list = new ConcurrentAppendOnlyList<long>(chunkShift: 3); // 8 elements per chunk

        const int threadCount = 8;
        const int itemsPerThread = 100;
        var exceptions = new ConcurrentBag<Exception>();

        // Each thread records its (value, index) pairs
        var allPairs = new ConcurrentBag<(long Value, int Index)>();

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    long value = threadId * 1_000_000L + i;
                    int index = list.Add(value);
                    allPairs.Add((value, index));
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
        await Assert.That(list.Count).IsEqualTo(threadCount * itemsPerThread);

        // Verify each value is at its recorded index
        foreach (var (value, index) in allPairs)
        {
            long stored = list[index];
            await Assert.That(stored).IsEqualTo(value);
        }

        // Verify all indices are unique
        var uniqueIndices = allPairs.Select(p => p.Index).Distinct().ToList();
        await Assert.That(uniqueIndices.Count).IsEqualTo(threadCount * itemsPerThread);
    }
}
