namespace Paradise.ECS.Test;

/// <summary>
/// Tests for ArchetypeRegistry.
/// </summary>
public sealed class ArchetypeRegistryTests : IDisposable
{
    private readonly ChunkManager<DefaultWorldConfig> _chunkManager = new(NativeMemoryAllocator.Shared);
    private readonly SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultWorldConfig> _sharedMetadata = new(NativeMemoryAllocator.Shared);
    private readonly ArchetypeRegistry<Bit64, ComponentRegistry, DefaultWorldConfig> _registry;

    public ArchetypeRegistryTests()
    {
        _registry = new ArchetypeRegistry<Bit64, ComponentRegistry, DefaultWorldConfig>(_sharedMetadata, _chunkManager);
    }

    public void Dispose()
    {
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
    }

    #region GetOrCreateArchetype Tests

    [Test]
    public async Task GetOrCreateArchetype_EmptyMask_ReturnsArchetype()
    {
        var mask = ImmutableBitSet<Bit64>.Empty;
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;

        var archetype = _registry.GetOrCreateArchetype(hashedKey);

        await Assert.That(archetype).IsNotNull();
        await Assert.That(archetype.Layout.ComponentMask.IsEmpty).IsTrue();
    }

    [Test]
    public async Task GetOrCreateArchetype_WithComponent_ReturnsArchetype()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;

        var archetype = _registry.GetOrCreateArchetype(hashedKey);

        await Assert.That(archetype).IsNotNull();
        await Assert.That(archetype.Layout.HasComponent<TestPosition>()).IsTrue();
    }

    [Test]
    public async Task GetOrCreateArchetype_SameMask_ReturnsSameArchetype()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;

        var arch1 = _registry.GetOrCreateArchetype(hashedKey);
        var arch2 = _registry.GetOrCreateArchetype(hashedKey);

        await Assert.That(arch1.Id).IsEqualTo(arch2.Id);
    }

    [Test]
    public async Task GetOrCreateArchetype_DifferentMasks_ReturnsDifferentArchetypes()
    {
        var mask1 = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var mask2 = ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);
        var hashedKey1 = (HashedKey<ImmutableBitSet<Bit64>>)mask1;
        var hashedKey2 = (HashedKey<ImmutableBitSet<Bit64>>)mask2;

        var arch1 = _registry.GetOrCreateArchetype(hashedKey1);
        var arch2 = _registry.GetOrCreateArchetype(hashedKey2);

        await Assert.That(arch1.Id).IsNotEqualTo(arch2.Id);
    }

    #endregion

    #region GetArchetypeById Tests

    [Test]
    public async Task GetArchetypeById_ValidId_ReturnsArchetype()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var archetype = _registry.GetOrCreateArchetype(hashedKey);

        var retrieved = _registry.GetArchetypeById(archetype.Id);

        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Id).IsEqualTo(archetype.Id);
    }

    [Test]
    public async Task GetArchetypeById_InvalidId_ReturnsNull()
    {
        var retrieved = _registry.GetArchetypeById(9999);

        await Assert.That(retrieved).IsNull();
    }

    #endregion

    #region GetOrCreateArchetypeWithAdd Tests

    [Test]
    public async Task GetOrCreateArchetypeWithAdd_AddsComponent()
    {
        var mask = ImmutableBitSet<Bit64>.Empty;
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var sourceArchetype = _registry.GetOrCreateArchetype(hashedKey);

        var targetArchetype = _registry.GetOrCreateArchetypeWithAdd(sourceArchetype, TestPosition.TypeId);

        await Assert.That(targetArchetype.Layout.HasComponent<TestPosition>()).IsTrue();
    }

    [Test]
    public async Task GetOrCreateArchetypeWithAdd_PreservesExistingComponents()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var sourceArchetype = _registry.GetOrCreateArchetype(hashedKey);

        var targetArchetype = _registry.GetOrCreateArchetypeWithAdd(sourceArchetype, TestPosition.TypeId);

        await Assert.That(targetArchetype.Layout.HasComponent<TestPosition>()).IsTrue();
        await Assert.That(targetArchetype.Layout.HasComponent<TestVelocity>()).IsTrue();
    }

    [Test]
    public async Task GetOrCreateArchetypeWithAdd_UsesEdgeCache()
    {
        var mask = ImmutableBitSet<Bit64>.Empty;
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var sourceArchetype = _registry.GetOrCreateArchetype(hashedKey);

        // First call creates and caches the edge
        var target1 = _registry.GetOrCreateArchetypeWithAdd(sourceArchetype, TestPosition.TypeId);

        // Second call should use cache
        var target2 = _registry.GetOrCreateArchetypeWithAdd(sourceArchetype, TestPosition.TypeId);

        await Assert.That(target1.Id).IsEqualTo(target2.Id);
    }

    #endregion

    #region GetOrCreateArchetypeWithRemove Tests

    [Test]
    public async Task GetOrCreateArchetypeWithRemove_RemovesComponent()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var sourceArchetype = _registry.GetOrCreateArchetype(hashedKey);

        var targetArchetype = _registry.GetOrCreateArchetypeWithRemove(sourceArchetype, TestPosition.TypeId);

        await Assert.That(targetArchetype.Layout.HasComponent<TestPosition>()).IsFalse();
    }

    [Test]
    public async Task GetOrCreateArchetypeWithRemove_PreservesOtherComponents()
    {
        var mask = ImmutableBitSet<Bit64>.Empty
            .Set(TestPosition.TypeId)
            .Set(TestVelocity.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var sourceArchetype = _registry.GetOrCreateArchetype(hashedKey);

        var targetArchetype = _registry.GetOrCreateArchetypeWithRemove(sourceArchetype, TestPosition.TypeId);

        await Assert.That(targetArchetype.Layout.HasComponent<TestPosition>()).IsFalse();
        await Assert.That(targetArchetype.Layout.HasComponent<TestVelocity>()).IsTrue();
    }

    [Test]
    public async Task GetOrCreateArchetypeWithRemove_LastComponent_ReturnsEmptyArchetype()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var sourceArchetype = _registry.GetOrCreateArchetype(hashedKey);

        var targetArchetype = _registry.GetOrCreateArchetypeWithRemove(sourceArchetype, TestPosition.TypeId);

        await Assert.That(targetArchetype.Layout.ComponentMask.IsEmpty).IsTrue();
    }

    #endregion

    #region GetOrCreateQuery Tests

    [Test]
    public async Task GetOrCreateQuery_ReturnsQuery()
    {
        var description = World<Bit64, ComponentRegistry, DefaultWorldConfig>.Query()
            .With<TestPosition>()
            .Description;
        var hashedDesc = (HashedKey<ImmutableQueryDescription<Bit64>>)description;

        var query = _registry.GetOrCreateQuery(hashedDesc);

        await Assert.That(query.ArchetypeCount).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task GetOrCreateQuery_SameDescription_ReturnsSameQuery()
    {
        var description = World<Bit64, ComponentRegistry, DefaultWorldConfig>.Query()
            .With<TestPosition>()
            .Description;
        var hashedDesc = (HashedKey<ImmutableQueryDescription<Bit64>>)description;

        var query1 = _registry.GetOrCreateQuery(hashedDesc);
        var query2 = _registry.GetOrCreateQuery(hashedDesc);

        // Both queries should have the same archetype count since they reference the same underlying list
        await Assert.That(query1.ArchetypeCount).IsEqualTo(query2.ArchetypeCount);
    }

    [Test]
    public async Task GetOrCreateQuery_MatchesExistingArchetypes()
    {
        // First create an archetype
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        _registry.GetOrCreateArchetype(hashedKey);

        // Then create a query that should match
        var description = World<Bit64, ComponentRegistry, DefaultWorldConfig>.Query()
            .With<TestPosition>()
            .Description;
        var hashedDesc = (HashedKey<ImmutableQueryDescription<Bit64>>)description;
        var query = _registry.GetOrCreateQuery(hashedDesc);

        await Assert.That(query.ArchetypeCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetOrCreateQuery_UpdatesWhenNewArchetypeCreated()
    {
        // First create a query
        var description = World<Bit64, ComponentRegistry, DefaultWorldConfig>.Query()
            .With<TestPosition>()
            .Description;
        var hashedDesc = (HashedKey<ImmutableQueryDescription<Bit64>>)description;
        var query = _registry.GetOrCreateQuery(hashedDesc);
        var initialCount = query.ArchetypeCount;

        // Then create matching archetype
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        _registry.GetOrCreateArchetype(hashedKey);

        // Query should now include the new archetype
        await Assert.That(query.ArchetypeCount).IsEqualTo(initialCount + 1);
    }

    #endregion

    #region Clear Tests

    [Test]
    public async Task Clear_RemovesAllArchetypes()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var archetype = _registry.GetOrCreateArchetype(hashedKey);
        var id = archetype.Id;

        _registry.Clear();

        // After clear, the archetype should no longer be retrievable
        var retrieved = _registry.GetArchetypeById(id);
        await Assert.That(retrieved).IsNull();
    }

    #endregion
}
