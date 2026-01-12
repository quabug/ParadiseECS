namespace Paradise.ECS.Test;

public class ArchetypeStoreTests : IDisposable
{
    private readonly ChunkManager _chunkManager;
    private readonly List<ImmutableArchetypeLayout<Bit64, ComponentRegistry>> _layouts = [];

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

        store.AllocateEntity(out var chunkHandle, out int indexInChunk);

        await Assert.That(chunkHandle.IsValid).IsTrue();
        await Assert.That(indexInChunk).IsEqualTo(0);
        await Assert.That(store.EntityCount).IsEqualTo(1);
        await Assert.That(store.ChunkCount).IsEqualTo(1);
    }

    [Test]
    public async Task AllocateEntity_MultipleEntities_IncrementsIndex()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        store.AllocateEntity(out var chunk1, out int idx1);
        store.AllocateEntity(out var chunk2, out int idx2);
        store.AllocateEntity(out var chunk3, out int idx3);

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
            store.AllocateEntity(out _, out _);
        }

        await Assert.That(store.ChunkCount).IsEqualTo(1);

        // Allocate one more - should trigger new chunk
        store.AllocateEntity(out _, out int newIndex);

        await Assert.That(store.ChunkCount).IsEqualTo(2);
        await Assert.That(newIndex).IsEqualTo(0); // First in new chunk
    }

    [Test]
    public async Task RemoveEntity_LastEntity_DoesNotMove()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        store.AllocateEntity(out _, out _);
        store.AllocateEntity(out _, out _);

        bool moved = store.RemoveEntity(1);

        await Assert.That(moved).IsFalse();
        await Assert.That(store.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task RemoveEntity_MiddleEntity_ReturnsMovedInfo()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        store.AllocateEntity(out _, out _); // index 0
        store.AllocateEntity(out _, out _); // index 1
        store.AllocateEntity(out _, out _); // index 2

        // Remove middle entity (index 1) - should swap with last (index 2)
        bool moved = store.RemoveEntity(1);

        await Assert.That(moved).IsTrue();
        await Assert.That(store.EntityCount).IsEqualTo(2);
    }

    [Test]
    public async Task RemoveEntity_InvalidIndex_ReturnsFalse()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        store.AllocateEntity(out _, out _);

        bool moved = store.RemoveEntity(99);

        await Assert.That(moved).IsFalse();
    }

    [Test]
    public async Task RemoveEntity_NegativeIndex_ReturnsFalse()
    {
        var store = CreateStore(ComponentTypeInfo.Create<TestPosition>());

        store.AllocateEntity(out _, out _);

        bool moved = store.RemoveEntity(-1);

        await Assert.That(moved).IsFalse();
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
            store.AllocateEntity(out _, out _);
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

        store.AllocateEntity(out var allocatedChunk, out _);

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
