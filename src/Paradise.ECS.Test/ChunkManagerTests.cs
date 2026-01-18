namespace Paradise.ECS.Test;

/// <summary>
/// Tests for <see cref="ChunkManager"/>.
/// </summary>
public sealed class ChunkManagerTests
{
    [Test]
    public async Task Create_WithDefaultCapacity_DisposeSucceeds()
    {
        var manager = ChunkManager.Create(new DefaultConfig());
        manager.Dispose();

        await Assert.That(manager).IsNotNull();
    }

    [Test]
    public async Task Create_WithDefaultCapacity_DisposeSucceeds2()
    {
        // TConfig.DefaultChunkCapacity is now used automatically
        var manager = ChunkManager.Create(new DefaultConfig());
        manager.Dispose();

        await Assert.That(manager).IsNotNull();
    }

    [Test]
    public async Task Allocate_ReturnsValidHandle()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());

        var handle = manager.Allocate();

        await Assert.That(handle.IsValid).IsTrue();
    }

    [Test]
    public async Task Allocate_MultipleHandles_ReturnsDistinctIds()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());

        var handle1 = manager.Allocate();
        var handle2 = manager.Allocate();
        var handle3 = manager.Allocate();

        await Assert.That(handle1.Id).IsNotEqualTo(handle2.Id);
        await Assert.That(handle2.Id).IsNotEqualTo(handle3.Id);
        await Assert.That(handle1.Id).IsNotEqualTo(handle3.Id);
    }

    [Test]
    public async Task GetBytes_ValidHandle_ReturnsNonEmptySpan()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        var bytes = manager.GetBytes(handle);

        await Assert.That(bytes.IsEmpty).IsFalse();
    }

    [Test]
    public async Task GetBytes_InvalidHandle_ReturnsEmptySpan()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());

        var bytes = manager.GetBytes(ChunkHandle.Invalid);

        await Assert.That(bytes.IsEmpty).IsTrue();
    }

    [Test]
    public async Task GetBytes_StaleHandle_ReturnsEmptySpan()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();
        manager.Free(handle);

        var bytes = manager.GetBytes(handle);

        await Assert.That(bytes.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Free_ValidHandle_InvalidatesHandle()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        manager.Free(handle);

        var bytes = manager.GetBytes(handle);
        await Assert.That(bytes.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Free_InvalidHandle_DoesNotThrow()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());

        manager.Free(ChunkHandle.Invalid);

        await Assert.That(manager).IsNotNull();
    }

    [Test]
    public async Task Free_StaleHandle_DoesNotThrow()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();
        manager.Free(handle);

        manager.Free(handle); // Double free should be safe

        await Assert.That(manager).IsNotNull();
    }

    [Test]
    public async Task Free_WhileAcquired_ThrowsInvalidOperationException()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        manager.Acquire(handle);
        Exception? caught = null;
        try
        {
            manager.Free(handle);
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        finally
        {
            manager.Release(handle);
        }

        await Assert.That(caught).IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task Free_AfterRelease_Succeeds()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        manager.Acquire(handle);
        manager.Release(handle);

        manager.Free(handle);

        var bytes = manager.GetBytes(handle);
        await Assert.That(bytes.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Allocate_AfterFree_ReusesSlot()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle1 = manager.Allocate();
        var id1 = handle1.Id;
        manager.Free(handle1);

        var handle2 = manager.Allocate();

        await Assert.That(handle2.Id).IsEqualTo(id1);
        await Assert.That(handle2.Version).IsGreaterThan(handle1.Version);
    }

    [Test]
    public async Task GetBytes_ReturnsWritableMemory()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        var span = manager.GetBytes(handle).GetSpan<int>(0, 10);
        for (int i = 0; i < 10; i++)
        {
            span[i] = i * 100;
        }

        // Capture values before await
        var v0 = span[0];
        var v5 = span[5];
        var v9 = span[9];

        await Assert.That(v0).IsEqualTo(0);
        await Assert.That(v5).IsEqualTo(500);
        await Assert.That(v9).IsEqualTo(900);
    }

    [Test]
    public async Task GetBytes_DataPersistsAcrossCalls()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        // Write data
        var span1 = manager.GetBytes(handle).GetSpan<int>(0, 10);
        for (int i = 0; i < 10; i++)
        {
            span1[i] = i * 100;
        }

        // Read data back and capture values before await
        var span2 = manager.GetBytes(handle).GetSpan<int>(0, 10);
        var v0 = span2[0];
        var v5 = span2[5];
        var v9 = span2[9];

        await Assert.That(v0).IsEqualTo(0);
        await Assert.That(v5).IsEqualTo(500);
        await Assert.That(v9).IsEqualTo(900);
    }

    [Test]
    public async Task GetBytes_GetRef_ReturnsWritableReference()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        ref int refValue = ref manager.GetBytes(handle).GetRef<int>(0);
        refValue = 42;

        var value = manager.GetBytes(handle).GetRef<int>(0);

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task GetBytes_ReturnsFullChunkSize()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        var bytes = manager.GetBytes(handle);

        await Assert.That(bytes.Length).IsEqualTo(DefaultConfig.ChunkSize);
    }

    [Test]
    public async Task GetBytes_Slice_ReturnsSpecifiedSize()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        var bytes = manager.GetBytes(handle).Slice(0, 100);

        await Assert.That(bytes.Length).IsEqualTo(100);
    }

    [Test]
    public async Task GetBytes_GetBytesAt_ReturnsCorrectSlice()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        var bytes = manager.GetBytes(handle).GetBytesAt(100, 50);

        await Assert.That(bytes.Length).IsEqualTo(50);
    }

    [Test]
    public async Task Acquire_ValidHandle_ReturnsTrue()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        var result = manager.Acquire(handle);

        manager.Release(handle);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Acquire_InvalidHandle_ReturnsFalse()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());

        var result = manager.Acquire(ChunkHandle.Invalid);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Acquire_StaleHandle_ReturnsFalse()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();
        manager.Free(handle);

        var result = manager.Acquire(handle);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task MultipleAcquires_AllSucceed()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        var r1 = manager.Acquire(handle);
        var r2 = manager.Acquire(handle);
        var r3 = manager.Acquire(handle);

        manager.Release(handle);
        manager.Release(handle);
        manager.Release(handle);

        await Assert.That(r1).IsTrue();
        await Assert.That(r2).IsTrue();
        await Assert.That(r3).IsTrue();
    }

    [Test]
    public async Task ManyAllocations_AllValid()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handles = new ChunkHandle[100];

        for (int i = 0; i < 100; i++)
        {
            handles[i] = manager.Allocate();
        }

        bool[] nonEmpty = new bool[100];
        for (int i = 0; i < 100; i++)
        {
            nonEmpty[i] = !manager.GetBytes(handles[i]).IsEmpty;
        }

        foreach (var valid in nonEmpty)
        {
            await Assert.That(valid).IsTrue();
        }
    }

    [Test]
    public async Task Dispose_InvalidatesFurtherAllocations()
    {
        var manager = ChunkManager.Create(new DefaultConfig());
        manager.Dispose();

        var caught = false;
        try
        {
            manager.Allocate();
        }
        catch (ObjectDisposedException)
        {
            caught = true;
        }

        await Assert.That(caught).IsTrue();
    }

    [Test]
    public async Task Dispose_InvalidatesFurtherGetBytes()
    {
        var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();
        manager.Dispose();

        var caught = false;
        try
        {
            _ = manager.GetBytes(handle);
        }
        catch (ObjectDisposedException)
        {
            caught = true;
        }

        await Assert.That(caught).IsTrue();
    }

    [Test]
    public async Task StructTypes_WorkCorrectly()
    {
        using var manager = ChunkManager.Create(new DefaultConfig());
        var handle = manager.Allocate();

        ref var data = ref manager.GetBytes(handle).GetRef<TestStruct>(0);
        data.X = 1.5f;
        data.Y = 2.5f;
        data.Value = 42;

        var captured = manager.GetBytes(handle).GetRef<TestStruct>(0);

        await Assert.That(captured.X).IsEqualTo(1.5f);
        await Assert.That(captured.Y).IsEqualTo(2.5f);
        await Assert.That(captured.Value).IsEqualTo(42);
    }

    private struct TestStruct
    {
        public float X;
        public float Y;
        public int Value;
    }
}
