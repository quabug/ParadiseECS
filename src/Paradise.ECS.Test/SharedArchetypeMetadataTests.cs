namespace Paradise.ECS.Test;

/// <summary>
/// Tests for SharedArchetypeMetadata.
/// </summary>
public sealed class SharedArchetypeMetadataTests : IDisposable
{
    private readonly SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig> _metadata;

    public SharedArchetypeMetadataTests()
    {
        _metadata = new SharedArchetypeMetadata<ImmutableBitSet<Bit64>, ComponentRegistry, DefaultConfig>(new DefaultConfig());
    }

    public void Dispose()
    {
        _metadata.Dispose();
    }

    #region GetOrCreateArchetypeId Tests

    [Test]
    public async Task GetOrCreateArchetypeId_EmptyMask_ReturnsValidId()
    {
        var mask = ImmutableBitSet<Bit64>.Empty;
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var matchedQueries = new List<int>();

        var id = _metadata.GetOrCreateArchetypeId(hashedKey, matchedQueries);

        await Assert.That(id).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task GetOrCreateArchetypeId_SameMask_ReturnsSameId()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var matchedQueries = new List<int>();

        var id1 = _metadata.GetOrCreateArchetypeId(hashedKey, matchedQueries);
        var id2 = _metadata.GetOrCreateArchetypeId(hashedKey, matchedQueries);

        await Assert.That(id1).IsEqualTo(id2);
    }

    [Test]
    public async Task GetOrCreateArchetypeId_DifferentMasks_ReturnsDifferentIds()
    {
        var mask1 = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var mask2 = ImmutableBitSet<Bit64>.Empty.Set(TestVelocity.TypeId);
        var hashedKey1 = (HashedKey<ImmutableBitSet<Bit64>>)mask1;
        var hashedKey2 = (HashedKey<ImmutableBitSet<Bit64>>)mask2;
        var matchedQueries = new List<int>();

        var id1 = _metadata.GetOrCreateArchetypeId(hashedKey1, matchedQueries);
        var id2 = _metadata.GetOrCreateArchetypeId(hashedKey2, matchedQueries);

        await Assert.That(id1).IsNotEqualTo(id2);
    }

    [Test]
    public async Task GetOrCreateArchetypeId_ManyArchetypes_AllUniqueIds()
    {
        var ids = new HashSet<int>();
        var matchedQueries = new List<int>();

        for (int i = 0; i < 5; i++)
        {
            var mask = ImmutableBitSet<Bit64>.Empty.Set(new ComponentId(i));
            var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
            var id = _metadata.GetOrCreateArchetypeId(hashedKey, matchedQueries);
            ids.Add(id);
        }

        await Assert.That(ids.Count).IsEqualTo(5);
    }

    #endregion

    #region GetLayoutData Tests

    [Test]
    public async Task GetLayoutData_ValidId_ReturnsNonZeroPointer()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var matchedQueries = new List<int>();
        var id = _metadata.GetOrCreateArchetypeId(hashedKey, matchedQueries);

        var layoutData = _metadata.GetLayoutData(id);

        await Assert.That(layoutData).IsNotEqualTo(IntPtr.Zero);
    }

    [Test]
    public async Task GetLayout_ValidId_ReturnsLayout()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var matchedQueries = new List<int>();
        var id = _metadata.GetOrCreateArchetypeId(hashedKey, matchedQueries);

        var layout = _metadata.GetLayout(id);
        var hasPosition = layout.HasComponent<TestPosition>();

        await Assert.That(hasPosition).IsTrue();
    }

    #endregion

    #region GetOrCreateArchetypeIdWithAdd Tests

    [Test]
    public async Task GetOrCreateArchetypeIdWithAdd_AddsComponent()
    {
        var mask = ImmutableBitSet<Bit64>.Empty;
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var matchedQueries = new List<int>();
        var sourceId = _metadata.GetOrCreateArchetypeId(hashedKey, matchedQueries);

        var targetId = _metadata.GetOrCreateArchetypeIdWithAdd(sourceId, TestPosition.TypeId, matchedQueries);
        var layout = _metadata.GetLayout(targetId);
        var hasPosition = layout.HasComponent<TestPosition>();

        await Assert.That(hasPosition).IsTrue();
    }

    [Test]
    public async Task GetOrCreateArchetypeIdWithAdd_CachesEdge()
    {
        var mask = ImmutableBitSet<Bit64>.Empty;
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var matchedQueries = new List<int>();
        var sourceId = _metadata.GetOrCreateArchetypeId(hashedKey, matchedQueries);

        // First call creates edge
        var target1 = _metadata.GetOrCreateArchetypeIdWithAdd(sourceId, TestPosition.TypeId, matchedQueries);
        // Second call should use cached edge
        var target2 = _metadata.GetOrCreateArchetypeIdWithAdd(sourceId, TestPosition.TypeId, matchedQueries);

        await Assert.That(target1).IsEqualTo(target2);
    }

    #endregion

    #region GetOrCreateArchetypeIdWithRemove Tests

    [Test]
    public async Task GetOrCreateArchetypeIdWithRemove_RemovesComponent()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var matchedQueries = new List<int>();
        var sourceId = _metadata.GetOrCreateArchetypeId(hashedKey, matchedQueries);

        var targetId = _metadata.GetOrCreateArchetypeIdWithRemove(sourceId, TestPosition.TypeId, matchedQueries);
        var layout = _metadata.GetLayout(targetId);
        var hasPosition = layout.HasComponent<TestPosition>();

        await Assert.That(hasPosition).IsFalse();
    }

    #endregion

    #region Query Tests

    [Test]
    public async Task GetOrCreateQueryId_ReturnsValidId()
    {
        var description = new QueryBuilder<ImmutableBitSet<Bit64>>()
            .With<TestPosition>()
            .Description;
        var hashedDesc = (HashedKey<ImmutableQueryDescription<ImmutableBitSet<Bit64>>>)description;

        var queryId = _metadata.GetOrCreateQueryId(hashedDesc);

        await Assert.That(queryId).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task GetOrCreateQueryId_SameDescription_ReturnsSameId()
    {
        var description = new QueryBuilder<ImmutableBitSet<Bit64>>()
            .With<TestPosition>()
            .Description;
        var hashedDesc = (HashedKey<ImmutableQueryDescription<ImmutableBitSet<Bit64>>>)description;

        var id1 = _metadata.GetOrCreateQueryId(hashedDesc);
        var id2 = _metadata.GetOrCreateQueryId(hashedDesc);

        await Assert.That(id1).IsEqualTo(id2);
    }

    [Test]
    public async Task GetMatchedArchetypeIds_ReturnsMatchingArchetypes()
    {
        // Create a query first
        var description = new QueryBuilder<ImmutableBitSet<Bit64>>()
            .With<TestPosition>()
            .Description;
        var hashedDesc = (HashedKey<ImmutableQueryDescription<ImmutableBitSet<Bit64>>>)description;
        var queryId = _metadata.GetOrCreateQueryId(hashedDesc);

        // Create an archetype that matches
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var matchedQueries = new List<int>();
        _metadata.GetOrCreateArchetypeId(hashedKey, matchedQueries);

        var matchedArchetypes = _metadata.GetMatchedArchetypeIds(queryId);

        await Assert.That(matchedArchetypes.Count).IsGreaterThan(0);
    }

    #endregion

    #region TryGetArchetypeId Tests

    [Test]
    public async Task TryGetArchetypeId_Existing_ReturnsTrue()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var matchedQueries = new List<int>();
        _metadata.GetOrCreateArchetypeId(hashedKey, matchedQueries);

        var found = _metadata.TryGetArchetypeId(hashedKey, out int archetypeId);

        await Assert.That(found).IsTrue();
        await Assert.That(archetypeId).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task TryGetArchetypeId_NonExisting_ReturnsFalse()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;

        var found = _metadata.TryGetArchetypeId(hashedKey, out _);

        await Assert.That(found).IsFalse();
    }

    #endregion

    #region Clear Tests

    [Test]
    public async Task Clear_RemovesAllData()
    {
        var mask = ImmutableBitSet<Bit64>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<ImmutableBitSet<Bit64>>)mask;
        var matchedQueries = new List<int>();
        _metadata.GetOrCreateArchetypeId(hashedKey, matchedQueries);

        _metadata.Clear();

        await Assert.That(_metadata.ArchetypeCount).IsEqualTo(0);
    }

    #endregion
}
