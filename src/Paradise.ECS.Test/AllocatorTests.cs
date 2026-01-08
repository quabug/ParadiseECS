namespace Paradise.ECS.Test;

public class AllocatorTests
{
    [Test]
    public async Task NativeMemoryAllocator_AllocateAndFree_Works()
    {
        var allocator = NativeMemoryAllocator.Shared;

        byte value0, value1023;
        bool ptrNotNull;

        unsafe
        {
            var ptr = allocator.Allocate(1024);
            ptrNotNull = ptr != null;

            // Write and read
            var span = new Span<byte>(ptr, 1024);
            span[0] = 42;
            span[1023] = 99;

            value0 = span[0];
            value1023 = span[1023];

            allocator.Free(ptr);
        }

        await Assert.That(ptrNotNull).IsTrue();
        await Assert.That(value0).IsEqualTo((byte)42);
        await Assert.That(value1023).IsEqualTo((byte)99);
    }

    [Test]
    public async Task NativeMemoryAllocator_AllocateZeroed_IsZeroed()
    {
        var allocator = NativeMemoryAllocator.Shared;

        bool allZero;

        unsafe
        {
            var ptr = allocator.AllocateZeroed(1024);
            var span = new Span<byte>(ptr, 1024);

            // All bytes should be zero
            allZero = true;
            foreach (byte b in span)
            {
                if (b != 0)
                {
                    allZero = false;
                    break;
                }
            }

            allocator.Free(ptr);
        }

        await Assert.That(allZero).IsTrue();
    }
}
