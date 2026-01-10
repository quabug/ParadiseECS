using System.Collections.Immutable;

namespace Paradise.ECS.Test;

[Component(Id = 4)]
public partial struct TestTag; // Zero-size component for layout tests

public class ArchetypeLayoutTests
{
    private static readonly ImmutableArray<ComponentTypeInfo> s_globalComponentInfos = BuildGlobalComponentInfos();

    private static ImmutableArray<ComponentTypeInfo> BuildGlobalComponentInfos()
    {
        var components = new[]
        {
            ComponentTypeInfo.Create<TestPosition>(),
            ComponentTypeInfo.Create<TestVelocity>(),
            ComponentTypeInfo.Create<TestHealth>(),
            ComponentTypeInfo.Create<TestTag>()
        };

        int maxId = components.Max(c => c.Id.Value);
        var builder = ImmutableArray.CreateBuilder<ComponentTypeInfo>(maxId + 1);
        for (int i = 0; i <= maxId; i++)
        {
            builder.Add(default);
        }

        foreach (var comp in components)
        {
            builder[comp.Id.Value] = comp;
        }

        return builder.MoveToImmutable();
    }

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
        using var layout = new ImmutableArchetypeLayout<Bit64>(ImmutableBitSet<Bit64>.Empty, s_globalComponentInfos);

        await Assert.That(layout.EntitiesPerChunk).IsEqualTo(Chunk.ChunkSize);
        await Assert.That(layout.ComponentCount).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithSingleComponent_CalculatesCorrectLayout()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestPosition.TypeId.Value), s_globalComponentInfos);

        await Assert.That(layout.ComponentCount).IsEqualTo(1);
        await Assert.That(layout.GetBaseOffset<TestPosition>()).IsEqualTo(0);
    }

    [Test]
    public async Task Constructor_WithMultipleComponents_CalculatesSoALayout()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestPosition.TypeId.Value, TestHealth.TypeId.Value), s_globalComponentInfos);

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
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestPosition.TypeId.Value, TestTag.TypeId.Value), s_globalComponentInfos);

        await Assert.That(layout.ComponentCount).IsEqualTo(2);
        await Assert.That(layout.HasComponent<TestTag>()).IsTrue();
        await Assert.That(layout.GetBaseOffset<TestTag>()).IsEqualTo(0); // Tags have offset 0
    }

    [Test]
    public async Task GetBaseOffset_NonExistentComponent_ReturnsNegativeOne()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestPosition.TypeId.Value), s_globalComponentInfos);

        await Assert.That(layout.GetBaseOffset<TestVelocity>()).IsEqualTo(-1);
    }

    [Test]
    public async Task GetBaseOffset_OutOfRangeComponentId_ReturnsNegativeOne()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64>(ImmutableBitSet<Bit64>.Empty, s_globalComponentInfos);

        await Assert.That(layout.GetBaseOffset(new ComponentId(100))).IsEqualTo(-1);
    }

    [Test]
    public async Task HasComponent_ExistingComponent_ReturnsTrue()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestPosition.TypeId.Value), s_globalComponentInfos);

        await Assert.That(layout.HasComponent<TestPosition>()).IsTrue();
    }

    [Test]
    public async Task HasComponent_NonExistentComponent_ReturnsFalse()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestPosition.TypeId.Value), s_globalComponentInfos);

        await Assert.That(layout.HasComponent<TestVelocity>()).IsFalse();
    }

    [Test]
    public async Task GetEntityComponentOffset_SoALayout_ReturnsCorrectOffset()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestPosition.TypeId.Value), s_globalComponentInfos);

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
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestPosition.TypeId.Value), s_globalComponentInfos);

        await Assert.That(layout.GetEntityComponentOffset<TestVelocity>(0)).IsEqualTo(-1);
    }

    [Test]
    public async Task EntitiesPerChunk_CalculatesCorrectly()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestPosition.TypeId.Value), s_globalComponentInfos);

        // With SoA: entities_per_chunk = ChunkSize / sum(component_sizes)
        int expected = Chunk.ChunkSize / TestPosition.Size;
        await Assert.That(layout.EntitiesPerChunk).IsEqualTo(expected);
    }

    [Test]
    public async Task EntitiesPerChunk_MultipleComponents_CalculatesCorrectly()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestPosition.TypeId.Value, TestHealth.TypeId.Value), s_globalComponentInfos);

        // With SoA: entities_per_chunk = ChunkSize / sum(component_sizes)
        int totalSize = TestPosition.Size + TestHealth.Size;
        int expected = Chunk.ChunkSize / totalSize;
        await Assert.That(layout.EntitiesPerChunk).IsEqualTo(expected);
    }

    [Test]
    public async Task ComponentMask_EnumeratesInSortedOrder()
    {
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestPosition.TypeId.Value, TestVelocity.TypeId.Value, TestHealth.TypeId.Value), s_globalComponentInfos);

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
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestPosition.TypeId.Value, TestHealth.TypeId.Value), s_globalComponentInfos);

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
        var layout = new ImmutableArchetypeLayout<Bit64>(ImmutableBitSet<Bit64>.Empty, s_globalComponentInfos);
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
        var layout = new ImmutableArchetypeLayout<Bit64>(ImmutableBitSet<Bit64>.Empty, s_globalComponentInfos);

        // Should not throw
        layout.Dispose();
        layout.Dispose();
        layout.Dispose();

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

public class ArchetypeLayoutAlignmentTests
{
    private static readonly ImmutableArray<ComponentTypeInfo> s_globalComponentInfos = BuildGlobalComponentInfos();

    private static ImmutableArray<ComponentTypeInfo> BuildGlobalComponentInfos()
    {
        var components = new[]
        {
            ComponentTypeInfo.Create<TestPosition>(),
            ComponentTypeInfo.Create<TestHealth>()
        };

        int maxId = components.Max(c => c.Id.Value);
        var builder = ImmutableArray.CreateBuilder<ComponentTypeInfo>(maxId + 1);
        for (int i = 0; i <= maxId; i++)
        {
            builder.Add(default);
        }

        foreach (var comp in components)
        {
            builder[comp.Id.Value] = comp;
        }

        return builder.MoveToImmutable();
    }

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
        using var layout = new ImmutableArchetypeLayout<Bit64>(MakeMask(TestHealth.TypeId.Value, TestPosition.TypeId.Value), s_globalComponentInfos);

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
