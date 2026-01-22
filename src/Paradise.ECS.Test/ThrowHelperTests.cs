namespace Paradise.ECS.Test;

/// <summary>
/// Tests for <see cref="ThrowHelper"/>.
/// </summary>
public sealed class ThrowHelperTests
{
    [Test]
    public async Task ThrowIfNegativeOffset_ValidOffset_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfNegativeOffset(0)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ThrowIfNegativeOffset(100)).ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfNegativeOffset_NegativeOffset_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfNegativeOffset(-1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ThrowIfNegativeCount_ValidCount_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfNegativeCount(0)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ThrowIfNegativeCount(50)).ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfNegativeCount_NegativeCount_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfNegativeCount(-1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ThrowIfNegativeSize_ValidSize_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfNegativeSize(0)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ThrowIfNegativeSize(1024)).ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfNegativeSize_NegativeSize_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfNegativeSize(-1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ThrowIfExceedsChunkSize_ValidSize_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfExceedsChunkSize(16384, 16384)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ThrowIfExceedsChunkSize(16384, 0)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ThrowIfExceedsChunkSize(16384, 100)).ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfExceedsChunkSize_ExceedsChunkSize_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfExceedsChunkSize(16384, 16385))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkRange_TwoParams_ValidRange_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(16384, 0, 100)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ValidateChunkRange(16384, 100, 16284)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ValidateChunkRange(16384, 0, 16384)).ThrowsNothing();
    }

    [Test]
    public async Task ValidateChunkRange_TwoParams_NegativeOffset_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(16384, -1, 100))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkRange_TwoParams_NegativeSize_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(16384, 0, -1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkRange_TwoParams_ExceedsChunkSize_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(16384, 100, 16385))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkRange_ThreeParams_ValidRange_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(16384, 0, 10, 4)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ValidateChunkRange(16384, 100, 100, 8)).ThrowsNothing();
    }

    [Test]
    public async Task ValidateChunkRange_ThreeParams_NegativeOffset_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(16384, -1, 10, 4))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkRange_ThreeParams_NegativeCount_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(16384, 0, -1, 4))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkRange_ThreeParams_ExceedsChunkSize_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(16384, 0, 5000, 4))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkSize_ValidSize_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkSize(16384, 0)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ValidateChunkSize(16384, 16384)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ValidateChunkSize(16384, 100)).ThrowsNothing();
    }

    [Test]
    public async Task ValidateChunkSize_NegativeSize_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkSize(16384, -1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkSize_ExceedsChunkSize_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkSize(16384, 16385))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ThrowIfDisposed_NotDisposed_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfDisposed(false, new object())).ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfDisposed_Disposed_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfDisposed(true, new object()))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ThrowIfComponentIdExceedsCapacity_ValidId_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfComponentIdExceedsCapacity(0, 64)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ThrowIfComponentIdExceedsCapacity(63, 64)).ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfComponentIdExceedsCapacity_ExceedsCapacity_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfComponentIdExceedsCapacity(64, 64))
            .Throws<InvalidOperationException>();
        await Assert.That(() => ThrowHelper.ThrowIfComponentIdExceedsCapacity(100, 64))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ThrowIfInvalidComponentId_ValidId_DoesNotThrow()
    {
        var validId = new ComponentId(0);
        await Assert.That(() => ThrowHelper.ThrowIfInvalidComponentId(validId)).ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfInvalidComponentId_InvalidId_Throws()
    {
        var invalidId = ComponentId.Invalid;
        await Assert.That(() => ThrowHelper.ThrowIfInvalidComponentId(invalidId))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ThrowArgumentException_ThrowsWithMessage()
    {
        await Assert.That(() => ThrowHelper.ThrowArgumentException("test message", "paramName"))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ThrowIfArchetypeIdExceedsLimit_ValidId_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfArchetypeIdExceedsLimit(0)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ThrowIfArchetypeIdExceedsLimit(IConfig.MaxArchetypeId)).ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfArchetypeIdExceedsLimit_ExceedsLimit_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfArchetypeIdExceedsLimit(IConfig.MaxArchetypeId + 1))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ThrowIfEntityIdExceedsLimit_ValidId_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfEntityIdExceedsLimit(0, 255, 1)).ThrowsNothing();
        await Assert.That(() => ThrowHelper.ThrowIfEntityIdExceedsLimit(255, 255, 1)).ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfEntityIdExceedsLimit_ExceedsLimit_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfEntityIdExceedsLimit(256, 255, 1))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ThrowInvalidEntityIdByteSize_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowInvalidEntityIdByteSize<int>(3))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ThrowChunkInUse_Throws()
    {
        var handle = default(ChunkHandle);
        await Assert.That(() => ThrowHelper.ThrowChunkInUse(handle))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ThrowChunkManagerCapacityExceeded_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowChunkManagerCapacityExceeded(16, 1024))
            .Throws<InvalidOperationException>();
    }
}
