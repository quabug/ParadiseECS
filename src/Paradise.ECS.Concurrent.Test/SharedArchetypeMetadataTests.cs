namespace Paradise.ECS.Concurrent.Test;

public class SharedArchetypeMetadataTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig> _metadata;

    public SharedArchetypeMetadataTests()
    {
        // Create a fresh instance for isolated testing
        _metadata = new SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig>(ComponentRegistry.Shared, s_config);
    }

    public void Dispose()
    {
        _metadata.Dispose();
    }

    [Test]
    public async Task Constructor_CreatesEmptyMetadata()
    {
        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(0);
        await Assert.That(_metadata.QueryDescriptionCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetOrCreateArchetypeId_NewMask_ReturnsSequentialId()
    {
        var mask1 = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var mask2 = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(TestVelocity.TypeId);

        int id1 = _metadata.GetOrCreateArchetypeId(mask1);
        int id2 = _metadata.GetOrCreateArchetypeId(mask2);

        await Assert.That(id1).IsEqualTo(0);
        await Assert.That(id2).IsEqualTo(1);
        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetOrCreateArchetypeId_SameMaskTwice_ReturnsSameId()
    {
        var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);

        int id1 = _metadata.GetOrCreateArchetypeId(mask);
        int id2 = _metadata.GetOrCreateArchetypeId(mask);

        await Assert.That(id1).IsEqualTo(id2);
        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetLayout_ValidId_ReturnsLayout()
    {
        var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        int id = _metadata.GetOrCreateArchetypeId(mask);

        var layout = _metadata.GetLayout(id);

        // Can't pass ref struct to Assert.That, so extract the value
        var componentMask = layout.ComponentMask;
        await Assert.That(componentMask).IsEqualTo(mask.Value);
    }

    [Test]
    public async Task GetLayout_InvalidId_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => _metadata.GetLayout(999)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task TryGetArchetypeId_ExistingMask_ReturnsTrue()
    {
        var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        int expectedId = _metadata.GetOrCreateArchetypeId(mask);

        bool found = _metadata.TryGetArchetypeId(mask, out int id);

        await Assert.That(found).IsTrue();
        await Assert.That(id).IsEqualTo(expectedId);
    }

    [Test]
    public async Task TryGetArchetypeId_NonExistingMask_ReturnsFalse()
    {
        var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);

        bool found = _metadata.TryGetArchetypeId(mask, out int id);

        await Assert.That(found).IsFalse();
        await Assert.That(id).IsEqualTo(0); // default value
    }

    [Test]
    public async Task GetOrCreateWithAdd_CreatesTargetArchetype()
    {
        var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        int sourceId = _metadata.GetOrCreateArchetypeId(mask);

        int targetId = _metadata.GetOrCreateWithAdd(sourceId, TestVelocity.TypeId);

        await Assert.That(targetId).IsNotEqualTo(sourceId);
        var targetLayout = _metadata.GetLayout(targetId);
        var expectedMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        await Assert.That(targetLayout.ComponentMask).IsEqualTo(expectedMask);
    }

    [Test]
    public async Task GetOrCreateWithAdd_CachesEdge()
    {
        var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        int sourceId = _metadata.GetOrCreateArchetypeId(mask);

        int targetId1 = _metadata.GetOrCreateWithAdd(sourceId, TestVelocity.TypeId);
        int targetId2 = _metadata.GetOrCreateWithAdd(sourceId, TestVelocity.TypeId);

        await Assert.That(targetId1).IsEqualTo(targetId2);
        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetOrCreateWithRemove_CreatesTargetArchetype()
    {
        var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty
            .Set(TestPosition.TypeId)
            .Set(TestVelocity.TypeId);
        int sourceId = _metadata.GetOrCreateArchetypeId(mask);

        int targetId = _metadata.GetOrCreateWithRemove(sourceId, TestVelocity.TypeId);

        await Assert.That(targetId).IsNotEqualTo(sourceId);
        var targetLayout = _metadata.GetLayout(targetId);
        var expectedMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        await Assert.That(targetLayout.ComponentMask).IsEqualTo(expectedMask);
    }

    [Test]
    public async Task GetOrCreateWithRemove_CachesEdge()
    {
        var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty
            .Set(TestPosition.TypeId)
            .Set(TestVelocity.TypeId);
        int sourceId = _metadata.GetOrCreateArchetypeId(mask);

        int targetId1 = _metadata.GetOrCreateWithRemove(sourceId, TestVelocity.TypeId);
        int targetId2 = _metadata.GetOrCreateWithRemove(sourceId, TestVelocity.TypeId);

        await Assert.That(targetId1).IsEqualTo(targetId2);
        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(2);
    }

    [Test]
    public async Task BidirectionalEdges_AreConsistent()
    {
        var posOnly = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        int sourceId = _metadata.GetOrCreateArchetypeId(posOnly);

        // Add Velocity: {Position} -> {Position, Velocity}
        int targetId = _metadata.GetOrCreateWithAdd(sourceId, TestVelocity.TypeId);

        // Verify bidirectional: removing Velocity from target should return source ID
        int backToSourceId = _metadata.GetOrCreateWithRemove(targetId, TestVelocity.TypeId);

        await Assert.That(backToSourceId).IsEqualTo(sourceId);
        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetOrCreateQueryId_NewDescription_ReturnsSequentialId()
    {
        var desc1 = new ImmutableQueryDescription<SmallBitSet<ulong>>(
            SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId),
            SmallBitSet<ulong>.Empty,
            SmallBitSet<ulong>.Empty);
        var desc2 = new ImmutableQueryDescription<SmallBitSet<ulong>>(
            SmallBitSet<ulong>.Empty.Set(TestVelocity.TypeId),
            SmallBitSet<ulong>.Empty,
            SmallBitSet<ulong>.Empty);

        int id1 = _metadata.GetOrCreateQueryId((HashedKey<ImmutableQueryDescription<SmallBitSet<ulong>>>)desc1);
        int id2 = _metadata.GetOrCreateQueryId((HashedKey<ImmutableQueryDescription<SmallBitSet<ulong>>>)desc2);

        await Assert.That(id1).IsEqualTo(0);
        await Assert.That(id2).IsEqualTo(1);
        await Assert.That(_metadata.QueryDescriptionCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetOrCreateQueryId_SameDescriptionTwice_ReturnsSameId()
    {
        var desc = new ImmutableQueryDescription<SmallBitSet<ulong>>(
            SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId),
            SmallBitSet<ulong>.Empty,
            SmallBitSet<ulong>.Empty);

        int id1 = _metadata.GetOrCreateQueryId((HashedKey<ImmutableQueryDescription<SmallBitSet<ulong>>>)desc);
        int id2 = _metadata.GetOrCreateQueryId((HashedKey<ImmutableQueryDescription<SmallBitSet<ulong>>>)desc);

        await Assert.That(id1).IsEqualTo(id2);
        await Assert.That(_metadata.QueryDescriptionCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetQueryDescription_ValidId_ReturnsDescription()
    {
        var desc = new ImmutableQueryDescription<SmallBitSet<ulong>>(
            SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId),
            SmallBitSet<ulong>.Empty,
            SmallBitSet<ulong>.Empty);
        int id = _metadata.GetOrCreateQueryId((HashedKey<ImmutableQueryDescription<SmallBitSet<ulong>>>)desc);

        var retrieved = _metadata.GetQueryDescription(id);

        await Assert.That(retrieved).IsEqualTo(desc);
    }

    [Test]
    public async Task GetQueryDescription_InvalidId_ThrowsArgumentOutOfRangeException()
    {
        await Assert.That(() => _metadata.GetQueryDescription(999)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Dispose_PreventsNewOperations()
    {
        var localMetadata = new SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig>(ComponentRegistry.Shared, s_config);
        localMetadata.Dispose();

        var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);

        await Assert.That(() => localMetadata.GetOrCreateArchetypeId(mask)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_MultipleTimes_DoesNotThrow()
    {
        using var localMetadata = new SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig>(ComponentRegistry.Shared, s_config);

        await Assert.That(() =>
        {
            localMetadata.Dispose();
            localMetadata.Dispose();
        }).ThrowsNothing();
    }
}

public class SharedArchetypeMetadataConcurrencyTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig> _metadata;

    private const int TestComponentCount = 5;

    public SharedArchetypeMetadataConcurrencyTests()
    {
        _metadata = new SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig>(ComponentRegistry.Shared, s_config);
    }

    public void Dispose()
    {
        _metadata.Dispose();
    }

    [Test]
    public async Task ConcurrentGetOrCreateArchetypeId_SameMask_ReturnsSameId()
    {
        var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);

        var tasks = new Task<int>[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => _metadata.GetOrCreateArchetypeId(mask));
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        int first = results[0];
        foreach (int id in results)
        {
            await Assert.That(id).IsEqualTo(first);
        }

        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConcurrentGetOrCreateArchetypeId_DifferentMasks_CreatesUniqueIds()
    {
        var tasks = new Task<int>[TestComponentCount];
        for (int i = 0; i < tasks.Length; i++)
        {
            int bitIndex = i;
            tasks[i] = Task.Run(() =>
            {
                var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(bitIndex);
                return _metadata.GetOrCreateArchetypeId(mask);
            });
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var uniqueIds = results.Distinct().ToList();
        await Assert.That(uniqueIds.Count).IsEqualTo(TestComponentCount);
        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(TestComponentCount);
    }

    [Test]
    public async Task ConcurrentGetOrCreateWithAdd_SameSourceAndComponent_ReturnsSameTarget()
    {
        var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        int sourceId = _metadata.GetOrCreateArchetypeId(mask);

        var tasks = new Task<int>[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => _metadata.GetOrCreateWithAdd(sourceId, TestVelocity.TypeId));
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        int first = results[0];
        foreach (int id in results)
        {
            await Assert.That(id).IsEqualTo(first);
        }

        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(2);
    }

    [Test]
    public async Task ConcurrentGetOrCreateWithAdd_DifferentComponents_CreatesUniqueTargets()
    {
        var mask = (HashedKey<SmallBitSet<ulong>>)SmallBitSet<ulong>.Empty;
        int sourceId = _metadata.GetOrCreateArchetypeId(mask);

        var tasks = new Task<int>[TestComponentCount];
        for (int i = 0; i < tasks.Length; i++)
        {
            int componentId = i;
            tasks[i] = Task.Run(() => _metadata.GetOrCreateWithAdd(sourceId, new ComponentId(componentId)));
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var uniqueIds = results.Distinct().ToList();
        await Assert.That(uniqueIds.Count).IsEqualTo(TestComponentCount);
        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(TestComponentCount + 1); // +1 for empty source
    }

    [Test]
    public async Task ConcurrentGetOrCreateQueryId_SameDescription_ReturnsSameId()
    {
        var desc = new ImmutableQueryDescription<SmallBitSet<ulong>>(
            SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId),
            SmallBitSet<ulong>.Empty,
            SmallBitSet<ulong>.Empty);
        var hashedDesc = (HashedKey<ImmutableQueryDescription<SmallBitSet<ulong>>>)desc;

        var tasks = new Task<int>[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => _metadata.GetOrCreateQueryId(hashedDesc));
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        int first = results[0];
        foreach (int id in results)
        {
            await Assert.That(id).IsEqualTo(first);
        }

        await Assert.That(_metadata.QueryDescriptionCount).IsEqualTo(1);
    }
}
