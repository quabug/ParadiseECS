using System.Numerics;
using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A small immutable bitset backed by a single primitive integer type.
/// Supports byte (8 bits), ushort (16 bits), uint (32 bits), and ulong (64 bits).
/// </summary>
/// <typeparam name="T">The underlying integer type (byte, ushort, uint, or ulong).</typeparam>
public readonly record struct SmallBitSet<T> : IBitSet<SmallBitSet<T>>
    where T : unmanaged, IBinaryInteger<T>, IUnsignedNumber<T>
{
    private readonly T _bits;

    /// <summary>
    /// Gets the maximum number of bits this bitset can store.
    /// </summary>
    public static int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Unsafe.SizeOf<T>() * 8;
    }

    /// <summary>
    /// Gets an empty bitset with all bits cleared.
    /// </summary>
    public static SmallBitSet<T> Empty => default;

    /// <summary>
    /// Gets a value indicating whether all bits are cleared.
    /// </summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bits == T.Zero;
    }

    private SmallBitSet(T bits) => _bits = bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(SmallBitSet<T> other) => _bits == other._bits;

    public override int GetHashCode() => _bits.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(int index)
    {
        if ((uint)index >= Capacity)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {Capacity - 1}.");
        return (_bits & (T.One << index)) != T.Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SmallBitSet<T> Set(int index)
    {
        if ((uint)index >= Capacity)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {Capacity - 1}.");
        return new SmallBitSet<T>(_bits | (T.One << index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SmallBitSet<T> Clear(int index)
    {
        if ((uint)index >= Capacity)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {Capacity - 1}.");
        return new SmallBitSet<T>(_bits & ~(T.One << index));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SmallBitSet<T> And(in SmallBitSet<T> other) => new(_bits & other._bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SmallBitSet<T> Or(in SmallBitSet<T> other) => new(_bits | other._bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SmallBitSet<T> AndNot(in SmallBitSet<T> other) => new(_bits & ~other._bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SmallBitSet<T> Xor(in SmallBitSet<T> other) => new(_bits ^ other._bits);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAll(in SmallBitSet<T> other) => (_bits & other._bits) == other._bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsAny(in SmallBitSet<T> other) => (_bits & other._bits) != T.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsNone(in SmallBitSet<T> other) => (_bits & other._bits) == T.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopCount() => int.CreateTruncating(T.PopCount(_bits));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FirstSetBit() => _bits == T.Zero ? -1 : int.CreateTruncating(T.TrailingZeroCount(_bits));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LastSetBit() => _bits == T.Zero ? -1 : Capacity - 1 - int.CreateTruncating(T.LeadingZeroCount(_bits));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ForEach<TAction>(scoped ref TAction action) where TAction : IBitAction, allows ref struct
    {
        T remaining = _bits;
        while (remaining != T.Zero)
        {
            int bitPos = int.CreateTruncating(T.TrailingZeroCount(remaining));
            action.Invoke(bitPos);
            remaining &= remaining - T.One; // Clear lowest set bit
        }
    }

    public override string ToString()
    {
        string hexFormat = Capacity switch
        {
            8 => $"0x{ulong.CreateTruncating(_bits):X2}",
            16 => $"0x{ulong.CreateTruncating(_bits):X4}",
            32 => $"0x{ulong.CreateTruncating(_bits):X8}",
            64 => $"0x{ulong.CreateTruncating(_bits):X16}",
            _ => $"0x{_bits}"
        };
        return $"SmallBitSet<{typeof(T).Name}>({PopCount()} bits set, {hexFormat})";
    }
}
