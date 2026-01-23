namespace Paradise.ECS.Test;

/// <summary>
/// Tests for ImmutableArchetypeLayout.
/// </summary>
public sealed class ImmutableArchetypeLayoutTests
{
    [Test]
    public async Task Create_EmptyMask_CreatesValidLayout()
    {
        var mask = SmallBitSet<ulong>.Empty;
        var data = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Create(NativeMemoryAllocator.Shared, mask);

        try
        {
            var layout = new ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>(data);
            var componentCount = layout.ComponentCount;
            var isEmpty = layout.ComponentMask.IsEmpty;
            var entitiesPerChunk = layout.EntitiesPerChunk;

            await Assert.That(componentCount).IsEqualTo(0);
            await Assert.That(isEmpty).IsTrue();
            await Assert.That(entitiesPerChunk).IsGreaterThan(0);
        }
        finally
        {
            ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, data);
        }
    }

    [Test]
    public async Task Create_SingleComponent_CreatesValidLayout()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var data = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Create(NativeMemoryAllocator.Shared, mask);

        try
        {
            var layout = new ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>(data);
            var componentCount = layout.ComponentCount;
            var hasPosition = layout.HasComponent<TestPosition>();
            var hasVelocity = layout.HasComponent<TestVelocity>();

            await Assert.That(componentCount).IsEqualTo(1);
            await Assert.That(hasPosition).IsTrue();
            await Assert.That(hasVelocity).IsFalse();
        }
        finally
        {
            ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, data);
        }
    }

    [Test]
    public async Task Create_MultipleComponents_CreatesValidLayout()
    {
        var mask = SmallBitSet<ulong>.Empty
            .Set(TestPosition.TypeId)
            .Set(TestVelocity.TypeId)
            .Set(TestHealth.TypeId);
        var data = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Create(NativeMemoryAllocator.Shared, mask);

        try
        {
            var layout = new ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>(data);
            var componentCount = layout.ComponentCount;
            var hasPosition = layout.HasComponent<TestPosition>();
            var hasVelocity = layout.HasComponent<TestVelocity>();
            var hasHealth = layout.HasComponent<TestHealth>();

            await Assert.That(componentCount).IsEqualTo(3);
            await Assert.That(hasPosition).IsTrue();
            await Assert.That(hasVelocity).IsTrue();
            await Assert.That(hasHealth).IsTrue();
        }
        finally
        {
            ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, data);
        }
    }

    [Test]
    public async Task EntitiesPerChunk_ReasonableValue()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var data = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Create(NativeMemoryAllocator.Shared, mask);

        try
        {
            var layout = new ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>(data);
            var entitiesPerChunk = layout.EntitiesPerChunk;

            // With 16KB chunks and 12-byte Position + 4-byte EntityId = 16 bytes per entity
            // Should fit many entities
            await Assert.That(entitiesPerChunk).IsGreaterThan(100);
        }
        finally
        {
            ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, data);
        }
    }

    [Test]
    public async Task GetBaseOffset_ExistingComponent_ReturnsValidOffset()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var data = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Create(NativeMemoryAllocator.Shared, mask);

        try
        {
            var layout = new ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>(data);
            var offset = layout.GetBaseOffset<TestPosition>();

            await Assert.That(offset).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, data);
        }
    }

    [Test]
    public async Task GetBaseOffset_NonExistentComponent_ReturnsNegativeOne()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var data = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Create(NativeMemoryAllocator.Shared, mask);

        try
        {
            var layout = new ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>(data);
            var offset = layout.GetBaseOffset<TestVelocity>();

            await Assert.That(offset).IsEqualTo(-1);
        }
        finally
        {
            ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, data);
        }
    }

    [Test]
    public async Task GetEntityComponentOffset_ValidComponent_ReturnsValidOffset()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var data = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Create(NativeMemoryAllocator.Shared, mask);

        try
        {
            var layout = new ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>(data);
            var offset = layout.GetEntityComponentOffset<TestPosition>(0);

            await Assert.That(offset).IsGreaterThanOrEqualTo(0);
        }
        finally
        {
            ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, data);
        }
    }

    [Test]
    public async Task GetEntityComponentOffset_DifferentEntities_DifferentOffsets()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var data = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Create(NativeMemoryAllocator.Shared, mask);

        try
        {
            var layout = new ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>(data);
            var offset0 = layout.GetEntityComponentOffset<TestPosition>(0);
            var offset1 = layout.GetEntityComponentOffset<TestPosition>(1);
            var offsetDiff = offset1 - offset0;

            // Offsets should differ by component size
            await Assert.That(offsetDiff).IsEqualTo(TestPosition.Size);
        }
        finally
        {
            ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, data);
        }
    }

    [Test]
    public async Task GetEntityIdOffset_ReturnsCorrectOffset()
    {
        var offset0 = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.GetEntityIdOffset(0);
        var offset1 = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.GetEntityIdOffset(1);

        await Assert.That(offset0).IsEqualTo(0);
        await Assert.That(offset1).IsEqualTo(4); // sizeof(int)
    }

    [Test]
    public async Task HasComponent_ByComponentId_ReturnsCorrectResult()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestPosition.TypeId);
        var data = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Create(NativeMemoryAllocator.Shared, mask);

        try
        {
            var layout = new ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>(data);
            var hasPosition = layout.HasComponent(TestPosition.TypeId);
            var hasVelocity = layout.HasComponent(TestVelocity.TypeId);

            await Assert.That(hasPosition).IsTrue();
            await Assert.That(hasVelocity).IsFalse();
        }
        finally
        {
            ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, data);
        }
    }

    [Test]
    public async Task MinComponentId_MaxComponentId_Correct()
    {
        var mask = SmallBitSet<ulong>.Empty
            .Set(TestPosition.TypeId)
            .Set(TestHealth.TypeId);
        var data = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Create(NativeMemoryAllocator.Shared, mask);

        try
        {
            var layout = new ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>(data);
            var minComponentId = layout.MinComponentId;
            var maxComponentId = layout.MaxComponentId;
            var expectedMin = Math.Min(TestHealth.TypeId.Value, TestPosition.TypeId.Value);
            var expectedMax = Math.Max(TestHealth.TypeId.Value, TestPosition.TypeId.Value);

            // TestHealth is ID 0, TestPosition is ID 1
            await Assert.That(minComponentId).IsEqualTo(expectedMin);
            await Assert.That(maxComponentId).IsEqualTo(expectedMax);
        }
        finally
        {
            ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, data);
        }
    }

    [Test]
    public async Task TagComponent_ZeroSize_HasValidOffset()
    {
        var mask = SmallBitSet<ulong>.Empty.Set(TestTag.TypeId);
        var data = ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Create(NativeMemoryAllocator.Shared, mask);

        try
        {
            var layout = new ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>(data);
            var hasTag = layout.HasComponent<TestTag>();
            // Tag components have offset 0 (they don't take space)
            var offset = layout.GetBaseOffset<TestTag>();

            await Assert.That(hasTag).IsTrue();
            await Assert.That(offset).IsEqualTo(0);
        }
        finally
        {
            ImmutableArchetypeLayout<SmallBitSet<ulong>, ComponentRegistry, DefaultConfig>.Free(NativeMemoryAllocator.Shared, data);
        }
    }
}
