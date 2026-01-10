using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// Thread-safe manager for Chunk memory allocations.
/// Owns native memory and issues safe handles with version-based stale detection.
/// Uses fully lock-free CAS operations for thread safety.
///
/// Memory layout:
/// - MetaBlocks: Fixed-size array of pointers to native memory blocks storing ChunkMeta entries
/// - Each MetaBlock can hold 1024 entries (16KB / 16 bytes per entry)
/// - Meta blocks are lazily allocated on-demand using CAS
/// - Maximum capacity: MaxMetaBlocks * EntriesPerMetaBlock (~1M chunks)
/// </summary>
public sealed unsafe class ChunkManager : IDisposable
{
    /// <summary>
    /// Metadata for a single chunk slot.
    /// Uses PackedVersion for Version (44 bits) and ShareCount (20 bits) packed for atomic CAS operations.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ChunkMeta
    {
        public ulong Pointer;             // 8 bytes - pointer to data chunk memory
        public ulong VersionAndShareCount; // 8 bytes - PackedVersion raw value for atomic CAS
    }

    private const int MetaSize = 16; // sizeof(ChunkMeta): 8 + 8
    private const int EntriesPerMetaBlock = Chunk.ChunkSize / MetaSize; // 1024
    private const int EntriesPerMetaBlockShift = 10; // log2(1024)
    private const int EntriesPerMetaBlockMask = EntriesPerMetaBlock - 1; // 0x3FF
    private const int MaxMetaBlocks = 1024; // ~1M chunks max capacity (16GB)

    private readonly IAllocator _allocator;
    private readonly nint[] _metaBlocks = new nint[MaxMetaBlocks];
    private readonly ConcurrentStack<int> _freeSlots = new();
    private int _nextSlotId; // Next fresh slot ID to allocate (atomic)
    private int _disposed; // 0 = not disposed, 1 = disposed

    [SuppressMessage("Style", "IDE0044:Add readonly modifier")]
    private int _activeOperations; // Count of in-flight operations (for safe disposal)

    /// <summary>
    /// Begins an operation scope that prevents disposal until complete.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private OperationGuard BeginOperation()
    {
        return new OperationGuard(ref _activeOperations);
    }

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
    /// <param name="initialCapacity">Initial number of chunk slots to pre-allocate meta blocks for.</param>
    public ChunkManager(IAllocator allocator, int initialCapacity = 256)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));

        // Pre-allocate meta blocks for initial capacity (optional optimization)
        int metaBlocksNeeded = (initialCapacity + EntriesPerMetaBlock - 1) / EntriesPerMetaBlock;
        if (metaBlocksNeeded < 1) metaBlocksNeeded = 1;
        else if (metaBlocksNeeded > MaxMetaBlocks) metaBlocksNeeded = MaxMetaBlocks;

        for (int i = 0; i < metaBlocksNeeded; i++)
        {
            _metaBlocks[i] = (nint)_allocator.AllocateZeroed(Chunk.ChunkSize);
        }

        // _nextSlotId starts at 0 - slots are allocated on-demand
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
    /// Uses fully lock-free operations.
    /// </summary>
    public ChunkHandle Allocate()
    {
        using var _ = BeginOperation();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        if (!_freeSlots.TryPop(out int id))
        {
            // No free slot available, allocate a new one
            id = Interlocked.Increment(ref _nextSlotId) - 1;

            int blockIndex = id >> EntriesPerMetaBlockShift;
            if (blockIndex >= MaxMetaBlocks)
                ThrowCapacityExceeded();

            // Ensure the meta block is allocated (lock-free)
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

        // Direct field access is safe here - slot is exclusively ours
        return new ChunkHandle(id, new PackedVersion(meta.VersionAndShareCount).Version);
    }

    /// <summary>
    /// Ensures the meta block at the given index is allocated.
    /// Uses CAS with a sentinel marker to claim the slot before allocating.
    /// </summary>
    private void EnsureBlockAllocated(int blockIndex)
    {
        ref var slot = ref _metaBlocks[blockIndex];

        var sw = new SpinWait();
        while ((long)Volatile.Read(ref slot) <= 0)
        {
            var prev = Interlocked.CompareExchange(ref slot, -1, 0);
            if (prev == 0)
            {
                Volatile.Write(ref slot, (nint)_allocator.AllocateZeroed(Chunk.ChunkSize));
                return;
            }
            if ((long)prev > 0)
                return; // Another thread finished allocation
            sw.SpinOnce(); // prev == AllocatingMarker, wait
        }
    }

    /// <summary>
    /// Frees the chunk associated with the handle.
    /// Throws if the chunk is currently borrowed.
    /// Uses lock-free CAS to atomically check ShareCount and increment Version.
    /// </summary>
    public void Free(ChunkHandle handle)
    {
        if (!handle.IsValid) return;

        using var _ = BeginOperation();
        if (_disposed != 0) return;

        if (handle.Id >= Volatile.Read(ref _nextSlotId)) return;

        ref var meta = ref GetMeta(handle.Id);

        // Lock-free CAS loop to atomically check and update version+shareCount
        while (true)
        {
            ulong current = Volatile.Read(ref meta.VersionAndShareCount);
            var packed = new PackedVersion(current);

            // Check version - if stale, nothing to do
            if (packed.Version != handle.Version)
                return; // Already freed or stale handle

            // Check if chunk is borrowed
            if (packed.Index != 0)
                ThrowChunkInUse(handle);

            // Atomically increment version (wraps on overflow) and set shareCount to 0
            ulong next = new PackedVersion(packed.Version + 1, 0).Value;
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
        if (!handle.IsValid)
            return default;

        using var _ = BeginOperation();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        if ((uint)handle.Id >= (uint)Volatile.Read(ref _nextSlotId))
            return default;

        ref var meta = ref GetMeta(handle.Id);

        // Lock-free CAS loop to atomically check version and increment shareCount
        while (true)
        {
            ulong current = Volatile.Read(ref meta.VersionAndShareCount);
            var packed = new PackedVersion(current);

            if (packed.Version != handle.Version)
                return default; // Stale handle

            ulong next = new PackedVersion(packed.Version, packed.Index + 1).Value;
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
        using var _ = BeginOperation();
        if (_disposed != 0) return;

        if (id >= Volatile.Read(ref _nextSlotId))
            return;

        ref var meta = ref GetMeta(id);

        // Lock-free CAS loop to atomically decrement shareCount
        while (true)
        {
            ulong current = Volatile.Read(ref meta.VersionAndShareCount);
            var packed = new PackedVersion(current);
            Debug.Assert(packed.Index > 0, "ShareCount underflow - Release called without matching Get");

            ulong next = new PackedVersion(packed.Version, packed.Index - 1).Value;
            if (Interlocked.CompareExchange(ref meta.VersionAndShareCount, next, current) == current)
                return;

            // CAS failed - retry
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowChunkInUse(ChunkHandle handle)
        => throw new InvalidOperationException($"Cannot free chunk while borrowed: {handle}");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCapacityExceeded()
        => throw new InvalidOperationException($"ChunkManager capacity exceeded (max {MaxMetaBlocks * EntriesPerMetaBlock} chunks)");

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Wait for all in-flight operations to complete
        var sw = new SpinWait();
        while (Volatile.Read(ref _activeOperations) > 0)
            sw.SpinOnce();

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

        _nextSlotId = 0;
    }
}
