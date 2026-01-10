using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// Header portion of the archetype layout data.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
[StructLayout(LayoutKind.Sequential)]
public struct ArchetypeLayoutHeader<TBits>
    where TBits : unmanaged, IStorage
{
    /// <summary>Maximum entities that fit in a single chunk for this archetype.</summary>
    public int EntitiesPerChunk;

    /// <summary>Number of component types in this archetype.</summary>
    public int ComponentCount;

    /// <summary>Minimum component ID in this archetype (for offset calculation).</summary>
    public int MinComponentId;

    /// <summary>Maximum component ID in this archetype.</summary>
    public int MaxComponentId;

    /// <summary>Byte offset to BaseOffsets array (short[]) relative to data start.</summary>
    public int BaseOffsetsOffset;

    /// <summary>The component mask for this archetype.</summary>
    public ImmutableBitSet<TBits> ComponentMask;

    /// <summary>Size of this header in bytes.</summary>
    public static int SizeInBytes => Unsafe.SizeOf<ArchetypeLayoutHeader<TBits>>();
}

/// <summary>
/// Describes the memory layout of component data within a chunk for a specific archetype.
/// Uses Struct of Arrays (SoA) layout where each component type has a contiguous array.
/// This provides better cache utilization and SIMD opportunities when iterating components.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <remarks>
/// Memory layout example for 100 entities with Position(12B), Velocity(12B), Health(8B):
/// <code>
/// Chunk: [Position×100][Velocity×100][Health×100]
///        |--1200B----|---1200B-----|---800B---|
/// </code>
/// Each ArchetypeLayout owns its own native memory block containing:
/// <code>
/// [ArchetypeLayoutHeader&lt;TBits&gt;][BaseOffsets (short[])]
/// </code>
/// BaseOffsets uses short (2 bytes) indexed by (componentId - minComponentId).
/// -1 indicates component not present; valid offsets are 0 to 32767.
/// Component sizes should be looked up from ComponentTypeInfo globally.
/// </remarks>
public sealed unsafe class ImmutableArchetypeLayout<TBits> : IDisposable
    where TBits : unmanaged, IStorage
{
    private readonly IAllocator _allocator;
    private byte* _data;

    /// <summary>
    /// Gets the maximum number of entities that fit in a single chunk.
    /// </summary>
    public int EntitiesPerChunk
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return Header.EntitiesPerChunk;
        }
    }

    /// <summary>
    /// Gets the number of component types in this archetype.
    /// </summary>
    public int ComponentCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return Header.ComponentCount;
        }
    }

    /// <summary>
    /// Gets the minimum component ID in this archetype.
    /// </summary>
    public int MinComponentId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return Header.MinComponentId;
        }
    }

    /// <summary>
    /// Gets the maximum component ID in this archetype.
    /// </summary>
    public int MaxComponentId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return Header.MaxComponentId;
        }
    }

    /// <summary>
    /// Gets the component mask for this archetype.
    /// </summary>
    public ImmutableBitSet<TBits> ComponentMask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return Header.ComponentMask;
        }
    }

    private ref ArchetypeLayoutHeader<TBits> Header
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.AsRef<ArchetypeLayoutHeader<TBits>>(_data);
    }

    /// <summary>
    /// Creates a new archetype layout from a component mask and global component info lookup.
    /// </summary>
    /// <param name="componentMask">The component mask defining which components are in this archetype.</param>
    /// <param name="globalComponentInfos">Global component type information array indexed by component ID.</param>
    /// <param name="allocator">The memory allocator to use. If null, uses <see cref="NativeMemoryAllocator.Shared"/>.</param>
    public ImmutableArchetypeLayout(ImmutableBitSet<TBits> componentMask, ImmutableArray<ComponentTypeInfo> globalComponentInfos, IAllocator? allocator = null)
    {
        _allocator = allocator ?? NativeMemoryAllocator.Shared;

        // Calculate min/max component ID range using FirstSetBit/LastSetBit
        int minId = componentMask.FirstSetBit();
        int maxId = componentMask.LastSetBit();

        // Handle empty archetype
        if (minId < 0)
        {
            minId = 0;
            maxId = -1; // Results in componentSlots = 0
        }

        int componentSlots = maxId - minId + 1;
        int componentCount = componentMask.PopCount();

        // Layout: [Header<TBits>][BaseOffsets (short[])]
        int baseOffsetsOffset = AlignUp(ArchetypeLayoutHeader<TBits>.SizeInBytes, sizeof(ushort));
        int totalBytes = baseOffsetsOffset + componentSlots * sizeof(ushort);

        _data = (byte*)_allocator.Allocate((nuint)totalBytes);

        // Initialize header
        ref var header = ref Header;
        header.ComponentCount = componentCount;
        header.MinComponentId = minId;
        header.MaxComponentId = maxId;
        header.BaseOffsetsOffset = baseOffsetsOffset;
        header.ComponentMask = componentMask;

        // Initialize baseOffsets to -1 (0xFFFF in two's complement, so InitBlock with 0xFF works)
        short* baseOffsets = (short*)(_data + baseOffsetsOffset);
        Unsafe.InitBlock(baseOffsets, 0xFF, (uint)(componentSlots * sizeof(short)));

        InitializeLayout(componentMask, globalComponentInfos);
    }

    private void InitializeLayout(ImmutableBitSet<TBits> componentMask, ImmutableArray<ComponentTypeInfo> globalComponentInfos)
    {
        ref var header = ref Header;
        short* baseOffsets = (short*)(_data + header.BaseOffsetsOffset);
        int minId = header.MinComponentId;

        if (componentMask.IsEmpty)
        {
            header.EntitiesPerChunk = Chunk.ChunkSize;
            return;
        }

        // Build component infos from mask using enumerator
        // (enumerator yields IDs in ascending order, which is already sorted by alignment)
        int componentCount = header.ComponentCount;
        Span<ComponentTypeInfo> components = stackalloc ComponentTypeInfo[componentCount];
        int idx = 0;
        foreach (int componentId in componentMask)
        {
            components[idx++] = globalComponentInfos[componentId];
        }

        // Calculate total size per entity (without alignment)
        int totalSizePerEntity = 0;
        foreach (var comp in components)
        {
            totalSizePerEntity += comp.Size;
        }

        if (totalSizePerEntity == 0)
        {
            header.EntitiesPerChunk = Chunk.ChunkSize;
            // Mark all tag components as present (offset 0)
            foreach (var comp in components)
            {
                baseOffsets[comp.Id.Value - minId] = 0;
            }
            return;
        }

        // Start with optimistic estimate
        int entitiesPerChunk = Chunk.ChunkSize / totalSizePerEntity;
        if (entitiesPerChunk == 0)
            entitiesPerChunk = 1;

        // Calculate base offsets and verify fit, adjusting entitiesPerChunk if needed
        while (entitiesPerChunk > 1)
        {
            int totalSize = CalculateAndStoreOffsets(components, baseOffsets, minId, entitiesPerChunk);
            if (totalSize <= Chunk.ChunkSize)
                break;
            entitiesPerChunk--;
        }

        // Final calculation with the determined entitiesPerChunk
        if (entitiesPerChunk == 1)
        {
            CalculateAndStoreOffsets(components, baseOffsets, minId, entitiesPerChunk);
        }

        header.EntitiesPerChunk = entitiesPerChunk;
    }

    private static int CalculateAndStoreOffsets(
        ReadOnlySpan<ComponentTypeInfo> sortedComponents,
        short* baseOffsets,
        int minId,
        int entitiesPerChunk)
    {
        int currentOffset = 0;
        foreach (var comp in sortedComponents)
        {
            int slotIndex = comp.Id.Value - minId;

            // Tag components (size 0) have offset 0
            if (comp.Size == 0)
            {
                baseOffsets[slotIndex] = 0;
                continue;
            }

            // Align the base offset
            int alignment = comp.Alignment > 0 ? comp.Alignment : 1;
            currentOffset = AlignUp(currentOffset, alignment);

            baseOffsets[slotIndex] = (short)currentOffset;
            currentOffset += comp.Size * entitiesPerChunk;
        }
        return currentOffset;
    }

    /// <summary>
    /// Gets the base offset for a component's array within the chunk.
    /// </summary>
    /// <param name="componentId">The component ID.</param>
    /// <returns>The base offset, or -1 if the component is not in this archetype.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetBaseOffset(ComponentId componentId)
    {
        ThrowIfDisposed();
        int id = componentId.Value;
        ref var header = ref Header;
        if (id < header.MinComponentId || id > header.MaxComponentId)
            return -1;

        int slotIndex = id - header.MinComponentId;
        short offset = ((short*)(_data + header.BaseOffsetsOffset))[slotIndex];
        return offset; // -1 indicates not present, valid offsets return >= 0
    }

    /// <summary>
    /// Gets the base offset for a component type's array within the chunk.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>The base offset, or -1 if the component is not in this archetype.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetBaseOffset<T>() where T : unmanaged, IComponent
    {
        return GetBaseOffset(T.TypeId);
    }

    /// <summary>
    /// Checks if this archetype contains the specified component.
    /// </summary>
    /// <param name="componentId">The component ID.</param>
    /// <returns>True if the component is present.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasComponent(ComponentId componentId)
    {
        return GetBaseOffset(componentId) >= 0;
    }

    /// <summary>
    /// Checks if this archetype contains the specified component type.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>True if the component is present.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasComponent<T>() where T : unmanaged, IComponent
    {
        return GetBaseOffset<T>() >= 0;
    }

    /// <summary>
    /// Calculates the byte offset for a specific entity and component.
    /// </summary>
    /// <param name="entityIndex">The entity's index within the chunk.</param>
    /// <param name="componentId">The component ID.</param>
    /// <param name="componentSize">The component size in bytes.</param>
    /// <returns>The byte offset from chunk start, or -1 if component not present.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetEntityComponentOffset(int entityIndex, ComponentId componentId, int componentSize)
    {
        int baseOffset = GetBaseOffset(componentId);
        if (baseOffset < 0)
            return -1;

        return baseOffset + entityIndex * componentSize;
    }

    /// <summary>
    /// Calculates the byte offset for a specific entity and component type.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entityIndex">The entity's index within the chunk.</param>
    /// <returns>The byte offset from chunk start, or -1 if component not present.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetEntityComponentOffset<T>(int entityIndex) where T : unmanaged, IComponent
    {
        return GetEntityComponentOffset(entityIndex, T.TypeId, T.Size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int AlignUp(int value, int alignment)
    {
        return (value + alignment - 1) & ~(alignment - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_data == null, this);
    }

    /// <summary>
    /// Releases the native memory.
    /// </summary>
    public void Dispose()
    {
        if (_data != null)
        {
            _allocator.Free(_data);
            _data = null;
        }
    }
}
