using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// Manager for Chunk memory allocations.
/// Owns native memory and issues safe handles with version-based stale detection.
/// Single-threaded version without concurrent access support.
///
/// Memory layout:
/// - Uses ChunkArray to store ChunkMeta entries in fixed-size blocks
/// - Each block can hold multiple entries (ChunkSize / sizeof(ChunkMeta))
/// - Blocks are lazily allocated on-demand
/// - Maximum capacity: MaxBlocks * EntriesPerBlock
/// </summary>
public sealed unsafe class ChunkManager : IChunkManager
{
    /// <summary>
    /// Metadata for a single chunk slot.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ChunkMeta
    {
        public ulong Pointer;             // 8 bytes - pointer to data chunk memory
        // Uses PackedVersion for Version (40 bits) and ShareCount (24 bits).
        public ulong VersionAndShareCount; // 8 bytes - PackedVersion raw value
    }

    private readonly int _chunkSize;
    private readonly IAllocator _allocator;
    private readonly ChunkArray<ChunkMeta> _metas;
    private readonly Stack<int> _freeSlots = new();
    private int _nextSlotId; // Next fresh slot ID to allocate
    private bool _disposed;

    /// <inheritdoc />
    public int ChunkSize => _chunkSize;

    /// <summary>
    /// Creates a new ChunkManager with the specified configuration.
    /// </summary>
    /// <param name="allocator">The memory allocator to use.</param>
    /// <param name="chunkSize">The size of each chunk in bytes.</param>
    /// <param name="maxMetaBlocks">The maximum number of meta blocks.</param>
    /// <param name="initializeChunkCapacity">The initial chunk capacity to pre-allocate.</param>
    public ChunkManager(IAllocator allocator, int chunkSize, int maxMetaBlocks, int initializeChunkCapacity)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator), "allocator cannot be null");
        _chunkSize = chunkSize;

        int entriesPerBlock = chunkSize / sizeof(ChunkMeta);
        int initialBlocks = (initializeChunkCapacity + entriesPerBlock - 1) / entriesPerBlock;
        if (initialBlocks < 1) initialBlocks = 1;
        else if (initialBlocks > maxMetaBlocks) initialBlocks = maxMetaBlocks;

        _metas = new ChunkArray<ChunkMeta>(allocator, chunkSize, maxMetaBlocks, initialBlocks);
    }

    public static ChunkManager Create<TConfig>() where TConfig : IConfig, new() => Create(new TConfig());
    public static ChunkManager Create<TConfig>(TConfig config) where TConfig : IConfig, new()
    {
        return new ChunkManager(config.ChunkAllocator, chunkSize: TConfig.ChunkSize, maxMetaBlocks: TConfig.MaxMetaBlocks, initializeChunkCapacity: config.DefaultChunkCapacity);
    }

    /// <summary>
    /// Gets a reference to the metadata for a given slot id.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref ChunkMeta GetMeta(int id) => ref _metas.GetRef(id);

    /// <summary>
    /// Allocates a new Chunk and returns a handle to it.
    /// </summary>
    public ChunkHandle Allocate()
    {
        ThrowHelper.ThrowIfDisposed(_disposed, this);

        if (!_freeSlots.TryPop(out int id))
        {
            // No free slot available, allocate a new one
            id = _nextSlotId;
            _nextSlotId++;

            if (id >= _metas.MaxCapacity)
                ThrowHelper.ThrowChunkManagerCapacityExceeded(_metas.MaxBlocks, _metas.EntriesPerBlock);

            // Ensure the block is allocated
            _metas.EnsureCapacity(id);
        }

        ref var meta = ref GetMeta(id);

        // Allocate data chunk memory if needed (reuse existing if available)
        if (meta.Pointer == 0)
        {
            meta.Pointer = (ulong)_allocator.AllocateZeroed((nuint)_chunkSize);
            // Initialize version to 1 for new slots (version 0 indicates invalid handle)
            meta.VersionAndShareCount = new PackedVersion(version: 1, index: 0).Value;
        }

        return new ChunkHandle(id, new PackedVersion(meta.VersionAndShareCount).Version);
    }

    /// <summary>
    /// Frees the chunk associated with the handle.
    /// Throws if the chunk is currently borrowed.
    /// </summary>
    public void Free(ChunkHandle handle)
    {
        if (!handle.IsValid) return;
        if (_disposed) return;
        if (handle.Id >= _nextSlotId) return;

        ref var meta = ref GetMeta(handle.Id);
        var packed = new PackedVersion(meta.VersionAndShareCount);

        // Check version - if stale, nothing to do
        if (packed.Version != handle.Version)
            return; // Already freed or stale handle

        // Check if chunk is borrowed
        if (packed.Index != 0)
            ThrowHelper.ThrowChunkInUse(handle);

        // Increment version to invalidate all existing handles
        meta.VersionAndShareCount = new PackedVersion(packed.Version + 1, 0).Value;

        // Safe to clear memory
        if (meta.Pointer != 0)
            _allocator.Clear((void*)meta.Pointer, (nuint)_chunkSize);

        _freeSlots.Push(handle.Id);
    }

    /// <summary>
    /// Gets the raw bytes of a chunk without incrementing the borrow count.
    /// Returns an empty span if the handle is invalid or stale.
    /// </summary>
    /// <param name="handle">The chunk handle.</param>
    /// <returns>A span over the chunk's raw bytes, or empty if invalid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetBytes(ChunkHandle handle)
    {
        if (!handle.IsValid)
            return Span<byte>.Empty;

        ThrowHelper.ThrowIfDisposed(_disposed, this);

        if ((uint)handle.Id >= (uint)_nextSlotId)
            return Span<byte>.Empty;

        ref var meta = ref GetMeta(handle.Id);
        var packed = new PackedVersion(meta.VersionAndShareCount);

        if (packed.Version != handle.Version)
            return Span<byte>.Empty; // Stale handle

        return new Span<byte>((void*)meta.Pointer, _chunkSize);
    }

    /// <summary>
    /// Acquires a borrow on a chunk, preventing it from being freed.
    /// Must be paired with a call to <see cref="Release(ChunkHandle)"/>.
    /// </summary>
    /// <param name="handle">The chunk handle.</param>
    /// <returns>True if the borrow was acquired, false if the handle is invalid or stale.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Acquire(ChunkHandle handle)
    {
        if (!handle.IsValid)
            return false;

        if (_disposed)
            return false;

        if ((uint)handle.Id >= (uint)_nextSlotId)
            return false;

        ref var meta = ref GetMeta(handle.Id);
        var packed = new PackedVersion(meta.VersionAndShareCount);

        if (packed.Version != handle.Version)
            return false; // Stale handle

        meta.VersionAndShareCount = new PackedVersion(packed.Version, packed.Index + 1).Value;
        return true;
    }

    /// <summary>
    /// Releases a borrow on a chunk acquired via <see cref="Acquire(ChunkHandle)"/>.
    /// </summary>
    /// <param name="handle">The chunk handle.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release(ChunkHandle handle)
    {
        if (!handle.IsValid) return;
        Release(handle.Id);
    }

    /// <summary>
    /// Releases the borrow on a chunk by ID.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Release(int id)
    {
        if (_disposed) return;
        if (id >= _nextSlotId) return;

        ref var meta = ref GetMeta(id);
        var packed = new PackedVersion(meta.VersionAndShareCount);
        Debug.Assert(packed.Index > 0, "ShareCount underflow - Release called without matching Acquire");
        meta.VersionAndShareCount = new PackedVersion(packed.Version, packed.Index - 1).Value;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Free all data chunk memory first
        for (int id = 0; id < _nextSlotId; id++)
        {
            ref var meta = ref _metas.GetRef(id);
            if (meta.Pointer != 0)
            {
                _allocator.Free((void*)meta.Pointer);
                meta.Pointer = 0;
            }
        }

        // Then dispose the meta list (frees meta blocks)
        _metas.Dispose();

        _freeSlots.Clear();
        _nextSlotId = 0;
    }
}
