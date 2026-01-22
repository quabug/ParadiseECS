namespace Paradise.ECS;

/// <summary>
/// Callback interface for iterating over set bits in a bitset.
/// Implement as a struct for optimal performance (enables JIT inlining).
/// </summary>
public interface IBitAction
{
    /// <summary>
    /// Called for each set bit in the bitset.
    /// </summary>
    /// <param name="bitIndex">The index of the set bit.</param>
    void Invoke(int bitIndex);
}

/// <summary>
/// Interface for fixed-size bitset operations used in archetype matching.
/// </summary>
public interface IBitSet<TSelf> : IEquatable<TSelf>
    where TSelf : unmanaged, IBitSet<TSelf>
{
    /// <summary>
    /// Maximum number of bits this bitset can hold.
    /// </summary>
    static abstract int Capacity { get; }

    /// <summary>
    /// An empty bitset with all bits cleared.
    /// </summary>
    static abstract TSelf Empty { get; }

    /// <summary>
    /// Gets whether all bits are zero.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Gets the bit at the specified index.
    /// </summary>
    bool Get(int index);

    /// <summary>
    /// Returns a new bitset with the specified bit set.
    /// </summary>
    TSelf Set(int index);

    /// <summary>
    /// Returns a new bitset with the specified bit cleared.
    /// </summary>
    TSelf Clear(int index);

    /// <summary>
    /// Bitwise AND.
    /// </summary>
    TSelf And(in TSelf other);

    /// <summary>
    /// Bitwise OR.
    /// </summary>
    TSelf Or(in TSelf other);

    /// <summary>
    /// Bitwise AND NOT (this &amp; ~other).
    /// </summary>
    TSelf AndNot(in TSelf other);

    /// <summary>
    /// Returns true if this contains all bits set in other: (this &amp; other) == other
    /// </summary>
    bool ContainsAll(in TSelf other);

    /// <summary>
    /// Returns true if this contains any bit set in other: (this &amp; other) != 0
    /// </summary>
    bool ContainsAny(in TSelf other);

    /// <summary>
    /// Returns true if this contains no bits set in other: (this &amp; other) == 0
    /// </summary>
    bool ContainsNone(in TSelf other);

    /// <summary>
    /// Counts the number of set bits.
    /// </summary>
    int PopCount();

    /// <summary>
    /// Returns the index of the first (lowest) bit that is set.
    /// </summary>
    /// <returns>The zero-based index of the first set bit, or -1 if no bits are set.</returns>
    int FirstSetBit();

    /// <summary>
    /// Returns the index of the last (highest) bit that is set.
    /// </summary>
    /// <returns>The zero-based index of the last set bit, or -1 if no bits are set.</returns>
    int LastSetBit();

    /// <summary>
    /// Iterates over all set bits and invokes the action for each.
    /// Implement this method with optimized bucket-based iteration for best performance.
    /// </summary>
    /// <typeparam name="TAction">The action type (use struct for inlining).</typeparam>
    /// <param name="action">The action to invoke for each set bit.</param>
    void ForEach<TAction>(scoped ref TAction action) where TAction : IBitAction, allows ref struct;
}
