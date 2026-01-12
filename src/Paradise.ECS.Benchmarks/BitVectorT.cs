using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS.Benchmarks;

/// <summary>
/// A mutable SIMD-optimized bit vector using platform-agnostic Vector&lt;T&gt;.
/// Automatically uses the widest available SIMD registers on the current hardware.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BitVectorT<TStorage> : IEquatable<BitVectorT<TStorage>>
    where TStorage : unmanaged, IStorage
{
    private TStorage _storage;

    public static BitVectorT<TStorage> Empty => default;

    public static int Capacity => Unsafe.SizeOf<TStorage>() * 8;

    private static int ULongCount => Unsafe.SizeOf<TStorage>() / sizeof(ulong);

    private static int VectorCount => Unsafe.SizeOf<TStorage>() / Vector<ulong>.Count / sizeof(ulong);

    private static bool UseVector => Vector.IsHardwareAccelerated && Unsafe.SizeOf<TStorage>() >= Vector<ulong>.Count * sizeof(ulong);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void And(in BitVectorT<TStorage> other)
    {
        if (UseVector)
        {
            ref var a = ref Unsafe.As<TStorage, Vector<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < VectorCount; i++)
            {
                Unsafe.Add(ref a, i) = Vector.BitwiseAnd(
                    Unsafe.Add(ref a, i),
                    Unsafe.Add(ref b, i));
            }
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < ULongCount; i++)
            {
                Unsafe.Add(ref a, i) &= Unsafe.Add(ref b, i);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Or(in BitVectorT<TStorage> other)
    {
        if (UseVector)
        {
            ref var a = ref Unsafe.As<TStorage, Vector<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < VectorCount; i++)
            {
                Unsafe.Add(ref a, i) = Vector.BitwiseOr(
                    Unsafe.Add(ref a, i),
                    Unsafe.Add(ref b, i));
            }
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < ULongCount; i++)
            {
                Unsafe.Add(ref a, i) |= Unsafe.Add(ref b, i);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Xor(in BitVectorT<TStorage> other)
    {
        if (UseVector)
        {
            ref var a = ref Unsafe.As<TStorage, Vector<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < VectorCount; i++)
            {
                Unsafe.Add(ref a, i) = Vector.Xor(
                    Unsafe.Add(ref a, i),
                    Unsafe.Add(ref b, i));
            }
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < ULongCount; i++)
            {
                Unsafe.Add(ref a, i) ^= Unsafe.Add(ref b, i);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AndNot(in BitVectorT<TStorage> other)
    {
        if (UseVector)
        {
            ref var a = ref Unsafe.As<TStorage, Vector<ulong>>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, Vector<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < VectorCount; i++)
            {
                Unsafe.Add(ref a, i) = Vector.AndNot(
                    Unsafe.Add(ref b, i),
                    Unsafe.Add(ref a, i));
            }
        }
        else
        {
            ref var a = ref Unsafe.As<TStorage, ulong>(ref _storage);
            ref var b = ref Unsafe.As<TStorage, ulong>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < ULongCount; i++)
            {
                Unsafe.Add(ref a, i) &= ~Unsafe.Add(ref b, i);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ContainsAll(in BitVectorT<TStorage> other)
    {
        if (UseVector)
        {
            ref var a = ref Unsafe.As<TStorage, Vector<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < VectorCount; i++)
            {
                var av = Unsafe.Add(ref a, i);
                var bv = Unsafe.Add(ref b, i);
                if (Vector.BitwiseAnd(av, bv) != bv)
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
    public readonly bool ContainsAny(in BitVectorT<TStorage> other)
    {
        if (UseVector)
        {
            ref var a = ref Unsafe.As<TStorage, Vector<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < VectorCount; i++)
            {
                if (Vector.BitwiseAnd(Unsafe.Add(ref a, i), Unsafe.Add(ref b, i)) != Vector<ulong>.Zero)
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
    public readonly bool ContainsNone(in BitVectorT<TStorage> other)
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
            if (UseVector)
            {
                ref var vecs = ref Unsafe.As<TStorage, Vector<ulong>>(ref Unsafe.AsRef(in _storage));
                for (int i = 0; i < VectorCount; i++)
                {
                    if (Unsafe.Add(ref vecs, i) != Vector<ulong>.Zero)
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
    public readonly bool Equals(in BitVectorT<TStorage> other)
    {
        if (UseVector)
        {
            ref var a = ref Unsafe.As<TStorage, Vector<ulong>>(ref Unsafe.AsRef(in _storage));
            ref var b = ref Unsafe.As<TStorage, Vector<ulong>>(ref Unsafe.AsRef(in other._storage));

            for (int i = 0; i < VectorCount; i++)
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

    public readonly bool Equals(BitVectorT<TStorage> other) => Equals(in other);

    public override readonly bool Equals(object? obj) => obj is BitVectorT<TStorage> other && Equals(in other);

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
        private readonly BitVectorT<TStorage> _bitVector;
        private int _wordIndex;
        private ulong _currentWord;
        private int _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(BitVectorT<TStorage> bitVector)
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
