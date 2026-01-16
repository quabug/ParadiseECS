using Microsoft.Coyote;
using Microsoft.Coyote.Specifications;
using Microsoft.Coyote.SystematicTesting;

namespace Paradise.ECS.Concurrent.ConcurrentTest;

/// <summary>
/// Entry point for running Coyote concurrent tests.
/// Run with: dotnet run [iterations]
/// </summary>
public static class Program
{
    /// <summary>
    /// Main entry point.
    /// </summary>
    public static int Main(string[] args)
    {
        int iterations = 100;
        if (args.Length > 0 && int.TryParse(args[0], out int parsed))
        {
            iterations = parsed;
        }

        Console.WriteLine($"Running Coyote concurrent tests with {iterations} iterations...");
        Console.WriteLine();

        var tests = new (string Name, Action Action)[]
        {
            // Basic concurrent operations
            ("ConcurrentAdd_MultipleThreads", ConcurrentAppendOnlyListTests.ConcurrentAdd_MultipleThreads),
            ("ConcurrentAdd_HighContention", ConcurrentAppendOnlyListTests.ConcurrentAdd_HighContention),
            ("ConcurrentRead_WhileAdding", ConcurrentAppendOnlyListTests.ConcurrentRead_WhileAdding),
            ("ConcurrentRead_MultipleReaders", ConcurrentAppendOnlyListTests.ConcurrentRead_MultipleReaders),

            // Growth scenarios
            ("ConcurrentAdd_ForcesMultipleGrowths", ConcurrentAppendOnlyListTests.ConcurrentAdd_ForcesMultipleGrowths),
            ("ConcurrentAdd_SimultaneousGrowthTrigger", ConcurrentAppendOnlyListTests.ConcurrentAdd_SimultaneousGrowthTrigger),
            ("ConcurrentRead_DuringGrowth", ConcurrentAppendOnlyListTests.ConcurrentRead_DuringGrowth),

            // Data integrity
            ("DataIntegrity_AllValuesPreserved", ConcurrentAppendOnlyListTests.DataIntegrity_AllValuesPreserved),
            ("DataIntegrity_ValuesMatchIndices", ConcurrentAppendOnlyListTests.DataIntegrity_ValuesMatchIndices),
            ("DataIntegrity_NoTornReads", ConcurrentAppendOnlyListTests.DataIntegrity_NoTornReads),

            // Ordering guarantees
            ("CommitOrdering_MonotonicallyIncreasing", ConcurrentAppendOnlyListTests.CommitOrdering_MonotonicallyIncreasing),

            // Stress tests
            ("StressTest_ManyThreadsManyOperations", ConcurrentAppendOnlyListTests.StressTest_ManyThreadsManyOperations),
            ("StressTest_RapidGrowthAndRead", ConcurrentAppendOnlyListTests.StressTest_RapidGrowthAndRead),
        };

        int passed = 0;
        int failed = 0;

        foreach (var (name, action) in tests)
        {
            Console.Write($"  {name}... ");

            var configuration = Configuration.Create()
                .WithTestingIterations((uint)iterations)
                .WithDeadlockTimeout(5000) // 5 seconds timeout for spin-wait patterns
                .WithPotentialDeadlocksReportedAsBugs(false); // SpinWait is intentional, not a deadlock

            using var engine = TestingEngine.Create(configuration, action);
            engine.Run();

            if (engine.TestReport.NumOfFoundBugs == 0)
            {
                Console.WriteLine("PASSED");
                passed++;
            }
            else
            {
                Console.WriteLine("FAILED");
                Console.WriteLine($"    Found {engine.TestReport.NumOfFoundBugs} bugs");
                Console.WriteLine($"    {engine.TestReport.BugReports}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Results: {passed} passed, {failed} failed");

        return failed == 0 ? 0 : 1;
    }
}

/// <summary>
/// Comprehensive concurrent tests for <see cref="ConcurrentAppendOnlyList{T}"/> using Coyote systematic testing.
/// Tests cover: concurrent adds, concurrent reads, growth scenarios, data integrity, and ordering.
/// </summary>
public static class ConcurrentAppendOnlyListTests
{
    #region Basic Concurrent Operations

    /// <summary>
    /// Tests concurrent Add from multiple threads without growth.
    /// </summary>
    [Test]
    public static void ConcurrentAdd_MultipleThreads()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 7); // 128 elements per chunk

        const int threadCount = 8;
        const int itemsPerThread = 10;

        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    list.Add(threadId * 1000 + i);
                }
            });
        }

        Task.WaitAll(tasks);

        Specification.Assert(
            list.Count == threadCount * itemsPerThread,
            $"Expected {threadCount * itemsPerThread} items but got {list.Count}");
    }

    /// <summary>
    /// Tests high contention scenario with many threads adding simultaneously.
    /// </summary>
    [Test]
    public static void ConcurrentAdd_HighContention()
    {
        var list = new ConcurrentAppendOnlyList<long>(chunkShift: 8); // 256 elements per chunk

        const int threadCount = 16;
        const int itemsPerThread = 10;

        var tasks = new Task[threadCount];

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    list.Add(threadId * 10000L + i);
                }
            });
        }

        Task.WaitAll(tasks);

        Specification.Assert(
            list.Count == threadCount * itemsPerThread,
            $"Count mismatch: expected {threadCount * itemsPerThread}, got {list.Count}");
    }

    /// <summary>
    /// Tests concurrent reads while adds are happening.
    /// </summary>
    [Test]
    public static void ConcurrentRead_WhileAdding()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 6); // 64 elements per chunk

        // Pre-populate
        for (int i = 0; i < 10; i++)
        {
            list.Add(i * 100);
        }

        const int writerCount = 4;
        const int readerCount = 4;

        var tasks = new Task[writerCount + readerCount];

        // Writers
        for (int t = 0; t < writerCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    list.Add((threadId + 100) * 1000 + i);
                }
            });
        }

        // Readers
        for (int t = 0; t < readerCount; t++)
        {
            tasks[writerCount + t] = Task.Run(() =>
            {
                for (int iter = 0; iter < 20; iter++)
                {
                    int count = list.Count;
                    if (count > 0)
                    {
                        // Read all currently visible elements
                        for (int i = 0; i < count; i++)
                        {
                            _ = list[i];
                        }
                    }
                }
            });
        }

        Task.WaitAll(tasks);
    }

    /// <summary>
    /// Tests multiple concurrent readers with no writers.
    /// </summary>
    [Test]
    public static void ConcurrentRead_MultipleReaders()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 7); // 128 elements per chunk

        // Pre-populate
        for (int i = 0; i < 50; i++)
        {
            list.Add(i);
        }

        const int readerCount = 8;
        var tasks = new Task[readerCount];

        for (int t = 0; t < readerCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int iter = 0; iter < 10; iter++)
                {
                    int count = list.Count;
                    for (int i = 0; i < count; i++)
                    {
                        int value = list[i];
                        Specification.Assert(value == i, $"Expected {i} but got {value}");
                    }
                }
            });
        }

        Task.WaitAll(tasks);
    }

    #endregion

    #region Growth Scenarios

    /// <summary>
    /// Tests concurrent adds that force multiple chunk allocations.
    /// </summary>
    [Test]
    public static void ConcurrentAdd_ForcesMultipleGrowths()
    {
        // Start with small chunk size to force multiple chunk allocations
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2); // 4 elements per chunk

        const int threadCount = 4;
        const int itemsPerThread = 30; // Total 120 items across 30 chunks

        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    list.Add(threadId * 1000 + i);
                }
            });
        }

        Task.WaitAll(tasks);

        int expectedCount = threadCount * itemsPerThread;
        Specification.Assert(
            list.Count == expectedCount,
            $"Expected {expectedCount} items but got {list.Count}");

        Specification.Assert(
            list.Capacity >= expectedCount,
            $"Capacity {list.Capacity} is less than count {expectedCount}");
    }

    /// <summary>
    /// Tests when multiple threads try to trigger chunk allocation simultaneously.
    /// </summary>
    [Test]
    public static void ConcurrentAdd_SimultaneousGrowthTrigger()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2); // 4 elements per chunk

        // Pre-fill to just below chunk boundary
        for (int i = 0; i < 3; i++)
        {
            list.Add(i);
        }

        const int threadCount = 8;
        var tasks = new Task[threadCount];

        // All threads try to add at the same time, triggering chunk allocation contention
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                list.Add(100 + threadId);
            });
        }

        Task.WaitAll(tasks);

        Specification.Assert(
            list.Count == 3 + threadCount,
            $"Expected {3 + threadCount} items but got {list.Count}");
    }

    /// <summary>
    /// Tests reading during chunk allocation.
    /// </summary>
    [Test]
    public static void ConcurrentRead_DuringGrowth()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2); // 4 elements per chunk

        // Pre-populate with known values
        for (int i = 0; i < 4; i++)
        {
            list.Add(i * 10);
        }

        var writerTask = Task.Run(() =>
        {
            // This will trigger multiple chunk allocations
            for (int i = 4; i < 50; i++)
            {
                list.Add(i * 10);
            }
        });

        var readerTask = Task.Run(() =>
        {
            for (int iter = 0; iter < 50; iter++)
            {
                int count = list.Count;
                for (int i = 0; i < count; i++)
                {
                    int value = list[i];
                    int expected = i * 10;
                    Specification.Assert(value == expected,
                        $"At index {i}: expected {expected}, got {value}");
                }
            }
        });

        Task.WaitAll(writerTask, readerTask);
    }

    #endregion

    #region Data Integrity

    /// <summary>
    /// Verifies that all added values are preserved and readable.
    /// </summary>
    [Test]
    public static void DataIntegrity_AllValuesPreserved()
    {
        var list = new ConcurrentAppendOnlyList<long>(chunkShift: 3); // 8 elements per chunk

        const int threadCount = 4;
        const int itemsPerThread = 25;

        var tasks = new Task[threadCount];

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    long value = threadId * 100000L + i;
                    list.Add(value);
                }
            });
        }

        Task.WaitAll(tasks);

        // Verify count
        int expectedCount = threadCount * itemsPerThread;
        Specification.Assert(list.Count == expectedCount,
            $"Expected count {expectedCount} but got {list.Count}");

        // Collect all values from list
        var listValues = new HashSet<long>();
        for (int i = 0; i < list.Count; i++)
        {
            listValues.Add(list[i]);
        }

        // Verify all expected values are present (deterministic computation)
        for (int t = 0; t < threadCount; t++)
        {
            for (int i = 0; i < itemsPerThread; i++)
            {
                long expectedValue = t * 100000L + i;
                Specification.Assert(listValues.Contains(expectedValue),
                    $"Value {expectedValue} was expected but not found in list");
            }
        }
    }

    /// <summary>
    /// For a single-threaded add, verifies values match their indices.
    /// </summary>
    [Test]
    public static void DataIntegrity_ValuesMatchIndices()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 2); // 4 elements per chunk

        const int count = 50;
        var indices = new int[count];

        var tasks = new Task[count];
        for (int i = 0; i < count; i++)
        {
            int value = i;
            tasks[i] = Task.Run(() =>
            {
                int idx = list.Add(value * 100);
                indices[value] = idx;
            });
        }

        Task.WaitAll(tasks);

        // Each value should be at its recorded index
        for (int i = 0; i < count; i++)
        {
            int idx = indices[i];
            int value = list[idx];
            Specification.Assert(value == i * 100,
                $"At index {idx}: expected {i * 100}, got {value}");
        }
    }

    /// <summary>
    /// Tests that large struct values are not torn during concurrent access.
    /// </summary>
    [Test]
    public static void DataIntegrity_NoTornReads()
    {
        var list = new ConcurrentAppendOnlyList<LargeStruct>(chunkShift: 3); // 8 elements per chunk

        const int threadCount = 4;
        const int itemsPerThread = 10;

        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    // All fields should have same value for integrity check
                    long val = threadId * 1000L + i;
                    var item = new LargeStruct(val, val, val, val);
                    list.Add(item);
                }
            });
        }

        Task.WaitAll(tasks);

        // Verify no torn reads - all fields should match
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            Specification.Assert(item.IsConsistent(),
                $"Torn read detected at index {i}: {item}");
        }
    }

    #endregion

    #region Ordering Guarantees

    /// <summary>
    /// Verifies that Count (committed count) only increases monotonically.
    /// </summary>
    [Test]
    public static void CommitOrdering_MonotonicallyIncreasing()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 3); // 8 elements per chunk

        const int writerCount = 4;
        const int itemsPerWriter = 20;

        var countObservations = new System.Collections.Concurrent.ConcurrentBag<int>();

        var writerTasks = new Task[writerCount];
        for (int t = 0; t < writerCount; t++)
        {
            int threadId = t;
            writerTasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerWriter; i++)
                {
                    list.Add(threadId * 1000 + i);
                }
            });
        }

        // Observer task that watches Count
        var observerTask = Task.Run(() =>
        {
            int lastSeen = 0;
            for (int iter = 0; iter < 200; iter++)
            {
                int current = list.Count;
                Specification.Assert(current >= lastSeen,
                    $"Count decreased from {lastSeen} to {current}");
                lastSeen = current;
                countObservations.Add(current);
            }
        });

        Task.WaitAll(writerTasks.Concat(new[] { observerTask }).ToArray());
    }

    #endregion

    #region Stress Tests

    /// <summary>
    /// Stress test with many threads performing many operations.
    /// </summary>
    [Test]
    public static void StressTest_ManyThreadsManyOperations()
    {
        var list = new ConcurrentAppendOnlyList<int>(chunkShift: 4); // 16 elements per chunk

        const int threadCount = 8;
        const int opsPerThread = 50;

        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < opsPerThread; i++)
                {
                    // Mix of adds and reads
                    if (i % 3 != 0)
                    {
                        list.Add(threadId * 10000 + i);
                    }
                    else
                    {
                        int count = list.Count;
                        if (count > 0)
                        {
                            _ = list[i % count];
                        }
                    }
                }
            });
        }

        Task.WaitAll(tasks);

        // Verify basic integrity
        int count = list.Count;
        for (int i = 0; i < count; i++)
        {
            _ = list[i];
        }
    }

    /// <summary>
    /// Stress test with rapid growth and concurrent reads.
    /// </summary>
    [Test]
    public static void StressTest_RapidGrowthAndRead()
    {
        var list = new ConcurrentAppendOnlyList<long>(chunkShift: 2); // 4 elements per chunk

        const int writerCount = 4;
        const int readerCount = 4;
        const int itemsPerWriter = 50;

        var tasks = new Task[writerCount + readerCount];

        // Writers that force rapid chunk allocation
        for (int t = 0; t < writerCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerWriter; i++)
                {
                    list.Add(threadId * 100000L + i);
                }
            });
        }

        // Aggressive readers
        for (int t = 0; t < readerCount; t++)
        {
            tasks[writerCount + t] = Task.Run(() =>
            {
                for (int iter = 0; iter < 100; iter++)
                {
                    int count = list.Count;
                    for (int i = 0; i < count; i++)
                    {
                        _ = list[i];
                    }
                }
            });
        }

        Task.WaitAll(tasks);

        Specification.Assert(
            list.Count == writerCount * itemsPerWriter,
            $"Expected {writerCount * itemsPerWriter} items, got {list.Count}");
    }

    #endregion
}

/// <summary>
/// A larger struct to test for torn reads.
/// </summary>
public readonly struct LargeStruct
{
    public readonly long A;
    public readonly long B;
    public readonly long C;
    public readonly long D;

    public LargeStruct(long a, long b, long c, long d)
    {
        A = a;
        B = b;
        C = c;
        D = d;
    }

    public bool IsConsistent() => A == B && B == C && C == D;

    public override string ToString() => $"[{A}, {B}, {C}, {D}]";
}
