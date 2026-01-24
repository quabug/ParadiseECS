namespace Paradise.ECS.Concurrent.Test;

public static class ArchetypeRegistryExtension
{
    extension<TMask, TConfig>(ArchetypeRegistry<TMask, TConfig> registry)
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
    {
        public Archetype<TMask, TConfig> GetOrCreate(TMask mask)
        {
            return registry.GetOrCreate((HashedKey<TMask>)mask);
        }

        public bool TryGet(TMask mask, out Archetype<TMask, TConfig>? store)
        {
            return registry.TryGet((HashedKey<TMask>)mask, out store);
        }
    }
}

public class ArchetypeRegistryTests : IDisposable
{
    private static readonly DefaultConfig s_config = new() { DefaultChunkCapacity = 16 };
    private readonly ChunkManager _chunkManager;
    private readonly SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig> _sharedMetadata;
    private readonly ArchetypeRegistry<SmallBitSet<ulong>, DefaultConfig> _registry;

    public ArchetypeRegistryTests()
    {
        _chunkManager = ChunkManager.Create(s_config);
        _sharedMetadata = new SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig>(ComponentRegistry.Shared.TypeInfos, s_config);
        _registry = new ArchetypeRegistry<SmallBitSet<ulong>, DefaultConfig>(
            _sharedMetadata, ComponentRegistry.Shared.TypeInfos, _chunkManager);
    }

    public void Dispose()
    {
        _registry?.Dispose();
        _sharedMetadata?.Dispose();
        _chunkManager?.Dispose();
    }

    [Test]
    public async Task GetOrCreate_NewMask_CreatesArchetype()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);

        var store = _registry.GetOrCreate(mask);

        await Assert.That(store).IsNotNull();
    }

    [Test]
    public async Task GetOrCreate_SameMaskTwice_ReturnsSameArchetype()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);

        var store1 = _registry.GetOrCreate(mask);
        var store2 = _registry.GetOrCreate(mask);

        await Assert.That(store1).IsSameReferenceAs(store2);
    }

    [Test]
    public async Task GetOrCreate_DifferentMasks_CreatesDifferentArchetypes()
    {
        var mask1 = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var mask2 = SmallBitSet<ulong>.Empty.Set(TestVelocity.TypeId);

        var store1 = _registry.GetOrCreate(mask1);
        var store2 = _registry.GetOrCreate(mask2);

        await Assert.That(store1).IsNotSameReferenceAs(store2);
        await Assert.That(store1.Id).IsNotEqualTo(store2.Id);
    }

    [Test]
    public async Task GetById_ValidId_ReturnsArchetype()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var created = _registry.GetOrCreate(mask);

        var retrieved = _registry.GetById(created.Id);

        await Assert.That(retrieved).IsSameReferenceAs(created);
    }

    [Test]
    public async Task GetById_InvalidId_ReturnsNull()
    {
        var result = _registry.GetById(999);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetById_NegativeId_ReturnsNull()
    {
        var result = _registry.GetById(-1);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TryGet_ExistingMask_ReturnsTrue()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        _registry.GetOrCreate(mask);

        bool found = _registry.TryGet(mask, out var store);

        await Assert.That(found).IsTrue();
        await Assert.That(store).IsNotNull();
    }

    [Test]
    public async Task TryGet_NonExistingMask_ReturnsFalse()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);

        bool found = _registry.TryGet(mask, out var store);

        await Assert.That(found).IsFalse();
        await Assert.That(store).IsNull();
    }

    [Test]
    public async Task Dispose_PreventsNewOperations()
    {
        _registry.Dispose();

        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);

        await Assert.That(() => _registry.GetOrCreate(mask)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_MultipleTimes_DoesNotThrow()
    {
        await Assert.That(() =>
        {
            _registry.Dispose();
            _registry.Dispose();
        }).ThrowsNothing();
    }

    [Test]
    public async Task GetOrCreateWithAdd_CreatesTargetArchetype()
    {
        // Start with {Position}
        var posOnly = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var source = _registry.GetOrCreate(posOnly);

        // Add Velocity -> should get {Position, Velocity}
        var target = _registry.GetOrCreateWithAdd(source, TestVelocity.TypeId);

        var expectedMask = posOnly.Set(TestVelocity.TypeId);
        await Assert.That(target).IsNotNull();
        await Assert.That(target.Layout.ComponentMask).IsEqualTo(expectedMask);
    }

    [Test]
    public async Task GetOrCreateWithAdd_ReusesExistingEdge()
    {
        var posOnly = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var source = _registry.GetOrCreate(posOnly);

        // First call creates edge
        var target1 = _registry.GetOrCreateWithAdd(source, TestVelocity.TypeId);
        // Second call should reuse edge
        var target2 = _registry.GetOrCreateWithAdd(source, TestVelocity.TypeId);

        await Assert.That(target1).IsSameReferenceAs(target2);
    }

    [Test]
    public async Task GetOrCreateWithAdd_CachesEdge()
    {
        var posOnly = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var source = _registry.GetOrCreate(posOnly);

        // First call creates edge
        var target1 = _registry.GetOrCreateWithAdd(source, TestVelocity.TypeId);
        // Second call should use cached edge (same result)
        var target2 = _registry.GetOrCreateWithAdd(source, TestVelocity.TypeId);

        await Assert.That(target1).IsSameReferenceAs(target2);
        await Assert.That(target1.Id).IsNotEqualTo(source.Id);
    }

    [Test]
    public async Task GetOrCreateWithRemove_CreatesTargetArchetype()
    {
        // Start with {Position, Velocity}
        var posVel = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var source = _registry.GetOrCreate(posVel);

        // Remove Velocity -> should get {Position}
        var target = _registry.GetOrCreateWithRemove(source, TestVelocity.TypeId);

        var expectedMask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        await Assert.That(target).IsNotNull();
        await Assert.That(target.Layout.ComponentMask).IsEqualTo(expectedMask);
    }

    [Test]
    public async Task GetOrCreateWithRemove_ReusesExistingEdge()
    {
        var posVel = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var source = _registry.GetOrCreate(posVel);

        // First call creates edge
        var target1 = _registry.GetOrCreateWithRemove(source, TestVelocity.TypeId);
        // Second call should reuse edge
        var target2 = _registry.GetOrCreateWithRemove(source, TestVelocity.TypeId);

        await Assert.That(target1).IsSameReferenceAs(target2);
    }

    [Test]
    public async Task BidirectionalEdges_AreConsistent()
    {
        var posOnly = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var source = _registry.GetOrCreate(posOnly);

        // Add Velocity: {Position} -> {Position, Velocity}
        var target = _registry.GetOrCreateWithAdd(source, TestVelocity.TypeId);

        // Verify bidirectional: removing Velocity from target should return source
        var backToSource = _registry.GetOrCreateWithRemove(target, TestVelocity.TypeId);

        await Assert.That(backToSource).IsSameReferenceAs(source);
    }

    [Test]
    public async Task GetOrCreateWithAdd_ReusesExistingArchetype()
    {
        // Pre-create {Position, Velocity}
        var posVel = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);
        var preExisting = _registry.GetOrCreate(posVel);

        // Create {Position}
        var posOnly = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var source = _registry.GetOrCreate(posOnly);

        // Add Velocity should return the pre-existing archetype
        var target = _registry.GetOrCreateWithAdd(source, TestVelocity.TypeId);

        await Assert.That(target).IsSameReferenceAs(preExisting);
    }

    [Test]
    public async Task GetOrCreateWithRemove_ReusesExistingArchetype()
    {
        // Pre-create {Position}
        var posOnly = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var preExisting = _registry.GetOrCreate(posOnly);

        // Create {Position, Velocity}
        var posVel = posOnly.Set(TestVelocity.TypeId);
        var source = _registry.GetOrCreate(posVel);

        // Remove Velocity should return the pre-existing archetype
        var target = _registry.GetOrCreateWithRemove(source, TestVelocity.TypeId);

        await Assert.That(target).IsSameReferenceAs(preExisting);
    }
}

public class ArchetypeRegistryConcurrencyTests : IDisposable
{
    private static readonly DefaultConfig s_config = new() { DefaultChunkCapacity = 64 };
    private readonly ChunkManager _chunkManager;
    private readonly SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig> _sharedMetadata;
    private readonly ArchetypeRegistry<SmallBitSet<ulong>, DefaultConfig> _registry;

    // Number of test components available in ComponentRegistry (0-4)
    private const int TestComponentCount = 5;

    public ArchetypeRegistryConcurrencyTests()
    {
        _chunkManager = ChunkManager.Create(s_config);
        _sharedMetadata = new SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig>(ComponentRegistry.Shared.TypeInfos, s_config);
        _registry = new ArchetypeRegistry<SmallBitSet<ulong>, DefaultConfig>(
            _sharedMetadata, ComponentRegistry.Shared.TypeInfos, _chunkManager);
    }

    public void Dispose()
    {
        _registry?.Dispose();
        _sharedMetadata?.Dispose();
        _chunkManager?.Dispose();
    }

    [Test]
    public async Task ConcurrentGetOrCreate_SameMask_CreatesSingleArchetype()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);

        var tasks = new Task<Archetype<SmallBitSet<ulong>, DefaultConfig>>[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => _registry.GetOrCreate(mask));
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // All should return the same archetype
        var first = results[0];
        foreach (var store in results)
        {
            await Assert.That(store).IsSameReferenceAs(first);
        }

    }

    [Test]
    public async Task ConcurrentGetOrCreate_DifferentMasks_CreatesMultipleArchetypes()
    {
        var tasks = new Task<Archetype<SmallBitSet<ulong>, DefaultConfig>>[TestComponentCount];
        for (int i = 0; i < tasks.Length; i++)
        {
            int bitIndex = i;
            tasks[i] = Task.Run(() =>
            {
                var mask = SmallBitSet<ulong>.Empty.Set(bitIndex);
                return _registry.GetOrCreate(mask);
            });
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Each should create a unique archetype
        var uniqueIds = results.Select(r => r.Id).Distinct().ToList();
        await Assert.That(uniqueIds.Count).IsEqualTo(TestComponentCount);
    }

    [Test]
    public async Task ConcurrentGetOrCreateWithAdd_SameSourceAndComponent_CreatesSingleEdge()
    {
        var posOnly = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var source = _registry.GetOrCreate(posOnly);

        var tasks = new Task<Archetype<SmallBitSet<ulong>, DefaultConfig>>[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => _registry.GetOrCreateWithAdd(source, TestVelocity.TypeId));
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // All should return the same archetype
        var first = results[0];
        foreach (var store in results)
        {
            await Assert.That(store).IsSameReferenceAs(first);
        }

    }

    [Test]
    public async Task ConcurrentGetOrCreateWithAdd_DifferentComponents_CreatesMultipleEdges()
    {
        var empty = SmallBitSet<ulong>.Empty;
        var source = _registry.GetOrCreate(empty);

        var tasks = new Task<Archetype<SmallBitSet<ulong>, DefaultConfig>>[TestComponentCount];
        for (int i = 0; i < tasks.Length; i++)
        {
            int componentId = i;
            tasks[i] = Task.Run(() => _registry.GetOrCreateWithAdd(source, new ComponentId(componentId)));
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Each should create a unique archetype
        var uniqueIds = results.Select(r => r.Id).Distinct().ToList();
        await Assert.That(uniqueIds.Count).IsEqualTo(TestComponentCount);
    }
}
