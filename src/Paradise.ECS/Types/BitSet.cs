using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// A fixed-size bitset for component masks, generic over the backing storage.
/// Uses InlineArray for efficient, stack-allocated storage.
/// </summary>
/// <typeparam name="TBits">An InlineArray of ulongs (e.g., Bits128, Bits256).</typeparam>
public readonly record struct ImmutableBitSet<TBits> : IBitSet<ImmutableBitSet<TBits>>
    where TBits : unmanaged, IStorage
{
    private static int ValidateAndGetULongCount()
    {
        int size = Unsafe.SizeOf<TBits>();
        if (size % sizeof(ulong) != 0)
        {
            throw new InvalidOperationException(
                $"BitSet storage type {typeof(TBits).Name} must be a multiple of {sizeof(ulong)} bytes, but is {size} bytes.");
        }
        return size / sizeof(ulong);
    }

    private readonly TBits _bits;

    private static int ULongCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    } = ValidateAndGetULongCount();

    public static int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ULongCount * 64;
    }

    public static ImmutableBitSet<TBits> Empty => default;

    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var span = GetReadOnlySpan();
            for (int i = 0; i < ULongCount; i++)
            {
                if (span[i] != 0) return false;
            }
            return true;
        }
    }

    private ImmutableBitSet(TBits bits) => _bits = bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ImmutableBitSet<TBits> other)
    {
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();
        for (int i = 0; i < ULongCount; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        var span = GetReadOnlySpan();
        var hash = new HashCode();
        for (int i = 0; i < ULongCount; i++)
            hash.Add(span[i]);
        return hash.ToHashCode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<ulong> GetReadOnlySpan()
    {
        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<TBits, ulong>(ref Unsafe.AsRef(in _bits)),
            ULongCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<ulong> GetSpan(ref TBits bits)
    {
        return MemoryMarshal.CreateSpan(
            ref Unsafe.As<TBits, ulong>(ref bits),
            ULongCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(int index)
    {
        if ((uint)index >= (uint)Capacity) return false;
        var span = GetReadOnlySpan();
        return (span[index >> 6] & (1UL << (index & 63))) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet<TBits> Set(int index)
    {
        if ((uint)index >= (uint)Capacity) return this;
        var newBits = _bits;
        var span = GetSpan(ref newBits);
        span[index >> 6] |= 1UL << (index & 63);
        return new ImmutableBitSet<TBits>(newBits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet<TBits> Clear(int index)
    {
        if ((uint)index >= (uint)Capacity) return this;
        var newBits = _bits;
        var span = GetSpan(ref newBits);
        span[index >> 6] &= ~(1UL << (index & 63));
        return new ImmutableBitSet<TBits>(newBits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet<TBits> And(ImmutableBitSet<TBits> other)
    {
        var result = default(TBits);
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();
        var r = GetSpan(ref result);

        for (int i = 0; i < ULongCount; i++)
            r[i] = a[i] & b[i];

        return new ImmutableBitSet<TBits>(result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet<TBits> Or(ImmutableBitSet<TBits> other)
    {
        var result = default(TBits);
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();
        var r = GetSpan(ref result);

        for (int i = 0; i < ULongCount; i++)
            r[i] = a[i] | b[i];

        return new ImmutableBitSet<TBits>(result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet<TBits> AndNot(ImmutableBitSet<TBits> other)
    {
        var result = default(TBits);
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();
        var r = GetSpan(ref result);

        for (int i = 0; i < ULongCount; i++)
            r[i] = a[i] & ~b[i];

        return new ImmutableBitSet<TBits>(result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAll(ImmutableBitSet<TBits> other)
    {
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();

        for (int i = 0; i < ULongCount; i++)
        {
            if ((a[i] & b[i]) != b[i]) return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAny(ImmutableBitSet<TBits> other)
    {
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();

        for (int i = 0; i < ULongCount; i++)
        {
            if ((a[i] & b[i]) != 0) return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsNone(ImmutableBitSet<TBits> other)
    {
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();

        for (int i = 0; i < ULongCount; i++)
        {
            if ((a[i] & b[i]) != 0) return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopCount()
    {
        var span = GetReadOnlySpan();
        int count = 0;
        for (int i = 0; i < ULongCount; i++)
            count += BitOperations.PopCount(span[i]);
        return count;
    }

    public override string ToString() => $"ImmutableBitSet<{typeof(TBits).Name}>({PopCount()} bits set)";
}
