using System.Numerics;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A 32-bit immutable bitset using uint storage.
/// </summary>
public readonly record struct ImmutableBitSet32 : IBitSet<ImmutableBitSet32>
{
    private readonly uint _bits;

    /// <summary>
    /// Gets the maximum number of bits this bitset can store.
    /// </summary>
    public static int Capacity => 32;

    /// <summary>
    /// Gets an empty bitset with all bits cleared.
    /// </summary>
    public static ImmutableBitSet32 Empty => default;

    /// <summary>
    /// Gets a value indicating whether all bits are cleared.
    /// </summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bits == 0;
    }

    private ImmutableBitSet32(uint bits) => _bits = bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ImmutableBitSet32 other) => _bits == other._bits;

    public override int GetHashCode() => _bits.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(int index)
    {
        if ((uint)index >= 32)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be between 0 and 31.");
        return (_bits & (1u << index)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet32 Set(int index)
    {
        if ((uint)index >= 32)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be between 0 and 31.");
        return new ImmutableBitSet32(_bits | (1u << index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet32 Clear(int index)
    {
        if ((uint)index >= 32)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be between 0 and 31.");
        return new ImmutableBitSet32(_bits & ~(1u << index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet32 And(in ImmutableBitSet32 other) => new(_bits & other._bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet32 Or(in ImmutableBitSet32 other) => new(_bits | other._bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet32 AndNot(in ImmutableBitSet32 other) => new(_bits & ~other._bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableBitSet32 Xor(in ImmutableBitSet32 other) => new(_bits ^ other._bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAll(in ImmutableBitSet32 other) => (_bits & other._bits) == other._bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAny(in ImmutableBitSet32 other) => (_bits & other._bits) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsNone(in ImmutableBitSet32 other) => (_bits & other._bits) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopCount() => BitOperations.PopCount(_bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FirstSetBit() => _bits == 0 ? -1 : BitOperations.TrailingZeroCount(_bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LastSetBit() => _bits == 0 ? -1 : 31 - BitOperations.LeadingZeroCount(_bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NextSetBit(int afterIndex)
    {
        int startIndex = afterIndex + 1;
        if (startIndex >= 32)
            return -1;
        // Mask off bits at or before startIndex
        uint masked = _bits & (~0u << startIndex);
        return masked == 0 ? -1 : BitOperations.TrailingZeroCount(masked);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ImmutableBitSet32 operator &(in ImmutableBitSet32 left, in ImmutableBitSet32 right)
        => left.And(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ImmutableBitSet32 operator |(in ImmutableBitSet32 left, in ImmutableBitSet32 right)
        => left.Or(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ImmutableBitSet32 operator ^(in ImmutableBitSet32 left, in ImmutableBitSet32 right)
        => left.Xor(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SetBitEnumerator GetEnumerator() => new(_bits);

    public override string ToString() => $"ImmutableBitSet32({PopCount()} bits set, 0x{_bits:X8})";

    public ref struct SetBitEnumerator
    {
        private uint _remaining;
        private int _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SetBitEnumerator(uint bits)
        {
            _remaining = bits;
            _current = -1;
        }

        public int Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _current;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_remaining == 0) return false;
            _current = BitOperations.TrailingZeroCount(_remaining);
            _remaining &= _remaining - 1; // Clear lowest set bit
            return true;
        }
    }
}
