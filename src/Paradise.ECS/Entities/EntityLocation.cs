using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Tracks where an entity's component data is stored.
/// Maps an entity to its archetype and global index within the archetype.
/// </summary>
/// <remarks>
/// This struct is stored in an array indexed by Entity.Id for O(1) lookups.
/// The Version field allows detecting stale entity handles.
/// ChunkIndex and IndexInChunk can be derived from GlobalIndex using the archetype's EntitiesPerChunk.
/// </remarks>
public struct EntityLocation
{
    /// <summary>
    /// The version of the entity at this slot.
    /// Used to validate entity handles are not stale.
    /// </summary>
    public uint Version;

    /// <summary>
    /// The archetype ID this entity belongs to.
    /// -1 indicates the entity is not alive or has no archetype.
    /// </summary>
    public int ArchetypeId;

    /// <summary>
    /// The global index of this entity within the archetype.
    /// Use archetype.GetChunkLocation() to derive ChunkIndex and IndexInChunk.
    /// </summary>
    public int GlobalIndex;

    /// <summary>
    /// An invalid/empty entity location.
    /// </summary>
    public static readonly EntityLocation Invalid = new()
    {
        Version = 0,
        ArchetypeId = -1,
        GlobalIndex = -1
    };

    /// <summary>
    /// Gets whether this location is valid (has a valid archetype).
    /// </summary>
    public readonly bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ArchetypeId >= 0;
    }

    /// <summary>
    /// Checks if this location matches the given entity's version.
    /// </summary>
    /// <param name="entity">The entity to check.</param>
    /// <returns>True if the versions match and the entity is valid at this location.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool MatchesEntity(Entity entity)
    {
        return Version == entity.Version && Version > 0;
    }

    public override readonly string ToString()
    {
        return IsValid
            ? $"EntityLocation(Ver: {Version}, Arch: {ArchetypeId}, Index: {GlobalIndex})"
            : "EntityLocation(Invalid)";
    }
}
