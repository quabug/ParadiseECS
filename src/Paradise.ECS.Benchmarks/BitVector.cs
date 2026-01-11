using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Paradise.ECS;

namespace Paradise.ECS.Benchmarks;

/// <summary>
/// A SIMD-optimized mutable bit vector for benchmarking comparison.
/// Uses Vector256 operations when available for maximum throughput.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BitVector<TStorage> : IEquatable<BitVector<TStorage>>
    where TStorage : unmanaged, IStorage
{
    private TStorage _storage;

    public static BitVector<TStorage> Empty => default;

    public static int BitCount => Unsafe.SizeOf<TStorage>() * 8;

    private static int ULongCount => Unsafe.SizeOf<TStorage>() / sizeof(ulong);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Get(int index)
    {
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        int wordIndex = index >> 6;
        int bitIndex = index & 63;
        return (Unsafe.Add(ref ulongs, wordIndex) & (1UL << bitIndex)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BitVector<TStorage> Set(int index)
    {
        var result = this;
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref result._storage);
        int wordIndex = index >> 6;
        int bitIndex = index & 63;
        Unsafe.Add(ref ulongs, wordIndex) |= 1UL << bitIndex;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BitVector<TStorage> Clear(int index)
    {
        var result = this;
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref result._storage);
        int wordIndex = index >> 6;
        int bitIndex = index & 63;
        Unsafe.Add(ref ulongs, wordIndex) &= ~(1UL << bitIndex);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BitVector<TStorage> And(in BitVector<TStorage> other)
    {
        var result = new BitVector<TStorage>();

        if (Vector256.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 32)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector256<ulong>>(ref result._storage);

            int vectorCount = Unsafe.SizeOf<TStorage>() / 32;
            for (int i = 0; i < vectorCount; i++)
            {
                Unsafe.Add(ref r, i) = Vector256.BitwiseAnd(
                    Unsafe.Add(ref a, i),
                    Unsafe.Add(ref b, i));
            }
        }
        else if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 16)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector128<ulong>>(ref result._storage);

            int vectorCount = Unsafe.SizeOf<TStorage>() / 16;
            for (int i = 0; i < vectorCount; i++)
            {
                Unsafe.Add(ref r, i) = Vector128.BitwiseAnd(
                    Unsafe.Add(ref a, i),
                    Unsafe.Add(ref b, i));
            }
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, ulong>(ref result._storage);

            for (int i = 0; i < ULongCount; i++)
            {
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) & Unsafe.Add(ref b, i);
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BitVector<TStorage> Or(in BitVector<TStorage> other)
    {
        var result = new BitVector<TStorage>();

        if (Vector256.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 32)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector256<ulong>>(ref result._storage);

            int vectorCount = Unsafe.SizeOf<TStorage>() / 32;
            for (int i = 0; i < vectorCount; i++)
            {
                Unsafe.Add(ref r, i) = Vector256.BitwiseOr(
                    Unsafe.Add(ref a, i),
                    Unsafe.Add(ref b, i));
            }
        }
        else if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 16)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector128<ulong>>(ref result._storage);

            int vectorCount = Unsafe.SizeOf<TStorage>() / 16;
            for (int i = 0; i < vectorCount; i++)
            {
                Unsafe.Add(ref r, i) = Vector128.BitwiseOr(
                    Unsafe.Add(ref a, i),
                    Unsafe.Add(ref b, i));
            }
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, ulong>(ref result._storage);

            for (int i = 0; i < ULongCount; i++)
            {
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) | Unsafe.Add(ref b, i);
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BitVector<TStorage> Xor(in BitVector<TStorage> other)
    {
        var result = new BitVector<TStorage>();

        if (Vector256.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 32)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector256<ulong>>(ref result._storage);

            int vectorCount = Unsafe.SizeOf<TStorage>() / 32;
            for (int i = 0; i < vectorCount; i++)
            {
                Unsafe.Add(ref r, i) = Vector256.Xor(
                    Unsafe.Add(ref a, i),
                    Unsafe.Add(ref b, i));
            }
        }
        else if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 16)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector128<ulong>>(ref result._storage);

            int vectorCount = Unsafe.SizeOf<TStorage>() / 16;
            for (int i = 0; i < vectorCount; i++)
            {
                Unsafe.Add(ref r, i) = Vector128.Xor(
                    Unsafe.Add(ref a, i),
                    Unsafe.Add(ref b, i));
            }
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, ulong>(ref result._storage);

            for (int i = 0; i < ULongCount; i++)
            {
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) ^ Unsafe.Add(ref b, i);
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BitVector<TStorage> AndNot(in BitVector<TStorage> other)
    {
        var result = new BitVector<TStorage>();

        if (Vector256.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 32)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector256<ulong>>(ref result._storage);

            int vectorCount = Unsafe.SizeOf<TStorage>() / 32;
            for (int i = 0; i < vectorCount; i++)
            {
                Unsafe.Add(ref r, i) = Vector256.AndNot(
                    Unsafe.Add(ref b, i),
                    Unsafe.Add(ref a, i));
            }
        }
        else if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 16)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, Vector128<ulong>>(ref result._storage);

            int vectorCount = Unsafe.SizeOf<TStorage>() / 16;
            for (int i = 0; i < vectorCount; i++)
            {
                Unsafe.Add(ref r, i) = Vector128.AndNot(
                    Unsafe.Add(ref b, i),
                    Unsafe.Add(ref a, i));
            }
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));
            ref var r = ref Unsafe.As<TStorage, ulong>(ref result._storage);

            for (int i = 0; i < ULongCount; i++)
            {
                Unsafe.Add(ref r, i) = Unsafe.Add(ref a, i) & ~Unsafe.Add(ref b, i);
            }
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ContainsAll(in BitVector<TStorage> other)
    {
        if (Vector256.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 32)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));

            int vectorCount = Unsafe.SizeOf<TStorage>() / 32;
            for (int i = 0; i < vectorCount; i++)
            {
                var av = Unsafe.Add(ref a, i);
                var bv = Unsafe.Add(ref b, i);
                if (Vector256.BitwiseAnd(av, bv) != bv)
                    return false;
            }
            return true;
        }
        else if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 16)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));

            int vectorCount = Unsafe.SizeOf<TStorage>() / 16;
            for (int i = 0; i < vectorCount; i++)
            {
                var av = Unsafe.Add(ref a, i);
                var bv = Unsafe.Add(ref b, i);
                if (Vector128.BitwiseAnd(av, bv) != bv)
                    return false;
            }
            return true;
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < ULongCount; i++)
            {
                var av = Unsafe.Add(ref a, i);
                var bv = Unsafe.Add(ref b, i);
                if ((av & bv) != bv)
                    return false;
            }
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ContainsAny(in BitVector<TStorage> other)
    {
        if (Vector256.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 32)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));

            int vectorCount = Unsafe.SizeOf<TStorage>() / 32;
            for (int i = 0; i < vectorCount; i++)
            {
                if (Vector256.BitwiseAnd(Unsafe.Add(ref a, i), Unsafe.Add(ref b, i)) != Vector256<ulong>.Zero)
                    return true;
            }
            return false;
        }
        else if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 16)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));

            int vectorCount = Unsafe.SizeOf<TStorage>() / 16;
            for (int i = 0; i < vectorCount; i++)
            {
                if (Vector128.BitwiseAnd(Unsafe.Add(ref a, i), Unsafe.Add(ref b, i)) != Vector128<ulong>.Zero)
                    return true;
            }
            return false;
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < ULongCount; i++)
            {
                if ((Unsafe.Add(ref a, i) & Unsafe.Add(ref b, i)) != 0)
                    return true;
            }
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ContainsNone(in BitVector<TStorage> other)
    {
        return !ContainsAny(in other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int PopCount()
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
    public readonly int FirstSetBit()
    {
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        for (int i = 0; i < ULongCount; i++)
        {
            var word = Unsafe.Add(ref ulongs, i);
            if (word != 0)
            {
                return (i << 6) + BitOperations.TrailingZeroCount(word);
            }
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int LastSetBit()
    {
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        for (int i = ULongCount - 1; i >= 0; i--)
        {
            var word = Unsafe.Add(ref ulongs, i);
            if (word != 0)
            {
                return (i << 6) + 63 - BitOperations.LeadingZeroCount(word);
            }
        }
        return -1;
    }

    public readonly bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (Vector256.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 32)
            {
                ref var vecs = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
                int vectorCount = Unsafe.SizeOf<TStorage>() / 32;
                for (int i = 0; i < vectorCount; i++)
                {
                    if (Unsafe.Add(ref vecs, i) != Vector256<ulong>.Zero)
                        return false;
                }
                return true;
            }
            else if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 16)
            {
                ref var vecs = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
                int vectorCount = Unsafe.SizeOf<TStorage>() / 16;
                for (int i = 0; i < vectorCount; i++)
                {
                    if (Unsafe.Add(ref vecs, i) != Vector128<ulong>.Zero)
                        return false;
                }
                return true;
            }
            else
            {
                ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
                for (int i = 0; i < ULongCount; i++)
                {
                    if (Unsafe.Add(ref ulongs, i) != 0)
                        return false;
                }
                return true;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(in BitVector<TStorage> other)
    {
        if (Vector256.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 32)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));

            int vectorCount = Unsafe.SizeOf<TStorage>() / 32;
            for (int i = 0; i < vectorCount; i++)
            {
                if (Unsafe.Add(ref a, i) != Unsafe.Add(ref b, i))
                    return false;
            }
            return true;
        }
        else if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= 16)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));

            int vectorCount = Unsafe.SizeOf<TStorage>() / 16;
            for (int i = 0; i < vectorCount; i++)
            {
                if (Unsafe.Add(ref a, i) != Unsafe.Add(ref b, i))
                    return false;
            }
            return true;
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < ULongCount; i++)
            {
                if (Unsafe.Add(ref a, i) != Unsafe.Add(ref b, i))
                    return false;
            }
            return true;
        }
    }

    public readonly bool Equals(BitVector<TStorage> other) => Equals(in other);

    public override readonly bool Equals(object? obj) => obj is BitVector<TStorage> other && Equals(in other);

    public override readonly int GetHashCode()
    {
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        var hash = new HashCode();
        for (int i = 0; i < ULongCount; i++)
        {
            hash.Add(Unsafe.Add(ref ulongs, i));
        }
        return hash.ToHashCode();
    }

    public readonly Enumerator GetEnumerator() => new(this);

    public ref struct Enumerator
    {
        private readonly BitVector<TStorage> _bitVector;
        private int _wordIndex;
        private ulong _currentWord;
        private int _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(BitVector<TStorage> bitVector)
        {
            _bitVector = bitVector;
            _wordIndex = 0;
            _current = -1;

            ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _bitVector._storage));
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

                ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _bitVector._storage));
                _currentWord = Unsafe.Add(ref ulongs, _wordIndex);
            }

            int bit = BitOperations.TrailingZeroCount(_currentWord);
            _current = (_wordIndex << 6) + bit;
            _currentWord &= _currentWord - 1;
            return true;
        }
    }
}
