using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Marker interface for InlineArray backing storage types used by BitSet.
/// </summary>
public interface IStorage;

/// <summary>
/// InlineArray backing storage for 64-bit bitset (1 ulong).
/// </summary>
[InlineArray(1)]
public struct Bits64 : IStorage
{
    private ulong _element0;
}

/// <summary>
/// InlineArray backing storage for 128-bit bitset (2 ulongs).
/// </summary>
[InlineArray(2)]
public struct Bits128 : IStorage
{
    private ulong _element0;
}

/// <summary>
/// InlineArray backing storage for 256-bit bitset (4 ulongs).
/// </summary>
[InlineArray(4)]
public struct Bits256 : IStorage
{
    private ulong _element0;
}

/// <summary>
/// InlineArray backing storage for 512-bit bitset (8 ulongs).
/// </summary>
[InlineArray(8)]
public struct Bits512 : IStorage
{
    private ulong _element0;
}

