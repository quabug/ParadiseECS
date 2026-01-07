# Paradise ECS - Style Guide

This style guide defines the coding standards and patterns for Paradise Engine, a game engine built with Pure C# (.NET 10) + WebGPU + Slang, targeting Native AOT compilation.

## Core Principles

### SOLID Architecture
- **Single Responsibility**: Each class or module has one clear responsibility
- **Open/Closed**: Design code that is open for extension but closed for modification
- **Liskov Substitution**: Derived classes must be substitutable for base classes
- **Interface Segregation**: Don't force clients to implement unused methods
- **Dependency Inversion**: Depend on abstractions, not concrete implementations

### Native AOT First
- All code must be AOT-compatible (no runtime reflection, dynamic code generation)
- Use `LibraryImport` instead of `DllImport` for P/Invoke
- Avoid APIs marked with `RequiresUnreferencedCode` or `RequiresDynamicCode`
- Test both JIT (`dotnet run`) and AOT (`dotnet publish`) builds

### Performance-First Development
- Prioritize performance and memory efficiency
- Use data-oriented design patterns where appropriate
- Minimize garbage collection pressure through object pooling
- Leverage modern C# features for zero-allocation code (Span, stackalloc, ref returns)
- Use unsafe code and pointers where performance benefits justify the complexity

### Keep It Simple (KISS)
- Seek simple and clear solutions; avoid unnecessary complexity
- Use straightforward algorithms and patterns where possible
- Simplicity enhances both performance and maintainability

### You Aren't Gonna Need It (YAGNI)
- Only implement features when they are needed
- Avoid speculative engineering
- Focus on current requirements

## C# Language Standards

### Modern C# Features
- **Target**: C# 14 / .NET 10 - always use the latest language features
- **Nullable Reference Types**: Enable and use nullable reference types
- **Pattern Matching**: Prefer pattern matching over traditional type checks
- **Records**: Use record types for immutable data structures
- **Spans and Memory**: Use `Span<T>`, `ReadOnlySpan<T>`, and `Memory<T>` for high-performance scenarios
- **Collection Expressions**: Use `[1, 2, 3]` syntax for collection initialization

**Example:**
```csharp
// Good - Modern C# with spans and file-scoped namespace
namespace Paradise.Core;

public sealed class DataProcessor
{
    public void Process(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return;
        // Process without allocation
    }
}

// Bad - Old style with namespace blocks and array allocations
namespace Paradise.Core
{
    public class DataProcessor
    {
        public void Process(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            // Allocates memory unnecessarily
        }
    }
}
```

### Naming Conventions

Follow these strict naming patterns (enforced by .editorconfig):

- **Private fields**: `_camelCase` with underscore prefix
- **Static private fields**: `s_camelCase` with 's_' prefix
- **Public fields/properties**: `PascalCase`
- **Methods**: `PascalCase`
- **Parameters**: `camelCase`
- **Const fields**: `PascalCase`
- **Local variables**: `camelCase`

**Example:**
```csharp
public sealed class Example
{
    private int _instanceField;
    private static int s_staticField;
    private const int MaxCapacity = 1024;

    public int PublicProperty { get; set; }

    public void DoSomething(int parameter)
    {
        int localVariable = parameter * 2;
    }
}
```

### File Organization

- **One primary type per file**: Each file should contain one main public type
- **File-scoped namespaces**: Always use file-scoped namespace declarations
- **Using directives**: Place at the top, outside namespace, sorted with System directives first
- **No regions**: Avoid #region directives; organize code logically instead

**Example:**
```csharp
using System;
using System.Collections.Generic;
using Silk.NET.Windowing;
using Silk.NET.WebGPU;

namespace Paradise.Core;

public sealed class GraphicsDevice : IDisposable
{
    // Implementation
}
```

## Documentation

### XML Documentation
All public APIs **must** have XML documentation comments:

- **Summary**: Describe what the type/member does
- **Type parameters**: Document all generic parameters with `<typeparam>`
- **Parameters**: Document all parameters with `<param>`
- **Returns**: Document return values with `<returns>`
- **Exceptions**: Document thrown exceptions with `<exception>`
- **Remarks**: Add additional context with `<remarks>` when needed

**Example:**
```csharp
/// <summary>
/// High-performance circular buffer implementation optimized for single-thread scenarios.
/// </summary>
/// <typeparam name="T">The type to store in the buffer</typeparam>
public sealed class CircularBuffer<T>
{
    /// <summary>
    /// Adds an item to the end of the buffer.
    /// If the buffer is full, expands the capacity automatically.
    /// </summary>
    /// <param name="item">The item to add</param>
    public void PushBack(T item)
    {
        // Implementation
    }
}
```

### Internal Documentation
- Use `// TODO:` comments for planned improvements
- Use `// HACK:` for temporary workarounds that need fixing
- Add inline comments to explain complex algorithms or non-obvious logic
- Don't comment obvious code

## Performance Patterns

### Memory Management

**Span Usage**: Use `Span<T>` and `ReadOnlySpan<T>` for memory slices
```csharp
// Good - No allocation
public int CopyTo(Span<T> destination)
{
    // Work with span directly
}

// Bad - Creates array
public T[] CopyTo()
{
    return new T[count]; // Allocates
}
```

**stackalloc**: Use stackalloc for small, short-lived buffers
```csharp
// Good - Stack allocation for small buffers used before await
Span<byte> buffer = stackalloc byte[256];
ProcessData(buffer);

// Bad - Heap allocation for small temporary buffer
var buffer = new byte[256];
```

### Generic Constraints
Use appropriate generic constraints to enable optimal code generation:

```csharp
// Good - Enables direct memory operations for value types
public void Process<T>(T data) where T : unmanaged

// Good - Works with any type
public void Store<T>(T data) where T : notnull
```

## Testing

### TUnit Framework
Use TUnit for all tests (AOT-compatible):

```csharp
[Test]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var buffer = new CircularBuffer<int>(4);

    // Act
    buffer.PushBack(1);
    buffer.PushBack(2);

    // Assert
    await Assert.That(buffer.Count).IsEqualTo(2);
}
```

### Test Patterns
- Use descriptive test method names: `MethodName_Scenario_ExpectedBehavior`
- Capture values before `await` boundary when using `stackalloc`
- Keep tests isolated and independent
- Run both JIT and AOT test builds

**Example - stackalloc with async:**
```csharp
[Test]
public async Task SpanOperation_BeforeAwait_WorksCorrectly()
{
    // stackalloc must be used before await
    Span<byte> bytes = stackalloc byte[4];
    bytes[0] = 0xAB;

    var result = ProcessSpan(bytes); // Capture result before await

    await Assert.That(result).IsTrue();
}
```

## Error Handling

### Defensive Programming
- Use `Debug.Assert` for internal invariants (compiled out in release)
- Use throw helpers for public API validation: `ArgumentNullException.ThrowIfNull()`
- Use `InvalidOperationException` for state errors

**Example:**
```csharp
public void Push(T item)
{
    ArgumentNullException.ThrowIfNull(item);

    if (_count >= _capacity)
        throw new InvalidOperationException("Buffer is full");

    Debug.Assert(_buffer != null, "Buffer should be initialized");
}
```

## Code Style Specifics

### Braces and Indentation
- **Allman style**: Opening braces on new line
- **4 spaces** for indentation (no tabs)
- **Always use braces** for multi-line control structures

```csharp
// Good
if (condition)
{
    DoSomething();
}

// Acceptable for single-line
if (condition) return;
```

### Expression Bodies
Use expression bodies for simple members:

```csharp
// Good for simple properties
public int Count => _count;
public bool IsEmpty => Count == 0;

// Good for simple methods
public void Clear() => _count = 0;
```

### Null Handling
- Use null-coalescing and null-conditional operators
- Use pattern matching for null checks
- Validate public API parameters

```csharp
// Good
_expandFunction = expandFunction ?? (capacity => capacity * 2);
var result = items?.Count ?? 0;

// Good - Pattern matching
if (data is null)
    ArgumentNullException.ThrowIfNull(data);
```

### LINQ Usage
- Avoid LINQ in hot paths (it allocates)
- Prefer foreach or for loops in performance-critical code
- LINQ is acceptable in initialization or infrequent code paths

```csharp
// Good - Hot path, no allocation
for (int i = 0; i < count; i++)
{
    Process(_buffer[i]);
}

// Acceptable - Test code or initialization
var failedKeys = keysToTest.Where(key => !IsPressed(key)).ToList();
```

## Type Design

### Sealed Classes
Seal classes by default unless designed for inheritance:

```csharp
// Good - Sealed for performance
public sealed class CircularBuffer<T> : IDisposable

// Only when inheritance is intended
public abstract class Component
```

### Struct vs Class
- **Structs** for small, immutable data (<= 16 bytes recommended)
- **Classes** for objects with identity, mutable state, or larger than 16 bytes
- **Readonly structs** for immutable value types

```csharp
// Good - Small immutable data
public readonly struct Vector2(float x, float y)
{
    public float X { get; } = x;
    public float Y { get; } = y;
}

// Good - Object with identity and state
public sealed class RenderPipeline : IDisposable
```

### IDisposable Pattern
Implement proper disposal for unmanaged resources:

```csharp
public sealed class ResourceHolder : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;

        // Dispose managed resources
        // Free unmanaged resources

        _disposed = true;
    }
}
```

## Code Review Checklist

Before submitting code, verify:

- [ ] All public APIs have XML documentation
- [ ] Naming conventions follow the style guide
- [ ] No boxing/unboxing in hot paths
- [ ] Appropriate use of Span/Memory for zero-allocation scenarios
- [ ] Proper disposal of unmanaged resources
- [ ] File-scoped namespaces used
- [ ] No LINQ in performance-critical code
- [ ] Defensive checks (Debug.Assert, argument validation)
- [ ] Tests added for new functionality (TUnit)
- [ ] Both JIT and AOT builds verified
- [ ] No reflection or dynamic code generation (AOT compatibility)
