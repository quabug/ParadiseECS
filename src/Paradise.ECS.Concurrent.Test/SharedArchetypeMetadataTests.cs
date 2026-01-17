namespace Paradise.ECS.Concurrent.Test;

public class SharedArchetypeMetadataTests : IDisposable
{
    private readonly SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig> _metadata;
    private readonly bool _ownsMetadata;

    public SharedArchetypeMetadataTests()
    {
        // Create a fresh instance for isolated testing (not the global Shared instance)
        _metadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig>();
        _ownsMetadata = true;
    }

    public void Dispose()
    {
        if (_ownsMetadata)
            _metadata.Dispose();
    }

    [Test]
    public async Task SharedProperty_ReturnsSingleton()
    {
        var shared1 = SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig>.Shared;
        var shared2 = SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig>.Shared;

        await Assert.That(shared1).IsSameReferenceAs(shared2);
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
        var mask1 = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var mask2 = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);

        int id1 = _metadata.GetOrCreateArchetypeId(mask1);
        int id2 = _metadata.GetOrCreateArchetypeId(mask2);

        await Assert.That(id1).IsEqualTo(0);
        await Assert.That(id2).IsEqualTo(1);
        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetOrCreateArchetypeId_SameMaskTwice_ReturnsSameId()
    {
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);

        int id1 = _metadata.GetOrCreateArchetypeId(mask);
        int id2 = _metadata.GetOrCreateArchetypeId(mask);

        await Assert.That(id1).IsEqualTo(id2);
        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetLayout_ValidId_ReturnsLayout()
    {
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
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
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        int expectedId = _metadata.GetOrCreateArchetypeId(mask);

        bool found = _metadata.TryGetArchetypeId(mask, out int id);

        await Assert.That(found).IsTrue();
        await Assert.That(id).IsEqualTo(expectedId);
    }

    [Test]
    public async Task TryGetArchetypeId_NonExistingMask_ReturnsFalse()
    {
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);

        bool found = _metadata.TryGetArchetypeId(mask, out int id);

        await Assert.That(found).IsFalse();
        await Assert.That(id).IsEqualTo(0); // default value
    }

    [Test]
    public async Task GetOrCreateWithAdd_CreatesTargetArchetype()
    {
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        int sourceId = _metadata.GetOrCreateArchetypeId(mask);

        int targetId = _metadata.GetOrCreateWithAdd(sourceId, TestVelocity.TypeId);

        await Assert.That(targetId).IsNotEqualTo(sourceId);
        var targetLayout = _metadata.GetLayout(targetId);
        var expectedMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        await Assert.That(targetLayout.ComponentMask).IsEqualTo(expectedMask);
    }

    [Test]
    public async Task GetOrCreateWithAdd_CachesEdge()
    {
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        int sourceId = _metadata.GetOrCreateArchetypeId(mask);

        int targetId1 = _metadata.GetOrCreateWithAdd(sourceId, TestVelocity.TypeId);
        int targetId2 = _metadata.GetOrCreateWithAdd(sourceId, TestVelocity.TypeId);

        await Assert.That(targetId1).IsEqualTo(targetId2);
        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetOrCreateWithRemove_CreatesTargetArchetype()
    {
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty
            .Set(TestPosition.TypeId)
            .Set(TestVelocity.TypeId);
        int sourceId = _metadata.GetOrCreateArchetypeId(mask);

        int targetId = _metadata.GetOrCreateWithRemove(sourceId, TestVelocity.TypeId);

        await Assert.That(targetId).IsNotEqualTo(sourceId);
        var targetLayout = _metadata.GetLayout(targetId);
        var expectedMask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        await Assert.That(targetLayout.ComponentMask).IsEqualTo(expectedMask);
    }

    [Test]
    public async Task GetOrCreateWithRemove_CachesEdge()
    {
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty
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
        var posOnly = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
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
        var desc1 = new ImmutableQueryDescription<Bit64>(
            ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId),
            ImmutableBitSet<Bit64>.Empty,
            ImmutableBitSet<Bit64>.Empty);
        var desc2 = new ImmutableQueryDescription<Bit64>(
            ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId),
            ImmutableBitSet<Bit64>.Empty,
            ImmutableBitSet<Bit64>.Empty);

        int id1 = _metadata.GetOrCreateQueryId((HashedKey<ImmutableQueryDescription<Bit64>>)desc1);
        int id2 = _metadata.GetOrCreateQueryId((HashedKey<ImmutableQueryDescription<Bit64>>)desc2);

        await Assert.That(id1).IsEqualTo(0);
        await Assert.That(id2).IsEqualTo(1);
        await Assert.That(_metadata.QueryDescriptionCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetOrCreateQueryId_SameDescriptionTwice_ReturnsSameId()
    {
        var desc = new ImmutableQueryDescription<Bit64>(
            ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId),
            ImmutableBitSet<Bit64>.Empty,
            ImmutableBitSet<Bit64>.Empty);

        int id1 = _metadata.GetOrCreateQueryId((HashedKey<ImmutableQueryDescription<Bit64>>)desc);
        int id2 = _metadata.GetOrCreateQueryId((HashedKey<ImmutableQueryDescription<Bit64>>)desc);

        await Assert.That(id1).IsEqualTo(id2);
        await Assert.That(_metadata.QueryDescriptionCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetQueryDescription_ValidId_ReturnsDescription()
    {
        var desc = new ImmutableQueryDescription<Bit64>(
            ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId),
            ImmutableBitSet<Bit64>.Empty,
            ImmutableBitSet<Bit64>.Empty);
        int id = _metadata.GetOrCreateQueryId((HashedKey<ImmutableQueryDescription<Bit64>>)desc);

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
        var localMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig>();
        localMetadata.Dispose();

        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);

        await Assert.That(() => localMetadata.GetOrCreateArchetypeId(mask)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_MultipleTimes_DoesNotThrow()
    {
        using var localMetadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig>();

        await Assert.That(() =>
        {
            localMetadata.Dispose();
            localMetadata.Dispose();
        }).ThrowsNothing();
    }
}

public class SharedArchetypeMetadataConcurrencyTests : IDisposable
{
    private readonly SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig> _metadata;

    private const int TestComponentCount = 5;

    public SharedArchetypeMetadataConcurrencyTests()
    {
        _metadata = new SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig>();
    }

    public void Dispose()
    {
        _metadata.Dispose();
    }

    [Test]
    public async Task ConcurrentGetOrCreateArchetypeId_SameMask_ReturnsSameId()
    {
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);

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
                var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(bitIndex);
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
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
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
        var mask = (HashedKey<ImmutableBitSet<Bit64>>)ImmutableBitSet<Bit64>.Empty;
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
        var desc = new ImmutableQueryDescription<Bit64>(
            ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId),
            ImmutableBitSet<Bit64>.Empty,
            ImmutableBitSet<Bit64>.Empty);
        var hashedDesc = (HashedKey<ImmutableQueryDescription<Bit64>>)desc;

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
