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

# Run a specific test project
dotnet test --project src/Paradise.ECS.Test/Paradise.ECS.Test.csproj

# Run a specific test class (TUnit uses --treenode-filter with /<Assembly>/<Namespace>/<Class>/<Test> format)
dotnet test --project src/Paradise.ECS.Test/Paradise.ECS.Test.csproj -- --treenode-filter "/Paradise.ECS.Test/Paradise.ECS.Test/ChunkManagerTests/*"

# Run a single test
dotnet test --project src/Paradise.ECS.Test/Paradise.ECS.Test.csproj -- --treenode-filter "/Paradise.ECS.Test/Paradise.ECS.Test/ChunkManagerTests/Allocate_ReturnsValidHandle"

# AOT publish (verify AOT compatibility)
dotnet publish src/Paradise.ECS.Test/Paradise.ECS.Test.csproj -c Release
```

## Project Structure

- **Paradise.ECS** - Single-threaded core implementation
- **Paradise.ECS.Concurrent** - Multi-threaded variants with thread-safe operations
- **Paradise.ECS.Generators** - Source generators for component registration
- **Paradise.ECS.Test** - Main test suite
- **Paradise.ECS.Concurrent.Test** - Concurrent implementation tests
- **Paradise.ECS.CoyoteTest** - Formal verification tests
- **Paradise.ECS.Benchmarks** - Performance benchmarks

## Architecture

### High-Level API (src/Paradise.ECS/World/)

- **World\<TBits, TRegistry\>**: Primary user-facing API for single-threaded ECS operations. Owns three core subsystems: `ChunkManager`, `ArchetypeRegistry`, and `EntityManager`.
  - Entity CRUD: `Spawn()`, `Despawn()`, `IsAlive()`
  - Component operations: `GetComponent<T>()`, `SetComponent<T>()`, `HasComponent<T>()`, `AddComponent<T>()`, `RemoveComponent<T>()`
  - Builder-based creation: `CreateEntity<TBuilder>()`, `OverwriteEntity<TBuilder>()`, `AddComponents<TBuilder>()`
  - Query factory: `World.Query()` static method
- **ComponentRef\<T\>**: Ref struct providing safe access to component data in chunks. Must be disposed to release chunk borrow.
- **EntityBuilder**: Fluent builder for zero-allocation entity creation.
  - **IComponentsBuilder**: Interface with `CollectTypes<TBits>()` and `WriteComponents<TBits, TRegistry>()` methods
  - Extension method `builder.Add<T>(value)` for fluent chaining
  - Tag component optimization: skips writes for zero-size components

### Entity Management (src/Paradise.ECS/Entities/)

- **Entity**: Readonly record struct with `Id` (int) and `Version` (uint) for handle safety. `IsValid` checks Version > 0.
- **EntityLocation**: Tracks where entity data is stored with `Version`, `ArchetypeId`, and `GlobalIndex`. Used to derive ChunkIndex via archetype.
- **EntityManager**: Centralized entity lifecycle and location tracking.
  - O(1) lookups via List indexed by Entity.Id
  - Version-based stale handle detection
  - Free slot reuse via Stack for efficient ID recycling
  - Methods: `Create()`, `Destroy()`, `IsAlive()`, `GetLocation()`, `SetLocation()`

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

### Archetypes (src/Paradise.ECS/Archetypes/)

- **Archetype**: Stores entities with a specific component combination. Identified by component mask.
- **ArchetypeRegistry\<TBits, TRegistry\>**: Manages local archetype instances and query cache. Uses graph edges for O(1) structural changes (add/remove component).
- **SharedArchetypeMetadata\<TBits, TRegistry\>**: Enables multiple worlds to share archetype metadata. Manages mask-to-ID mappings, layouts, graph edges, and query registrations.
- **ImmutableArchetypeLayout**: Describes component layout within an archetype (offsets, sizes).
- **EdgeKey**: Packed 32-bit key for O(1) archetype graph traversal.
  - 20 bits for archetype ID (max 1,048,575)
  - 11 bits for component ID (max 2,047)
  - 1 bit for add/remove flag
  - Factory methods: `ForAdd()` and `ForRemove()`

### Query System (src/Paradise.ECS/Query/)

- **Query\<TBits, TRegistry\>**: Lightweight readonly struct view over matching archetypes. Wraps a list owned by ArchetypeRegistry. Zero-allocation when passed by value.
- **QueryBuilder**: Immutable ref struct builder for creating queries with fluent API (With, Without, WithAny).
- **ImmutableQueryDescription**: Record struct defining query constraints (All, None, Any masks). Cached in registry with HashedKey for fast lookup.

### Tag System (src/Paradise.ECS.Tag/)

- **TaggedWorld**: World wrapper that adds tag support. Tags stored as bitmask in auto-generated `EntityTags` component.
- **ChunkTagRegistry**: Tracks per-chunk tag masks for chunk-level query filtering.
- **TaggedWorldQueryBuilder**: Fluent API for building tag-filtered queries.
- **StaleBitStatistics**: Diagnostic record for monitoring sticky mask accumulation.

**Sticky Mask Trade-off:**
- Tag removal doesn't recompute chunk masks (O(1) removal vs O(n) per-chunk scan)
- Stale bits may cause false-positive chunk matches during tag queries
- `ComputeStaleBitStatistics()` diagnoses stale bit accumulation
- `RebuildChunkMasks()` clears stale bits (O(n) full scan, call at natural breakpoints)

### Global Limits & Validation

- **IConfig**: Static configuration interface with system-wide limits and configurable parameters.
  - Constants: `MaxArchetypeId` (1,048,575), `MaxComponentTypeId` (2,047)
  - Static abstract: `ChunkSize`, `DefaultEntityCapacity`, `MaxMetaBlocks`, `EntityIdByteSize`
- **Config\<T\>**: Computed values derived from config (e.g., `MaxEntityId` from `EntityIdByteSize`)
- **DefaultConfig**: Default configuration with 16KB chunks, 4-byte entity IDs
- **ThrowHelper**: Centralized validation utilities.
  - Chunk validation: `ValidateChunkRange()`, `ValidateChunkSize()`, `ThrowIfExceedsChunkSize()`
  - Component/Archetype validation: `ThrowIfComponentIdExceedsCapacity()`, `ThrowIfArchetypeIdExceedsLimit()`
  - Entity validation: `ThrowIfEntityIdExceedsLimit()` (validates against `EntityIdByteSize`)
  - General helpers: `ThrowIfNegative()`, `ThrowIfGreaterThan()`, `ThrowIfDisposed()`, `ThrowIfNull()`
  - Hot paths use `[MethodImpl(MethodImplOptions.AggressiveInlining)]`, cold paths use `NoInlining`

## Source Generators (src/Paradise.ECS.Generators/)

### ComponentGenerator

IIncrementalGenerator that processes `[Component]` attributes:
1. Finds all structs with `[Component]` attribute
2. Assigns TypeIds based on alphabetical ordering of fully qualified names
3. Generates IComponentRegistry implementation with `TypeInfos` array
4. Validates components don't exceed `IConfig.MaxComponentTypeId` (2,047)

**Configuration** (priority order):
1. `[ComponentRegistryNamespaceAttribute]` on assembly
2. RootNamespace build property
3. Default: "Paradise.ECS"

### Generator Code Style
- **Global Type Paths**: Always use fully qualified type names with `global::` prefix in generated code (e.g., `global::System.Int32`, `global::Paradise.ECS.ComponentType`) to avoid namespace conflicts and ambiguity.
- **GUID Generation**: When adding GUIDs to components, always use `uuidgen` (macOS/Linux) or `[guid]::NewGuid()` (PowerShell) to generate valid GUIDs. Never guess or fabricate GUIDs.

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
