using System.Runtime.InteropServices;

namespace Paradise.ECS.Concurrent.Test;

public class ChunkTests : IDisposable
{
    private readonly ChunkManager<DefaultConfig> _manager;

    public ChunkTests()
    {
        _manager = new ChunkManager<DefaultConfig>(new DefaultConfig { DefaultChunkCapacity = 16 });
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }

    [Test]
    public async Task ChunkSize_Is16KB()
    {
        int chunkSize = DefaultConfig.ChunkSize;
        await Assert.That(chunkSize).IsEqualTo(16 * 1024);
    }

    [Test]
    public async Task Allocate_CreatesValidChunk()
    {
        var handle = _manager.Allocate();
        var bytes = _manager.GetBytes(handle);

        await Assert.That(handle.IsValid).IsTrue();
        await Assert.That(handle.Version).IsGreaterThanOrEqualTo(0u);
        await Assert.That(bytes.IsEmpty).IsFalse();
    }

    [Test]
    public async Task GetBytes_ReturnsCorrectSize()
    {
        var handle = _manager.Allocate();
        var bytes = _manager.GetBytes(handle);

        await Assert.That(bytes.Length).IsEqualTo(DefaultConfig.ChunkSize);
    }

    [Test]
    public async Task GetBytes_AllowsReadWrite()
    {
        var handle = _manager.Allocate();
        var bytes = _manager.GetBytes(handle);

        // Write data
        var span = MemoryMarshal.Cast<byte, int>(bytes.Slice(0, 64 * sizeof(int)));
        for (int i = 0; i < span.Length; i++)
            span[i] = i * 10;

        // Verify
        var verifyBytes = _manager.GetBytes(handle);
        var verifySpan = MemoryMarshal.Cast<byte, int>(verifyBytes.Slice(0, 64 * sizeof(int)));
        bool allMatch = true;
        for (int i = 0; i < verifySpan.Length; i++)
        {
            if (verifySpan[i] != i * 10)
            {
                allMatch = false;
                break;
            }
        }

        await Assert.That(allMatch).IsTrue();
    }

    [Test]
    public async Task GetBytes_WithOffset_ReturnsCorrectData()
    {
        var handle = _manager.Allocate();
        var bytes = _manager.GetBytes(handle);

        // Write
        var firstSpan = MemoryMarshal.Cast<byte, int>(bytes.Slice(0, 32 * sizeof(int)));
        for (int i = 0; i < 32; i++) firstSpan[i] = 100 + i;

        var secondSpan = MemoryMarshal.Cast<byte, int>(bytes.Slice(128, 32 * sizeof(int)));
        for (int i = 0; i < 32; i++) secondSpan[i] = 200 + i;

        // Read
        var readBytes = _manager.GetBytes(handle);
        var value1 = MemoryMarshal.Cast<byte, int>(readBytes.Slice(0, sizeof(int)))[0];
        var value2 = MemoryMarshal.Cast<byte, int>(readBytes.Slice(128, sizeof(int)))[0];

        await Assert.That(value1).IsEqualTo(100);
        await Assert.That(value2).IsEqualTo(200);
    }

    [Test]
    public async Task Free_InvalidatesHandle()
    {
        var handle = _manager.Allocate();

        // Handle should be valid and GetBytes should succeed
        bool wasValid = handle.IsValid;
        var bytes = _manager.GetBytes(handle);
        bool hadBytes = !bytes.IsEmpty;

        _manager.Free(handle);

        // After free, reallocating should give a new version for the same slot
        var handle2 = _manager.Allocate();
        bool sameSlot = handle2.Id == handle.Id;
        bool differentVersion = handle2.Version != handle.Version;

        await Assert.That(wasValid).IsTrue();
        await Assert.That(hadBytes).IsTrue();
        await Assert.That(sameSlot).IsTrue();
        await Assert.That(differentVersion).IsTrue();

        _manager.Free(handle2);
    }

    [Test]
    public async Task Free_WhileAcquired_Throws()
    {
        var handle = _manager.Allocate();
        _manager.Acquire(handle);

        bool threw = false;
        try
        {
            _manager.Free(handle);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        finally
        {
            _manager.Release(handle);
        }

        await Assert.That(threw).IsTrue();
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
    public async Task GetBytes_WithStaleHandle_ReturnsEmptySpan()
    {
        var handle = _manager.Allocate();
        _manager.Free(handle);

        var bytes = _manager.GetBytes(handle);

        await Assert.That(bytes.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Acquire_Release_AllowsFree()
    {
        var handle = _manager.Allocate();

        // Acquire and release
        _manager.Acquire(handle);
        _manager.Release(handle);

        // Should be able to free after release
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

    [Test]
    public async Task Acquire_ValidHandle_ReturnsTrue()
    {
        var handle = _manager.Allocate();

        var result = _manager.Acquire(handle);
        _manager.Release(handle);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Acquire_StaleHandle_ReturnsFalse()
    {
        var handle = _manager.Allocate();
        _manager.Free(handle);

        var result = _manager.Acquire(handle);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task GetBytes_Slice_ReturnsCorrectSlice()
    {
        var handle = _manager.Allocate();
        var bytes = _manager.GetBytes(handle);

        // Write some data first
        bytes[100] = 42;

        // Get bytes at offset
        var slice = bytes.Slice(100, 50);
        var length = slice.Length;
        var firstByte = slice[0];

        await Assert.That(length).IsEqualTo(50);
        await Assert.That(firstByte).IsEqualTo((byte)42);
    }
}
