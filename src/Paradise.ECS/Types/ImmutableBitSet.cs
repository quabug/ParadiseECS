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
    public static ImmutableBitSet<TBits> Empty => default;

    /// <summary>
    /// Gets a value indicating whether all bits in this bitset are cleared.
    /// </summary>
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

    /// <summary>
    /// Determines whether the specified bitset is equal to this bitset.
    /// </summary>
    /// <param name="other">The bitset to compare with this bitset.</param>
    /// <returns><c>true</c> if all bits match; otherwise, <c>false</c>.</returns>
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

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var span = GetReadOnlySpan();
        var hash = new HashCode();
        hash.AddBytes(MemoryMarshal.AsBytes(span));
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

    /// <summary>
    /// Gets the value of the bit at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the bit to get.</param>
    /// <returns><c>true</c> if the bit is set; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or greater than or equal to <see cref="Capacity"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(int index)
    {
        if ((uint)index >= (uint)Capacity)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {Capacity - 1}.");
        var span = GetReadOnlySpan();
        return (span[index >> 6] & (1UL << (index & 63))) != 0;
    }

    /// <summary>
    /// Returns a new bitset with the bit at the specified index set to 1.
    /// </summary>
    /// <param name="index">The zero-based index of the bit to set.</param>
    /// <returns>A new bitset with the specified bit set.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or greater than or equal to <see cref="Capacity"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet<TBits> Set(int index)
    {
        if ((uint)index >= (uint)Capacity)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {Capacity - 1}.");
        var newBits = _bits;
        var span = GetSpan(ref newBits);
        span[index >> 6] |= 1UL << (index & 63);
        return new ImmutableBitSet<TBits>(newBits);
    }

    /// <summary>
    /// Returns a new bitset with the bit at the specified index cleared to 0.
    /// </summary>
    /// <param name="index">The zero-based index of the bit to clear.</param>
    /// <returns>A new bitset with the specified bit cleared.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or greater than or equal to <see cref="Capacity"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet<TBits> Clear(int index)
    {
        if ((uint)index >= (uint)Capacity)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {Capacity - 1}.");
        var newBits = _bits;
        var span = GetSpan(ref newBits);
        span[index >> 6] &= ~(1UL << (index & 63));
        return new ImmutableBitSet<TBits>(newBits);
    }

    /// <summary>
    /// Performs a bitwise AND operation with another bitset.
    /// </summary>
    /// <param name="other">The bitset to AND with.</param>
    /// <returns>A new bitset containing the result of the AND operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet<TBits> And(in ImmutableBitSet<TBits> other)
    {
        var result = default(TBits);
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();
        var r = GetSpan(ref result);

        for (int i = 0; i < ULongCount; i++)
            r[i] = a[i] & b[i];

        return new ImmutableBitSet<TBits>(result);
    }

    /// <summary>
    /// Performs a bitwise OR operation with another bitset.
    /// </summary>
    /// <param name="other">The bitset to OR with.</param>
    /// <returns>A new bitset containing the result of the OR operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet<TBits> Or(in ImmutableBitSet<TBits> other)
    {
        var result = default(TBits);
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();
        var r = GetSpan(ref result);

        for (int i = 0; i < ULongCount; i++)
            r[i] = a[i] | b[i];

        return new ImmutableBitSet<TBits>(result);
    }

    /// <summary>
    /// Performs a bitwise AND-NOT operation (this AND NOT other).
    /// </summary>
    /// <param name="other">The bitset to AND-NOT with.</param>
    /// <returns>A new bitset containing bits that are set in this bitset but not in the other.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet<TBits> AndNot(in ImmutableBitSet<TBits> other)
    {
        var result = default(TBits);
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();
        var r = GetSpan(ref result);

        for (int i = 0; i < ULongCount; i++)
            r[i] = a[i] & ~b[i];

        return new ImmutableBitSet<TBits>(result);
    }

    /// <summary>
    /// Performs a bitwise XOR operation with another bitset.
    /// </summary>
    /// <param name="other">The bitset to XOR with.</param>
    /// <returns>A new bitset containing the result of the XOR operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet<TBits> Xor(in ImmutableBitSet<TBits> other)
    {
        var result = default(TBits);
        var a = GetReadOnlySpan();
        var b = other.GetReadOnlySpan();
        var r = GetSpan(ref result);

        for (int i = 0; i < ULongCount; i++)
            r[i] = a[i] ^ b[i];

        return new ImmutableBitSet<TBits>(result);
    }

    /// <summary>
    /// Determines whether this bitset contains all bits that are set in the other bitset.
    /// </summary>
    /// <param name="other">The bitset to check against.</param>
    /// <returns><c>true</c> if this bitset is a superset of the other; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAll(in ImmutableBitSet<TBits> other)
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
    public bool ContainsAny(in ImmutableBitSet<TBits> other)
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
    public bool ContainsNone(in ImmutableBitSet<TBits> other) => !ContainsAny(other);

    /// <summary>
    /// Returns the number of bits that are set in this bitset.
    /// </summary>
    /// <returns>The population count (number of 1 bits).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopCount()
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
    public int FirstSetBit()
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
    public int LastSetBit()
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
    /// Returns the index of the next set bit after the specified index.
    /// </summary>
    /// <param name="afterIndex">The index to start searching after.</param>
    /// <returns>The zero-based index of the next set bit, or -1 if no more bits are set.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NextSetBit(int afterIndex)
    {
        int startIndex = afterIndex + 1;
        if (startIndex >= Capacity)
            return -1;

        var span = GetReadOnlySpan();
        int bucketIndex = startIndex / 64;
        int bitInBucket = startIndex % 64;

        // Check remaining bits in current bucket
        ulong currentBucket = span[bucketIndex];
        // Mask off bits at or before startIndex in this bucket
        currentBucket &= ~((1UL << bitInBucket) - 1);
        if (currentBucket != 0)
        {
            return bucketIndex * 64 + BitOperations.TrailingZeroCount(currentBucket);
        }

        // Check subsequent buckets
        for (int i = bucketIndex + 1; i < ULongCount; i++)
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
    /// Performs a bitwise AND operation between two bitsets.
    /// </summary>
    /// <param name="left">The first bitset.</param>
    /// <param name="right">The second bitset.</param>
    /// <returns>A new bitset containing the result of the AND operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ImmutableBitSet<TBits> operator &(in ImmutableBitSet<TBits> left, in ImmutableBitSet<TBits> right)
        => left.And(right);

    /// <summary>
    /// Performs a bitwise OR operation between two bitsets.
    /// </summary>
    /// <param name="left">The first bitset.</param>
    /// <param name="right">The second bitset.</param>
    /// <returns>A new bitset containing the result of the OR operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ImmutableBitSet<TBits> operator |(in ImmutableBitSet<TBits> left, in ImmutableBitSet<TBits> right)
        => left.Or(right);

    /// <summary>
    /// Performs a bitwise XOR operation between two bitsets.
    /// </summary>
    /// <param name="left">The first bitset.</param>
    /// <param name="right">The second bitset.</param>
    /// <returns>A new bitset containing the result of the XOR operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ImmutableBitSet<TBits> operator ^(in ImmutableBitSet<TBits> left, in ImmutableBitSet<TBits> right)
        => left.Xor(right);

    /// <summary>
    /// Returns an enumerator that iterates through the indices of all set bits.
    /// </summary>
    /// <returns>An enumerator for the set bit indices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SetBitEnumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    public override string ToString() => $"ImmutableBitSet<{typeof(TBits).Name}>({PopCount()} bits set)";

    /// <summary>
    /// Enumerator for iterating through set bit indices in an <see cref="ImmutableBitSet{TBits}"/>.
    /// </summary>
    public ref struct SetBitEnumerator
    {
        private readonly ImmutableBitSet<TBits> _bitset;
        private int _bucketIndex;
        private ulong _currentBucket;
        private int _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SetBitEnumerator(ImmutableBitSet<TBits> bitset)
        {
            _bitset = bitset;
            _bucketIndex = 0;
            _currentBucket = 0;
            _current = -1;

            // Load first non-empty bucket
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
        public int Current
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
                    // Find the lowest set bit in current bucket
                    int bitPos = BitOperations.TrailingZeroCount(_currentBucket);
                    _current = _bucketIndex * 64 + bitPos;

                    // Clear this bit for next iteration
                    _currentBucket &= _currentBucket - 1;
                    return true;
                }

                // Move to next bucket
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
