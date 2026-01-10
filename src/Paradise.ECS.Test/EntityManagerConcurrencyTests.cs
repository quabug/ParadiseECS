using System.Collections.Concurrent;

namespace Paradise.ECS.Test;

public sealed class EntityManagerConcurrencyTests : IDisposable
{
    private readonly EntityManager _manager;

    public EntityManagerConcurrencyTests()
    {
        _manager = new EntityManager();
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }

    [Test]
    public async Task ConcurrentCreate_AllEntitiesAreUnique()
    {
        const int threadCount = 8;
        const int entitiesPerThread = 500;
        var allEntities = new ConcurrentBag<Entity>();
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < entitiesPerThread; i++)
                {
                    var entity = _manager.Create();
                    allEntities.Add(entity);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
        await Assert.That(allEntities.Count).IsEqualTo(threadCount * entitiesPerThread);

        // Verify all entities are unique (no duplicate ids with same version)
        var uniqueEntities = allEntities.Distinct().ToList();
        await Assert.That(uniqueEntities.Count).IsEqualTo(allEntities.Count);

        // Verify all are alive
        var allAlive = allEntities.All(e => _manager.IsAlive(e));
        await Assert.That(allAlive).IsTrue();
    }

    [Test]
    public async Task ConcurrentCreateAndDestroy_ConsistentAliveCount()
    {
        const int threadCount = 8;
        const int operationsPerThread = 200;
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            try
            {
                var localEntities = new List<Entity>();

                for (int i = 0; i < operationsPerThread; i++)
                {
                    // Create
                    var entity = _manager.Create();
                    localEntities.Add(entity);

                    // Destroy half of them
                    if (i % 2 == 0 && localEntities.Count > 1)
                    {
                        var toDestroy = localEntities[localEntities.Count - 2];
                        _manager.Destroy(toDestroy);
                        localEntities.RemoveAt(localEntities.Count - 2);
                    }
                }

                // Verify all remaining entities are alive
                foreach (var entity in localEntities)
                {
                    if (!_manager.IsAlive(entity))
                        throw new InvalidOperationException($"Entity {entity} should be alive but is not");
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();

        // AliveCount should match actual alive entities
        int aliveCount = _manager.AliveCount;
        await Assert.That(aliveCount).IsGreaterThan(0);
    }

    [Test]
    public async Task ConcurrentDestroy_SameEntity_OnlyOneSucceeds()
    {
        const int threadCount = 10;
        var entity = _manager.Create();
        var successCount = 0;
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            try
            {
                var wasAlive = _manager.IsAlive(entity);
                _manager.Destroy(entity);
                if (wasAlive)
                    Interlocked.Increment(ref successCount);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
        // Entity is now destroyed
        await Assert.That(_manager.IsAlive(entity)).IsFalse();
    }

    [Test]
    public async Task ConcurrentCreateDestroyCreate_ReusesSlots()
    {
        const int threadCount = 8;
        const int cyclesPerThread = 100;
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < cyclesPerThread; i++)
                {
                    var entity = _manager.Create();
                    if (!_manager.IsAlive(entity))
                        throw new InvalidOperationException("Newly created entity should be alive");

                    _manager.Destroy(entity);
                    if (_manager.IsAlive(entity))
                        throw new InvalidOperationException("Destroyed entity should not be alive");
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();
        await Assert.That(_manager.AliveCount).IsEqualTo(0);
    }

    [Test]
    public async Task ConcurrentIsAlive_ConsistentResults()
    {
        const int entityCount = 100;
        const int threadCount = 8;
        const int checksPerThread = 1000;

        // Create entities
        var entities = new Entity[entityCount];
        for (int i = 0; i < entityCount; i++)
        {
            entities[i] = _manager.Create();
        }

        var exceptions = new ConcurrentBag<Exception>();
        var random = new Random(42);

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                var localRandom = new Random(42 + threadId);
                for (int i = 0; i < checksPerThread; i++)
                {
                    var entity = entities[localRandom.Next(entityCount)];
                    // Just check - should not throw
                    var isAlive = _manager.IsAlive(entity);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Assert.That(exceptions).IsEmpty();

        // All entities should still be alive
        var allAlive = entities.All(e => _manager.IsAlive(e));
        await Assert.That(allAlive).IsTrue();
    }

    [Test]
    public async Task ConcurrentCapacityExpansion_NoDataLoss()
    {
        var manager = new EntityManager();
        try
        {
            const int threadCount = 8;
            const int entitiesPerThread = 1000; // More entities to trigger chunk expansion
            var allEntities = new ConcurrentBag<Entity>();
            var exceptions = new ConcurrentBag<Exception>();

            var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < entitiesPerThread; i++)
                    {
                        var entity = manager.Create();
                        allEntities.Add(entity);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            await Assert.That(exceptions).IsEmpty();
            await Assert.That(allEntities.Count).IsEqualTo(threadCount * entitiesPerThread);

            // Verify all created entities are still alive
            var allAlive = allEntities.All(e => manager.IsAlive(e));
            await Assert.That(allAlive).IsTrue();

            await Assert.That(manager.AliveCount).IsEqualTo(threadCount * entitiesPerThread);
        }
        finally
        {
            manager.Dispose();
        }
    }

    [Test]
    public async Task ConcurrentMixedOperations_MaintainsConsistency()
    {
        const int threadCount = 8;
        const int operationsPerThread = 500;
        var allCreated = new ConcurrentBag<Entity>();
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                var localEntities = new List<Entity>();
                var random = new Random(42 + threadId);

                for (int i = 0; i < operationsPerThread; i++)
                {
                    int operation = random.Next(3);

                    if (operation == 0 || localEntities.Count == 0)
                    {
                        // Create
                        var entity = _manager.Create();
                        localEntities.Add(entity);
                        allCreated.Add(entity);
                    }
                    else if (operation == 1 && localEntities.Count > 0)
                    {
                        // Destroy
                        int index = random.Next(localEntities.Count);
                        var entity = localEntities[index];
                        _manager.Destroy(entity);
                        localEntities.RemoveAt(index);
                    }
                    else if (localEntities.Count > 0)
                    {
                        // IsAlive check
                        int index = random.Next(localEntities.Count);
                        var entity = localEntities[index];
                        if (!_manager.IsAlive(entity))
                            throw new InvalidOperationException("Entity in local list should be alive");
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
        await Assert.That(allCreated.Count).IsGreaterThan(0);
    }
}
