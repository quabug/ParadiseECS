using System.Diagnostics;

namespace Paradise.ECS;

/// <summary>
/// Key for archetype graph edges. Packed into 32 bits.
/// </summary>
internal readonly struct EdgeKey : IEquatable<EdgeKey>
{
    /// <summary>
    /// Number of bits used for component type ID (including remove flag).
    /// </summary>
    internal const int TypeBits = 12;

    /// <summary>
    /// Number of bits used for component ID (excluding remove flag).
    /// </summary>
    internal const int ComponentBits = TypeBits - 1;

    /// <summary>
    /// Number of bits used for archetype ID.
    /// </summary>
    internal const int ArchetypeBits = sizeof(uint) * 8 - TypeBits;

    private const uint RemoveFlag = 1u << ComponentBits;

    private readonly uint _value;

    private EdgeKey(uint value) => _value = value;

    public static EdgeKey ForAdd(int archetypeId, int componentId)
    {
        Debug.Assert(archetypeId >= 0 && archetypeId <= IWorldConfig.MaxArchetypeId);
        Debug.Assert(componentId >= 0 && componentId <= IWorldConfig.MaxComponentTypeId);
        return new EdgeKey(((uint)archetypeId << TypeBits) | (uint)componentId);
    }

    public static EdgeKey ForRemove(int archetypeId, int componentId)
    {
        Debug.Assert(archetypeId >= 0 && archetypeId <= IWorldConfig.MaxArchetypeId);
        Debug.Assert(componentId >= 0 && componentId <= IWorldConfig.MaxComponentTypeId);
        return new EdgeKey(((uint)archetypeId << TypeBits) | (uint)componentId | RemoveFlag);
    }

    public bool Equals(EdgeKey other) => _value == other._value;
    public override bool Equals(object? obj) => obj is EdgeKey other && Equals(other);
    public override int GetHashCode() => (int)_value;
}
