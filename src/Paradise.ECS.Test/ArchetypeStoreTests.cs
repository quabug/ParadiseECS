namespace Paradise.ECS.Test;

public sealed class ArchetypeStoreTests : IDisposable
{
    private readonly ChunkManager _chunkManager;
    private readonly List<ImmutableArchetypeLayout<Bit64, ComponentRegistry>> _layouts = [];
    private int _nextEntityId;

    public ArchetypeStoreTests()
    {
        _chunkManager = new ChunkManager(initialCapacity: 16);
    }

    public void Dispose()
    {
        foreach (var layout in _layouts)
        {
            layout.Dispose();
        }
        _chunkManager?.Dispose();
    }

    private Archetype<Bit64, ComponentRegistry> CreateStore(params ComponentTypeInfo[] components)
    {
        var mask = ImmutableBitSet<Bit64>.Empty;
        foreach (var comp in components)
        {
            mask = mask.Set(comp.Id.Value);
        }
        var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(mask);
        _layouts.Add(layout);
        return new Archetype<Bit64, ComponentRegistry>(_layouts.Count - 1, layout, _chunkManager);
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

        var (chunkHandle, indexInChunk) = store.AllocateEntity(CreateTestEntity());

        await Assert.That(chunkHandle.IsValid).IsTrue();
        await Assert.That(indexInChunk).IsEqualTo(0);
        await Assert.That(store.EntityCount).IsEqualTo(1);
        await Assert.That(store.ChunkCount).IsEqualTo(1);
    }

    [Test]
    public async Task AllocateEntity_MultipleEntities_IncrementsIndex()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        var (chunk1, idx1) = store.AllocateEntity(CreateTestEntity());
        var (chunk2, idx2) = store.AllocateEntity(CreateTestEntity());
        var (chunk3, idx3) = store.AllocateEntity(CreateTestEntity());

        await Assert.That(idx1).IsEqualTo(0);
        await Assert.That(idx2).IsEqualTo(1);
        await Assert.That(idx3).IsEqualTo(2);

        // All in same chunk since we have room
        await Assert.That(chunk1.Id).IsEqualTo(chunk2.Id);
        await Assert.That(chunk2.Id).IsEqualTo(chunk3.Id);
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
        var (_, newIndex) = store.AllocateEntity(CreateTestEntity());

        await Assert.That(store.ChunkCount).IsEqualTo(2);
        await Assert.That(newIndex).IsEqualTo(0); // First in new chunk
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

        var (allocatedChunk, _) = store.AllocateEntity(CreateTestEntity());

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

        store.GetChunkLocation(5, out int chunkIdx, out int indexInChunk);
        await Assert.That(chunkIdx).IsEqualTo(0);
        await Assert.That(indexInChunk).IsEqualTo(5);

        store.GetChunkLocation(entitiesPerChunk + 5, out chunkIdx, out indexInChunk);
        await Assert.That(chunkIdx).IsEqualTo(1);
        await Assert.That(indexInChunk).IsEqualTo(5);
    }

    [Test]
    public async Task Layout_Property_ReturnsValidLayout()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());
        var layout = store.Layout;

        await Assert.That(layout.ComponentCount).IsEqualTo(1);
        await Assert.That(layout.HasComponent<TestPosition>()).IsTrue();
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
