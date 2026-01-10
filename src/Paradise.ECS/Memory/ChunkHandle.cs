using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// A lightweight handle to a chunk managed by a ChunkManager.
/// Prevents direct unsafe usage of Chunk pointers/structs.
/// The default value represents an invalid handle; valid handles have Version >= 1.
/// </summary>
public readonly record struct ChunkHandle
{
    private readonly PackedVersion _packed;

    /// <summary>
    /// Creates a new ChunkHandle from an Id and Version.
    /// </summary>
    /// <param name="id">The index of the chunk in the ChunkManager (0 to ~1M-1).</param>
    /// <param name="version">Incrementing version for stale handle detection (must be >= 1 for valid handles).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunkHandle(int id, ulong version)
        => _packed = new PackedVersion(version, id);

    /// <summary>
    /// The index of the chunk in the ChunkManager.
    /// </summary>
    public int Id
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _packed.Index;
    }

    /// <summary>
    /// Incrementing version for stale handle detection (44 bits, wraps on overflow).
    /// Valid handles have Version >= 1; Version 0 indicates an invalid handle.
    /// </summary>
    public ulong Version
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _packed.Version;
    }

    /// <summary>
    /// The Invalid handle (default value, all zeros).
    /// </summary>
    public static readonly ChunkHandle Invalid = default;

    /// <summary>
    /// Gets whether this handle is valid (Version >= 1).
    /// Note: Does not check if the handle is still active in the manager.
    /// </summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _packed.IsValid;
    }

    public override string ToString() =>
        IsValid ? $"ChunkHandle(Id: {Id}, Ver: {Version})" : "ChunkHandle(Invalid)";
}
