namespace Paradise.ECS.Test;

public class ChunkTests : IDisposable
{
    private readonly ChunkManager _manager;

    public ChunkTests()
    {
        _manager = new ChunkManager(initialCapacity: 16);
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }

    [Test]
    public async Task ChunkSize_Is16KB()
    {
        int chunkSize = Chunk.ChunkSize;
        await Assert.That(chunkSize).IsEqualTo(16 * 1024);
    }

    [Test]
    public async Task Allocate_CreatesValidChunk()
    {
        var handle = _manager.Allocate();
        {
            using var chunk = _manager.Get(handle); // Should not throw
        }

        await Assert.That(handle.IsValid).IsTrue();
        await Assert.That(handle.Version).IsGreaterThanOrEqualTo(0u);
    }

    [Test]
    public async Task GetSpan_ReturnsCorrectSpan()
    {
        var handle = _manager.Allocate();
        int spanLength;
        {
            using var chunk = _manager.Get(handle);
            spanLength = chunk.GetSpan<float>(byteOffset: 0, count: 64).Length;
        }

        await Assert.That(spanLength).IsEqualTo(64);
    }

    [Test]
    public async Task GetSpan_AllowsReadWrite()
    {
        var handle = _manager.Allocate();
        bool allMatch;
        {
            using var chunk = _manager.Get(handle);

            // Write data
            var span = chunk.GetSpan<int>(byteOffset: 0, count: 64);
            for (int i = 0; i < span.Length; i++)
                span[i] = i * 10;

            // Verify
            var verifySpan = chunk.GetSpan<int>(byteOffset: 0, count: 64);
            allMatch = true;
            for (int i = 0; i < verifySpan.Length; i++)
            {
                if (verifySpan[i] != i * 10)
                {
                    allMatch = false;
                    break;
                }
            }
        }

        await Assert.That(allMatch).IsTrue();
    }

    [Test]
    public async Task GetSpan_WithOffset_ReturnsCorrectData()
    {
        var handle = _manager.Allocate();
        int value1, value2;
        {
            using var chunk = _manager.Get(handle);

            // Write
            var firstSpan = chunk.GetSpan<int>(byteOffset: 0, count: 32);
            for (int i = 0; i < 32; i++) firstSpan[i] = 100 + i;

            var secondSpan = chunk.GetSpan<int>(byteOffset: 128, count: 32);
            for (int i = 0; i < 32; i++) secondSpan[i] = 200 + i;

            // Read
            value1 = chunk.GetSpan<int>(byteOffset: 0, count: 32)[0];
            value2 = chunk.GetSpan<int>(byteOffset: 128, count: 32)[0];
        }

        await Assert.That(value1).IsEqualTo(100);
        await Assert.That(value2).IsEqualTo(200);
    }

    [Test]
    public async Task GetDataBytes_ReturnsEntireChunk()
    {
        var handle = _manager.Allocate();
        int bytesLength;
        {
            using var chunk = _manager.Get(handle);
            bytesLength = chunk.GetDataBytes().Length;
        }

        await Assert.That(bytesLength).IsEqualTo(Chunk.ChunkSize);
    }

    [Test]
    public async Task GetDataBytes_WithSize_ReturnsRequestedSize()
    {
        var handle = _manager.Allocate();
        int bytesLength;
        {
            using var chunk = _manager.Get(handle);
            bytesLength = chunk.GetDataBytes(256).Length;
        }

        await Assert.That(bytesLength).IsEqualTo(256);
    }

    [Test]
    public async Task GetDataBytes_WithOversizedRequest_ThrowsException()
    {
        var handle = _manager.Allocate();

        bool threw = false;
        try
        {
            using var chunk = _manager.Get(handle);
            _ = chunk.GetDataBytes(Chunk.ChunkSize + 1);
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task Free_InvalidatesHandle()
    {
        var handle = _manager.Allocate();

        // Handle should be valid and Get should succeed
        bool wasValid = handle.IsValid;
        {
            using var chunk = _manager.Get(handle); // Borrow and release
        }

        _manager.Free(handle);

        // After free, reallocating should give a new version for the same slot
        var handle2 = _manager.Allocate();
        bool sameSlot = handle2.Id == handle.Id;
        bool differentVersion = handle2.Version != handle.Version;

        await Assert.That(wasValid).IsTrue();
        await Assert.That(sameSlot).IsTrue();
        await Assert.That(differentVersion).IsTrue();

        _manager.Free(handle2);
    }

    [Test]
    public async Task Free_WhileBorrowed_Throws()
    {
        var handle = _manager.Allocate();
        bool threw;
        {
            using var chunk = _manager.Get(handle);

            threw = false;
            try
            {
                _manager.Free(handle);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
        }

        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task GetRawBytes_ReturnsEntireChunkMemory()
    {
        var handle = _manager.Allocate();
        int rawBytesLength;
        {
            using var chunk = _manager.Get(handle);
            rawBytesLength = chunk.GetRawBytes().Length;
        }

        await Assert.That(rawBytesLength).IsEqualTo(Chunk.ChunkSize);
    }

    [Test]
    public async Task Free_IncrementsVersion()
    {
        var handle1 = _manager.Allocate();
        var version1 = handle1.Version;

        _manager.Free(handle1);

        var handle2 = _manager.Allocate();
        var version2 = handle2.Version;

        // Same slot reused, but version should be incremented
        await Assert.That(handle2.Id).IsEqualTo(handle1.Id);
        await Assert.That(version2).IsEqualTo(version1 + 1);
    }

    [Test]
    public async Task Get_WithStaleHandle_ReturnsDefaultChunk()
    {
        var handle = _manager.Allocate();
        _manager.Free(handle);

        // Should not throw - returns default chunk
        bool noException = true;
        try
        {
            using var chunk = _manager.Get(handle);
            // Default chunk's Dispose() is safe (checks for null manager)
        }
        catch
        {
            noException = false;
        }

        await Assert.That(noException).IsTrue();
    }

    [Test]
    public async Task Chunk_Dispose_ReleasesLock()
    {
        var handle = _manager.Allocate();

        // Borrow and release
        {
            using var chunk = _manager.Get(handle);
        }

        // Should be able to free after dispose
        bool threw = false;
        try
        {
            _manager.Free(handle);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        await Assert.That(threw).IsFalse();
    }
}
