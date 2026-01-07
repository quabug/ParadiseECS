using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// Thread-safe manager for Chunk memory allocations.
/// Owns native memory and issues safe handles with version-based stale detection.
///
/// Memory layout:
/// - MetaBlocks: Native memory blocks storing ChunkMeta entries (pointer, version, shareCount)
/// - Each MetaBlock can hold 1024 entries (16KB / 16 bytes per entry)
/// - Growing simply adds a new MetaBlock (no array resize needed)
/// </summary>
public sealed unsafe class ChunkManager : IDisposable
{
    /// <summary>
    /// Metadata for a single chunk slot.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private ref struct ChunkMeta
    {
        public ulong Pointer;   // 8 bytes - pointer to data chunk memory
        public int Version;     // 4 bytes - version for stale handle detection
        public int ShareCount;  // 4 bytes - borrow count
    }

    private const int MetaSize = 16; // sizeof(ChunkMeta): 8 + 4 + 4
    private const int EntriesPerMetaBlock = Chunk.ChunkSize / MetaSize; // 1024
    private const int EntriesPerMetaBlockShift = 10; // log2(1024)
    private const int EntriesPerMetaBlockMask = EntriesPerMetaBlock - 1; // 0x3FF

    private readonly Lock _lock = new();
    private readonly List<nint> _metaBlocks = [];
    private readonly ConcurrentStack<int> _freeSlots = new();
    private int _capacity;
    private int _disposed; // 0 = not disposed, 1 = disposed

    public ChunkManager(int initialCapacity = 256)
    {
        // Round up to nearest meta block boundary
        int metaBlocksNeeded = (initialCapacity + EntriesPerMetaBlock - 1) / EntriesPerMetaBlock;
        if (metaBlocksNeeded < 1) metaBlocksNeeded = 1;

        for (int i = 0; i < metaBlocksNeeded; i++)
        {
            _metaBlocks.Add((nint)NativeMemory.AllocZeroed(Chunk.ChunkSize));
        }

        _capacity = metaBlocksNeeded * EntriesPerMetaBlock;

        // Push all slots to free stack (in reverse for LIFO behavior)
        for (int i = _capacity - 1; i >= 0; i--)
            _freeSlots.Push(i);
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
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        if (!_freeSlots.TryPop(out int id))
        {
            using var _ = _lock.EnterScope();
            // Double-check after acquiring lock
            if (!_freeSlots.TryPop(out id))
            {
                Grow();
                _freeSlots.TryPop(out id);
            }
        }

        ref var meta = ref GetMeta(id);

        // Allocate data chunk memory if needed (reuse existing if available)
        if (meta.Pointer == 0)
            meta.Pointer = (ulong)NativeMemory.AllocZeroed(Chunk.ChunkSize);

        return new ChunkHandle(id, meta.Version);
    }

    /// <summary>
    /// Frees the chunk associated with the handle.
    /// Throws if the chunk is currently borrowed.
    /// </summary>
    public void Free(ChunkHandle handle)
    {
        if (!handle.IsValid || _disposed != 0) return;

        if (handle.Id >= _capacity)
            return;

        ref var meta = ref GetMeta(handle.Id);

        // Check if chunk is borrowed
        if (Volatile.Read(ref meta.ShareCount) > 0)
            ThrowChunkInUse(handle);

        // Increment version for next allocation
        int newVersion = handle.Version + 1;

        // Atomically check and update version
        if (Interlocked.CompareExchange(ref meta.Version, newVersion, handle.Version) != handle.Version)
            return; // Already freed or stale handle

        // Clear memory for security/correctness
        if (meta.Pointer != 0)
            NativeMemory.Clear((void*)meta.Pointer, Chunk.ChunkSize);

        _freeSlots.Push(handle.Id);
    }

    /// <summary>
    /// Gets a Chunk view for the given handle. The chunk borrows the memory
    /// and must be disposed when done to allow freeing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk Get(ChunkHandle handle)
    {
        if ((uint)handle.Id >= (uint)_capacity)
            ThrowInvalidHandle(handle);

        ref var meta = ref GetMeta(handle.Id);

        if (Volatile.Read(ref meta.Version) != handle.Version)
            ThrowStaleHandle(handle);

        Interlocked.Increment(ref meta.ShareCount);
        return new Chunk(this, handle.Id, (void*)meta.Pointer);
    }

    /// <summary>
    /// Tries to get a Chunk view for the given handle. The chunk borrows the memory
    /// and must be disposed when done to allow freeing.
    /// </summary>
    public bool TryGet(ChunkHandle handle, out Chunk chunk)
    {
        if (handle.IsValid && (uint)handle.Id < (uint)_capacity)
        {
            ref var meta = ref GetMeta(handle.Id);

            if (Volatile.Read(ref meta.Version) == handle.Version)
            {
                Interlocked.Increment(ref meta.ShareCount);
                chunk = new Chunk(this, handle.Id, (void*)meta.Pointer);
                return true;
            }
        }

        chunk = default;
        return false;
    }

    /// <summary>
    /// Releases the borrow on a chunk. Called by Chunk.Dispose().
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Release(int id)
    {
        if ((uint)id < (uint)_capacity)
        {
            ref var meta = ref GetMeta(id);
            Interlocked.Decrement(ref meta.ShareCount);
        }
    }

    private void Grow()
    {
        // Simply add a new meta block - no array resize needed!
        _metaBlocks.Add((nint)NativeMemory.AllocZeroed(Chunk.ChunkSize));

        int oldCapacity = _capacity;
        _capacity += EntriesPerMetaBlock;

        // Push new slots to free stack
        for (int i = _capacity - 1; i >= oldCapacity; i--)
        {
            _freeSlots.Push(i);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidHandle(ChunkHandle handle)
        => throw new ArgumentException($"Invalid ChunkHandle: {handle}");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowStaleHandle(ChunkHandle handle)
        => throw new InvalidOperationException($"Stale ChunkHandle usage: {handle}");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowChunkInUse(ChunkHandle handle)
        => throw new InvalidOperationException($"Cannot free chunk while borrowed: {handle}");

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Free all data chunks
        for (int blockIndex = 0; blockIndex < _metaBlocks.Count; blockIndex++)
        {
            var metaBlock = (ChunkMeta*)_metaBlocks[blockIndex];
            int entriesInBlock = blockIndex < _metaBlocks.Count - 1
                ? EntriesPerMetaBlock
                : _capacity - blockIndex * EntriesPerMetaBlock;

            for (int i = 0; i < entriesInBlock; i++)
            {
                if (metaBlock[i].Pointer != 0)
                {
                    NativeMemory.Free((void*)metaBlock[i].Pointer);
                }
            }
        }

        // Free all meta blocks
        foreach (var metaBlock in _metaBlocks)
        {
            NativeMemory.Free((void*)metaBlock);
        }

        _metaBlocks.Clear();
        _capacity = 0;
    }
}
