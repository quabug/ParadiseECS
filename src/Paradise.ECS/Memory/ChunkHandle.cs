namespace Paradise.ECS;

/// <summary>
/// A lightweight handle to a chunk managed by a ChunkManager.
/// Prevents direct unsafe usage of Chunk pointers/structs.
/// </summary>
/// <param name="Id">The index of the chunk in the ChunkManager.</param>
/// <param name="Version">Incrementing version for stale handle detection (48 bits, wraps on overflow).</param>
internal readonly record struct ChunkHandle(int Id, ulong Version)
{
    /// <summary>
    /// The Invalid handle.
    /// </summary>
    public static readonly ChunkHandle Invalid = new(-1, 0);

    /// <summary>
    /// Gets whether this handle is valid (not the Invalid handle).
    /// Note: Does not check if the handle is still active in the manager.
    /// </summary>
    public bool IsValid => Id != -1;

    public override string ToString() =>
        IsValid ? $"ChunkHandle(Id: {Id}, Ver: {Version})" : "ChunkHandle(Invalid)";
}
