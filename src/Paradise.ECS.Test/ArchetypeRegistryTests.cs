namespace Paradise.ECS.Test;

/// <summary>
/// Tests for ArchetypeRegistry.
/// </summary>
public sealed class ArchetypeRegistryTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly ChunkManager _chunkManager = ChunkManager.Create(s_config);
    private readonly SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig> _sharedMetadata = new(ComponentRegistry.Shared.TypeInfos, s_config);
    private readonly ArchetypeRegistry<SmallBitSet<ulong>, DefaultConfig> _registry;

    public ArchetypeRegistryTests()
    {
        _registry = new ArchetypeRegistry<SmallBitSet<ulong>, DefaultConfig>(_sharedMetadata, _chunkManager);
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
        var mask = SmallBitSet<ulong>.Empty;
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;

        var archetype = _registry.GetOrCreate(hashedKey);

        await Assert.That(archetype).IsNotNull();
        await Assert.That(archetype.Layout.ComponentMask.IsEmpty).IsTrue();
    }

    [Test]
    public async Task GetOrCreateArchetype_WithComponent_ReturnsArchetype()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;

        var archetype = _registry.GetOrCreate(hashedKey);

        await Assert.That(archetype).IsNotNull();
        await Assert.That(archetype.Layout.HasComponent(TestPosition.TypeId)).IsTrue();
    }

    [Test]
    public async Task GetOrCreateArchetype_SameMask_ReturnsSameArchetype()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;

        var arch1 = _registry.GetOrCreate(hashedKey);
        var arch2 = _registry.GetOrCreate(hashedKey);

        await Assert.That(arch1.Id).IsEqualTo(arch2.Id);
    }

    [Test]
    public async Task GetOrCreateArchetype_DifferentMasks_ReturnsDifferentArchetypes()
    {
        var mask1 = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var mask2 = SmallBitSet<ulong>.Empty.Set(TestVelocity.TypeId);
        var hashedKey1 = (HashedKey<SmallBitSet<ulong>>)mask1;
        var hashedKey2 = (HashedKey<SmallBitSet<ulong>>)mask2;

        var arch1 = _registry.GetOrCreate(hashedKey1);
        var arch2 = _registry.GetOrCreate(hashedKey2);

        await Assert.That(arch1.Id).IsNotEqualTo(arch2.Id);
    }

    #endregion

    #region GetArchetypeById Tests

    [Test]
    public async Task GetArchetypeById_ValidId_ReturnsArchetype()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        var retrieved = _registry.GetById(archetype.Id);

        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved!.Id).IsEqualTo(archetype.Id);
    }

    [Test]
    public async Task GetArchetypeById_InvalidId_ReturnsNull()
    {
        var retrieved = _registry.GetById(9999);

        await Assert.That(retrieved).IsNull();
    }

    #endregion

    #region GetOrCreateArchetypeWithAdd Tests

    [Test]
    public async Task GetOrCreateArchetypeWithAdd_AddsComponent()
    {
        var mask = SmallBitSet<ulong>.Empty;
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var sourceArchetype = _registry.GetOrCreate(hashedKey);

        var targetArchetype = _registry.GetOrCreateWithAdd(sourceArchetype, TestPosition.TypeId);

        await Assert.That(targetArchetype.Layout.HasComponent(TestPosition.TypeId)).IsTrue();
    }

    [Test]
    public async Task GetOrCreateArchetypeWithAdd_PreservesExistingComponents()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestVelocity.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var sourceArchetype = _registry.GetOrCreate(hashedKey);

        var targetArchetype = _registry.GetOrCreateWithAdd(sourceArchetype, TestPosition.TypeId);

        await Assert.That(targetArchetype.Layout.HasComponent(TestPosition.TypeId)).IsTrue();
        await Assert.That(targetArchetype.Layout.HasComponent(TestVelocity.TypeId)).IsTrue();
    }

    [Test]
    public async Task GetOrCreateArchetypeWithAdd_UsesEdgeCache()
    {
        var mask = SmallBitSet<ulong>.Empty;
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var sourceArchetype = _registry.GetOrCreate(hashedKey);

        // First call creates and caches the edge
        var target1 = _registry.GetOrCreateWithAdd(sourceArchetype, TestPosition.TypeId);

        // Second call should use cache
        var target2 = _registry.GetOrCreateWithAdd(sourceArchetype, TestPosition.TypeId);

        await Assert.That(target1.Id).IsEqualTo(target2.Id);
    }

    #endregion

    #region GetOrCreateArchetypeWithRemove Tests

    [Test]
    public async Task GetOrCreateArchetypeWithRemove_RemovesComponent()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var sourceArchetype = _registry.GetOrCreate(hashedKey);

        var targetArchetype = _registry.GetOrCreateWithRemove(sourceArchetype, TestPosition.TypeId);

        await Assert.That(targetArchetype.Layout.HasComponent(TestPosition.TypeId)).IsFalse();
    }

    [Test]
    public async Task GetOrCreateArchetypeWithRemove_PreservesOtherComponents()
    {
        var mask = SmallBitSet<ulong>.Empty
            .Set(TestPosition.TypeId)
            .Set(TestVelocity.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var sourceArchetype = _registry.GetOrCreate(hashedKey);

        var targetArchetype = _registry.GetOrCreateWithRemove(sourceArchetype, TestPosition.TypeId);

        await Assert.That(targetArchetype.Layout.HasComponent(TestPosition.TypeId)).IsFalse();
        await Assert.That(targetArchetype.Layout.HasComponent(TestVelocity.TypeId)).IsTrue();
    }

    [Test]
    public async Task GetOrCreateArchetypeWithRemove_LastComponent_ReturnsEmptyArchetype()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var sourceArchetype = _registry.GetOrCreate(hashedKey);

        var targetArchetype = _registry.GetOrCreateWithRemove(sourceArchetype, TestPosition.TypeId);

        await Assert.That(targetArchetype.Layout.ComponentMask.IsEmpty).IsTrue();
    }

    #endregion

    #region GetOrCreateQuery Tests

    [Test]
    public async Task GetOrCreateQuery_ReturnsQuery()
    {
        var description = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Description;
        var hashedDesc = (HashedKey<ImmutableQueryDescription<SmallBitSet<ulong>>>)description;

        var query = _registry.GetOrCreateQuery(hashedDesc);

        await Assert.That(query.ArchetypeCount).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task GetOrCreateQuery_SameDescription_ReturnsSameQuery()
    {
        var description = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Description;
        var hashedDesc = (HashedKey<ImmutableQueryDescription<SmallBitSet<ulong>>>)description;

        var query1 = _registry.GetOrCreateQuery(hashedDesc);
        var query2 = _registry.GetOrCreateQuery(hashedDesc);

        // Both queries should have the same archetype count since they reference the same underlying list
        await Assert.That(query1.ArchetypeCount).IsEqualTo(query2.ArchetypeCount);
    }

    [Test]
    public async Task GetOrCreateQuery_MatchesExistingArchetypes()
    {
        // First create an archetype
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        _registry.GetOrCreate(hashedKey);

        // Then create a query that should match
        var description = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Description;
        var hashedDesc = (HashedKey<ImmutableQueryDescription<SmallBitSet<ulong>>>)description;
        var query = _registry.GetOrCreateQuery(hashedDesc);

        await Assert.That(query.ArchetypeCount).IsEqualTo(1);
    }

    [Test]
    public async Task GetOrCreateQuery_UpdatesWhenNewArchetypeCreated()
    {
        // First create a query
        var description = new QueryBuilder<SmallBitSet<ulong>>()
            .With<TestPosition>()
            .Description;
        var hashedDesc = (HashedKey<ImmutableQueryDescription<SmallBitSet<ulong>>>)description;
        var query = _registry.GetOrCreateQuery(hashedDesc);
        var initialCount = query.ArchetypeCount;

        // Then create matching archetype
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        _registry.GetOrCreate(hashedKey);

        // Query should now include the new archetype
        await Assert.That(query.ArchetypeCount).IsEqualTo(initialCount + 1);
    }

    #endregion

    #region Clear Tests

    [Test]
    public async Task Clear_RemovesAllArchetypes()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);
        var id = archetype.Id;

        _registry.Clear();

        // After clear, the archetype should no longer be retrievable
        var retrieved = _registry.GetById(id);
        await Assert.That(retrieved).IsNull();
    }

    #endregion
}
