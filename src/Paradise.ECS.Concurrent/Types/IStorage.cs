using System.Runtime.CompilerServices;

namespace Paradise.ECS.Concurrent;

public interface IStorage;

[InlineArray(1)]
public struct Bit64 : IStorage
{
    private ulong _element0;
}

[InlineArray(2)]
public struct Bit128 : IStorage
{
    private ulong _element0;
}

[InlineArray(4)]
public struct Bit256 : IStorage
{
    private ulong _element0;
}

[InlineArray(8)]
public struct Bit512 : IStorage
{
    private ulong _element0;
}

[InlineArray(16)]
public struct Bit1024 : IStorage
{
    private ulong _element0;
}
