using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// Thread-safe manager for Chunk memory allocations.
/// Owns native memory and issues safe handles with version-based stale detection.
/// Uses lock-free CAS operations for thread safety.
///
/// Memory layout:
/// - MetaBlocks: Native memory blocks storing ChunkMeta entries (pointer, versionAndShareCount)
/// - Each MetaBlock can hold 1024 entries (16KB / 16 bytes per entry)
/// - Growing simply adds a new MetaBlock (no array resize needed)
/// </summary>
internal sealed unsafe class ChunkManager : IDisposable
{
    /// <summary>
    /// Metadata for a single chunk slot.
    /// Uses explicit layout to create a union: Version and ShareCount can be accessed
    /// individually or as a combined 64-bit value (VersionAndShareCount) for atomic CAS operations.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct ChunkMeta
    {
        [FieldOffset(0)]
        public ulong Pointer;              // 8 bytes - pointer to data chunk memory

        [FieldOffset(8)]
        public ulong VersionAndShareCount; // 8 bytes - for atomic CAS operations

        [FieldOffset(8)]
        public uint ShareCount;            // 4 bytes - overlaps low 32 bits of VersionAndShareCount

        [FieldOffset(12)]
        public uint Version;               // 4 bytes - overlaps high 32 bits of VersionAndShareCount
    }

    private const int MetaSize = 16; // sizeof(ChunkMeta): 8 + 8
    private const int EntriesPerMetaBlock = Chunk.ChunkSize / MetaSize; // 1024
    private const int EntriesPerMetaBlockShift = 10; // log2(1024)
    private const int EntriesPerMetaBlockMask = EntriesPerMetaBlock - 1; // 0x3FF

    private readonly IAllocator _allocator;
    private readonly Lock _lock = new(); // Only used for Grow()
    private readonly List<nint> _metaBlocks = new();
    private readonly ConcurrentStack<int> _freeSlots = new();
    private int _capacity;
    private int _disposed; // 0 = not disposed, 1 = disposed

    /// <summary>
    /// Creates a new ChunkManager with the default <see cref="NativeMemoryAllocator"/>.
    /// </summary>
    /// <param name="initialCapacity">Initial number of chunk slots to allocate.</param>
    public ChunkManager(int initialCapacity = 256)
        : this(NativeMemoryAllocator.Shared, initialCapacity)
    {
    }

    /// <summary>
    /// Creates a new ChunkManager with a custom allocator.
    /// </summary>
    /// <param name="allocator">The allocator to use for memory operations.</param>
    /// <param name="initialCapacity">Initial number of chunk slots to allocate.</param>
    public ChunkManager(IAllocator allocator, int initialCapacity = 256)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));

        // Round up to nearest meta block boundary
        int metaBlocksNeeded = (initialCapacity + EntriesPerMetaBlock - 1) / EntriesPerMetaBlock;
        if (metaBlocksNeeded < 1) metaBlocksNeeded = 1;

        for (int i = 0; i < metaBlocksNeeded; i++)
        {
            _metaBlocks.Add((nint)_allocator.AllocateZeroed(Chunk.ChunkSize));
        }

        _capacity = metaBlocksNeeded * EntriesPerMetaBlock;

        // Push all slots to free stack (in reverse for LIFO behavior)
        for (int i = _capacity - 1; i >= 0; i--)
            _freeSlots.Push(i);
    }

    /// <summary>
    /// Gets the allocator used by this ChunkManager.
    /// </summary>
    public IAllocator Allocator => _allocator;

    /// <summary>
    /// Packs version and shareCount into a single 64-bit value for CAS operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Pack(uint version, uint shareCount)
        => ((ulong)version << 32) | shareCount;

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
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

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
            meta.Pointer = (ulong)_allocator.AllocateZeroed(Chunk.ChunkSize);

        // Direct field access is safe here - slot is exclusively ours after TryPop
        return new ChunkHandle(id, meta.Version);
    }

    /// <summary>
    /// Frees the chunk associated with the handle.
    /// Throws if the chunk is currently borrowed.
    /// Uses lock-free CAS to atomically check ShareCount and increment Version.
    /// </summary>
    public void Free(ChunkHandle handle)
    {
        if (!handle.IsValid || _disposed != 0) return;

        if (handle.Id >= _capacity)
            return;

        ref var meta = ref GetMeta(handle.Id);

        // Lock-free CAS loop to atomically check and update version+shareCount
        while (true)
        {
            ulong current = Volatile.Read(ref meta.VersionAndShareCount);
            uint version = (uint)(current >> 32);
            uint shareCount = (uint)current;

            // Check version - if stale, nothing to do
            if (version != handle.Version)
                return; // Already freed or stale handle

            // Check if chunk is borrowed
            if (shareCount != 0)
                ThrowChunkInUse(handle);

            // Atomically increment version (wraps on overflow) and set shareCount to 0
            ulong next = Pack(version + 1, 0);
            if (Interlocked.CompareExchange(ref meta.VersionAndShareCount, next, current) == current)
                break; // Success

            // CAS failed - another thread modified it, retry
        }

        // Safe to clear memory - version already bumped, no one can Get() with old handle
        if (meta.Pointer != 0)
            _allocator.Clear((void*)meta.Pointer, Chunk.ChunkSize);

        _freeSlots.Push(handle.Id);
    }

    /// <summary>
    /// Gets a Chunk view for the given handle. The chunk borrows the memory
    /// and must be disposed when done to allow freeing.
    /// Returns an invalid (default) chunk if the handle is invalid or stale.
    /// Uses lock-free CAS to atomically check version and increment ShareCount.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Chunk Get(ChunkHandle handle)
    {
        if (!handle.IsValid || (uint)handle.Id >= (uint)_capacity)
            return default;

        ref var meta = ref GetMeta(handle.Id);

        // Lock-free CAS loop to atomically check version and increment shareCount
        while (true)
        {
            ulong current = Volatile.Read(ref meta.VersionAndShareCount);
            uint version = (uint)(current >> 32);
            uint shareCount = (uint)current;

            if (version != handle.Version)
                return default; // Stale handle

            ulong next = Pack(version, shareCount + 1);
            if (Interlocked.CompareExchange(ref meta.VersionAndShareCount, next, current) == current)
                return new Chunk(this, handle.Id, (void*)meta.Pointer);

            // CAS failed - retry
        }
    }

    /// <summary>
    /// Releases the borrow on a chunk. Called by Chunk.Dispose().
    /// Uses lock-free CAS to atomically decrement ShareCount.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Release(int id)
    {
        if ((uint)id >= (uint)_capacity)
            return;

        ref var meta = ref GetMeta(id);

        // Lock-free CAS loop to atomically decrement shareCount
        while (true)
        {
            ulong current = Volatile.Read(ref meta.VersionAndShareCount);
            uint version = (uint)(current >> 32);
            uint shareCount = (uint)current;

            ulong next = Pack(version, shareCount - 1);
            if (Interlocked.CompareExchange(ref meta.VersionAndShareCount, next, current) == current)
                return;

            // CAS failed - retry
        }
    }

    private void Grow()
    {
        // Simply add a new meta block - no array resize needed!
        _metaBlocks.Add((nint)_allocator.AllocateZeroed(Chunk.ChunkSize));

        int oldCapacity = _capacity;
        _capacity += EntriesPerMetaBlock;

        // Push new slots to free stack
        for (int i = _capacity - 1; i >= oldCapacity; i--)
        {
            _freeSlots.Push(i);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowChunkInUse(ChunkHandle handle)
        => throw new InvalidOperationException($"Cannot free chunk while borrowed: {handle}");

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Free all data chunks
        // Note: _capacity is always a multiple of EntriesPerMetaBlock
        for (int blockIndex = 0; blockIndex < _metaBlocks.Count; blockIndex++)
        {
            var metaBlock = (ChunkMeta*)_metaBlocks[blockIndex];
            for (int i = 0; i < EntriesPerMetaBlock; i++)
            {
                if (metaBlock[i].Pointer != 0)
                {
                    _allocator.Free((void*)metaBlock[i].Pointer);
                }
            }
        }

        // Free all meta blocks
        foreach (var metaBlock in _metaBlocks)
        {
            _allocator.Free((void*)metaBlock);
        }

        _metaBlocks.Clear();
        _capacity = 0;
    }
}
