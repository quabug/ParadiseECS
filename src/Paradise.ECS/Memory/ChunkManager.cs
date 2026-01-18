using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// Manager for Chunk memory allocations.
/// Owns native memory and issues safe handles with version-based stale detection.
/// Single-threaded version without concurrent access support.
///
/// Memory layout:
/// - MetaBlocks: Fixed-size array of pointers to native memory blocks storing ChunkMeta entries
/// - Each MetaBlock can hold EntriesPerMetaBlock entries (ChunkSize / 16 bytes per entry)
/// - Meta blocks are lazily allocated on-demand
/// - Maximum capacity: MaxMetaBlocks * EntriesPerMetaBlock
/// </summary>
public sealed unsafe class ChunkManager : IChunkManager
{
    /// <summary>
    /// Metadata for a single chunk slot.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ChunkMeta
    {
        public ulong Pointer;             // 8 bytes - pointer to data chunk memory
        // Uses PackedVersion for Version (40 bits) and ShareCount (24 bits).
        public ulong VersionAndShareCount; // 8 bytes - PackedVersion raw value
    }

    private readonly int _chunkSize;
    private readonly int _maxMetaBlocks;
    private readonly int _entriesPerMetaBlock;
    private readonly int _entriesPerMetaBlockShift;
    private readonly int _entriesPerMetaBlockMask;

    private readonly IAllocator _allocator;
    private readonly nint[] _metaBlocks;
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
        _maxMetaBlocks = maxMetaBlocks;
        _entriesPerMetaBlock = chunkSize / sizeof(ChunkMeta);
        _entriesPerMetaBlockShift = BitOperations.Log2((uint)_entriesPerMetaBlock);
        _entriesPerMetaBlockMask = _entriesPerMetaBlock - 1;
        _metaBlocks = new nint[maxMetaBlocks];

        // Pre-allocate meta blocks for initial capacity
        int metaBlocksNeeded = (initializeChunkCapacity + _entriesPerMetaBlock - 1) / _entriesPerMetaBlock;
        if (metaBlocksNeeded < 1) metaBlocksNeeded = 1;
        else if (metaBlocksNeeded > maxMetaBlocks) metaBlocksNeeded = maxMetaBlocks;

        for (int i = 0; i < metaBlocksNeeded; i++)
        {
            _metaBlocks[i] = (nint)_allocator.AllocateZeroed((nuint)chunkSize);
        }
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
    private ref ChunkMeta GetMeta(int id)
    {
        int blockIndex = id >> _entriesPerMetaBlockShift;
        int indexInBlock = id & _entriesPerMetaBlockMask;
        return ref ((ChunkMeta*)_metaBlocks[blockIndex])[indexInBlock];
    }

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

            int blockIndex = id >> _entriesPerMetaBlockShift;
            if (blockIndex >= _maxMetaBlocks)
                ThrowHelper.ThrowChunkManagerCapacityExceeded(_maxMetaBlocks, _entriesPerMetaBlock);

            // Ensure the meta block is allocated
            EnsureBlockAllocated(blockIndex);
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
    /// Ensures the meta block at the given index is allocated.
    /// </summary>
    private void EnsureBlockAllocated(int blockIndex)
    {
        if (_metaBlocks[blockIndex] == 0)
        {
            _metaBlocks[blockIndex] = (nint)_allocator.AllocateZeroed((nuint)_chunkSize);
        }
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

        // Free all data chunks and meta blocks
        for (int blockIndex = 0; blockIndex < _maxMetaBlocks; blockIndex++)
        {
            nint metaBlockPtr = _metaBlocks[blockIndex];
            if (metaBlockPtr == 0)
                continue;

            var metaBlock = (ChunkMeta*)metaBlockPtr;
            for (int i = 0; i < _entriesPerMetaBlock; i++)
            {
                if (metaBlock[i].Pointer != 0)
                {
                    _allocator.Free((void*)metaBlock[i].Pointer);
                }
            }

            _allocator.Free((void*)metaBlockPtr);
            _metaBlocks[blockIndex] = 0;
        }

        _freeSlots.Clear();
        _nextSlotId = 0;
    }
}
