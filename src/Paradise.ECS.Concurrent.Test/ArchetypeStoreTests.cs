namespace Paradise.ECS.Concurrent.Test;

public sealed class ArchetypeStoreTests : IDisposable
{
    private readonly ChunkManager<DefaultConfig> _chunkManager;
    private readonly List<nint> _layoutDataList = [];
    private int _nextEntityId;

    public ArchetypeStoreTests()
    {
        _chunkManager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 16 });
    }

    public void Dispose()
    {
        foreach (var layoutData in _layoutDataList)
        {
            ImmutableArchetypeLayout<Bit64, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, layoutData);
        }
        _chunkManager?.Dispose();
    }

    private Archetype<Bit64, ComponentRegistry, DefaultConfig> CreateStore(params ComponentTypeInfo[] components)
    {
        var mask = ImmutableBitSet<Bit64>.Empty;
        foreach (var comp in components)
        {
            mask = mask.Set(comp.Id.Value);
        }
        var layoutData = ImmutableArchetypeLayout<Bit64, ComponentRegistry, DefaultConfig>.Create(NativeMemoryAllocator.Shared, mask);
        _layoutDataList.Add(layoutData);
        return new Archetype<Bit64, ComponentRegistry, DefaultConfig>(_layoutDataList.Count - 1, layoutData, _chunkManager);
    }

    private Entity CreateTestEntity()
    {
        return new Entity(_nextEntityId++, 1);
    }

    [Test]
    public async Task Constructor_CreatesEmptyStore()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        await Assert.That(store.EntityCount).IsEqualTo(0);
        await Assert.That(store.ChunkCount).IsEqualTo(0);
    }

    [Test]
    public async Task AllocateEntity_ReturnsValidLocation()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        int globalIndex = store.AllocateEntity(CreateTestEntity());
        var (chunkIndex, indexInChunk) = store.GetChunkLocation(globalIndex);
        var chunkHandle = store.GetChunk(chunkIndex);

        await Assert.That(chunkHandle.IsValid).IsTrue();
        await Assert.That(globalIndex).IsEqualTo(0);
        await Assert.That(indexInChunk).IsEqualTo(0);
        await Assert.That(store.EntityCount).IsEqualTo(1);
        await Assert.That(store.ChunkCount).IsEqualTo(1);
    }

    [Test]
    public async Task AllocateEntity_MultipleEntities_IncrementsIndex()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        int idx1 = store.AllocateEntity(CreateTestEntity());
        int idx2 = store.AllocateEntity(CreateTestEntity());
        int idx3 = store.AllocateEntity(CreateTestEntity());

        await Assert.That(idx1).IsEqualTo(0);
        await Assert.That(idx2).IsEqualTo(1);
        await Assert.That(idx3).IsEqualTo(2);

        // All in same chunk since we have room
        var (chunk1Idx, _) = store.GetChunkLocation(idx1);
        var (chunk2Idx, _) = store.GetChunkLocation(idx2);
        var (chunk3Idx, _) = store.GetChunkLocation(idx3);
        await Assert.That(chunk1Idx).IsEqualTo(chunk2Idx);
        await Assert.That(chunk2Idx).IsEqualTo(chunk3Idx);
    }

    [Test]
    public async Task AllocateEntity_ExceedsChunkCapacity_AllocatesNewChunk()
    {
        // Create a large component to limit entities per chunk
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());
        var layout = store.Layout;

        int entitiesPerChunk = layout.EntitiesPerChunk;

        // Fill first chunk
        for (int i = 0; i < entitiesPerChunk; i++)
        {
            _ = store.AllocateEntity(CreateTestEntity());
        }

        await Assert.That(store.ChunkCount).IsEqualTo(1);

        // Allocate one more - should trigger new chunk
        int globalIndex = store.AllocateEntity(CreateTestEntity());
        var (_, newIndexInChunk) = store.GetChunkLocation(globalIndex);

        await Assert.That(store.ChunkCount).IsEqualTo(2);
        await Assert.That(newIndexInChunk).IsEqualTo(0); // First in new chunk
    }

    [Test]
    public async Task RemoveEntity_LastEntity_ReturnsNoMovedEntity()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        _ = store.AllocateEntity(CreateTestEntity());
        _ = store.AllocateEntity(CreateTestEntity());

        int movedEntityId = store.RemoveEntity(1);

        await Assert.That(movedEntityId).IsEqualTo(-1);
        await Assert.That(store.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task RemoveEntity_MiddleEntity_ReturnsMovedEntityId()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        var entity0 = CreateTestEntity();
        var entity1 = CreateTestEntity();
        var entity2 = CreateTestEntity();

        _ = store.AllocateEntity(entity0); // index 0
        _ = store.AllocateEntity(entity1); // index 1
        _ = store.AllocateEntity(entity2); // index 2

        // Remove middle entity (index 1) - should swap with last (index 2)
        int movedEntityId = store.RemoveEntity(1);

        await Assert.That(movedEntityId).IsGreaterThanOrEqualTo(0);
        await Assert.That(movedEntityId).IsEqualTo(entity2.Id);
        await Assert.That(store.EntityCount).IsEqualTo(2);
    }

    [Test]
    public async Task RemoveEntity_InvalidIndex_ReturnsNoMovedEntity()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        _ = store.AllocateEntity(CreateTestEntity());

        int movedEntityId = store.RemoveEntity(99);

        await Assert.That(movedEntityId).IsEqualTo(-1);
    }

    [Test]
    public async Task RemoveEntity_NegativeIndex_ReturnsNoMovedEntity()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        _ = store.AllocateEntity(CreateTestEntity());

        int movedEntityId = store.RemoveEntity(-1);

        await Assert.That(movedEntityId).IsEqualTo(-1);
    }

    [Test]
    public async Task RemoveEntity_TrimsEmptyChunks()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());
        var layout = store.Layout;

        int entitiesPerChunk = layout.EntitiesPerChunk;

        // Fill one chunk plus one entity in second chunk
        for (int i = 0; i <= entitiesPerChunk; i++)
        {
            _ = store.AllocateEntity(CreateTestEntity());
        }

        await Assert.That(store.ChunkCount).IsEqualTo(2);

        // Remove the last entity (in second chunk)
        store.RemoveEntity(entitiesPerChunk);

        await Assert.That(store.ChunkCount).IsEqualTo(1); // Second chunk trimmed
    }

    [Test]
    public async Task GetChunk_ValidIndex_ReturnsHandle()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        int globalIndex = store.AllocateEntity(CreateTestEntity());
        var (chunkIndex, _) = store.GetChunkLocation(globalIndex);
        var allocatedChunk = store.GetChunk(chunkIndex);

        var retrievedChunk = store.GetChunk(0);

        await Assert.That(retrievedChunk.Id).IsEqualTo(allocatedChunk.Id);
    }

    [Test]
    public async Task GetGlobalIndex_CalculatesCorrectly()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());
        var layout = store.Layout;

        int entitiesPerChunk = layout.EntitiesPerChunk;

        int globalIdx = store.GetGlobalIndex(0, 5);
        await Assert.That(globalIdx).IsEqualTo(5);

        globalIdx = store.GetGlobalIndex(1, 5);
        await Assert.That(globalIdx).IsEqualTo(entitiesPerChunk + 5);
    }

    [Test]
    public async Task GetChunkLocation_CalculatesCorrectly()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());
        var layout = store.Layout;

        int entitiesPerChunk = layout.EntitiesPerChunk;

        var (chunkIdx, indexInChunk) = store.GetChunkLocation(5);
        await Assert.That(chunkIdx).IsEqualTo(0);
        await Assert.That(indexInChunk).IsEqualTo(5);

        (chunkIdx, indexInChunk) = store.GetChunkLocation(entitiesPerChunk + 5);
        await Assert.That(chunkIdx).IsEqualTo(1);
        await Assert.That(indexInChunk).IsEqualTo(5);
    }

    [Test]
    public async Task Layout_Property_ReturnsValidLayout()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());
        var layout = store.Layout;

        // Extract values before await (ref struct can't cross await boundary)
        int componentCount = layout.ComponentCount;
        bool hasPosition = layout.HasComponent<TestPosition>();

        await Assert.That(componentCount).IsEqualTo(1);
        await Assert.That(hasPosition).IsTrue();
    }

    [Test]
    public async Task Id_Property_ReturnsId()
    {
        // First store gets ID 0
        var store1 = CreateStore(ComponentTypeInfo.Create<TestPosition>());
        await Assert.That(store1.Id).IsEqualTo(0);

        // Second store gets ID 1
        var store2 = CreateStore(ComponentTypeInfo.Create<TestVelocity>());
        await Assert.That(store2.Id).IsEqualTo(1);
    }
}
