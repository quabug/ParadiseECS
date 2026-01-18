namespace Paradise.ECS;

/// <summary>
/// Interface for chunk memory management.
/// Provides allocation, deallocation, and access to chunk memory with version-based stale detection.
/// </summary>
public interface IChunkManager : IDisposable
{
    /// <summary>
    /// Gets the size of each chunk in bytes.
    /// </summary>
    int ChunkSize { get; }

    /// <summary>
    /// Allocates a new chunk and returns a handle to it.
    /// </summary>
    /// <returns>A valid handle to the newly allocated chunk.</returns>
    ChunkHandle Allocate();

    /// <summary>
    /// Frees the chunk associated with the handle.
    /// Throws if the chunk is currently borrowed.
    /// </summary>
    /// <param name="handle">The chunk handle to free.</param>
    void Free(ChunkHandle handle);

    /// <summary>
    /// Gets the raw bytes of a chunk without incrementing the borrow count.
    /// Returns an empty span if the handle is invalid or stale.
    /// </summary>
    /// <param name="handle">The chunk handle.</param>
    /// <returns>A span over the chunk's raw bytes, or empty if invalid.</returns>
    Span<byte> GetBytes(ChunkHandle handle);

    /// <summary>
    /// Acquires a borrow on a chunk, preventing it from being freed.
    /// Must be paired with a call to <see cref="Release(ChunkHandle)"/>.
    /// </summary>
    /// <param name="handle">The chunk handle.</param>
    /// <returns>True if the borrow was acquired, false if the handle is invalid or stale.</returns>
    bool Acquire(ChunkHandle handle);

    /// <summary>
    /// Releases a borrow on a chunk acquired via <see cref="Acquire(ChunkHandle)"/>.
    /// </summary>
    /// <param name="handle">The chunk handle.</param>
    void Release(ChunkHandle handle);
}
