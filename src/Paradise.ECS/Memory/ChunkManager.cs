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
/// - Each MetaBlock can hold 1024 entries (16KB / 16 bytes per entry)
/// - Meta blocks are lazily allocated on-demand
/// - Maximum capacity: MaxMetaBlocks * EntriesPerMetaBlock (~1M chunks)
/// </summary>
public sealed unsafe class ChunkManager : IDisposable
{
    /// <summary>
    /// Metadata for a single chunk slot.
    /// Uses PackedVersion for Version (40 bits) and ShareCount (24 bits).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ChunkMeta
    {
        public ulong Pointer;             // 8 bytes - pointer to data chunk memory
        public ulong VersionAndShareCount; // 8 bytes - PackedVersion raw value
    }

    private const int MetaSize = 16; // sizeof(ChunkMeta): 8 + 8
    private const int EntriesPerMetaBlock = Chunk.ChunkSize / MetaSize; // 1024
    private const int EntriesPerMetaBlockShift = 10; // log2(1024)
    private const int EntriesPerMetaBlockMask = EntriesPerMetaBlock - 1; // 0x3FF
    private const int MaxMetaBlocks = 1024; // ~1M chunks max capacity (16GB)

    private readonly IAllocator _allocator;
    private readonly nint[] _metaBlocks = new nint[MaxMetaBlocks];
    private readonly Stack<int> _freeSlots = new();
    private int _nextSlotId; // Next fresh slot ID to allocate
    private bool _disposed;

    /// <summary>
    /// Creates a new ChunkManager with the specified allocator.
    /// </summary>
    /// <param name="allocator">The allocator to use for memory operations.</param>
    /// <param name="initialCapacity">Initial number of chunk slots to pre-allocate meta blocks for.</param>
    public ChunkManager(IAllocator allocator, int initialCapacity = 256)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));

        // Pre-allocate meta blocks for initial capacity
        int metaBlocksNeeded = (initialCapacity + EntriesPerMetaBlock - 1) / EntriesPerMetaBlock;
        if (metaBlocksNeeded < 1) metaBlocksNeeded = 1;
        else if (metaBlocksNeeded > MaxMetaBlocks) metaBlocksNeeded = MaxMetaBlocks;

        for (int i = 0; i < metaBlocksNeeded; i++)
        {
            _metaBlocks[i] = (nint)_allocator.AllocateZeroed(Chunk.ChunkSize);
        }
    }

    /// <summary>
    /// Gets a reference to the metadata for a given slot id.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref ChunkMeta GetMeta(int id)
    {
        int blockIndex = id >> EntriesPerMetaBlockShift;
        int indexInBlock = id & EntriesPerMetaBlockMask;
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

            int blockIndex = id >> EntriesPerMetaBlockShift;
            if (blockIndex >= MaxMetaBlocks)
                ThrowCapacityExceeded();

            // Ensure the meta block is allocated
            EnsureBlockAllocated(blockIndex);
        }

        ref var meta = ref GetMeta(id);

        // Allocate data chunk memory if needed (reuse existing if available)
        if (meta.Pointer == 0)
        {
            meta.Pointer = (ulong)_allocator.AllocateZeroed(Chunk.ChunkSize);
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
            _metaBlocks[blockIndex] = (nint)_allocator.AllocateZeroed(Chunk.ChunkSize);
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
            ThrowChunkInUse(handle);

        // Increment version to invalidate all existing handles
        meta.VersionAndShareCount = new PackedVersion(packed.Version + 1, 0).Value;

        // Safe to clear memory
        if (meta.Pointer != 0)
            _allocator.Clear((void*)meta.Pointer, Chunk.ChunkSize);

        _freeSlots.Push(handle.Id);
    }

    /// <summary>
    /// Gets a Chunk view for the given handle. The chunk borrows the memory
    /// and must be disposed when done to allow freeing.
    /// Returns an invalid (default) chunk if the handle is invalid or stale.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk Get(ChunkHandle handle)
    {
        if (!handle.IsValid)
            return default;

        ThrowHelper.ThrowIfDisposed(_disposed, this);

        if ((uint)handle.Id >= (uint)_nextSlotId)
            return default;

        ref var meta = ref GetMeta(handle.Id);
        var packed = new PackedVersion(meta.VersionAndShareCount);

        if (packed.Version != handle.Version)
            return default; // Stale handle

        meta.VersionAndShareCount = new PackedVersion(packed.Version, packed.Index + 1).Value;
        return new Chunk(this, handle.Id, (nint)meta.Pointer);
    }

    /// <summary>
    /// Releases the borrow on a chunk. Called by Chunk.Dispose().
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Release(int id)
    {
        if (_disposed) return;
        if (id >= _nextSlotId) return;

        ref var meta = ref GetMeta(id);
        var packed = new PackedVersion(meta.VersionAndShareCount);
        meta.VersionAndShareCount = new PackedVersion(packed.Version, packed.Index - 1).Value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowChunkInUse(ChunkHandle handle)
        => throw new InvalidOperationException($"Cannot free chunk while borrowed: {handle}");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCapacityExceeded()
        => throw new InvalidOperationException($"ChunkManager capacity exceeded (max {MaxMetaBlocks * EntriesPerMetaBlock} chunks)");

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Free all data chunks and meta blocks
        for (int blockIndex = 0; blockIndex < MaxMetaBlocks; blockIndex++)
        {
            nint metaBlockPtr = _metaBlocks[blockIndex];
            if (metaBlockPtr == 0)
                continue;

            var metaBlock = (ChunkMeta*)metaBlockPtr;
            for (int i = 0; i < EntriesPerMetaBlock; i++)
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
