using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS.Concurrent.Benchmarks;

/// <summary>
/// A mutable fixed-size bitset for component masks, generic over the backing storage.
/// Uses InlineArray for efficient, stack-allocated storage.
/// </summary>
/// <typeparam name="TBits">An InlineArray of ulongs (e.g., Bits128, Bits256).</typeparam>
public struct BitSet<TBits> : IEquatable<BitSet<TBits>>
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

    private TBits _bits;

    private static int ULongCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    } = ValidateAndGetULongCount();

    /// <summary>
    /// Gets the maximum number of bits this bitset can store.
    /// </summary>
    public static int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ULongCount * 64;
    }

    /// <summary>
    /// Gets an empty bitset with all bits cleared.
    /// </summary>
    public static BitSet<TBits> Empty => default;

    /// <summary>
    /// Gets a value indicating whether all bits in this bitset are cleared.
    /// </summary>
    public readonly bool IsEmpty
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

    /// <summary>
    /// Creates a new bitset from the specified bits.
    /// </summary>
    /// <param name="bits">The backing storage.</param>
    public BitSet(TBits bits) => _bits = bits;

    /// <summary>
    /// Creates a mutable copy from an immutable bitset.
    /// </summary>
    /// <param name="immutable">The immutable bitset to copy from.</param>
    public BitSet(in ImmutableBitSet<TBits> immutable)
    {
        _bits = Unsafe.As<ImmutableBitSet<TBits>, TBits>(ref Unsafe.AsRef(in immutable));
    }

    /// <summary>
    /// Converts this mutable bitset to an immutable one.
    /// </summary>
    /// <returns>An immutable copy of this bitset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ImmutableBitSet<TBits> ToImmutable()
    {
        return Unsafe.As<TBits, ImmutableBitSet<TBits>>(ref Unsafe.AsRef(in _bits));
    }

    /// <summary>
    /// Determines whether the specified bitset is equal to this bitset.
    /// </summary>
    /// <param name="other">The bitset to compare with this bitset.</param>
    /// <returns><c>true</c> if all bits match; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(BitSet<TBits> other)
    {
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();
        for (int i = 0; i < ULongCount; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is BitSet<TBits> other && Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        var span = GetReadOnlySpan();
        var hash = new HashCode();
        for (int i = 0; i < ULongCount; i++)
            hash.Add(span[i]);
        return hash.ToHashCode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly ReadOnlySpan<ulong> GetReadOnlySpan()
    {
        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<TBits, ulong>(ref Unsafe.AsRef(in _bits)),
            ULongCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<ulong> GetSpan()
    {
        return MemoryMarshal.CreateSpan(
            ref Unsafe.As<TBits, ulong>(ref _bits),
            ULongCount);
    }

    /// <summary>
    /// Gets the value of the bit at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the bit to get.</param>
    /// <returns><c>true</c> if the bit is set; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or greater than or equal to <see cref="Capacity"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Get(int index)
    {
        if ((uint)index >= (uint)Capacity)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {Capacity - 1}.");
        var span = GetReadOnlySpan();
        return (span[index >> 6] & (1UL << (index & 63))) != 0;
    }

    /// <summary>
    /// Sets the bit at the specified index to 1.
    /// </summary>
    /// <param name="index">The zero-based index of the bit to set.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or greater than or equal to <see cref="Capacity"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index)
    {
        if ((uint)index >= (uint)Capacity)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {Capacity - 1}.");
        var span = GetSpan();
        span[index >> 6] |= 1UL << (index & 63);
    }

    /// <summary>
    /// Clears the bit at the specified index to 0.
    /// </summary>
    /// <param name="index">The zero-based index of the bit to clear.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or greater than or equal to <see cref="Capacity"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(int index)
    {
        if ((uint)index >= (uint)Capacity)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {Capacity - 1}.");
        var span = GetSpan();
        span[index >> 6] &= ~(1UL << (index & 63));
    }

    /// <summary>
    /// Clears all bits in this bitset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearAll()
    {
        _bits = default;
    }

    /// <summary>
    /// Performs a bitwise AND operation with another bitset, modifying this instance.
    /// </summary>
    /// <param name="other">The bitset to AND with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void And(in BitSet<TBits> other)
    {
        var a = GetSpan();
        var b = other.GetReadOnlySpan();

        for (int i = 0; i < ULongCount; i++)
            a[i] &= b[i];
    }

    /// <summary>
    /// Performs a bitwise OR operation with another bitset, modifying this instance.
    /// </summary>
    /// <param name="other">The bitset to OR with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Or(in BitSet<TBits> other)
    {
        var a = GetSpan();
        var b = other.GetReadOnlySpan();

        for (int i = 0; i < ULongCount; i++)
            a[i] |= b[i];
    }

    /// <summary>
    /// Performs a bitwise AND-NOT operation (this AND NOT other), modifying this instance.
    /// </summary>
    /// <param name="other">The bitset to AND-NOT with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AndNot(in BitSet<TBits> other)
    {
        var a = GetSpan();
        var b = other.GetReadOnlySpan();

        for (int i = 0; i < ULongCount; i++)
            a[i] &= ~b[i];
    }

    /// <summary>
    /// Performs a bitwise XOR operation with another bitset, modifying this instance.
    /// </summary>
    /// <param name="other">The bitset to XOR with.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Xor(in BitSet<TBits> other)
    {
        var a = GetSpan();
        var b = other.GetReadOnlySpan();

        for (int i = 0; i < ULongCount; i++)
            a[i] ^= b[i];
    }

    /// <summary>
    /// Determines whether this bitset contains all bits that are set in the other bitset.
    /// </summary>
    /// <param name="other">The bitset to check against.</param>
    /// <returns><c>true</c> if this bitset is a superset of the other; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ContainsAll(in BitSet<TBits> other)
    {
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();

        for (int i = 0; i < ULongCount; i++)
        {
            if ((a[i] & b[i]) != b[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Determines whether this bitset contains any bits that are set in the other bitset.
    /// </summary>
    /// <param name="other">The bitset to check against.</param>
    /// <returns><c>true</c> if the bitsets have any overlapping bits; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ContainsAny(in BitSet<TBits> other)
    {
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();

        for (int i = 0; i < ULongCount; i++)
        {
            if ((a[i] & b[i]) != 0) return true;
        }
        return false;
    }

    /// <summary>
    /// Determines whether this bitset contains none of the bits that are set in the other bitset.
    /// </summary>
    /// <param name="other">The bitset to check against.</param>
    /// <returns><c>true</c> if the bitsets have no overlapping bits; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ContainsNone(in BitSet<TBits> other) => !ContainsAny(other);

    /// <summary>
    /// Returns the number of bits that are set in this bitset.
    /// </summary>
    /// <returns>The population count (number of 1 bits).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int PopCount()
    {
        var span = GetReadOnlySpan();
        int count = 0;
        for (int i = 0; i < ULongCount; i++)
            count += BitOperations.PopCount(span[i]);
        return count;
    }

    /// <summary>
    /// Returns the index of the first (lowest) bit that is set.
    /// </summary>
    /// <returns>The zero-based index of the first set bit, or -1 if no bits are set.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int FirstSetBit()
    {
        var span = GetReadOnlySpan();
        for (int i = 0; i < ULongCount; i++)
        {
            ulong value = span[i];
            if (value != 0)
            {
                return i * 64 + BitOperations.TrailingZeroCount(value);
            }
        }
        return -1;
    }

    /// <summary>
    /// Returns the index of the last (highest) bit that is set.
    /// </summary>
    /// <returns>The zero-based index of the last set bit, or -1 if no bits are set.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int LastSetBit()
    {
        var span = GetReadOnlySpan();
        for (int i = ULongCount - 1; i >= 0; i--)
        {
            ulong value = span[i];
            if (value != 0)
            {
                return i * 64 + (63 - BitOperations.LeadingZeroCount(value));
            }
        }
        return -1;
    }

    /// <summary>
    /// Performs a bitwise AND operation between two bitsets.
    /// </summary>
    /// <param name="left">The first bitset.</param>
    /// <param name="right">The second bitset.</param>
    /// <returns>A new bitset containing the result of the AND operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BitSet<TBits> operator &(in BitSet<TBits> left, in BitSet<TBits> right)
    {
        var result = left;
        result.And(right);
        return result;
    }

    /// <summary>
    /// Performs a bitwise OR operation between two bitsets.
    /// </summary>
    /// <param name="left">The first bitset.</param>
    /// <param name="right">The second bitset.</param>
    /// <returns>A new bitset containing the result of the OR operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BitSet<TBits> operator |(in BitSet<TBits> left, in BitSet<TBits> right)
    {
        var result = left;
        result.Or(right);
        return result;
    }

    /// <summary>
    /// Performs a bitwise XOR operation between two bitsets.
    /// </summary>
    /// <param name="left">The first bitset.</param>
    /// <param name="right">The second bitset.</param>
    /// <returns>A new bitset containing the result of the XOR operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BitSet<TBits> operator ^(in BitSet<TBits> left, in BitSet<TBits> right)
    {
        var result = left;
        result.Xor(right);
        return result;
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(in BitSet<TBits> left, in BitSet<TBits> right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(in BitSet<TBits> left, in BitSet<TBits> right) => !left.Equals(right);

    /// <summary>
    /// Implicit conversion from mutable to immutable bitset.
    /// </summary>
    public static implicit operator ImmutableBitSet<TBits>(in BitSet<TBits> bitSet) => bitSet.ToImmutable();

    /// <summary>
    /// Explicit conversion from immutable to mutable bitset.
    /// </summary>
    public static explicit operator BitSet<TBits>(in ImmutableBitSet<TBits> immutable) => new(in immutable);

    /// <summary>
    /// Returns an enumerator that iterates through the indices of all set bits.
    /// </summary>
    /// <returns>An enumerator for the set bit indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly SetBitEnumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    public override readonly string ToString() => $"BitSet<{typeof(TBits).Name}>({PopCount()} bits set)";

    /// <summary>
    /// Enumerator for iterating through set bit indices in a <see cref="BitSet{TBits}"/>.
    /// </summary>
    public ref struct SetBitEnumerator
    {
        private readonly BitSet<TBits> _bitset;
        private int _bucketIndex;
        private ulong _currentBucket;
        private int _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SetBitEnumerator(BitSet<TBits> bitset)
        {
            _bitset = bitset;
            _bucketIndex = 0;
            _currentBucket = 0;
            _current = -1;

            var span = bitset.GetReadOnlySpan();
            while (_bucketIndex < ULongCount && span[_bucketIndex] == 0)
            {
                _bucketIndex++;
            }

            if (_bucketIndex < ULongCount)
            {
                _currentBucket = span[_bucketIndex];
            }
        }

        /// <summary>
        /// Gets the current set bit index.
        /// </summary>
        public readonly int Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }

        /// <summary>
        /// Advances the enumerator to the next set bit.
        /// </summary>
        /// <returns><c>true</c> if there is another set bit; otherwise, <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (_bucketIndex < ULongCount)
            {
                if (_currentBucket != 0)
                {
                    int bitPos = BitOperations.TrailingZeroCount(_currentBucket);
                    _current = _bucketIndex * 64 + bitPos;

                    _currentBucket &= _currentBucket - 1;
                    return true;
                }

                _bucketIndex++;
                if (_bucketIndex < ULongCount)
                {
                    _currentBucket = _bitset.GetReadOnlySpan()[_bucketIndex];
                }
            }

            return false;
        }
    }
}
