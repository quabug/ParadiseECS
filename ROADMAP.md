# Paradise.ECS Roadmap

> Last updated: 2026-01-15 (updated)

## Vision

Paradise.ECS is a high-performance Entity Component System library for .NET 10, designed for Native AOT compilation. Part of the Paradise Engine ecosystem targeting game development with Pure C# + WebGPU + Slang.

## Current Status

**Core ECS foundation is complete.** All fundamental systems (Memory Management, Entities, Archetypes, Queries, World API, Source Generator) are implemented, tested, and production-ready. The codebase has comprehensive test coverage (5,870 LOC tests for 4,919 LOC source) with zero technical debt markers.

**Architecture Score: 8.5/10** - Solid foundation with clean, modular design following SOLID principles.

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
  - Data race fix in indexer (feature/native-list branch)

### In Progress

- [~] **Shared Archetype** (feature/shared-archetype)
  - Move archetype mask, query descriptions, and graph edges to a shared location

### Planned

- [ ] **Shared Archetype Infrastructure**
  - Move archetype mask, query descriptions, and graph edges to a shared location
  - Enable multiple worlds to share archetype metadata
  - Reduce memory overhead for multi-world scenarios

- [ ] **Static World Configuration**
  - Define interface-based configuration for world/archetype settings
  - Configurable chunk size (default 16KB)
  - Configurable TBits storage size (Bit64, Bit128, Bit256, etc.)
  - Configurable entity size and entity key type
  - Compile-time configuration validation

- [ ] **Queryable Archetype/Query Source Generator**
  - Attribute-based archetype and query definition
  - Code generation for strongly-typed query structs
  - Zero-allocation iteration patterns via generated code
  - Compile-time query validation

- [ ] **World Clone & Readonly World**
  - World cloning for snapshot/rollback scenarios
  - Readonly world view for safe concurrent reads
  - Copy-on-write semantics for efficient cloning
  - Support for game state serialization and networking

- [ ] **Performance Optimizations**
  - GetChunkIndex linear scan optimization ([#17](https://github.com/quabug/ParadiseECS/issues/17))
  - Incremental hash computation ([#14](https://github.com/quabug/ParadiseECS/issues/14))
  - FrozenDictionary vs Dictionary benchmark ([#13](https://github.com/quabug/ParadiseECS/issues/13))
  - Query archetype caching for high-frequency scenarios ([#12](https://github.com/quabug/ParadiseECS/issues/12))

- [ ] **Zero-Allocation Tag Component System** ([#10](https://github.com/quabug/ParadiseECS/issues/10))
  - Design and implement tag components without storage overhead

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

- [ ] **Debugging & Profiling**
  - Entity inspector integration
  - Performance profiler
  - Memory usage visualization
  - System execution timeline

- [ ] **Benchmark Suite**
  - BenchmarkDotNet integration
  - Performance targets definition
  - Regression detection
  - Comparison with other ECS libraries

---

## Open Issues Summary

| Issue | Title | Category |
|-------|-------|----------|
| [#18](https://github.com/quabug/ParadiseECS/issues/18) | Query Iteration Strategies for Structural Changes | Research |
| [#17](https://github.com/quabug/ParadiseECS/issues/17) | GetChunkIndex O(N) linear scan | Performance |
| [#14](https://github.com/quabug/ParadiseECS/issues/14) | Incremental hash computation | Performance |
| [#13](https://github.com/quabug/ParadiseECS/issues/13) | FrozenDictionary vs Dictionary benchmark | Performance |
| [#12](https://github.com/quabug/ParadiseECS/issues/12) | Query archetype caching | Performance |
| [#10](https://github.com/quabug/ParadiseECS/issues/10) | Zero-Allocation Tag Components | Enhancement |
| [#8](https://github.com/quabug/ParadiseECS/issues/8) | Thread safety improvements | Concurrency |

---

## Technical Debt

None currently tracked. Codebase is clean with zero TODO/FIXME markers.

---

## Notes

### Design Decisions

1. **Native AOT First**: No reflection, no dynamic code generation. All type information resolved at compile-time via source generators.

2. **Cache-Friendly Design**: 16KB chunks sized for L1 cache, SoA memory layout for sequential access patterns.

3. **Lock-Free Where Possible**: CAS operations for entity/chunk management, lock-based write serialization only where necessary.

4. **Zero-Allocation Queries**: Query objects are lightweight readonly structs that don't allocate.

### Next Priority

1. **Shared Archetype Infrastructure** - Enables multi-world scenarios with shared metadata
2. **Static World Configuration** - Provides compile-time configurable ECS parameters
3. **Queryable Archetype/Query Source Generator** - Enables type-safe, zero-allocation query patterns
4. **World Clone & Readonly World** - Enables snapshots, rollback, and safe concurrent reads
5. Address open performance issues ([#17](https://github.com/quabug/ParadiseECS/issues/17), [#14](https://github.com/quabug/ParadiseECS/issues/14), [#13](https://github.com/quabug/ParadiseECS/issues/13), [#12](https://github.com/quabug/ParadiseECS/issues/12))
6. Research query iteration strategies ([#18](https://github.com/quabug/ParadiseECS/issues/18))
7. Implement **System Scheduling** - this unlocks the ability to write actual game logic using the ECS

### Recent Activity

- **2026-01-14**: Merged [#19](https://github.com/quabug/ParadiseECS/pull/19) - ConcurrentAppendOnlyList and OperationGuard refactor
- **2026-01-13**: Merged [#16](https://github.com/quabug/ParadiseECS/pull/16) - World API with EntityBuilder
- **2026-01-12**: Merged [#11](https://github.com/quabug/ParadiseECS/pull/11) - Query system with push-based notifications
