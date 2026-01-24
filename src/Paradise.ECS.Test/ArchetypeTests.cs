namespace Paradise.ECS.Test;

/// <summary>
/// Tests for Archetype.
/// </summary>
public sealed class ArchetypeTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly ChunkManager _chunkManager = ChunkManager.Create(s_config);
    private readonly SharedArchetypeMetadata<SmallBitSet<ulong>, DefaultConfig> _sharedMetadata = new(ComponentRegistry.Shared.TypeInfos, s_config);
    private readonly ArchetypeRegistry<SmallBitSet<ulong>, DefaultConfig> _registry;

    public ArchetypeTests()
    {
        _registry = new ArchetypeRegistry<SmallBitSet<ulong>, DefaultConfig>(_sharedMetadata, _chunkManager);
    }

    public void Dispose()
    {
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
    }

    #region Construction Tests

    [Test]
    public async Task Archetype_NewlyCreated_HasZeroEntities()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;

        var archetype = _registry.GetOrCreate(hashedKey);

        await Assert.That(archetype.EntityCount).IsEqualTo(0);
        await Assert.That(archetype.ChunkCount).IsEqualTo(0);
    }

    [Test]
    public async Task Archetype_HasCorrectId()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;

        var archetype = _registry.GetOrCreate(hashedKey);

        await Assert.That(archetype.Id).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Archetype_HasCorrectLayout()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;

        var archetype = _registry.GetOrCreate(hashedKey);

        await Assert.That(archetype.Layout.HasComponent(TestPosition.TypeId)).IsTrue();
        await Assert.That(archetype.Layout.HasComponent(TestVelocity.TypeId)).IsFalse();
    }

    #endregion

    #region AllocateEntity Tests

    [Test]
    public async Task AllocateEntity_FirstEntity_ReturnsIndexZero()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        var entity = new Entity(0, 1);
        var index = archetype.AllocateEntity(entity);

        await Assert.That(index).IsEqualTo(0);
    }

    [Test]
    public async Task AllocateEntity_IncrementsEntityCount()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        archetype.AllocateEntity(new Entity(0, 1));
        archetype.AllocateEntity(new Entity(1, 1));

        await Assert.That(archetype.EntityCount).IsEqualTo(2);
    }

    [Test]
    public async Task AllocateEntity_MultipleEntities_ReturnsSequentialIndices()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        var idx0 = archetype.AllocateEntity(new Entity(0, 1));
        var idx1 = archetype.AllocateEntity(new Entity(1, 1));
        var idx2 = archetype.AllocateEntity(new Entity(2, 1));

        await Assert.That(idx0).IsEqualTo(0);
        await Assert.That(idx1).IsEqualTo(1);
        await Assert.That(idx2).IsEqualTo(2);
    }

    [Test]
    public async Task AllocateEntity_CreatesChunkWhenNeeded()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        archetype.AllocateEntity(new Entity(0, 1));

        await Assert.That(archetype.ChunkCount).IsEqualTo(1);
    }

    #endregion

    #region RemoveEntity Tests

    [Test]
    public async Task RemoveEntity_SingleEntity_DecrementsCount()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        archetype.AllocateEntity(new Entity(0, 1));
        archetype.RemoveEntity(0);

        await Assert.That(archetype.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task RemoveEntity_LastEntity_ReturnsNegativeOne()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        archetype.AllocateEntity(new Entity(0, 1));
        var movedId = archetype.RemoveEntity(0);

        await Assert.That(movedId).IsEqualTo(-1);
    }

    [Test]
    public async Task RemoveEntity_SwapRemove_ReturnsMovedEntityId()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        archetype.AllocateEntity(new Entity(0, 1));
        archetype.AllocateEntity(new Entity(1, 1));
        archetype.AllocateEntity(new Entity(2, 1));

        // Remove first entity, last entity (id=2) should be moved
        var movedId = archetype.RemoveEntity(0);

        await Assert.That(movedId).IsEqualTo(2);
        await Assert.That(archetype.EntityCount).IsEqualTo(2);
    }

    [Test]
    public async Task RemoveEntity_InvalidIndex_ReturnsNegativeOne()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        archetype.AllocateEntity(new Entity(0, 1));

        var result = archetype.RemoveEntity(-1);
        await Assert.That(result).IsEqualTo(-1);

        result = archetype.RemoveEntity(5);
        await Assert.That(result).IsEqualTo(-1);
    }

    [Test]
    public async Task RemoveEntity_TrimsEmptyChunks()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        archetype.AllocateEntity(new Entity(0, 1));
        var chunkCountBefore = archetype.ChunkCount;

        archetype.RemoveEntity(0);

        await Assert.That(archetype.ChunkCount).IsEqualTo(0);
        await Assert.That(chunkCountBefore).IsEqualTo(1);
    }

    #endregion

    #region GetChunk Tests

    [Test]
    public async Task GetChunk_ValidIndex_ReturnsChunkHandle()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        archetype.AllocateEntity(new Entity(0, 1));

        var chunk = archetype.GetChunk(0);

        await Assert.That(chunk.Id).IsGreaterThanOrEqualTo(0);
    }

    #endregion

    #region GetGlobalIndex Tests

    [Test]
    public async Task GetGlobalIndex_FirstChunkFirstEntity_ReturnsZero()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        var globalIndex = archetype.GetGlobalIndex(0, 0);

        await Assert.That(globalIndex).IsEqualTo(0);
    }

    [Test]
    public async Task GetGlobalIndex_CalculatesCorrectly()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        int entitiesPerChunk = archetype.Layout.EntitiesPerChunk;

        // Second chunk, first entity
        var globalIndex = archetype.GetGlobalIndex(1, 0);
        await Assert.That(globalIndex).IsEqualTo(entitiesPerChunk);

        // Second chunk, second entity
        globalIndex = archetype.GetGlobalIndex(1, 1);
        await Assert.That(globalIndex).IsEqualTo(entitiesPerChunk + 1);
    }

    #endregion

    #region GetChunkLocation Tests

    [Test]
    public async Task GetChunkLocation_ZeroIndex_ReturnsFirstChunkFirstPosition()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(0);

        await Assert.That(chunkIndex).IsEqualTo(0);
        await Assert.That(indexInChunk).IsEqualTo(0);
    }

    [Test]
    public async Task GetChunkLocation_RoundTrip_Consistent()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        int entitiesPerChunk = archetype.Layout.EntitiesPerChunk;
        int testIndex = entitiesPerChunk + 5;

        var (chunkIndex, indexInChunk) = archetype.GetChunkLocation(testIndex);
        var roundTrip = archetype.GetGlobalIndex(chunkIndex, indexInChunk);

        await Assert.That(roundTrip).IsEqualTo(testIndex);
    }

    #endregion

    #region Clear Tests

    [Test]
    public async Task Clear_RemovesAllEntities()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        archetype.AllocateEntity(new Entity(0, 1));
        archetype.AllocateEntity(new Entity(1, 1));
        archetype.AllocateEntity(new Entity(2, 1));

        archetype.Clear();

        await Assert.That(archetype.EntityCount).IsEqualTo(0);
        await Assert.That(archetype.ChunkCount).IsEqualTo(0);
    }

    [Test]
    public async Task Clear_FreesAllChunks()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        // Allocate enough entities to create at least one chunk
        for (int i = 0; i < 10; i++)
        {
            archetype.AllocateEntity(new Entity(i, 1));
        }

        var chunkCountBefore = archetype.ChunkCount;

        archetype.Clear();

        await Assert.That(chunkCountBefore).IsGreaterThan(0);
        await Assert.That(archetype.ChunkCount).IsEqualTo(0);
    }

    [Test]
    public async Task Clear_CanAllocateAgain()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        archetype.AllocateEntity(new Entity(0, 1));
        archetype.Clear();

        var newIndex = archetype.AllocateEntity(new Entity(1, 1));

        await Assert.That(newIndex).IsEqualTo(0);
        await Assert.That(archetype.EntityCount).IsEqualTo(1);
    }

    #endregion

    #region Empty Archetype Tests

    [Test]
    public async Task EmptyArchetype_AllocatesEntities()
    {
        var mask = SmallBitSet<ulong>.Empty;
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        var idx = archetype.AllocateEntity(new Entity(0, 1));

        await Assert.That(idx).IsEqualTo(0);
        await Assert.That(archetype.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task EmptyArchetype_RemovesEntities()
    {
        var mask = SmallBitSet<ulong>.Empty;
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        archetype.AllocateEntity(new Entity(0, 1));
        archetype.AllocateEntity(new Entity(1, 1));

        var movedId = archetype.RemoveEntity(0);

        await Assert.That(movedId).IsEqualTo(1);
        await Assert.That(archetype.EntityCount).IsEqualTo(1);
    }

    #endregion

    #region Multi-Component Tests

    [Test]
    public async Task MultiComponent_AllocateAndRemove_Works()
    {
        var mask = SmallBitSet<ulong>.Empty
            .Set(TestPosition.TypeId)
            .Set(TestVelocity.TypeId)
            .Set(TestHealth.TypeId);
        var hashedKey = (HashedKey<SmallBitSet<ulong>>)mask;
        var archetype = _registry.GetOrCreate(hashedKey);

        archetype.AllocateEntity(new Entity(0, 1));
        archetype.AllocateEntity(new Entity(1, 1));
        archetype.AllocateEntity(new Entity(2, 1));

        var movedId = archetype.RemoveEntity(1);

        await Assert.That(movedId).IsEqualTo(2);
        await Assert.That(archetype.EntityCount).IsEqualTo(2);
    }

    #endregion
}
