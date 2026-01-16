namespace Paradise.ECS.Concurrent;

/// <summary>
/// Global limits for the ECS system.
/// These constraints are based on the archetype graph edge key packing.
/// </summary>
public static class EcsLimits
{
    /// <summary>
    /// Maximum supported archetype ID (20 bits = 1,048,575).
    /// </summary>
    public const int MaxArchetypeId = (1 << EdgeKey.ArchetypeBits) - 1;

    /// <summary>
    /// Maximum supported component type ID (11 bits = 2,047).
    /// </summary>
    public const int MaxComponentTypeId = (1 << EdgeKey.ComponentBits) - 1;
}
