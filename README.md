# Paradise.ECS

A high-performance Entity Component System library for .NET 10, designed for Native AOT compilation.

## Features

- **Native AOT First** - No reflection, no dynamic code generation. All type information resolved at compile-time via source generators.
- **Cache-Friendly Design** - 16KB chunks sized for L1 cache with SoA (Struct of Arrays) memory layout for sequential access patterns.
- **O(1) Structural Changes** - Graph-based archetype transitions enable constant-time add/remove component operations.
- **Fluent Builder API** - Type-safe, zero-allocation entity creation with compile-time validation.
- **Multi-World Support** - Shared archetype metadata enables multiple worlds to share type information.

## Requirements

- .NET 10.0 or later
- C# 14

## Installation

Add a reference to the `Paradise.ECS` project and the source generator:

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
```

The source generator automatically implements `IComponent` and assigns type IDs.

### Create a World

```csharp
using Paradise.ECS;

// Create shared metadata and chunk manager
using var chunkManager = new ChunkManager(NativeMemoryAllocator.Shared);
using var sharedMetadata = new SharedArchetypeMetadata<Bit256, ComponentRegistry>();

// Create the world
var world = new World<Bit256, ComponentRegistry>(sharedMetadata, chunkManager);
```

> **Note**: `Bit256` determines the maximum number of component types (256). Choose from `Bit64`, `Bit128`, `Bit256`, `Bit512`, or `Bit1024` based on your needs.

### Spawn Entities

Using the fluent builder API (recommended for multiple components):

```csharp
// Create entity with components using fluent builder
var entity = EntityBuilder.Create()
    .Add(new Position { X = 0, Y = 0, Z = 0 })
    .Add(new Velocity { X = 1, Y = 0, Z = 0 })
    .Add(new Health { Current = 100, Max = 100 })
    .Build(world);

// Create entity with tag component
var player = EntityBuilder.Create()
    .Add(new Position { X = 0, Y = 0, Z = 0 })
    .Add<PlayerTag>()  // Tag components use default value
    .Build(world);
```

Or spawn and add components individually:

```csharp
// Spawn empty entity
var entity = world.Spawn();

// Add components one at a time
world.AddComponent(entity, new Position { X = 0, Y = 0, Z = 0 });
world.AddComponent(entity, new Velocity { X = 1, Y = 0, Z = 0 });
```

### Access Components

```csharp
// Check if entity has component
if (world.HasComponent<Position>(entity))
{
    // Get component reference (must be disposed)
    using var posRef = world.GetComponent<Position>(entity);
    Console.WriteLine($"Position: {posRef.Value.X}, {posRef.Value.Y}, {posRef.Value.Z}");

    // Modify through reference
    posRef.Value.X += 10;
}

// Set component value directly
world.SetComponent(entity, new Position { X = 100, Y = 200, Z = 0 });
```

### Modify Entity Structure

```csharp
// Add component (structural change)
world.AddComponent(entity, new Health { Current = 100, Max = 100 });

// Remove component (structural change)
world.RemoveComponent<Velocity>(entity);

// Overwrite all components on existing entity
EntityBuilder.Create()
    .Add(new Position { X = 50, Y = 50, Z = 0 })
    .Add(new Health { Current = 50, Max = 100 })
    .Overwrite(entity, world);

// Add multiple components at once
EntityBuilder.Create()
    .Add(new Velocity { X = 2, Y = 0, Z = 0 })
    .Add<PlayerTag>()
    .AddTo(entity, world);
```

### Destroy Entities

```csharp
// Check if entity is alive
if (world.IsAlive(entity))
{
    // Destroy entity
    world.Despawn(entity);
}

// Clear all entities
world.Clear();
```

### Query Entities

Build queries to iterate over entities with specific component combinations:

```csharp
// Query all entities with Position AND Velocity
var movingQuery = World<Bit256, ComponentRegistry>.Query()
    .With<Position>()
    .With<Velocity>()
    .Build(archetypeRegistry);

// Query entities with Position but WITHOUT PlayerTag
var nonPlayerQuery = World<Bit256, ComponentRegistry>.Query()
    .With<Position>()
    .Without<PlayerTag>()
    .Build(archetypeRegistry);

// Query entities with Position AND (Health OR Shield)
var damagableQuery = World<Bit256, ComponentRegistry>.Query()
    .With<Position>()
    .WithAny<Health>()
    .WithAny<Shield>()
    .Build(archetypeRegistry);
```

## Architecture

### Core Concepts

| Concept | Description |
|---------|-------------|
| **Entity** | A unique identifier (ID + Version) representing a game object |
| **Component** | Plain data attached to entities (unmanaged structs) |
| **Archetype** | A unique combination of component types; entities with the same components share an archetype |
| **Chunk** | A 16KB memory block storing entity data in SoA layout |
| **World** | The container that manages entities, components, and archetypes |
| **Query** | A filter for finding entities with specific component combinations |

### Memory Layout

Paradise.ECS uses a Struct of Arrays (SoA) memory layout within 16KB chunks:

```
Chunk (16KB):
┌─────────────────────────────────────────────────────┐
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
│   ├── Archetypes/            # Archetype management and metadata
│   ├── Components/            # Component interfaces and attributes
│   ├── Entities/              # Entity and EntityManager
│   ├── Memory/                # Chunk-based memory management
│   ├── Query/                 # Query builder and execution
│   ├── Types/                 # BitSet, HashedKey, and utilities
│   └── World/                 # World API and EntityBuilder
├── Paradise.ECS.Concurrent/   # Thread-safe ECS variant
├── Paradise.ECS.Generators/   # Source generators
├── Paradise.ECS.Test/         # Unit tests
├── Paradise.ECS.Benchmarks/   # Performance benchmarks
└── Paradise.ECS.CoyoteTest/   # Concurrency verification tests
```

## Limits

| Limit | Value | Notes |
|-------|-------|-------|
| Max component types | 2,047 | Configurable via TBits generic parameter |
| Max archetypes | 1,048,575 | ~1 million unique component combinations |
| Chunk size | 16KB | Optimized for L1 cache |

## Build Commands

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~ChunkManagerTests"

# AOT publish (verify AOT compatibility)
dotnet publish src/Paradise.ECS.Test/Paradise.ECS.Test.csproj -c Release
```

## Advanced Usage

### Component GUIDs for Serialization

For stable identification across compilations (useful for save files, networking):

```csharp
[Component("12345678-1234-1234-1234-123456789012")]
public partial struct Position
{
    public float X, Y, Z;
}
```

### Manual Component IDs

Override auto-assigned IDs when needed:

```csharp
[Component(Id = 100)]
public partial struct NetworkSyncedComponent
{
    public int Value;
}
```

### Multi-World Scenarios

Share archetype metadata across multiple worlds:

```csharp
using var chunkManager = new ChunkManager(NativeMemoryAllocator.Shared);
using var sharedMetadata = new SharedArchetypeMetadata<Bit256, ComponentRegistry>();

// Multiple worlds share the same metadata
var gameWorld = new World<Bit256, ComponentRegistry>(sharedMetadata, chunkManager);
var uiWorld = new World<Bit256, ComponentRegistry>(sharedMetadata, chunkManager);
var physicsWorld = new World<Bit256, ComponentRegistry>(sharedMetadata, chunkManager);
```

### Custom Allocators

Implement `IAllocator` for custom memory allocation strategies:

```csharp
public class ArenaAllocator : IAllocator
{
    public nint Allocate(int size, int alignment) { /* ... */ }
    public void Free(nint ptr) { /* ... */ }
}

using var allocator = new ArenaAllocator();
using var chunkManager = new ChunkManager(allocator);
```

## Roadmap

See [ROADMAP.md](ROADMAP.md) for planned features including:

- System scheduling and execution
- Parallel job system
- Event system
- Serialization
- Prefabs and templates
- Debugging and profiling tools

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
