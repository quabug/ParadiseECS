using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Static helper methods for common argument validation and exception throwing.
/// </summary>
internal static class ThrowHelper
{
    /// <summary>
    /// Throws if <paramref name="byteOffset"/> is negative.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeOffset(int byteOffset)
        => ThrowIfNegative(byteOffset);

    /// <summary>
    /// Throws if <paramref name="count"/> is negative.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeCount(int count)
        => ThrowIfNegative(count);

    /// <summary>
    /// Throws if <paramref name="size"/> is negative.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeSize(int size)
        => ThrowIfNegative(size);

    /// <summary>
    /// Throws if <paramref name="totalBytes"/> exceeds chunk size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfExceedsChunkSize<TConfig>(int totalBytes)
        where TConfig : IConfig
        => ThrowIfGreaterThan(totalBytes, TConfig.ChunkSize);

    /// <summary>
    /// Validates byte offset and size for chunk bounds.
    /// Throws if offset or size is negative, or if the range exceeds chunk size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateChunkRange<TConfig>(int byteOffset, int size)
        where TConfig : IConfig
    {
        ThrowIfNegative(byteOffset);
        ThrowIfNegative(size);
        ThrowIfGreaterThan(size, TConfig.ChunkSize - byteOffset);
    }

    /// <summary>
    /// Validates byte offset, count, and element size for chunk bounds.
    /// Throws if offset or count is negative, or if the range exceeds chunk size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateChunkRange<TConfig>(int byteOffset, int count, int elementSize)
        where TConfig : IConfig
    {
        ThrowIfNegative(byteOffset);
        ThrowIfNegative(count);
        // Validate count against max possible elements to prevent overflow in multiplication
        int maxCount = (TConfig.ChunkSize - byteOffset) / elementSize;
        ThrowIfGreaterThan(count, maxCount);
    }

    /// <summary>
    /// Validates size for chunk bounds.
    /// Throws if size is negative or exceeds chunk size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateChunkSize<TConfig>(int size)
        where TConfig : IConfig
    {
        ThrowIfNegative(size);
        ThrowIfGreaterThan(size, TConfig.ChunkSize);
    }

    /// <summary>
    /// Throws <see cref="ObjectDisposedException"/> if the condition is true.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDisposed(bool condition, object instance)
        => ObjectDisposedException.ThrowIf(condition, instance);

    /// <summary>
    /// Throws if the pointer is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void ThrowIfNull(void* ptr)
        => ArgumentNullException.ThrowIfNull(ptr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfGreaterThan(int value, int other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        => ArgumentOutOfRangeException.ThrowIfGreaterThan(value, other, paramName);

    /// <summary>
    /// Throws if the component ID exceeds the capacity of the bit storage.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfComponentIdExceedsCapacity(int componentId, int capacity)
    {
        if (componentId >= capacity)
            ThrowComponentIdExceedsCapacity(componentId, capacity);
    }

    /// <summary>
    /// Throws if the component ID is invalid (negative).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfInvalidComponentId(ComponentId id)
    {
        if (!id.IsValid)
            ThrowInvalidComponentId();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowComponentIdExceedsCapacity(int componentId, int capacity)
        => throw new InvalidOperationException(
            $"Component ID {componentId} exceeds capacity {capacity}. Use a larger bit storage type.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidComponentId()
        => throw new InvalidOperationException(
            "Component type has not been registered. Ensure the type is marked with [Component] attribute.");

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> with the specified message.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentException(string message, string? paramName = null)
        => throw new ArgumentException(message, paramName);

    /// <summary>
    /// Throws if the archetype ID exceeds the maximum allowed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfArchetypeIdExceedsLimit(int archetypeId)
    {
        if (archetypeId > IConfig.MaxArchetypeId)
            ThrowArchetypeIdExceedsLimit();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArchetypeIdExceedsLimit()
        => throw new InvalidOperationException(
            $"Archetype count exceeded maximum of {IConfig.MaxArchetypeId}.");

    /// <summary>
    /// Throws if the entity ID exceeds what can be stored in EntityIdByteSize bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfEntityIdExceedsLimit<TConfig>(int entityId)
        where TConfig : IConfig
    {
        if (entityId > Config<TConfig>.MaxEntityId)
            ThrowEntityIdExceedsLimit(entityId, Config<TConfig>.MaxEntityId, TConfig.EntityIdByteSize);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowEntityIdExceedsLimit(int entityId, int maxEntityId, int byteSize)
        => throw new InvalidOperationException(
            $"Entity ID {entityId} exceeds maximum of {maxEntityId} for EntityIdByteSize={byteSize}.");

    /// <summary>
    /// Throws an exception for an invalid EntityIdByteSize value.
    /// Returns T to allow use in switch expressions.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowInvalidEntityIdByteSize<T>(int entityIdByteSize)
        => throw new InvalidOperationException(
            $"Invalid EntityIdByteSize: {entityIdByteSize}. Supported values are 1, 2, and 4.");
}
