namespace Paradise.ECS.Concurrent.Test;

public class ThrowHelperTests
{
    [Test]
    public async Task ThrowIfNegativeOffset_WithNegativeValue_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfNegativeOffset(-1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ThrowIfNegativeOffset_WithZero_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfNegativeOffset(0))
            .ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfNegativeCount_WithNegativeValue_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfNegativeCount(-1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ThrowIfNegativeCount_WithZero_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfNegativeCount(0))
            .ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfNegativeSize_WithNegativeValue_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfNegativeSize(-1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ThrowIfNegativeSize_WithZero_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfNegativeSize(0))
            .ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfExceedsChunkSize_WithExceedingValue_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfExceedsChunkSize(Chunk.ChunkSize + 1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ThrowIfExceedsChunkSize_WithChunkSize_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfExceedsChunkSize(Chunk.ChunkSize))
            .ThrowsNothing();
    }

    [Test]
    public async Task ValidateChunkRange_TwoParams_WithNegativeOffset_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(-1, 100))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkRange_TwoParams_WithNegativeSize_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(0, -1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkRange_TwoParams_WithExceedingRange_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(100, Chunk.ChunkSize))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkRange_TwoParams_WithValidRange_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(0, Chunk.ChunkSize))
            .ThrowsNothing();
    }

    [Test]
    public async Task ValidateChunkRange_ThreeParams_WithNegativeOffset_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(-1, 10, 4))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkRange_ThreeParams_WithNegativeCount_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(0, -1, 4))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkRange_ThreeParams_WithExceedingCount_Throws()
    {
        // At offset 0, with element size 4, max count is ChunkSize / 4
        int maxCount = Chunk.ChunkSize / 4;
        await Assert.That(() => ThrowHelper.ValidateChunkRange(0, maxCount + 1, 4))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkRange_ThreeParams_WithValidRange_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkRange(0, 100, 4))
            .ThrowsNothing();
    }

    [Test]
    public async Task ValidateChunkSize_WithNegativeSize_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkSize(-1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkSize_WithExceedingSize_Throws()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkSize(Chunk.ChunkSize + 1))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ValidateChunkSize_WithValidSize_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ValidateChunkSize(Chunk.ChunkSize))
            .ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfDisposed_WhenDisposed_Throws()
    {
        var obj = new object();
        await Assert.That(() => ThrowHelper.ThrowIfDisposed(true, obj))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ThrowIfDisposed_WhenNotDisposed_DoesNotThrow()
    {
        var obj = new object();
        await Assert.That(() => ThrowHelper.ThrowIfDisposed(false, obj))
            .ThrowsNothing();
    }

    [Test]
    public unsafe Task ThrowIfNull_WithNullPointer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ThrowHelper.ThrowIfNull(null));
        return Task.CompletedTask;
    }

    [Test]
    public async Task ThrowIfComponentIdExceedsCapacity_WithExceedingId_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfComponentIdExceedsCapacity(100, 50))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ThrowIfComponentIdExceedsCapacity_WithValidId_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfComponentIdExceedsCapacity(49, 50))
            .ThrowsNothing();
    }

    [Test]
    public async Task ThrowIfInvalidComponentId_WithInvalidId_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowIfInvalidComponentId(ComponentId.Invalid))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ThrowIfInvalidComponentId_WithValidId_DoesNotThrow()
    {
        await Assert.That(() => ThrowHelper.ThrowIfInvalidComponentId(new ComponentId(0)))
            .ThrowsNothing();
    }

    [Test]
    public async Task ThrowArgumentException_Throws()
    {
        await Assert.That(() => ThrowHelper.ThrowArgumentException("test message", "paramName"))
            .Throws<ArgumentException>();
    }
}
