using System.Runtime.CompilerServices;

namespace Paradise.ECS.Concurrent;

/// <summary>
/// A packed 64-bit value with a 40-bit Version (upper) and 24-bit Index (lower).
/// Used as the backing storage for ChunkHandle and ChunkManager's VersionAndShareCount.
/// The default value (0) represents an invalid state; valid instances have Version >= 1.
/// </summary>
internal readonly record struct PackedVersion
{
    private const int IndexBits = 24;
    private const ulong IndexMask = (1UL << IndexBits) - 1; // 0xFFFFFF (max ~16M)

    private readonly ulong _value;

    /// <summary>
    /// Creates a PackedVersion from a raw packed value.
    /// </summary>
    /// <param name="value">The raw packed value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PackedVersion(ulong value) => _value = value;

    /// <summary>
    /// Creates a new PackedVersion from a version and index.
    /// </summary>
    /// <param name="version">The version (40 bits, must be >= 1 for valid state).</param>
    /// <param name="index">The index (24 bits, 0 to ~16M-1).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PackedVersion(ulong version, int index)
        => _value = (version << IndexBits) | ((ulong)index & IndexMask);

    /// <summary>
    /// The raw packed value.
    /// </summary>
    public ulong Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }

    /// <summary>
    /// The version (upper 40 bits). Valid instances have Version >= 1.
    /// </summary>
    public ulong Version
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value >> IndexBits;
    }

    /// <summary>
    /// The index (lower 24 bits).
    /// </summary>
    public int Index
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (int)(_value & IndexMask);
    }

    /// <summary>
    /// Gets whether this is valid (Version >= 1).
    /// </summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value >= (1UL << IndexBits); // Optimized: single comparison, no shift needed
    }
}
