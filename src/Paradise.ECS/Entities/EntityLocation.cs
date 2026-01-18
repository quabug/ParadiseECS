using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// Tracks where an entity's component data is stored as a 64-bit packed struct.
/// Maps an entity to its archetype and global index within the archetype.
/// </summary>
/// <remarks>
/// Packing format (64 bits total):
/// - Bits 0-23 (24 bits): Version (0 to 16,777,215)
/// - Bits 24-43 (20 bits): ArchetypeId + 1 (0 = invalid/-1, 1-1,048,576 = 0-1,048,575)
/// - Bits 44-63 (20 bits): GlobalIndex + 1 (0 = invalid/-1, 1-1,048,576 = 0-1,048,575)
///
/// The Version field allows detecting stale entity handles.
/// ChunkIndex and IndexInChunk can be derived from GlobalIndex using the archetype's EntitiesPerChunk.
/// The 64-bit packed format allows using <see cref="Interlocked.CompareExchange(ref long, long, long)"/>
/// for lock-free atomic updates.
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = 8)]
public readonly struct EntityLocation : IEquatable<EntityLocation>
{
    private const int VersionBits = 24;
    private const int ArchetypeBits = 20;
    private const int IndexBits = 20;

    private const ulong VersionMask = (1UL << VersionBits) - 1;
    private const ulong ArchetypeMask = (1UL << ArchetypeBits) - 1;
    private const ulong IndexMask = (1UL << IndexBits) - 1;

    private const int ArchetypeShift = VersionBits;
    private const int IndexShift = VersionBits + ArchetypeBits;

    /// <summary>
    /// Maximum supported version value (16,777,215).
    /// </summary>
    public const uint MaxVersion = (1U << VersionBits) - 1;

    /// <summary>
    /// Maximum supported archetype ID (1,048,574).
    /// </summary>
    public const int MaxArchetypeId = (1 << ArchetypeBits) - 2; // -1 reserved for invalid

    /// <summary>
    /// Maximum supported global index (1,048,574).
    /// </summary>
    public const int MaxGlobalIndex = (1 << IndexBits) - 2; // -1 reserved for invalid

    [FieldOffset(0)]
    private readonly ulong _packed;

    /// <summary>
    /// Creates a new entity location from raw packed value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EntityLocation(ulong packed)
    {
        _packed = packed;
    }

    /// <summary>
    /// Creates a new entity location.
    /// </summary>
    /// <param name="version">The entity version.</param>
    /// <param name="archetypeId">The archetype ID (-1 for invalid).</param>
    /// <param name="globalIndex">The global index within the archetype (-1 for invalid).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityLocation(uint version, int archetypeId, int globalIndex)
    {
        // Clamp version to max if it exceeds (wrap around behavior)
        uint clampedVersion = version & (uint)VersionMask;

        // Offset by 1 so -1 becomes 0, 0 becomes 1, etc.
        ulong packedArchetype = (ulong)(archetypeId + 1) & ArchetypeMask;
        ulong packedIndex = (ulong)(globalIndex + 1) & IndexMask;

        _packed = clampedVersion
            | (packedArchetype << ArchetypeShift)
            | (packedIndex << IndexShift);
    }

    /// <summary>
    /// Gets the raw packed value for atomic operations.
    /// </summary>
    public ulong Packed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _packed;
    }

    /// <summary>
    /// Gets the entity version.
    /// </summary>
    public uint Version
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (uint)(_packed & VersionMask);
    }

    /// <summary>
    /// Gets the archetype ID (-1 if invalid).
    /// </summary>
    public int ArchetypeId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (int)((_packed >> ArchetypeShift) & ArchetypeMask) - 1;
    }

    /// <summary>
    /// Gets the global index within the archetype (-1 if invalid).
    /// </summary>
    public int GlobalIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (int)((_packed >> IndexShift) & IndexMask) - 1;
    }

    /// <summary>
    /// Gets whether this location is valid (has a valid archetype).
    /// </summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ArchetypeId >= 0;
    }

    /// <summary>
    /// Creates an entity location from a raw packed value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityLocation FromPacked(ulong packed) => new(packed);

    /// <summary>
    /// An invalid/empty entity location.
    /// </summary>
    public static readonly EntityLocation Invalid = new(0, -1, -1);

    /// <summary>
    /// Checks if this location matches the given entity's version.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <returns>True if the versions match and the entity is valid at this location.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MatchesEntity(Entity entity)
    {
        return Version == entity.Version && Version > 0;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(EntityLocation other) => _packed == other._packed;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EntityLocation other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _packed.GetHashCode();

    /// <summary>
    /// Equality operator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(EntityLocation left, EntityLocation right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(EntityLocation left, EntityLocation right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString()
    {
        return IsValid
            ? $"EntityLocation(Ver: {Version}, Arch: {ArchetypeId}, Index: {GlobalIndex})"
            : "EntityLocation(Invalid)";
    }
}
