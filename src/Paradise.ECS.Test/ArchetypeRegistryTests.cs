namespace Paradise.ECS.Test;

public class ArchetypeRegistryTests : IDisposable
{
    private readonly ChunkManager _chunkManager;
    private readonly ArchetypeRegistry<Bit64, ComponentRegistry> _registry;

    public ArchetypeRegistryTests()
    {
        _chunkManager = new ChunkManager(initialCapacity: 16);
        _registry = new ArchetypeRegistry<Bit64, ComponentRegistry>(_chunkManager);
    }

    public void Dispose()
    {
        _registry?.Dispose();
        _chunkManager?.Dispose();
    }

    [Test]
    public async Task Constructor_CreatesEmptyRegistry()
    {
        await Assert.That(_registry.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetOrCreate_NewMask_CreatesArchetype()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);

        var store = _registry.GetOrCreate(mask);

        await Assert.That(store).IsNotNull();
        await Assert.That(store.Id).IsEqualTo(0);
        await Assert.That(_registry.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetOrCreate_SameMaskTwice_ReturnsSameArchetype()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);

        var store1 = _registry.GetOrCreate(mask);
        var store2 = _registry.GetOrCreate(mask);

        await Assert.That(store1).IsSameReferenceAs(store2);
        await Assert.That(_registry.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetOrCreate_DifferentMasks_CreatesDifferentArchetypes()
    {
        var mask1 = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var mask2 = ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);

        var store1 = _registry.GetOrCreate(mask1);
        var store2 = _registry.GetOrCreate(mask2);

        await Assert.That(store1).IsNotSameReferenceAs(store2);
        await Assert.That(store1.Id).IsNotEqualTo(store2.Id);
        await Assert.That(_registry.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetById_ValidId_ReturnsArchetype()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
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
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        _registry.GetOrCreate(mask);

        bool found = _registry.TryGet(mask, out var store);

        await Assert.That(found).IsTrue();
        await Assert.That(store).IsNotNull();
    }

    [Test]
    public async Task TryGet_NonExistingMask_ReturnsFalse()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);

        bool found = _registry.TryGet(mask, out var store);

        await Assert.That(found).IsFalse();
        await Assert.That(store).IsNull();
    }

    [Test]
    public async Task GetMatching_WithAll_FiltersCorrectly()
    {
        // Create archetypes: {Position}, {Velocity}, {Position, Velocity}
        var posOnly = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var velOnly = ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);
        var posVel = posOnly.Set(TestVelocity.TypeId);

        _registry.GetOrCreate(posOnly);
        _registry.GetOrCreate(velOnly);
        _registry.GetOrCreate(posVel);

        // Query for archetypes with Position
        var all = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var none = ImmutableBitSet<Bit64>.Empty;
        var any = ImmutableBitSet<Bit64>.Empty;

        var matches = new List<ArchetypeStore<Bit64, ComponentRegistry>>();
        int count = _registry.GetMatching(all, none, any, matches);

        await Assert.That(count).IsEqualTo(2); // posOnly and posVel
        await Assert.That(matches.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetMatching_WithNone_FiltersCorrectly()
    {
        // Create archetypes: {Position}, {Position, Velocity}
        var posOnly = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var posVel = posOnly.Set(TestVelocity.TypeId);

        _registry.GetOrCreate(posOnly);
        _registry.GetOrCreate(posVel);

        // Query for archetypes with Position but NOT Velocity
        var all = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var none = ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);
        var any = ImmutableBitSet<Bit64>.Empty;

        var matches = new List<ArchetypeStore<Bit64, ComponentRegistry>>();
        int count = _registry.GetMatching(all, none, any, matches);

        await Assert.That(count).IsEqualTo(1); // Only posOnly
    }

    [Test]
    public async Task GetMatching_WithAny_FiltersCorrectly()
    {
        // Create archetypes: {Position}, {Velocity}, {Health}
        var posOnly = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var velOnly = ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);
        var healthOnly = ImmutableBitSet<Bit64>.Empty.Set(TestHealth.TypeId);

        _registry.GetOrCreate(posOnly);
        _registry.GetOrCreate(velOnly);
        _registry.GetOrCreate(healthOnly);

        // Query for archetypes with Position OR Velocity
        var all = ImmutableBitSet<Bit64>.Empty;
        var none = ImmutableBitSet<Bit64>.Empty;
        var any = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId).Set(TestVelocity.TypeId);

        var matches = new List<ArchetypeStore<Bit64, ComponentRegistry>>();
        int count = _registry.GetMatching(all, none, any, matches);

        await Assert.That(count).IsEqualTo(2); // posOnly and velOnly
    }

    [Test]
    public async Task GetMatching_EmptyFilters_ReturnsAll()
    {
        var mask1 = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var mask2 = ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);

        _registry.GetOrCreate(mask1);
        _registry.GetOrCreate(mask2);

        var all = ImmutableBitSet<Bit64>.Empty;
        var none = ImmutableBitSet<Bit64>.Empty;
        var any = ImmutableBitSet<Bit64>.Empty;

        var matches = new List<ArchetypeStore<Bit64, ComponentRegistry>>();
        int count = _registry.GetMatching(all, none, any, matches);

        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task Dispose_PreventsNewOperations()
    {
        _registry.Dispose();

        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);

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
}

public class ArchetypeRegistryConcurrencyTests : IDisposable
{
    private readonly ChunkManager _chunkManager;
    private readonly ArchetypeRegistry<Bit64, ComponentRegistry> _registry;

    // Number of test components available in ComponentRegistry (0-4)
    private const int TestComponentCount = 5;

    public ArchetypeRegistryConcurrencyTests()
    {
        _chunkManager = new ChunkManager(initialCapacity: 64);
        _registry = new ArchetypeRegistry<Bit64, ComponentRegistry>(_chunkManager);
    }

    public void Dispose()
    {
        _registry?.Dispose();
        _chunkManager?.Dispose();
    }

    [Test]
    public async Task ConcurrentGetOrCreate_SameMask_CreatesSingleArchetype()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);

        var tasks = new Task<ArchetypeStore<Bit64, ComponentRegistry>>[10];
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

        await Assert.That(_registry.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ConcurrentGetOrCreate_DifferentMasks_CreatesMultipleArchetypes()
    {
        var tasks = new Task<ArchetypeStore<Bit64, ComponentRegistry>>[TestComponentCount];
        for (int i = 0; i < tasks.Length; i++)
        {
            int bitIndex = i;
            tasks[i] = Task.Run(() =>
            {
                var mask = ImmutableBitSet<Bit64>.Empty.Set(bitIndex);
                return _registry.GetOrCreate(mask);
            });
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Each should create a unique archetype
        var uniqueIds = results.Select(r => r.Id).Distinct().ToList();
        await Assert.That(uniqueIds.Count).IsEqualTo(TestComponentCount);
        await Assert.That(_registry.Count).IsEqualTo(TestComponentCount);
    }
}
