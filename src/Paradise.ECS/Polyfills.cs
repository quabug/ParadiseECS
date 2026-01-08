#if NETSTANDARD2_1

// ReSharper disable once CheckNamespace
namespace System.Threading
{
    /// <summary>
    /// Polyfill for Lock class introduced in .NET 9.
    /// </summary>
    internal sealed class Lock
    {
        private readonly object _syncRoot = new();

        public Scope EnterScope()
        {
            Monitor.Enter(_syncRoot);
            return new Scope(this);
        }

        public ref struct Scope
        {
            private Lock? _lock;

            internal Scope(Lock @lock)
            {
                _lock = @lock;
            }

            public void Dispose()
            {
                if (_lock is not null)
                {
                    Monitor.Exit(_lock._syncRoot);
                    _lock = null;
                }
            }
        }
    }
}

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    using System;

    /// <summary>
    /// Polyfill for init-only setters in netstandard2.1.
    /// </summary>
    internal static class IsExternalInit;

    /// <summary>
    /// Polyfill for CallerArgumentExpressionAttribute in netstandard2.1.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }
    }
}

// ReSharper disable once CheckNamespace
namespace System.Runtime.InteropServices
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Polyfill for NativeMemory in netstandard2.1.
    /// </summary>
    internal static unsafe class NativeMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* Alloc(nuint byteCount)
            => (void*)Marshal.AllocHGlobal((nint)byteCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* AllocZeroed(nuint byteCount)
        {
            var ptr = Alloc(byteCount);
            Clear(ptr, byteCount);
            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free(void* ptr)
            => Marshal.FreeHGlobal((nint)ptr);

        /// <summary>
        /// Clears memory at the specified pointer.
        /// Handles large allocations (>2GB) by clearing in chunks.
        /// </summary>
        public static void Clear(void* ptr, nuint byteCount)
        {
            const int maxChunkSize = int.MaxValue;
            var bytePtr = (byte*)ptr;

            while (byteCount > 0)
            {
                int chunkSize = byteCount > (nuint)maxChunkSize ? maxChunkSize : (int)byteCount;
                new Span<byte>(bytePtr, chunkSize).Clear();
                bytePtr += chunkSize;
                byteCount -= (nuint)chunkSize;
            }
        }
    }
}

// ReSharper disable once CheckNamespace
namespace System
{
    using System.Runtime.CompilerServices;

    internal static class ArgumentNullExceptionPolyfill
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNull(object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null)
                throw new ArgumentNullException(paramName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ThrowIfNull(void* argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument == null)
                throw new ArgumentNullException(paramName);
        }
    }

    internal static class ArgumentOutOfRangeExceptionPolyfill
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(paramName, value, "Value must be non-negative.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIfGreaterThan(int value, int other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value > other)
                throw new ArgumentOutOfRangeException(paramName, value, $"Value must be less than or equal to {other}.");
        }
    }

    internal static class ObjectDisposedExceptionPolyfill
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ThrowIf(bool condition, object instance)
        {
            if (condition)
                throw new ObjectDisposedException(instance.GetType().FullName);
        }
    }
}

#endif
