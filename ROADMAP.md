# Paradise.ECS Roadmap

> Last updated: 2026-01-23

## Vision

Paradise.ECS is a high-performance Entity Component System library for .NET 10, designed for Native AOT compilation. Part of the Paradise Engine ecosystem targeting game development with Pure C# + WebGPU + Slang.

---

## Milestones

### Completed

- [x] **Memory Management System** ([#2](https://github.com/quabug/ParadiseECS/pull/2))
  - Lock-free ChunkManager with CAS operations
  - 16KB chunks optimized for L1 cache
  - Version-based handle validation (48-bit)
  - NativeMemoryAllocator implementation

- [x] **Type System** ([#3](https://github.com/quabug/ParadiseECS/pull/3), [#19](https://github.com/quabug/ParadiseECS/pull/19))
  - Generic ImmutableBitSet with InlineArray (stack-allocated)
  - Predefined sizes: Bit64, Bit128, Bit256, Bit512, Bit1024
  - HashedKey<T> for pre-computed dictionary keys
  - ConcurrentAppendOnlyList<T> with lock-free Add

- [x] **Entity Lifecycle** ([#6](https://github.com/quabug/ParadiseECS/pull/6))
  - Thread-safe EntityManager with lock-free Create/Destroy
  - Version-based stale handle detection
  - Free slot reuse via ConcurrentStack
  - Atomic entity lifecycle management

- [x] **Component Type System** ([#5](https://github.com/quabug/ParadiseECS/pull/5))
  - Component ID assignment (alphabetical ordering)
  - Unmanaged type validation
  - Registry generation with type information
  - ComponentRegistryNamespaceAttribute support

- [x] **Archetype System** ([#7](https://github.com/quabug/ParadiseECS/pull/7), [#9](https://github.com/quabug/ParadiseECS/pull/9))
  - SoA (Struct of Arrays) memory layout
  - Archetype graph edges for O(1) structural changes
  - Component mask matching
  - Swap-remove semantics for O(1) deletion

- [x] **Query System** ([#11](https://github.com/quabug/ParadiseECS/pull/11))
  - Zero-allocation query execution
  - QueryBuilder with fluent API (With, Without, WithAny)
  - ImmutableQueryDescription for cached queries
  - Push-based archetype notifications

- [x] **World API** ([#16](https://github.com/quabug/ParadiseECS/pull/16))
  - Entity CRUD operations (Spawn, Despawn, IsAlive)
  - Component operations (Get, Set, Has, Add, Remove)
  - EntityBuilder for fluent component attachment
  - OperationGuard for disposal safety

- [x] **ConcurrentAppendOnlyList** ([#19](https://github.com/quabug/ParadiseECS/pull/19))
  - Chunked managed array approach
  - Lock-free Add operations
  - Data race fix in indexer

- [x] **Shared Archetype Infrastructure** ([#25](https://github.com/quabug/ParadiseECS/pull/25))
  - SharedArchetypeMetadata for multi-world archetype sharing
  - Centralized mask-to-ID mappings, layouts, graph edges
  - Query description caching across worlds
  - EdgeKey-based O(1) archetype transitions

- [x] **Single-Threaded World Optimization** ([#30](https://github.com/quabug/ParadiseECS/pull/30))
  - Simplified EntityManager without concurrent overhead
  - Consolidated entity location storage
  - ThrowHelper for centralized validation
  - Refactored GetChunkLocation to return tuples

- [x] **Static World Configuration** ([#32](https://github.com/quabug/ParadiseECS/pull/32))
  - `IConfig` interface with static abstract members for compile-time constraints
  - Configurable chunk size (default 16KB)
  - Configurable `MaxMetaBlocks` for chunk management capacity
  - Configurable `EntityIdByteSize` for entity ID storage (1/2/4 bytes)
  - Instance members for runtime hints (`DefaultEntityCapacity`, `DefaultChunkCapacity`)
  - `Config<T>` helper for computed values (`MaxEntityId`)
  - `[DefaultConfig]` attribute with `Paradise.ECS.DefaultConfig` fallback
  - Parameterless constructors for `World`, `ChunkManager`, `SharedArchetypeMetadata`

- [x] **WorldQuery and WorldEntity** ([#33](https://github.com/quabug/ParadiseECS/pull/33))
  - Convenient entity iteration via `WorldQuery<TBits, TRegistry, TConfig>`
  - `WorldEntity` wrapper for fluent component access
  - 64-bit packed `EntityLocation` for lock-free atomic operations
  - QueryBuilder extension method `Build(world)` for ergonomic query creation

- [x] **Query Code Generator** ([#35](https://github.com/quabug/ParadiseECS/pull/35))
  - Source generator for strongly-typed query structs
  - Zero-allocation iteration patterns via generated code
  - Compile-time query validation
  - KGP-style typed iteration with direct component property access
  - `WithAttribute<T>` with Name, IsReadOnly, QueryOnly parameters
  - `OptionalAttribute<T>` for optional components with Has/Get pattern
  - Chunk-level batch iteration with span properties for SIMD-friendly processing

- [x] **Zero-Allocation Tag Component System** ([#38](https://github.com/quabug/ParadiseECS/pull/38))
  - Paradise.ECS.Tag optional package for tag functionality
  - Auto-generated EntityTags component when Paradise.ECS.Tag is referenced
  - TaggedWorld wrapper providing O(1) tag operations without archetype changes
  - Chunk-level tag presence masks with sticky (bloom filter) semantics
  - Tag-specific APIs: `AddTag<T>()`, `RemoveTag<T>()`, `HasTag<T>()`, `GetTags()`
  - `ComputeStaleBitStatistics()` and `RebuildChunkMasks()` for maintenance

### In Progress

- [~] **Refactor Generic Parameters** (feature/refactor-generic-parameters)
  - Refactor generic type parameters across the codebase for improved API clarity and consistency

### Planned

- [ ] **World Clone & Snapshots**
  - World cloning for snapshot/rollback scenarios
  - Copy-on-write semantics for efficient cloning
  - Support for game state serialization and networking
  - Integration with ReadOnlyWorld for snapshot queries

- [ ] **System Scheduling**
    - System base class/interface
    - Query iteration patterns
    - System execution ordering
    - Dependency management between systems

- [ ] **Parallel Job System**
    - Job scheduling infrastructure
    - Work stealing for load balancing
    - Parallel query iteration
    - Safety rails for data races

- [ ] **Query with Structural Change Strategies** ([#18](https://github.com/quabug/ParadiseECS/issues/18))
    - Investigate safe iteration patterns when entities are added/removed during query
    - Deferred structural changes (command buffers) vs immediate changes
    - Archetype stability guarantees during iteration
    - Chunk invalidation and iterator invalidation detection
    - Consider: Unity DOTS EntityCommandBuffer, Bevy Commands, Flecs defer patterns

- [ ] **Extensible Metadata Interface**
    - Define interface for world/archetype metadata (e.g., `IWorldMetadata`)
    - Allow custom implementations beyond the default `SharedArchetypeMetadata`
    - Enable extension points for custom caching strategies, persistence, or debugging hooks
    - Support composition of metadata providers for modular functionality

- [ ] **Specialized World Types**
    - `SingleThreadWorld` - Optimized for single-threaded scenarios, minimal locking overhead
    - `JobsWorld` - Multi-threaded world with job system integration, parallel query iteration
    - `ReadOnlyWorld` - Immutable view for safe concurrent reads, snapshot queries
    - `ReplayWorld` - Recording and playback of world state for debugging/networking
    - Common interface (`IWorld`) with usage-specific implementations
    - Compile-time world type selection for optimal performance

- [ ] **Reference Type Component Support**
  - Enable managed/reference type components (classes, strings, arrays)
  - Separate storage strategy for managed vs unmanaged components
  - GC-aware chunk management for reference types
  - Consider hybrid archetypes with both unmanaged and managed components
  - Investigate pinning strategies for interop scenarios

- [ ] **Exclusive Write Constraints**
  - Compile-time or runtime enforcement that only one system can write to a component type
  - Enable maximum parallelization by guaranteeing no write conflicts
  - Read-many, write-one access pattern for component types
  - Automatic dependency graph generation based on read/write declarations
  - Integration with System Scheduling for optimal parallel execution

- [ ] **Event System**
  - Component change events
  - Entity lifecycle events
  - Inter-system communication
  - Event buffering/batching

- [ ] **Serialization**
  - Entity/component persistence
  - World state snapshots
  - AOT-compatible serialization
  - Version migration support

- [ ] **Object Pool**
  - Generic object pooling for frequently allocated objects
  - Thread-safe pool implementation for concurrent access
  - Integration with ECS systems for reusable component data
  - Configurable pool sizes and growth strategies

- [ ] **Memory Allocation Strategies**
  - Extend existing `IAllocator` interface with additional implementations
  - **Arena Allocator**: Fast bump-pointer allocation with bulk deallocation, ideal for frame-temporary data
  - **Virtual Memory Allocator**: Reserve large contiguous address space, commit pages on demand for sparse data
  - **Stack Allocator**: LIFO allocation pattern for temporary allocations within a scope
  - **Pool Allocator**: Fixed-size block allocation for uniform objects (complement to Object Pool)
  - Consider alignment requirements for SIMD operations
  - Memory budget tracking and diagnostics integration
  - Hot-swappable allocator strategies per World or subsystem

- [ ] **Godot Integration**
  - Create binding layer for Godot 4.x C# (.NET 8+)
  - Bridge Paradise.ECS entities with Godot Node system
  - Component-to-Node synchronization for rendering, physics, and audio
  - GDScript interop for querying and manipulating ECS data
  - Godot editor integration for entity inspection and debugging
  - Scene tree synchronization strategies (lazy vs eager)
  - Consider GodotSharp API compatibility and lifecycle management
  - Performance considerations: minimize managed-to-native boundary crossings
  - Example project demonstrating ECS-driven game logic with Godot rendering

- [ ] **Performance Optimizations**
    - GetChunkIndex linear scan optimization ([#17](https://github.com/quabug/ParadiseECS/issues/17))
    - Incremental hash computation ([#14](https://github.com/quabug/ParadiseECS/issues/14))
    - FrozenDictionary vs Dictionary benchmark ([#13](https://github.com/quabug/ParadiseECS/issues/13))
    - Query archetype caching for high-frequency scenarios ([#12](https://github.com/quabug/ParadiseECS/issues/12))
    - Hybrid edge caching for archetype transitions ([#20](https://github.com/quabug/ParadiseECS/issues/20))
    - Inverted index for query matching optimization

- [ ] **Per-World Component IDs**
    - Allow each world to have independent component type registrations
    - Currently component IDs are global via `IComponentRegistry` generated at compile-time
    - Enable runtime component registration with world-local ID assignment
    - Support scenarios where different worlds use different component sets
    - Consider trade-offs: global IDs enable world sharing, local IDs enable isolation
    - Potential approaches:
        - World-specific registry instances with local ID mapping
        - Hybrid: shared base components + world-local extensions
        - Runtime component type registration with AOT-compatible patterns

- [ ] **Cross-Assembly ECS Support**
    - Enable components, queryables, and world types to be defined across multiple assemblies
    - Components from library assemblies should be usable in application assemblies
    - Queryables in one assembly should reference components from other assemblies
    - World type aliases should work with component registries spanning assemblies
    - Consider assembly-level coordination for component ID assignment
    - Challenges: source generator runs per-assembly, need cross-assembly type discovery
    - Potential approaches:
        - Incremental ID ranges reserved per assembly
        - Assembly-level attribute to declare component ID offsets
        - Runtime component registration with AOT-compatible patterns
        - Shared component base assembly with extension assemblies

- [ ] **Resource Management**
    - Asset loading abstractions
    - Resource lifecycle management
    - Hot-reload support
    - Memory budgeting

- [ ] **Prefabs & Templates**
    - Entity archetype templates
    - Instantiation patterns
    - Nested prefabs
    - Runtime modification
---

## Open Issues Summary

| Issue | Title | Category |
|-------|-------|----------|
| [#28](https://github.com/quabug/ParadiseECS/issues/28) | Investigate empty archetype for componentless entities | Research |
| [#27](https://github.com/quabug/ParadiseECS/issues/27) | Investigate lazy location allocation in EntityManager | Research |
| [#26](https://github.com/quabug/ParadiseECS/issues/26) | Review: SharedArchetypeMetadata for multi-world sharing | Review |
| [#20](https://github.com/quabug/ParadiseECS/issues/20) | Hybrid edge caching for archetype transitions | Research |
| [#18](https://github.com/quabug/ParadiseECS/issues/18) | Query Iteration Strategies for Structural Changes | Research |
| [#17](https://github.com/quabug/ParadiseECS/issues/17) | GetChunkIndex O(N) linear scan | Performance |
| [#14](https://github.com/quabug/ParadiseECS/issues/14) | Incremental hash computation | Performance |
| [#13](https://github.com/quabug/ParadiseECS/issues/13) | FrozenDictionary vs Dictionary benchmark | Performance |
| [#12](https://github.com/quabug/ParadiseECS/issues/12) | Query archetype caching | Performance |
| [#10](https://github.com/quabug/ParadiseECS/issues/10) | Zero-Allocation Tag Components | Enhancement |
| [#8](https://github.com/quabug/ParadiseECS/issues/8) | Thread safety improvements | Concurrency |

---

## Technical Debt

Minor TODOs in codebase:

| Location | Description | Related Issue |
|----------|-------------|---------------|
| `SharedArchetypeMetadata.cs` | Optimize query matching with inverted index | - |
| `SharedArchetypeMetadata.cs` (Concurrent) | Incremental hash computation | [#14](https://github.com/quabug/ParadiseECS/issues/14) |

---

## Notes

### Design Decisions

1. **Native AOT First**: No reflection, no dynamic code generation. All type information resolved at compile-time via source generators.

2. **Cache-Friendly Design**: 16KB chunks sized for L1 cache, SoA memory layout for sequential access patterns.

3. **Lock-Free Where Possible**: CAS operations for entity/chunk management, lock-based write serialization only where necessary.

4. **Zero-Allocation Queries**: Query objects are lightweight readonly structs that don't allocate.

5. **Dual Implementation**: Both single-threaded (`Paradise.ECS`) and concurrent (`Paradise.ECS.Concurrent`) variants share core types while optimizing for their specific use cases.

### Next Priority

1. **Specialized World Types** - SingleThreadWorld, JobsWorld, ReadOnlyWorld for different usage patterns
2. **System Scheduling** - Enables writing actual game logic using the ECS
3. Address open performance issues ([#17](https://github.com/quabug/ParadiseECS/issues/17), [#14](https://github.com/quabug/ParadiseECS/issues/14), [#13](https://github.com/quabug/ParadiseECS/issues/13), [#12](https://github.com/quabug/ParadiseECS/issues/12))
4. Research query iteration strategies ([#18](https://github.com/quabug/ParadiseECS/issues/18))

### Recent Activity

- **2026-01-22**: Merged [#38](https://github.com/quabug/ParadiseECS/pull/38) - Add zero-allocation tag system with source generation
- **2026-01-19**: Merged [#37](https://github.com/quabug/ParadiseECS/pull/37) - Add [SuppressGlobalUsings] attribute to disable global using alias generation
- **2026-01-19**: Merged [#35](https://github.com/quabug/ParadiseECS/pull/35) - Add QueryableGenerator for compile-time query type registration
- **2026-01-18**: Merged [#33](https://github.com/quabug/ParadiseECS/pull/33) - Add WorldQuery and WorldEntity for convenient entity iteration
- **2026-01-18**: Merged [#32](https://github.com/quabug/ParadiseECS/pull/32) - Static World Configuration
  - `IConfig` interface with static abstract + instance members
  - Configurable `EntityIdByteSize` for 1/2/4-byte entity IDs
  - `[DefaultConfig]` attribute with `Paradise.ECS.DefaultConfig` fallback
  - Parameterless constructors for convenience
- **2026-01-17**: Merged [#30](https://github.com/quabug/ParadiseECS/pull/30) - Add single-threaded Paradise.ECS with comprehensive test coverage
- **2026-01-16**: Merged [#25](https://github.com/quabug/ParadiseECS/pull/25) - SharedArchetypeMetadata for multi-world sharing
- **2026-01-15**: Merged [#24](https://github.com/quabug/ParadiseECS/pull/24) - Project roadmap
- **2026-01-15**: Merged [#21](https://github.com/quabug/ParadiseECS/pull/21) - Windows test fix and ConcurrentAppendOnlyList performance
- **2026-01-14**: Merged [#19](https://github.com/quabug/ParadiseECS/pull/19) - ConcurrentAppendOnlyList and OperationGuard refactor
- **2026-01-13**: Merged [#16](https://github.com/quabug/ParadiseECS/pull/16) - World API with EntityBuilder
- **2026-01-12**: Merged [#11](https://github.com/quabug/ParadiseECS/pull/11) - Query system with push-based notifications
