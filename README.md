# Paradise.ECS

A high-performance Entity Component System library for .NET 10, designed for Native AOT compilation.

## Features

- **Native AOT First** - No reflection, no dynamic code generation. All type information resolved at compile-time via source generators.
- **Cache-Friendly Design** - 16KB chunks sized for L1 cache with SoA (Struct of Arrays) memory layout for sequential access patterns.
- **Fast Structural Changes** - Graph-based archetype transitions with O(1) lookup; data movement is O(c) where c is component count.
- **Zero-Allocation Queries** - Query objects are lightweight readonly structs that don't allocate during iteration.
- **Type-Safe Queryables** - Source-generated strongly-typed query structs with direct component property access.
- **Fluent Builder API** - Type-safe, zero-allocation entity creation with compile-time validation.
- **Multi-World Optimization** - Shared archetype metadata enables multiple worlds to share type information efficiently.
- **Static Configuration** - Compile-time configurable chunk sizes, entity ID sizes, and capacity limits via `IConfig`.
- **Batch Processing** - Chunk-level iteration with span-based component access for SIMD-friendly operations.
- **Entity Tags** - Lightweight boolean flags with source-generated IDs for efficient entity classification and filtering.

## Requirements

- .NET 10.0 or later
- C# 14

## Installation

Add references to the `Paradise.ECS` project and the source generator:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Paradise.ECS/Paradise.ECS.csproj" />
  <ProjectReference Include="path/to/Paradise.ECS.Generators/Paradise.ECS.Generators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Quick Start

### Define Components

Components are unmanaged structs marked with the `[Component]` attribute:

```csharp
using Paradise.ECS;

[Component]
public partial struct Position
{
    public float X, Y, Z;
}

[Component]
public partial struct Velocity
{
    public float X, Y, Z;
}

[Component]
public partial struct Health
{
    public int Current;
    public int Max;
}

// Tag component (zero-size, no data)
[Component]
public partial struct PlayerTag { }

// Component with stable GUID for serialization
[Component("12345678-1234-1234-1234-123456789012")]
public partial struct NetworkId
{
    public ulong Id;
}
```

The source generator automatically implements `IComponent` and assigns type IDs.

### Define Tags

Tags are lightweight boolean flags for entity classification, more efficient than zero-size components:

```csharp
using Paradise.ECS;

[Tag]
public partial struct IsActive;

[Tag]
public partial struct IsVisible;

[Tag]
public partial struct PlayerTag;

[Tag]
public partial struct EnemyTag;

// Tag with stable GUID for serialization
[Tag("87654321-4321-4321-4321-210987654321")]
public partial struct NetworkSyncedTag;
```

The source generator implements `ITag` and assigns tag IDs. Tags are stored as bit flags per entity, separate from the component archetype.

### Create a World

```csharp
using Paradise.ECS;

// Create shared resources (can be reused across multiple worlds)
using var sharedMetadata = new SharedArchetypeMetadata();
using var chunkManager = DefaultChunkManager.Create();

// Create the world using the generated World alias
var world = new World(sharedMetadata, chunkManager);
```

> **Note**: The source generator creates type aliases (`World`, `ComponentMask`, etc.) based on your component count. For manual configuration, use the full generic types like `World<Bit256, ComponentRegistry, DefaultConfig>`.

#### Using Tags with TaggedWorld

To use the tag system, add `[EnableTags]` to your assembly. This generates a `World` alias that wraps `TaggedWorld`:

```csharp
[assembly: EnableTags]
```

The `TaggedWorld` is created with internal dependencies, simplifying usage:

```csharp
// With [EnableTags], World is an alias for TaggedWorld
using var world = new World();
```

### Spawn Entities

Using the fluent builder API (recommended for multiple components):

```csharp
// Create entity with components using fluent builder
var entity = EntityBuilder.Create()
    .Add(new Position { X = 0, Y = 0, Z = 0 })
    .Add(new Velocity { X = 1, Y = 0, Z = 0 })
    .Add(new Health { Current = 100, Max = 100 })
    .Build(world);

// Create entity with tags (requires TaggedWorld via [EnableTags])
var player = EntityBuilder.Create()
    .Add(new Position { X = 0, Y = 0, Z = 0 })
    .AddTag(default(PlayerTag), world)
    .AddTag(default(IsActive), world)
    .Build(world);
```

Or spawn and add components individually:

```csharp
// Spawn empty entity
var entity = world.Spawn();

// Add components one at a time (causes archetype transitions)
world.AddComponent(entity, new Position { X = 0, Y = 0, Z = 0 });
world.AddComponent(entity, new Velocity { X = 1, Y = 0, Z = 0 });
```

### Access Components

```csharp
// Check if entity has component
if (world.HasComponent<Position>(entity))
{
    // Get component value
    var pos = world.GetComponent<Position>(entity);
    Console.WriteLine($"Position: {pos.X}, {pos.Y}, {pos.Z}");
}

// Set component value directly
world.SetComponent(entity, new Position { X = 100, Y = 200, Z = 0 });
```

### Modify Entity Structure

```csharp
// Add component (structural change - entity moves to new archetype)
world.AddComponent(entity, new Health { Current = 100, Max = 100 });

// Remove component (structural change)
world.RemoveComponent<Velocity>(entity);

// Overwrite all components on existing entity
EntityBuilder.Create()
    .Add(new Position { X = 50, Y = 50, Z = 0 })
    .Add(new Health { Current = 50, Max = 100 })
    .Overwrite(entity, world);

// Add multiple components at once (single structural change)
EntityBuilder.Create()
    .Add(new Velocity { X = 2, Y = 0, Z = 0 })
    .Add(default(PlayerTag))
    .AddTo(entity, world);
```

### Destroy Entities

```csharp
// Check if entity is alive (handles are versioned)
if (world.IsAlive(entity))
{
    // Destroy entity
    world.Despawn(entity);
}

// Clear all entities from world
world.Clear();
```

### Tag Operations

Tags provide lightweight boolean flags for entity classification (requires `[EnableTags]`):

```csharp
// Check if entity has tag
if (world.HasTag<PlayerTag>(entity))
{
    Console.WriteLine("This is a player");
}

// Add tag to entity
world.AddTag<IsVisible>(entity);

// Remove tag from entity
world.RemoveTag<IsVisible>(entity);

// Get all tags as a bitmask
var tags = world.GetTags(entity);
bool isActive = tags.Get(IsActive.TagId);
```

## Query System

Paradise.ECS provides multiple ways to query and iterate over entities, from low-level to high-level APIs.

### QueryBuilder (Dynamic Queries)

Build queries dynamically to iterate over entities with specific component combinations:

```csharp
// Query all entities with Position AND Velocity
var movingQuery = QueryBuilder.Create()
    .With<Position>()
    .With<Velocity>()
    .Build(world);

// Iterate using WorldQuery (returns WorldEntity with convenient accessors)
foreach (var entity in movingQuery)
{
    ref var pos = ref entity.Get<Position>();
    var vel = entity.Get<Velocity>();
    pos.X += vel.X;
    pos.Y += vel.Y;
}

// Query with exclusion filter
var nonPlayerQuery = QueryBuilder.Create()
    .With<Position>()
    .Without<PlayerTag>()
    .Build(world);

// Query with "any" filter (must have at least one)
var combatantQuery = QueryBuilder.Create()
    .With<Position>()
    .WithAny<PlayerTag>()
    .WithAny<EnemyTag>()
    .Build(world);
```

### Tag-Based Queries

With `[EnableTags]`, use `world.Query()` for tag-filtered queries:

```csharp
// Query entities with specific tag
var activeQuery = world.Query()
    .WithTag<IsActive>()
    .Build();

foreach (var entity in activeQuery)
{
    // entity is WorldEntity with Get<T>() for component access
    Console.WriteLine($"Active entity: {entity.Entity.Id}");
}

// Query with multiple tags
var activeVisibleQuery = world.Query()
    .WithTag<IsActive>()
    .WithTag<IsVisible>()
    .Build();

// Combine tags with component filters
var activeMovableQuery = world.Query()
    .WithTag<IsActive>()
    .With<Position>()
    .With<Velocity>()
    .Build();

foreach (var entity in activeMovableQuery)
{
    var pos = entity.Get<Position>();
    var vel = entity.Get<Velocity>();
    Console.WriteLine($"Entity {entity.Entity.Id}: pos={pos}, vel={vel}");
}
```

### Queryable Structs (Type-Safe Generated Queries)

Define strongly-typed query structs for compile-time safety and optimal performance:

```csharp
// Define a queryable with direct property access
[Queryable]
[With<Position>]
[With<Velocity>]
public readonly ref partial struct Movable;

// Use custom property names and read-only access
[Queryable]
[With<Position>]
[With<Health>(Name = "Hp", IsReadOnly = true)]
[Without<Dead>]
public readonly ref partial struct AliveEntity;

// Query-only components (filter without property generation)
[Queryable]
[With<Position>]
[With<PlayerTag>(QueryOnly = true)]
public readonly ref partial struct PlayerPosition;

// Optional components with Has/Get pattern
[Queryable]
[With<Health>]
[Optional<Position>(IsReadOnly = true)]
public readonly ref partial struct Damageable;

// WithAny constraint (must have at least one)
[Queryable]
[With<Position>]
[With<Health>]
[WithAny<Velocity>]
public readonly ref partial struct ActiveEntity;
```

Using queryables in game loops:

```csharp
// Type-safe iteration with direct property access
foreach (var entity in Movable.Query.Build(world))
{
    // Direct property access (no method calls needed)
    entity.Position = new Position(
        entity.Position.X + entity.Velocity.X,
        entity.Position.Y + entity.Velocity.Y,
        entity.Position.Z + entity.Velocity.Z);
}

// Optional component pattern
foreach (var entity in Damageable.Query.Build(world))
{
    var health = entity.Health;
    if (entity.HasPosition)
    {
        ref readonly var pos = ref entity.GetPosition();
        Console.WriteLine($"Entity at {pos} has {health.Current} HP");
    }
}

// Query properties
var query = Movable.Query.Build(world);
Console.WriteLine($"Moving entities: {query.EntityCount}");
if (!query.IsEmpty)
{
    // Process entities...
}
```

### Chunk-Level Batch Processing

For SIMD-friendly operations, iterate over chunks with span-based access:

```csharp
// Dynamic chunk query
var chunkQuery = QueryBuilder.Create()
    .With<Position>()
    .With<Health>()
    .BuildChunk(world);

foreach (var chunk in chunkQuery)
{
    // Span-based batch access
    var positions = chunk.GetSpan<Position>();
    var healths = chunk.GetSpan<Health>();

    for (int i = 0; i < chunk.EntityCount; i++)
    {
        positions[i].X += 1;
        healths[i].Current = Math.Max(0, healths[i].Current - 1);
    }

    // Check for optional components in chunk
    if (chunk.TryGetSpan<Velocity>(out var velocities))
    {
        // Process velocities...
    }
}

// Type-safe chunk query with generated span properties
foreach (var chunk in Movable.ChunkQuery.Build(world))
{
    // Generated span properties (pluralized names)
    Span<Position> positions = chunk.Positions;
    Span<Velocity> velocities = chunk.Velocitys;

    for (int i = 0; i < chunk.EntityCount; i++)
    {
        positions[i].X += velocities[i].X;
        positions[i].Y += velocities[i].Y;
    }
}
```

### Low-Level Query Iteration

For advanced use cases, access the underlying query directly:

```csharp
var worldQuery = QueryBuilder.Create()
    .With<Position>()
    .Build(world);

// Access underlying Query for low-level iteration
var query = worldQuery.Query;

// Iterate over entity IDs
foreach (int entityId in query)
{
    Console.WriteLine($"Entity ID: {entityId}");
}

// Iterate over archetypes
foreach (var archetype in query.Archetypes)
{
    Console.WriteLine($"Archetype {archetype.Id}: {archetype.EntityCount} entities");
}

// Iterate over chunks with metadata
foreach (var chunkInfo in query.Chunks)
{
    Console.WriteLine($"Chunk: {chunkInfo.EntityCount} entities");
}
```

## Architecture

### Core Concepts

| Concept | Description |
|---------|-------------|
| **Entity** | A unique identifier (ID + Version) representing a game object |
| **Component** | Plain data attached to entities (unmanaged structs) |
| **Tag** | Lightweight boolean flag for entity classification (stored as bits, not in archetype) |
| **Archetype** | A unique combination of component types; entities with the same components share an archetype |
| **Chunk** | A 16KB memory block storing entity data in SoA layout |
| **World** | The container that manages entities, components, and archetypes |
| **TaggedWorld** | World wrapper that adds tag operations (enabled via `[EnableTags]`) |
| **Query** | A filter for finding entities with specific component combinations |
| **Queryable** | A source-generated type-safe query struct with direct component access |

### Memory Layout

Paradise.ECS uses a Struct of Arrays (SoA) memory layout within 16KB chunks:

```
Chunk (16KB):
┌─────────────────────────────────────────────────────┐
│ [EntityId0][EntityId1][EntityId2]...                │
│ [Position0][Position1][Position2]...                │
│ [Velocity0][Velocity1][Velocity2]...                │
│ [Health0][Health1][Health2]...                      │
└─────────────────────────────────────────────────────┘
```

This layout maximizes cache efficiency when iterating over a single component type across many entities.

### Project Structure

```
src/
├── Paradise.ECS/              # Core single-threaded ECS
│   ├── Archetypes/            # Archetype management and shared metadata
│   ├── Components/            # Component interfaces and attributes
│   ├── Entities/              # Entity, EntityManager, EntityLocation
│   ├── Memory/                # Chunk-based memory management
│   ├── Query/                 # Query builder, attributes, and execution
│   ├── Tags/                  # Tag system (TaggedWorld, ChunkTagRegistry)
│   ├── Types/                 # BitSet, HashedKey, and utilities
│   └── World/                 # World API, EntityBuilder, WorldQuery
├── Paradise.ECS.Concurrent/   # Thread-safe ECS variant (lock-free operations)
├── Paradise.ECS.Generators/   # Source generators
│   ├── ComponentGenerator     # Generates IComponent, ITag implementations
│   └── QueryableGenerator     # Generates type-safe query structs
├── Paradise.ECS.Test/         # Unit tests (TUnit framework)
├── Paradise.ECS.Concurrent.Test/ # Concurrent implementation tests
├── Paradise.ECS.Benchmarks/   # Performance benchmarks
├── Paradise.ECS.CoyoteTest/   # Formal concurrency verification
└── Paradise.ECS.Sample/       # Sample application demonstrating ECS usage
```

## Source Generators

Paradise.ECS uses Roslyn source generators to provide compile-time type safety and Native AOT compatibility. No reflection is used at runtime.

### ComponentGenerator

Processes structs marked with `[Component]` and `[Tag]`, and generates:

| Generated File | Contents |
|---------------|----------|
| `{TypeName}.g.cs` | Partial struct implementing `IComponent` with `TypeId`, `Guid`, `Size`, `Alignment` |
| `{TagName}.g.cs` | Partial struct implementing `ITag` with `TagId`, `Guid` |
| `ComponentRegistry.g.cs` | Type-to-ID and GUID-to-ID mappings with module initializer |
| `TagRegistry.g.cs` | Tag-to-ID mappings, `EntityTags` component, `TagMask` type |
| `ComponentAliases.g.cs` | Global using aliases for `World`, `Query`, `ComponentMask`, etc. |
| `DefaultChunkManager.g.cs` | Factory class for creating ChunkManager with default config |

When `[EnableTags]` is present, the generator also creates:
- `EntityTags` component storing per-entity tag bits
- `TagMask` type alias for the tag bitmask
- `World` alias pointing to `TaggedWorld` instead of plain `World`

**Component ID Assignment:**
1. Components with manual `Id = N` are assigned first
2. Remaining components are auto-assigned by alignment (descending), then alphabetically
3. This ensures larger components are packed first for better memory alignment

**Bit Storage Type Selection:**
The generator automatically selects the smallest bit storage type based on component count:

| Component Count | Storage Type |
|----------------|--------------|
| 1-64 | `Bit64` |
| 65-128 | `Bit128` |
| 129-256 | `Bit256` |
| 257-512 | `Bit512` |
| 513-1024 | `Bit1024` |
| >1024 | Custom `BitN` (generated) |

### QueryableGenerator

Processes `ref struct` types marked with `[Queryable]` and generates:

| Generated File | Contents |
|---------------|----------|
| `Queryable_{TypeName}.g.cs` | Partial struct with `QueryableId`, `Query`, `ChunkQuery` properties |
| | Nested `Data<TBits, TRegistry, TConfig>` struct with typed component properties |
| | Nested `ChunkData<...>` struct with span-based batch access |
| | `QueryBuilder` and `Query` wrapper structs with enumerators |
| `QueryableRegistry.g.cs` | Static registry mapping queryable IDs to query descriptions |
| `QueryableAliases.g.cs` | Global using alias for `QueryableRegistry` |
| `QueryableRegistryInitializer.g.cs` | Module initializer ensuring registry is loaded |

**Generated Data Struct Members:**

For `[With<T>]` components:
- `ref T PropertyName` - Direct component access (or `ref readonly` if `IsReadOnly = true`)

For `[Optional<T>]` components:
- `bool HasPropertyName` - Check if component exists
- `ref T GetPropertyName()` - Access component (throws if not present)

For chunk iteration (`ChunkData`):
- `Span<T> PropertyNames` - Batch span access (pluralized name)
- `bool HasPropertyName` / `GetPropertyNames()` - For optional components

### Suppressing Global Usings

If the generated global using aliases conflict with your codebase, suppress them:

```csharp
[assembly: SuppressGlobalUsings]
```

This disables generation of:
- `ComponentMaskBits`, `ComponentMask`
- `World`, `Query`, `QueryBuilder`
- `SharedArchetypeMetadata`, `ArchetypeRegistry`
- `QueryableRegistry`

You can then define your own local aliases or use fully qualified types.

### Configuration Attributes

| Attribute | Target | Purpose |
|-----------|--------|---------|
| `[assembly: ComponentRegistryNamespace("Namespace")]` | Assembly | Override namespace for generated ComponentRegistry |
| `[DefaultConfig]` | Struct/Class | Mark an `IConfig` implementation as default for World alias |
| `[assembly: SuppressGlobalUsings]` | Assembly | Disable global using alias generation |
| `[assembly: EnableTags]` | Assembly | Enable tag system and generate `TaggedWorld` as `World` alias |
| `[Tag]` | Struct | Define a tag type with auto-assigned ID |
| `[Tag("guid")]` | Struct | Define a tag with stable GUID for serialization |

## Configuration

### Static World Configuration

Paradise.ECS uses compile-time configuration via the `IConfig` interface:

```csharp
// Use default configuration
var world = new World(sharedMetadata, chunkManager);

// Or define custom configuration and mark it as default
[DefaultConfig]  // Marks this as the default config for World alias generation
public readonly struct SmallConfig : IConfig
{
    // Static compile-time constraints
    public static int ChunkSize => 4096;           // 4KB chunks
    public static int MaxMetaBlocks => 64;         // Fewer metadata blocks
    public static int EntityIdByteSize => 2;       // 2-byte entity IDs (max 65,535)

    // Instance runtime hints
    public int DefaultEntityCapacity => 1000;
    public int DefaultChunkCapacity => 16;

    // Memory allocators (use shared native allocator)
    public IAllocator ChunkAllocator => NativeMemoryAllocator.Shared;
    public IAllocator MetadataAllocator => NativeMemoryAllocator.Shared;
    public IAllocator LayoutAllocator => NativeMemoryAllocator.Shared;
}
```

### Component Configuration

```csharp
// Component with stable GUID for serialization
[Component("550e8400-e29b-41d4-a716-446655440000")]
public partial struct NetworkSyncedComponent
{
    public int Value;
}

// Manual component ID (useful for network protocols)
[Component(Id = 100)]
public partial struct FixedIdComponent
{
    public int Value;
}

// Custom registry namespace
[assembly: ComponentRegistryNamespace("MyGame.ECS")]
```

## Limits

| Limit | Value | Notes |
|-------|-------|-------|
| Max component types | 2,047 | Determined by `IConfig.MaxComponentTypeId` |
| Max archetypes | 1,048,575 | ~1 million unique component combinations |
| Chunk size | 16KB (default) | Configurable via `IConfig.ChunkSize` |
| Entity ID size | 1/2/4 bytes | Configurable via `IConfig.EntityIdByteSize` |

## Build Commands

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run specific test class (TUnit uses --treenode-filter)
dotnet test --project src/Paradise.ECS.Test/Paradise.ECS.Test.csproj \
  -- --treenode-filter "/Paradise.ECS.Test/Paradise.ECS.Test/WorldTests/*"

# Run sample application
dotnet run --project src/Paradise.ECS.Sample/Paradise.ECS.Sample.csproj

# AOT publish (verify AOT compatibility)
dotnet publish src/Paradise.ECS.Test/Paradise.ECS.Test.csproj -c Release

# Run benchmarks
dotnet run --project src/Paradise.ECS.Benchmarks/Paradise.ECS.Benchmarks.csproj -c Release
```

## Advanced Usage

### Multi-World Scenarios

Share archetype metadata across multiple worlds for memory efficiency:

```csharp
using var sharedMetadata = new SharedArchetypeMetadata();
using var chunkManager = DefaultChunkManager.Create();

// Multiple worlds share the same metadata and chunk manager
var gameWorld = new World(sharedMetadata, chunkManager);
var uiWorld = new World(sharedMetadata, chunkManager);
var physicsWorld = new World(sharedMetadata, chunkManager);

// Each world has independent entities but shares type information
var player = EntityBuilder.Create()
    .Add(new Position { X = 0, Y = 0, Z = 0 })
    .Build(gameWorld);

var button = EntityBuilder.Create()
    .Add(new Position { X = 100, Y = 50, Z = 0 })
    .Build(uiWorld);
```

### Custom Memory Allocators

Implement `IAllocator` for custom memory allocation strategies:

```csharp
public class ArenaAllocator : IAllocator
{
    private readonly byte[] _buffer;
    private int _offset;

    public ArenaAllocator(int capacity) => _buffer = new byte[capacity];

    public nint Allocate(int size, int alignment)
    {
        _offset = (_offset + alignment - 1) & ~(alignment - 1);
        var ptr = (nint)Unsafe.AsPointer(ref _buffer[_offset]);
        _offset += size;
        return ptr;
    }

    public void Free(nint ptr) { } // Arena doesn't free individual allocations

    public void Reset() => _offset = 0; // Reset for next frame
}

using var allocator = new ArenaAllocator(1024 * 1024); // 1MB arena
using var chunkManager = new ChunkManager(allocator);
```

### Queryable Attribute Reference

| Attribute | Description |
|-----------|-------------|
| `[Queryable]` | Marks a partial ref struct as a queryable type |
| `[Queryable(Id = N)]` | Assigns a manual queryable ID |
| `[With<T>]` | Requires component T (generates property) |
| `[With<T>(Name = "X")]` | Custom property name |
| `[With<T>(IsReadOnly = true)]` | Generates `ref readonly` property |
| `[With<T>(QueryOnly = true)]` | Filter only, no property generated |
| `[Without<T>]` | Excludes entities with component T |
| `[WithAny<T>]` | Requires at least one WithAny component |
| `[Optional<T>]` | Generates `HasT` property and `GetT()` method |
| `[Optional<T>(IsReadOnly = true)]` | Generates `ref readonly GetT()` method |

## Roadmap

See [ROADMAP.md](ROADMAP.md) for detailed progress and planned features including:

- **Planned**: System scheduling and execution ordering
- **Planned**: Parallel job system with work stealing
- **Planned**: Godot engine integration
- **Planned**: Event system for component/entity changes
- **Planned**: Serialization with AOT support
- **Planned**: Prefabs and entity templates

## Contributing

Contributions are welcome!

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
