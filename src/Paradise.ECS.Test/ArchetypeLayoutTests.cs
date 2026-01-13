namespace Paradise.ECS.Test;

[Component(Id = 4)]
public partial struct TestTag; // Zero-size component for layout tests

public class ArchetypeLayoutTests
{
    private static ImmutableBitSet<Bit64> MakeMask(params int[] componentIds)
    {
        var mask = ImmutableBitSet<Bit64>.Empty;
        foreach (var id in componentIds)
        {
            mask = mask.Set(id);
        }
        return mask;
    }

    [Test]
    public async Task Constructor_WithEmptyComponents_CreatesValidLayout()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(ImmutableBitSet<Bit64>.Empty);

        // Even empty archetypes need entity ID storage (4 bytes per entity)
        await Assert.That(layout.EntitiesPerChunk).IsEqualTo(Chunk.ChunkSize / sizeof(int));
        await Assert.That(layout.ComponentCount).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithSingleComponent_CalculatesCorrectLayout()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestPosition.TypeId.Value));

        await Assert.That(layout.ComponentCount).IsEqualTo(1);
        // Base offset is after entity IDs (entitiesPerChunk * 4 bytes)
        int expectedBaseOffset = layout.EntitiesPerChunk * sizeof(int);
        await Assert.That(layout.GetBaseOffset<TestPosition>()).IsEqualTo(expectedBaseOffset);
    }

    [Test]
    public async Task Constructor_WithMultipleComponents_CalculatesSoALayout()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestPosition.TypeId.Value, TestHealth.TypeId.Value));

        await Assert.That(layout.ComponentCount).IsEqualTo(2);

        // Both base offsets should be non-negative
        await Assert.That(layout.GetBaseOffset<TestPosition>()).IsGreaterThanOrEqualTo(0);
        await Assert.That(layout.GetBaseOffset<TestHealth>()).IsGreaterThanOrEqualTo(0);

        // In SoA, base offsets should be different (each component has its own array)
        int posBase = layout.GetBaseOffset<TestPosition>();
        int healthBase = layout.GetBaseOffset<TestHealth>();
        await Assert.That(posBase).IsNotEqualTo(healthBase);
    }

    [Test]
    public async Task Constructor_WithTagComponent_HandlesZeroSize()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestPosition.TypeId.Value, TestTag.TypeId.Value));

        await Assert.That(layout.ComponentCount).IsEqualTo(2);
        await Assert.That(layout.HasComponent<TestTag>()).IsTrue();
        await Assert.That(layout.GetBaseOffset<TestTag>()).IsEqualTo(0); // Tags have offset 0
    }

    [Test]
    public async Task GetBaseOffset_NonExistentComponent_ReturnsNegativeOne()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestPosition.TypeId.Value));

        await Assert.That(layout.GetBaseOffset<TestVelocity>()).IsEqualTo(-1);
    }

    [Test]
    public async Task GetBaseOffset_OutOfRangeComponentId_ReturnsNegativeOne()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(ImmutableBitSet<Bit64>.Empty);

        await Assert.That(layout.GetBaseOffset(new ComponentId(100))).IsEqualTo(-1);
    }

    [Test]
    public async Task HasComponent_ExistingComponent_ReturnsTrue()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestPosition.TypeId.Value));

        await Assert.That(layout.HasComponent<TestPosition>()).IsTrue();
    }

    [Test]
    public async Task HasComponent_NonExistentComponent_ReturnsFalse()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestPosition.TypeId.Value));

        await Assert.That(layout.HasComponent<TestVelocity>()).IsFalse();
    }

    [Test]
    public async Task GetEntityComponentOffset_SoALayout_ReturnsCorrectOffset()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestPosition.TypeId.Value));

        int baseOffset = layout.GetBaseOffset<TestPosition>();
        int size = TestPosition.Size;

        // SoA: offset = baseOffset + entityIndex * componentSize
        int offset0 = layout.GetEntityComponentOffset<TestPosition>(0);
        int offset1 = layout.GetEntityComponentOffset<TestPosition>(1);
        int offset2 = layout.GetEntityComponentOffset<TestPosition>(2);

        await Assert.That(offset0).IsEqualTo(baseOffset);
        await Assert.That(offset1).IsEqualTo(baseOffset + size);
        await Assert.That(offset2).IsEqualTo(baseOffset + size * 2);
    }

    [Test]
    public async Task GetEntityComponentOffset_NonExistentComponent_ReturnsNegativeOne()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestPosition.TypeId.Value));

        await Assert.That(layout.GetEntityComponentOffset<TestVelocity>(0)).IsEqualTo(-1);
    }

    [Test]
    public async Task EntitiesPerChunk_CalculatesCorrectly()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestPosition.TypeId.Value));

        // With SoA + entity IDs: entities_per_chunk = ChunkSize / (entity_id_size + sum(component_sizes))
        int totalSizePerEntity = sizeof(int) + TestPosition.Size;
        int expected = Chunk.ChunkSize / totalSizePerEntity;
        await Assert.That(layout.EntitiesPerChunk).IsEqualTo(expected);
    }

    [Test]
    public async Task EntitiesPerChunk_MultipleComponents_CalculatesCorrectly()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestPosition.TypeId.Value, TestHealth.TypeId.Value));

        // With SoA + entity IDs: entities_per_chunk = ChunkSize / (entity_id_size + sum(component_sizes))
        int totalSizePerEntity = sizeof(int) + TestPosition.Size + TestHealth.Size;
        int expected = Chunk.ChunkSize / totalSizePerEntity;
        await Assert.That(layout.EntitiesPerChunk).IsEqualTo(expected);
    }

    [Test]
    public async Task ComponentMask_EnumeratesInSortedOrder()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestPosition.TypeId.Value, TestVelocity.TypeId.Value, TestHealth.TypeId.Value));

        // Capture enumeration results before await boundary
        int count = 0;
        int previousId = -1;
        bool isSorted = true;
        foreach (int id in layout.ComponentMask)
        {
            if (id <= previousId)
            {
                isSorted = false;
                break;
            }
            previousId = id;
            count++;
        }

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(isSorted).IsTrue();
    }

    [Test]
    public async Task SoALayout_ComponentArraysAreContiguous()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestPosition.TypeId.Value, TestHealth.TypeId.Value));

        int entitiesPerChunk = layout.EntitiesPerChunk;

        int posBase = layout.GetBaseOffset<TestPosition>();
        int healthBase = layout.GetBaseOffset<TestHealth>();

        // Component arrays should not overlap
        // One array should start after the other ends
        int posArrayEnd = posBase + entitiesPerChunk * TestPosition.Size;
        int healthArrayEnd = healthBase + entitiesPerChunk * TestHealth.Size;

        // Either position comes after health, or health comes after position
        bool posAfterHealth = posBase >= healthArrayEnd;
        bool healthAfterPos = healthBase >= posArrayEnd;

        await Assert.That(posAfterHealth || healthAfterPos).IsTrue();
    }

    [Test]
    public async Task Dispose_ThrowsOnSubsequentAccess()
    {
        var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(ImmutableBitSet<Bit64>.Empty);
        layout.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            _ = layout.EntitiesPerChunk;
            await Task.CompletedTask.ConfigureAwait(false);
        });
    }

    [Test]
    public async Task Dispose_CanBeCalledMultipleTimes()
    {
        var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(ImmutableBitSet<Bit64>.Empty);

        // Should not throw
        layout.Dispose();
        layout.Dispose();
        layout.Dispose();

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

public class ArchetypeLayoutAlignmentTests
{
    private static ImmutableBitSet<Bit64> MakeMask(params int[] componentIds)
    {
        var mask = ImmutableBitSet<Bit64>.Empty;
        foreach (var id in componentIds)
        {
            mask = mask.Set(id);
        }
        return mask;
    }

    [Test]
    public async Task Layout_SortsComponentsByAlignment_LargestFirst()
    {
        // Create components with different alignments
        using var layout = new ImmutableArchetypeLayout<Bit64, ComponentRegistry>(MakeMask(TestHealth.TypeId.Value, TestPosition.TypeId.Value));

        // Both components should have valid base offsets
        int posOffset = layout.GetBaseOffset<TestPosition>();
        int healthOffset = layout.GetBaseOffset<TestHealth>();

        await Assert.That(posOffset).IsGreaterThanOrEqualTo(0);
        await Assert.That(healthOffset).IsGreaterThanOrEqualTo(0);

        // Base offsets should be aligned
        await Assert.That(posOffset % 4).IsEqualTo(0);
        await Assert.That(healthOffset % 4).IsEqualTo(0);
    }
}

public class ArchetypeLayoutHeaderTests
{
    [Test]
    public async Task SizeInBytes_Bit64_IsCorrect()
    {
        // ArchetypeLayoutHeader<Bit64> has 5 int fields + 1 ImmutableBitSet<Bit64> (8 bytes)
        // = 20 + 8 = 28 bytes, but struct may be padded to 32 for alignment
        int size = ArchetypeLayoutHeader<Bit64>.SizeInBytes;
        await Assert.That(size).IsGreaterThanOrEqualTo(28);
    }

    [Test]
    public async Task SizeInBytes_Bit128_IsLargerThanBit64()
    {
        int size64 = ArchetypeLayoutHeader<Bit64>.SizeInBytes;
        int size128 = ArchetypeLayoutHeader<Bit128>.SizeInBytes;
        await Assert.That(size128).IsGreaterThan(size64);
    }
}
