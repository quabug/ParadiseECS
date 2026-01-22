using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Paradise.ECS.Concurrent.Benchmarks;

/// <summary>
/// A SIMD-optimized immutable bit vector for benchmarking comparison.
/// Uses Vector256 operations when available for maximum throughput.
/// </summary>
public readonly struct ImmutableBitVector<TStorage> : IEquatable<ImmutableBitVector<TStorage>>, IBitSet<ImmutableBitVector<TStorage>>
    where TStorage : unmanaged, IStorage
{
    private readonly TStorage _storage;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ImmutableBitVector(TStorage storage) => _storage = storage;

    static ImmutableBitVector()
    {
        if (Unsafe.SizeOf<TStorage>() % sizeof(ulong) != 0)
#pragma warning disable CA1065 // Intentional: validate storage alignment at type initialization
            throw new InvalidOperationException(
                $"Storage type {typeof(TStorage).Name} size ({Unsafe.SizeOf<TStorage>()} bytes) must be a multiple of {sizeof(ulong)} bytes.");
#pragma warning restore CA1065
    }

    public static ImmutableBitVector<TStorage> Empty => default;

    public static int Capacity => ByteCount * 8;
    private static int ByteCount => Unsafe.SizeOf<TStorage>();
    private static int ULongCount => ByteCount / sizeof(ulong);
    private static int Vector64Count => ByteCount / 8;
    private static int Vector128Count => ByteCount / 16;
    private static int Vector256Count => ByteCount / 32;
    private static int Vector512Count => ByteCount / 64;

    // Alignment checks - these become compile-time constants per generic instantiation
    private static bool UseVector512 => Vector512.IsHardwareAccelerated && ByteCount % 64 == 0;
    private static bool UseVector256 => Vector256.IsHardwareAccelerated && ByteCount % 32 == 0;
    private static bool UseVector128 => Vector128.IsHardwareAccelerated && ByteCount % 16 == 0;
    private static bool UseVector64 => Vector64.IsHardwareAccelerated && ByteCount % 8 == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(int index)
    {
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        int wordIndex = index >> 6;
        int bitIndex = index & 63;
        return (Unsafe.Add(ref ulongs, wordIndex) & (1UL << bitIndex)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitVector<TStorage> Set(int index)
    {
        var storage = _storage;
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref storage);
        int wordIndex = index >> 6;
        int bitIndex = index & 63;
        Unsafe.Add(ref ulongs, wordIndex) |= 1UL << bitIndex;
        return new ImmutableBitVector<TStorage>(storage);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitVector<TStorage> Clear(int index)
    {
        var storage = _storage;
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref storage);
        int wordIndex = index >> 6;
        int bitIndex = index & 63;
        Unsafe.Add(ref ulongs, wordIndex) &= ~(1UL << bitIndex);
        return new ImmutableBitVector<TStorage>(storage);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitVector<TStorage> And(in ImmutableBitVector<TStorage> other)
    {
        var storage = default(TStorage);

        if (UseVector512)
        {
            ref var a = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector512<ulong>>(ref storage);

            for (int i = 0; i < Vector512Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) & Unsafe.Add(ref b, i);
        }
        else if (UseVector256)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector256<ulong>>(ref storage);

            for (int i = 0; i < Vector256Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) & Unsafe.Add(ref b, i);
        }
        else if (UseVector128)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector128<ulong>>(ref storage);

            for (int i = 0; i < Vector128Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) & Unsafe.Add(ref b, i);
        }
        else if (UseVector64)
        {
            ref var a = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector64<ulong>>(ref storage);

            for (int i = 0; i < Vector64Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) & Unsafe.Add(ref b, i);
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, ulong>(ref storage);

            for (int i = 0; i < ULongCount; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) & Unsafe.Add(ref b, i);
        }

        return new ImmutableBitVector<TStorage>(storage);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitVector<TStorage> Or(in ImmutableBitVector<TStorage> other)
    {
        var storage = default(TStorage);

        if (UseVector512)
        {
            ref var a = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector512<ulong>>(ref storage);

            for (int i = 0; i < Vector512Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) | Unsafe.Add(ref b, i);
        }
        else if (UseVector256)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector256<ulong>>(ref storage);

            for (int i = 0; i < Vector256Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) | Unsafe.Add(ref b, i);
        }
        else if (UseVector128)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector128<ulong>>(ref storage);

            for (int i = 0; i < Vector128Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) | Unsafe.Add(ref b, i);
        }
        else if (UseVector64)
        {
            ref var a = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector64<ulong>>(ref storage);

            for (int i = 0; i < Vector64Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) | Unsafe.Add(ref b, i);
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, ulong>(ref storage);

            for (int i = 0; i < ULongCount; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) | Unsafe.Add(ref b, i);
        }

        return new ImmutableBitVector<TStorage>(storage);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitVector<TStorage> Xor(in ImmutableBitVector<TStorage> other)
    {
        var storage = default(TStorage);

        if (UseVector512)
        {
            ref var a = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector512<ulong>>(ref storage);

            for (int i = 0; i < Vector512Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) ^ Unsafe.Add(ref b, i);
        }
        else if (UseVector256)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector256<ulong>>(ref storage);

            for (int i = 0; i < Vector256Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) ^ Unsafe.Add(ref b, i);
        }
        else if (UseVector128)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector128<ulong>>(ref storage);

            for (int i = 0; i < Vector128Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) ^ Unsafe.Add(ref b, i);
        }
        else if (UseVector64)
        {
            ref var a = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector64<ulong>>(ref storage);

            for (int i = 0; i < Vector64Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) ^ Unsafe.Add(ref b, i);
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, ulong>(ref storage);

            for (int i = 0; i < ULongCount; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) ^ Unsafe.Add(ref b, i);
        }

        return new ImmutableBitVector<TStorage>(storage);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitVector<TStorage> AndNot(in ImmutableBitVector<TStorage> other)
    {
        var storage = default(TStorage);

        if (UseVector512)
        {
            ref var a = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector512<ulong>>(ref storage);

            for (int i = 0; i < Vector512Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) & ~Unsafe.Add(ref b, i);
        }
        else if (UseVector256)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector256<ulong>>(ref storage);

            for (int i = 0; i < Vector256Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) & ~Unsafe.Add(ref b, i);
        }
        else if (UseVector128)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector128<ulong>>(ref storage);

            for (int i = 0; i < Vector128Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) & ~Unsafe.Add(ref b, i);
        }
        else if (UseVector64)
        {
            ref var a = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector64<ulong>>(ref storage);

            for (int i = 0; i < Vector64Count; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) & ~Unsafe.Add(ref b, i);
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, ulong>(ref storage);

            for (int i = 0; i < ULongCount; i++)
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) & ~Unsafe.Add(ref b, i);
        }

        return new ImmutableBitVector<TStorage>(storage);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAll(in ImmutableBitVector<TStorage> other)
    {
        if (UseVector512)
        {
            ref var a = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector512Count; i++)
            {
                var av = Unsafe.Add(ref a, i);
                var bv = Unsafe.Add(ref b, i);
                if ((av & bv) != bv)
                    return false;
            }
            return true;
        }
        if (UseVector256)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector256Count; i++)
            {
                var av = Unsafe.Add(ref a, i);
                var bv = Unsafe.Add(ref b, i);
                if ((av & bv) != bv)
                    return false;
            }
            return true;
        }
        if (UseVector128)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector128Count; i++)
            {
                var av = Unsafe.Add(ref a, i);
                var bv = Unsafe.Add(ref b, i);
                if ((av & bv) != bv)
                    return false;
            }
            return true;
        }
        if (UseVector64)
        {
            ref var a = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector64Count; i++)
            {
                var av = Unsafe.Add(ref a, i);
                var bv = Unsafe.Add(ref b, i);
                if ((av & bv) != bv)
                    return false;
            }
            return true;
        }

        ref var au = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        ref var bu = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

        for (int i = 0; i < ULongCount; i++)
        {
            var av = Unsafe.Add(ref au, i);
            var bv = Unsafe.Add(ref bu, i);
            if ((av & bv) != bv)
                return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAny(in ImmutableBitVector<TStorage> other)
    {
        if (UseVector512)
        {
            ref var a = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector512Count; i++)
            {
                if ((Unsafe.Add(ref a, i) & Unsafe.Add(ref b, i)) != Vector512<ulong>.Zero)
                    return true;
            }
            return false;
        }
        if (UseVector256)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector256Count; i++)
            {
                if ((Unsafe.Add(ref a, i) & Unsafe.Add(ref b, i)) != Vector256<ulong>.Zero)
                    return true;
            }
            return false;
        }
        if (UseVector128)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector128Count; i++)
            {
                if ((Unsafe.Add(ref a, i) & Unsafe.Add(ref b, i)) != Vector128<ulong>.Zero)
                    return true;
            }
            return false;
        }
        if (UseVector64)
        {
            ref var a = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector64Count; i++)
            {
                if ((Unsafe.Add(ref a, i) & Unsafe.Add(ref b, i)) != Vector64<ulong>.Zero)
                    return true;
            }
            return false;
        }

        ref var au = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        ref var bu = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

        for (int i = 0; i < ULongCount; i++)
        {
            if ((Unsafe.Add(ref au, i) & Unsafe.Add(ref bu, i)) != 0)
                return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsNone(in ImmutableBitVector<TStorage> other) => !ContainsAny(in other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopCount()
    {
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        int count = 0;
        for (int i = 0; i < ULongCount; i++)
        {
            count += BitOperations.PopCount(Unsafe.Add(ref ulongs, i));
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FirstSetBit()
    {
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        for (int i = 0; i < ULongCount; i++)
        {
            var word = Unsafe.Add(ref ulongs, i);
            if (word != 0)
                return (i << 6) + BitOperations.TrailingZeroCount(word);
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LastSetBit()
    {
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        for (int i = ULongCount - 1; i >= 0; i--)
        {
            var word = Unsafe.Add(ref ulongs, i);
            if (word != 0)
                return (i << 6) + 63 - BitOperations.LeadingZeroCount(word);
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NextSetBit(int afterIndex)
    {
        int startIndex = afterIndex + 1;
        if (startIndex >= Capacity)
            return -1;

        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        int bucketIndex = startIndex >> 6;
        int bitInBucket = startIndex & 63;

        // Check current bucket (mask off bits before startIndex)
        ulong currentBucket = Unsafe.Add(ref ulongs, bucketIndex);
        currentBucket &= ~((1UL << bitInBucket) - 1);
        if (currentBucket != 0)
            return (bucketIndex << 6) + BitOperations.TrailingZeroCount(currentBucket);

        // Check remaining buckets
        for (int i = bucketIndex + 1; i < ULongCount; i++)
        {
            ulong value = Unsafe.Add(ref ulongs, i);
            if (value != 0)
                return (i << 6) + BitOperations.TrailingZeroCount(value);
        }
        return -1;
    }

    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (UseVector512)
            {
                ref var vecs = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in _storage));

                for (int i = 0; i < Vector512Count; i++)
                {
                    if (Unsafe.Add(ref vecs, i) != Vector512<ulong>.Zero)
                        return false;
                }
                return true;
            }
            if (UseVector256)
            {
                ref var vecs = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));

                for (int i = 0; i < Vector256Count; i++)
                {
                    if (Unsafe.Add(ref vecs, i) != Vector256<ulong>.Zero)
                        return false;
                }
                return true;
            }
            if (UseVector128)
            {
                ref var vecs = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));

                for (int i = 0; i < Vector128Count; i++)
                {
                    if (Unsafe.Add(ref vecs, i) != Vector128<ulong>.Zero)
                        return false;
                }
                return true;
            }
            if (UseVector64)
            {
                ref var vecs = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in _storage));

                for (int i = 0; i < Vector64Count; i++)
                {
                    if (Unsafe.Add(ref vecs, i) != Vector64<ulong>.Zero)
                        return false;
                }
                return true;
            }

            ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
            for (int i = 0; i < ULongCount; i++)
            {
                if (Unsafe.Add(ref ulongs, i) != 0)
                    return false;
            }
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(in ImmutableBitVector<TStorage> other)
    {
        if (UseVector512)
        {
            ref var a = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector512Count; i++)
            {
                if (Unsafe.Add(ref a, i) != Unsafe.Add(ref b, i))
                    return false;
            }
            return true;
        }
        if (UseVector256)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector256Count; i++)
            {
                if (Unsafe.Add(ref a, i) != Unsafe.Add(ref b, i))
                    return false;
            }
            return true;
        }
        if (UseVector128)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector128Count; i++)
            {
                if (Unsafe.Add(ref a, i) != Unsafe.Add(ref b, i))
                    return false;
            }
            return true;
        }
        if (UseVector64)
        {
            ref var a = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector64Count; i++)
            {
                if (Unsafe.Add(ref a, i) != Unsafe.Add(ref b, i))
                    return false;
            }
            return true;
        }

        ref var au = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        ref var bu = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

        for (int i = 0; i < ULongCount; i++)
        {
            if (Unsafe.Add(ref au, i) != Unsafe.Add(ref bu, i))
                return false;
        }
        return true;
    }

    public bool Equals(ImmutableBitVector<TStorage> other) => Equals(in other);

    public override bool Equals(object? obj) => obj is ImmutableBitVector<TStorage> other && Equals(in other);

    public override int GetHashCode()
    {
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        var hash = new HashCode();
        for (int i = 0; i < ULongCount; i++)
        {
            hash.Add(Unsafe.Add(ref ulongs, i));
        }
        return hash.ToHashCode();
    }

    public Enumerator GetEnumerator() => new(this);

    public ref struct Enumerator
    {
        private readonly ImmutableBitVector<TStorage> _immutableBitVector;
        private int _wordIndex;
        private ulong _currentWord;
        private int _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(ImmutableBitVector<TStorage> immutableBitVector)
        {
            _immutableBitVector = immutableBitVector;
            _wordIndex = 0;
            _current = -1;

            ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _immutableBitVector._storage));
            _currentWord = Unsafe.Add(ref ulongs, 0);
        }

        public readonly int Current => _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (_currentWord == 0)
            {
                _wordIndex++;
                if (_wordIndex >= ULongCount)
                    return false;

                ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _immutableBitVector._storage));
                _currentWord = Unsafe.Add(ref ulongs, _wordIndex);
            }

            int bit = BitOperations.TrailingZeroCount(_currentWord);
            _current = (_wordIndex << 6) + bit;
            _currentWord &= _currentWord - 1;
            return true;
        }
    }
}
