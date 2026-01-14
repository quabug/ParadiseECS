using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paradise.ECS;

/// <summary>
/// A thread-safe, append-only list with immutable values.
/// Allocates memory using <see cref="NativeMemory"/> and supports concurrent Add and Read operations.
/// Values once set are immutable - they never change after being written.
/// </summary>
/// <typeparam name="T">The unmanaged element type.</typeparam>
public sealed unsafe class ConcurrentAppendOnlyList<T> : IDisposable where T : unmanaged
{
    private const int DefaultInitialCapacity = 16;
    private const int MinimumCapacity = 4;

    private readonly Lock _growLock = new();
    private readonly bool _zeroMemory;
    private readonly OperationGuard _writeGuard = new();  // For write operations (Add)
    private readonly OperationGuard _readGuard = new();   // For read operations (indexer)

    private nint _buffer;           // Pointer to current buffer
    private int _capacity;          // Current capacity
    private int _count;             // Number of slots reserved (atomic increment)
    private int _committedCount;    // Number of fully written elements (for visibility)
    private int _disposed;          // 0 = not disposed, 1 = disposed

    /// <summary>
    /// Creates a new <see cref="ConcurrentAppendOnlyList{T}"/>.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity. Default is 16.</param>
    /// <param name="zeroMemory">If true, allocated memory is zeroed. Default is true.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="initialCapacity"/> is less than <see cref="MinimumCapacity"/>.</exception>
    public ConcurrentAppendOnlyList(int initialCapacity = DefaultInitialCapacity, bool zeroMemory = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialCapacity, MinimumCapacity);

        _zeroMemory = zeroMemory;
        _capacity = initialCapacity;
        _buffer = (nint)Allocate((nuint)(initialCapacity * sizeof(T)));
    }

    /// <summary>
    /// Gets the number of elements in the list.
    /// Only counts fully committed (visible) elements.
    /// </summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _committedCount);
    }

    /// <summary>
    /// Gets the current capacity of the list.
    /// </summary>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _capacity);
    }

    /// <summary>
    /// Adds a value to the list. Thread-safe and lock-free for the common case.
    /// The value becomes immutable after this call returns.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <returns>The index at which the value was stored.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the list has been disposed.</exception>
    public int Add(T value)
    {
        using var _ = _writeGuard.EnterScope();
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        // Reserve a slot atomically
        int index = Interlocked.Increment(ref _count) - 1;

        // Ensure capacity (may need to grow)
        EnsureCapacity(index);

        // Write value to the slot.
        // If another thread grows the buffer while we're writing, we must ensure
        // our write ends up in the new buffer. Keep writing until buffer stabilizes.
        T* currentBuffer;
        T* newBuffer = (T*)Volatile.Read(ref _buffer);
        do
        {
            currentBuffer = newBuffer;
            currentBuffer[index] = value;
            newBuffer = (T*)Volatile.Read(ref _buffer);
        } while (newBuffer != currentBuffer);

        // Remember which buffer we wrote to
        T* writtenBuffer = currentBuffer;

        // Make the value visible to readers by committing in order
        // We must wait for all prior slots to be committed first
        SpinWait spinWait = default;
        while (Volatile.Read(ref _committedCount) != index)
        {
            spinWait.SpinOnce();
        }
        Volatile.Write(ref _committedCount, index + 1);

        // After committing, ensure value is in the current buffer.
        // Growth might have published a new buffer between our write loop and commit,
        // and that new buffer wouldn't have our data since we weren't committed yet.
        newBuffer = (T*)Volatile.Read(ref _buffer);
        while (newBuffer != writtenBuffer)
        {
            writtenBuffer = newBuffer;
            writtenBuffer[index] = value;
            newBuffer = (T*)Volatile.Read(ref _buffer);
        }

        return index;
    }

    /// <summary>
    /// Gets the value at the specified index.
    /// Thread-safe: can be called concurrently with Add operations.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get.</param>
    /// <returns>The value at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if index is out of range.</exception>
    public T this[int index]
    {
        get
        {
            using var _ = _readGuard.EnterScope();

            int count = Volatile.Read(ref _committedCount);
            if ((uint)index >= (uint)count)
                ThrowArgumentOutOfRange(index, count);

            var buffer = (T*)Volatile.Read(ref _buffer);
            return buffer[index];
        }
    }

    /// <summary>
    /// Ensures capacity for the given index.
    /// Uses lock for thread-safe growth.
    /// </summary>
    private void EnsureCapacity(int index)
    {
        if (index < Volatile.Read(ref _capacity))
            return;

        using var _ = _growLock.EnterScope();

        // Double-check after acquiring lock
        int currentCapacity = _capacity;
        if (index < currentCapacity)
            return;

        // Calculate new capacity (double, or enough for index + 1)
        int newCapacity = Math.Max(currentCapacity * 2, index + 1);

        // Allocate new buffer
        var newBuffer = (T*)Allocate((nuint)(newCapacity * sizeof(T)));

        // Copy existing committed data
        var oldBuffer = (T*)_buffer;
        int countToCopy = Volatile.Read(ref _committedCount);
        if (countToCopy > 0)
        {
            Buffer.MemoryCopy(oldBuffer, newBuffer,
                (long)newCapacity * sizeof(T),
                (long)countToCopy * sizeof(T));
        }

        // Publish new buffer and capacity atomically
        // Order matters: capacity first (readers check capacity before buffer)
        Volatile.Write(ref _capacity, newCapacity);
        Volatile.Write(ref _buffer, (nint)newBuffer);

        // Wait for all in-flight read operations to complete, then free old buffer
        if (oldBuffer != null)
        {
            _readGuard.WaitForCompletion();
            NativeMemory.Free(oldBuffer);
        }
    }

    /// <summary>
    /// Disposes the list and frees all allocated memory.
    /// Waits for all in-flight operations to complete before freeing.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Wait for all in-flight operations to complete
        _writeGuard.WaitForCompletion();
        _readGuard.WaitForCompletion();

        // Free current buffer
        var buffer = (void*)_buffer;
        if (buffer != null)
        {
            NativeMemory.Free(buffer);
            _buffer = 0;
        }

        _capacity = 0;
        _count = 0;
        _committedCount = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void* Allocate(nuint size)
        => _zeroMemory ? NativeMemory.AllocZeroed(size) : NativeMemory.Alloc(size);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRange(int index, int count)
        => throw new ArgumentOutOfRangeException(nameof(index),
            $"Index {index} is out of range. Count: {count}");
}
