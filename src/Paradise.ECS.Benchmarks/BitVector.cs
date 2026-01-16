using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Paradise.ECS.Concurrent.Benchmarks;

/// <summary>
/// A mutable SIMD-optimized bit vector for benchmarking comparison.
/// Uses Vector256 operations when available for maximum throughput.
/// </summary>
public struct BitVector<TStorage> : IEquatable<BitVector<TStorage>>
    where TStorage : unmanaged, IStorage
{
    private TStorage _storage;

    static BitVector()
    {
        if (Unsafe.SizeOf<TStorage>() % sizeof(ulong) != 0)
#pragma warning disable CA1065 // Intentional: validate storage alignment at type initialization
            throw new InvalidOperationException(
                $"Storage type {typeof(TStorage).Name} size ({Unsafe.SizeOf<TStorage>()} bytes) must be a multiple of {sizeof(ulong)} bytes.");
#pragma warning restore CA1065
    }

    public static BitVector<TStorage> Empty => default;

    public static int Capacity => ByteCount * 8;
    private static int ByteCount => Unsafe.SizeOf<TStorage>();
    private static int ULongCount => ByteCount / sizeof(ulong);
    private static int Vector64Count => ByteCount / 8;
    private static int Vector128Count => ByteCount / 16;
    private static int Vector256Count => ByteCount / 32;
    private static int Vector512Count => ByteCount / 64;

    private static bool UseVector512 => Vector512.IsHardwareAccelerated && ByteCount % 64 == 0;
    private static bool UseVector256 => Vector256.IsHardwareAccelerated && ByteCount % 32 == 0;
    private static bool UseVector128 => Vector128.IsHardwareAccelerated && ByteCount % 16 == 0;
    private static bool UseVector64 => Vector64.IsHardwareAccelerated && ByteCount % 8 == 0;

    /// <summary>
    /// Creates a mutable copy from an immutable bit vector.
    /// </summary>
    /// <param name="immutable">The immutable bit vector to copy from.</param>
    public BitVector(in ImmutableBitVector<TStorage> immutable)
    {
        _storage = Unsafe.As<ImmutableBitVector<TStorage>, TStorage>(ref Unsafe.AsRef(in immutable));
    }

    /// <summary>
    /// Converts this mutable bit vector to an immutable one.
    /// </summary>
    /// <returns>An immutable copy of this bit vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ImmutableBitVector<TStorage> ToImmutable()
    {
        return Unsafe.As<TStorage, ImmutableBitVector<TStorage>>(ref Unsafe.AsRef(in _storage));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Get(int index)
    {
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in _storage));
        int wordIndex = index >> 6;
        int bitIndex = index & 63;
        return (Unsafe.Add(ref ulongs, wordIndex) & (1UL << bitIndex)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index)
    {
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref _storage);
        int wordIndex = index >> 6;
        int bitIndex = index & 63;
        Unsafe.Add(ref ulongs, wordIndex) |= 1UL << bitIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(int index)
    {
        ref var ulongs = ref Unsafe.As<TStorage, ulong>(ref _storage);
        int wordIndex = index >> 6;
        int bitIndex = index & 63;
        Unsafe.Add(ref ulongs, wordIndex) &= ~(1UL << bitIndex);
    }

    /// <summary>
    /// Clears all bits in this bit vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearAll()
    {
        _storage = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void And(in BitVector<TStorage> other)
    {
        if (UseVector512)
        {
            ref var a = ref Unsafe.As<TStorage, Vector512<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector512Count; i++)
                Unsafe.Add(ref a, i) &= Unsafe.Add(ref b, i);
        }
        else if (UseVector256)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector256Count; i++)
                Unsafe.Add(ref a, i) &= Unsafe.Add(ref b, i);
        }
        else if (UseVector128)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector128Count; i++)
                Unsafe.Add(ref a, i) &= Unsafe.Add(ref b, i);
        }
        else if (UseVector64)
        {
            ref var a = ref Unsafe.As<TStorage, Vector64<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector64Count; i++)
                Unsafe.Add(ref a, i) &= Unsafe.Add(ref b, i);
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < ULongCount; i++)
                Unsafe.Add(ref a, i) &= Unsafe.Add(ref b, i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Or(in BitVector<TStorage> other)
    {
        if (UseVector512)
        {
            ref var a = ref Unsafe.As<TStorage, Vector512<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector512Count; i++)
                Unsafe.Add(ref a, i) |= Unsafe.Add(ref b, i);
        }
        else if (UseVector256)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector256Count; i++)
                Unsafe.Add(ref a, i) |= Unsafe.Add(ref b, i);
        }
        else if (UseVector128)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector128Count; i++)
                Unsafe.Add(ref a, i) |= Unsafe.Add(ref b, i);
        }
        else if (UseVector64)
        {
            ref var a = ref Unsafe.As<TStorage, Vector64<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector64Count; i++)
                Unsafe.Add(ref a, i) |= Unsafe.Add(ref b, i);
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < ULongCount; i++)
                Unsafe.Add(ref a, i) |= Unsafe.Add(ref b, i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Xor(in BitVector<TStorage> other)
    {
        if (UseVector512)
        {
            ref var a = ref Unsafe.As<TStorage, Vector512<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector512Count; i++)
                Unsafe.Add(ref a, i) ^= Unsafe.Add(ref b, i);
        }
        else if (UseVector256)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector256Count; i++)
                Unsafe.Add(ref a, i) ^= Unsafe.Add(ref b, i);
        }
        else if (UseVector128)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector128Count; i++)
                Unsafe.Add(ref a, i) ^= Unsafe.Add(ref b, i);
        }
        else if (UseVector64)
        {
            ref var a = ref Unsafe.As<TStorage, Vector64<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector64Count; i++)
                Unsafe.Add(ref a, i) ^= Unsafe.Add(ref b, i);
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < ULongCount; i++)
                Unsafe.Add(ref a, i) ^= Unsafe.Add(ref b, i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AndNot(in BitVector<TStorage> other)
    {
        if (UseVector512)
        {
            ref var a = ref Unsafe.As<TStorage, Vector512<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector512<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector512Count; i++)
                Unsafe.Add(ref a, i) &= ~Unsafe.Add(ref b, i);
        }
        else if (UseVector256)
        {
            ref var a = ref Unsafe.As<TStorage, Vector256<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector256<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector256Count; i++)
                Unsafe.Add(ref a, i) &= ~Unsafe.Add(ref b, i);
        }
        else if (UseVector128)
        {
            ref var a = ref Unsafe.As<TStorage, Vector128<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector128<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector128Count; i++)
                Unsafe.Add(ref a, i) &= ~Unsafe.Add(ref b, i);
        }
        else if (UseVector64)
        {
            ref var a = ref Unsafe.As<TStorage, Vector64<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector64<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < Vector64Count; i++)
                Unsafe.Add(ref a, i) &= ~Unsafe.Add(ref b, i);
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < ULongCount; i++)
                Unsafe.Add(ref a, i) &= ~Unsafe.Add(ref b, i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ContainsAll(in BitVector<TStorage> other)
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
    public readonly bool ContainsAny(in BitVector<TStorage> other)
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
    public readonly bool ContainsNone(in BitVector<TStorage> other) => !ContainsAny(in other);

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
                return (i << 6) + BitOperations.TrailingZeroCount(word);
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
                return (i << 6) + 63 - BitOperations.LeadingZeroCount(word);
        }
        return -1;
    }

    public readonly bool IsEmpty
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
    public readonly bool Equals(in BitVector<TStorage> other)
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

    public static bool operator ==(in BitVector<TStorage> left, in BitVector<TStorage> right) => left.Equals(in right);

    public static bool operator !=(in BitVector<TStorage> left, in BitVector<TStorage> right) => !left.Equals(in right);

    /// <summary>
    /// Implicit conversion from mutable to immutable bit vector.
    /// </summary>
    public static implicit operator ImmutableBitVector<TStorage>(in BitVector<TStorage> bitVector) => bitVector.ToImmutable();

    /// <summary>
    /// Explicit conversion from immutable to mutable bit vector.
    /// </summary>
    public static explicit operator BitVector<TStorage>(in ImmutableBitVector<TStorage> immutable) => new(in immutable);

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
