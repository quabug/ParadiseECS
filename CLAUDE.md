# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Paradise.ECS is a high-performance Entity Component System library for .NET 10, designed for Native AOT compilation. Part of the Paradise Engine ecosystem targeting game development with Pure C# + WebGPU + Slang.

## Build Commands

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run a specific test class
dotnet test --filter "FullyQualifiedName~ChunkManagerTests"

# Run a single test
dotnet test --filter "FullyQualifiedName~ChunkManagerTests.Allocate_ReturnsValidHandle"

# AOT publish (verify AOT compatibility)
dotnet publish src/Paradise.ECS.Test/Paradise.ECS.Test.csproj -c Release
```

## Architecture

### Memory Management (src/Paradise.ECS/Memory/)

The ECS uses a custom memory management system optimized for cache-friendly iteration:

- **Chunk**: A 16KB ref struct memory block sized to fit in L1 cache. Borrows memory from ChunkManager and must be disposed.
- **ChunkManager**: Thread-safe, lock-free manager using CAS operations. Manages chunk lifecycle with version-based stale handle detection. Metadata stored in 16KB blocks, each holding 1024 entries.
- **ChunkHandle**: Lightweight handle with Id and 48-bit Version for safe chunk access without raw pointers.
- **IAllocator**: Abstraction for memory allocation strategies (native, arena, etc.)

### Type System (src/Paradise.ECS/Types/)

- **ImmutableBitSet\<TBits\>**: Generic fixed-size bitset using InlineArray for stack-allocated storage. Used for component masks and archetype matching.
- **IStorage**: Marker interface with predefined sizes (Bit64, Bit128, Bit256, Bit512, Bit1024) for bitset backing storage. Custom types are generated if component count exceeds 1024.
- **IBitSet\<TSelf\>**: Interface defining bitset operations (And, Or, ContainsAll, etc.)
- **HashedKey\<T\>**: Wrapper that pre-computes and caches hash code for dictionary keys. Use explicit cast to convert from value types.

### Archetypes (src/Paradise.ECS/Components/)

- **Archetype**: Stores entities with a specific component combination. Identified by component mask.
- **ArchetypeRegistry**: Manages archetype creation/lookup with caching. Uses graph edges for O(1) structural changes (add/remove component). Also owns query caches.
- **ImmutableArchetypeLayout**: Describes component layout within an archetype (offsets, sizes).

### Query System (src/Paradise.ECS/Query/)

- **Query\<TBits, TRegistry\>**: Lightweight readonly struct view over matching archetypes. Wraps a list owned by ArchetypeRegistry. Zero-allocation when passed by value.
- **QueryBuilder**: Immutable ref struct builder for creating queries with fluent API (With, Without, WithAny).
- **ImmutableQueryDescription**: Record struct defining query constraints (All, None, Any masks). Cached in registry with HashedKey for fast lookup.

## Code Style

### Naming Conventions
- Private fields: `_camelCase`
- Static private fields: `s_camelCase`
- Constants: `PascalCase`

### Key Requirements
- **Native AOT First**: No reflection, no dynamic code generation
- **Performance Critical**: Use Span\<T\>, stackalloc, ref returns to minimize allocations
- **Sealed by Default**: Seal classes unless inheritance is explicitly needed
- **File-scoped Namespaces**: Always use `namespace Paradise.ECS;` style
- **Allman Braces**: Opening braces on new line, 4-space indentation
- **TUnit Framework**: Tests use TUnit (AOT-compatible), not xUnit/NUnit

### Source Generators
- **Global Type Paths**: Always use fully qualified type names with `global::` prefix in generated code (e.g., `global::System.Int32`, `global::Paradise.ECS.ComponentType`) to avoid namespace conflicts and ambiguity.
- **GUID Generation**: When adding GUIDs to components, always use `uuidgen` (macOS/Linux) or `[guid]::NewGuid()` (PowerShell) to generate valid GUIDs. Never guess or fabricate GUIDs.

### XML Documentation
All public APIs require XML docs with `<summary>`, `<param>`, `<returns>`, and `<typeparam>` tags.

### Testing Pattern
```csharp
[Test]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange, Act
    var result = ...;

    // Assert - TUnit uses await Assert.That()
    await Assert.That(result).IsEqualTo(expected);
}
```

Note: When using `stackalloc`, capture values before `await` boundaries.
