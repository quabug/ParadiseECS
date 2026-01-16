using System.Runtime.CompilerServices;

namespace Paradise.ECS.Concurrent;

/// <summary>
/// Tracks where an entity's component data is stored.
/// Maps an entity to its archetype and position within a chunk.
/// </summary>
/// <remarks>
/// This struct is stored in an array indexed by Entity.Id for O(1) lookups.
/// The Version field allows detecting stale entity handles.
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
    /// The chunk handle where this entity's data is stored.
    /// </summary>
    public ChunkHandle ChunkHandle;

    /// <summary>
    /// The index of this entity within the chunk.
    /// </summary>
    public int IndexInChunk;

    /// <summary>
    /// An invalid/empty entity location.
    /// </summary>
    public static readonly EntityLocation Invalid = new()
    {
        Version = 0,
        ArchetypeId = -1,
        ChunkHandle = ChunkHandle.Invalid,
        IndexInChunk = -1
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
            ? $"EntityLocation(Ver: {Version}, Arch: {ArchetypeId}, Chunk: {ChunkHandle.Id}, Index: {IndexInChunk})"
            : "EntityLocation(Invalid)";
    }
}
