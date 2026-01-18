namespace Paradise.ECS.Concurrent.Test;

/// <summary>
/// Tests for EntityBuilder fluent API.
/// </summary>
public sealed class EntityBuilderTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly ChunkManager _chunkManager = ChunkManager.Create(s_config);
    private readonly SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig> _sharedMetadata = new(s_config);
    private readonly World<Bit64, ComponentRegistry, DefaultConfig> _world;

    public EntityBuilderTests()
    {
        _world = new World<Bit64, ComponentRegistry, DefaultConfig>(
            s_config,
            _sharedMetadata,
            _chunkManager);
    }

    public void Dispose()
    {
        _world.Dispose();
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
    }

    [Test]
    public async Task Build_EmptyBuilder_CreatesEntityWithNoComponents()
    {
        var entity = EntityBuilder.Create()
            .Build(_world);

        var isValid = entity.IsValid;
        var isAlive = _world.IsAlive(entity);
        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasVel = _world.HasComponent<TestVelocity>(entity);

        await Assert.That(isValid).IsTrue();
        await Assert.That(isAlive).IsTrue();
        await Assert.That(hasPos).IsFalse();
        await Assert.That(hasVel).IsFalse();
    }

    [Test]
    public async Task Build_WithOneComponent_CreatesEntityWithComponent()
    {
        // Simplified API - no need to specify all generic types!
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 10, Y = 20, Z = 30 })
            .Build(_world);

        var isValid = entity.IsValid;
        var hasPos = _world.HasComponent<TestPosition>(entity);

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }

        await Assert.That(isValid).IsTrue();
        await Assert.That(hasPos).IsTrue();
        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
        await Assert.That(pos.Z).IsEqualTo(30f);
    }

    [Test]
    public async Task Build_WithMultipleComponents_CreatesEntityWithAllComponents()
    {
        // Simplified API - chaining works naturally!
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 10, Y = 20, Z = 30 })
            .Add(new TestVelocity { X = 1, Y = 2, Z = 3 })
            .Build(_world);

        var isValid = entity.IsValid;
        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasVel = _world.HasComponent<TestVelocity>(entity);

        TestPosition pos;
        TestVelocity vel;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }
        using (var velRef = _world.GetComponent<TestVelocity>(entity))
        {
            vel = velRef.Value;
        }

        await Assert.That(isValid).IsTrue();
        await Assert.That(hasPos).IsTrue();
        await Assert.That(hasVel).IsTrue();
        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
        await Assert.That(pos.Z).IsEqualTo(30f);
        await Assert.That(vel.X).IsEqualTo(1f);
        await Assert.That(vel.Y).IsEqualTo(2f);
        await Assert.That(vel.Z).IsEqualTo(3f);
    }

    [Test]
    public async Task Build_WithDefaultComponent_CreatesEntityWithDefaultValue()
    {
        // Simplified API - only need to specify the component type
        var entity = EntityBuilder.Create()
            .Add(default(TestPosition))
            .Build(_world);

        var isValid = entity.IsValid;
        var hasPos = _world.HasComponent<TestPosition>(entity);

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }

        await Assert.That(isValid).IsTrue();
        await Assert.That(hasPos).IsTrue();
        await Assert.That(pos.X).IsEqualTo(0f);
        await Assert.That(pos.Y).IsEqualTo(0f);
        await Assert.That(pos.Z).IsEqualTo(0f);
    }

    [Test]
    public async Task Build_MultipleEntities_IndependentInstances()
    {
        var e1 = EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Build(_world);

        var e2 = EntityBuilder.Create()
            .Add(new TestPosition { X = 200 })
            .Build(_world);

        float posX1, posX2;
        using (var ref1 = _world.GetComponent<TestPosition>(e1))
        {
            posX1 = ref1.Value.X;
        }
        using (var ref2 = _world.GetComponent<TestPosition>(e2))
        {
            posX2 = ref2.Value.X;
        }

        await Assert.That(e1.Id).IsNotEqualTo(e2.Id);
        await Assert.That(posX1).IsEqualTo(100f);
        await Assert.That(posX2).IsEqualTo(200f);
    }

    [Test]
    public async Task Build_MultipleEntitiesSameArchetype_BothHaveSameComponents()
    {
        var e1 = EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Build(_world);

        var e2 = EntityBuilder.Create()
            .Add(new TestPosition { X = 200 })
            .Build(_world);

        // Both should have Position component
        await Assert.That(_world.HasComponent<TestPosition>(e1)).IsTrue();
        await Assert.That(_world.HasComponent<TestPosition>(e2)).IsTrue();
    }

    [Test]
    public async Task Build_DifferentComponentCombinations_HaveDifferentComponents()
    {
        var e1 = EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Build(_world);

        var e2 = EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Add(new TestVelocity { X = 1 })
            .Build(_world);

        // e1 has only Position, e2 has Position and Velocity
        await Assert.That(_world.HasComponent<TestPosition>(e1)).IsTrue();
        await Assert.That(_world.HasComponent<TestVelocity>(e1)).IsFalse();
        await Assert.That(_world.HasComponent<TestPosition>(e2)).IsTrue();
        await Assert.That(_world.HasComponent<TestVelocity>(e2)).IsTrue();
    }

    [Test]
    public async Task EntityCount_AfterMultipleBuilds_CorrectCount()
    {
        for (int i = 0; i < 10; i++)
        {
            EntityBuilder.Create()
                .Add(new TestPosition { X = i })
                .Build(_world);
        }

        var count = _world.EntityCount;
        await Assert.That(count).IsEqualTo(10);
    }

    [Test]
    public async Task Overwrite_SpawnedEntity_SetsUpComponents()
    {
        var entity = _world.Spawn();

        var result = EntityBuilder.Create()
            .Add(new TestPosition { X = 10, Y = 20, Z = 30 })
            .Overwrite(entity, _world);

        var isAlive = _world.IsAlive(result);
        var hasPos = _world.HasComponent<TestPosition>(result);

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(result))
        {
            pos = posRef.Value;
        }

        await Assert.That(result.Id).IsEqualTo(entity.Id);
        await Assert.That(result.Version).IsEqualTo(entity.Version);
        await Assert.That(isAlive).IsTrue();
        await Assert.That(hasPos).IsTrue();
        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
        await Assert.That(pos.Z).IsEqualTo(30f);
    }

    [Test]
    public async Task Overwrite_WithMultipleComponents_SetsUpAllComponents()
    {
        var entity = _world.Spawn();

        EntityBuilder.Create()
            .Add(new TestPosition { X = 1, Y = 2, Z = 3 })
            .Add(new TestVelocity { X = 4, Y = 5, Z = 6 })
            .Overwrite(entity, _world);

        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasVel = _world.HasComponent<TestVelocity>(entity);

        TestPosition pos;
        TestVelocity vel;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }
        using (var velRef = _world.GetComponent<TestVelocity>(entity))
        {
            vel = velRef.Value;
        }

        await Assert.That(hasPos).IsTrue();
        await Assert.That(hasVel).IsTrue();
        await Assert.That(pos.X).IsEqualTo(1f);
        await Assert.That(pos.Y).IsEqualTo(2f);
        await Assert.That(pos.Z).IsEqualTo(3f);
        await Assert.That(vel.X).IsEqualTo(4f);
        await Assert.That(vel.Y).IsEqualTo(5f);
        await Assert.That(vel.Z).IsEqualTo(6f);
    }

    [Test]
    public async Task Overwrite_EmptyBuilder_EntityStaysWithoutComponents()
    {
        var entity = _world.Spawn();

        var result = EntityBuilder.Create()
            .Overwrite(entity, _world);

        var isAlive = _world.IsAlive(result);
        var hasPos = _world.HasComponent<TestPosition>(result);
        var hasVel = _world.HasComponent<TestVelocity>(result);

        await Assert.That(result.Id).IsEqualTo(entity.Id);
        await Assert.That(isAlive).IsTrue();
        await Assert.That(hasPos).IsFalse();
        await Assert.That(hasVel).IsFalse();
    }

    [Test]
    public async Task Overwrite_EmptyBuilder_ClearsExistingComponents()
    {
        // Regression test: Overwrite with empty builder should clear existing components.
        // Bug: When mask is empty, OverwriteEntity returned early without removing from
        // current archetype, leaving stale components on the entity.

        // Create entity with components
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 100, Y = 200, Z = 300 })
            .Add(new TestVelocity { X = 1, Y = 2, Z = 3 })
            .Build(_world);

        // Overwrite with empty builder - should clear all components
        EntityBuilder.Create()
            .Overwrite(entity, _world);

        var isAlive = _world.IsAlive(entity);
        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasVel = _world.HasComponent<TestVelocity>(entity);

        await Assert.That(isAlive).IsTrue();
        // Bug: These would still be true if components weren't cleared
        await Assert.That(hasPos).IsFalse();
        await Assert.That(hasVel).IsFalse();
    }

    [Test]
    public async Task Overwrite_DeadEntity_ThrowsInvalidOperationException()
    {
        var entity = _world.Spawn();
        _world.Despawn(entity);

        var action = () => EntityBuilder.Create()
            .Add(new TestPosition { X = 10 })
            .Overwrite(entity, _world);

        await Assert.That(action).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Overwrite_InvalidEntity_ThrowsException()
    {
        var invalidEntity = default(Entity);

        var action = () => EntityBuilder.Create()
            .Add(new TestPosition { X = 10 })
            .Overwrite(invalidEntity, _world);

        await Assert.That(action).ThrowsException();
    }

    [Test]
    public async Task Overwrite_MultipleEntities_IndependentInstances()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();

        EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Overwrite(e1, _world);

        EntityBuilder.Create()
            .Add(new TestPosition { X = 200 })
            .Overwrite(e2, _world);

        float posX1, posX2;
        using (var ref1 = _world.GetComponent<TestPosition>(e1))
        {
            posX1 = ref1.Value.X;
        }
        using (var ref2 = _world.GetComponent<TestPosition>(e2))
        {
            posX2 = ref2.Value.X;
        }

        await Assert.That(e1.Id).IsNotEqualTo(e2.Id);
        await Assert.That(posX1).IsEqualTo(100f);
        await Assert.That(posX2).IsEqualTo(200f);
    }

    [Test]
    public async Task Overwrite_SameArchetypeAsBuiltEntities_BothHaveSameComponents()
    {
        // Create entity using Build
        var built = EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Build(_world);

        // Create entity using Spawn + Overwrite
        var spawned = _world.Spawn();
        EntityBuilder.Create()
            .Add(new TestPosition { X = 200 })
            .Overwrite(spawned, _world);

        // Both should have Position component
        await Assert.That(_world.HasComponent<TestPosition>(built)).IsTrue();
        await Assert.That(_world.HasComponent<TestPosition>(spawned)).IsTrue();
    }

    [Test]
    public async Task Overwrite_EntityWithExistingComponents_ReplacesComponents()
    {
        // Create entity with Position component
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 100, Y = 200, Z = 300 })
            .Build(_world);

        // Overwrite with Velocity - this replaces, not adds
        EntityBuilder.Create()
            .Add(new TestVelocity { X = 1, Y = 2, Z = 3 })
            .Overwrite(entity, _world);

        // Entity now has only Velocity, not Position (Overwrite replaces all components)
        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasVel = _world.HasComponent<TestVelocity>(entity);

        await Assert.That(hasPos).IsFalse();
        await Assert.That(hasVel).IsTrue();

        TestVelocity vel;
        using (var velRef = _world.GetComponent<TestVelocity>(entity))
        {
            vel = velRef.Value;
        }

        await Assert.That(vel.X).IsEqualTo(1f);
        await Assert.That(vel.Y).IsEqualTo(2f);
        await Assert.That(vel.Z).IsEqualTo(3f);
    }

    [Test]
    public async Task AddTo_SpawnedEntity_AddsComponents()
    {
        var entity = _world.Spawn();

        EntityBuilder.Create()
            .Add(new TestPosition { X = 10, Y = 20, Z = 30 })
            .AddTo(entity, _world);

        var hasPos = _world.HasComponent<TestPosition>(entity);

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }

        await Assert.That(hasPos).IsTrue();
        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
        await Assert.That(pos.Z).IsEqualTo(30f);
    }

    [Test]
    public async Task AddTo_EntityWithExistingComponents_PreservesAndAdds()
    {
        // Create entity with Position
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 100, Y = 200, Z = 300 })
            .Build(_world);

        // Add Velocity - should preserve Position
        EntityBuilder.Create()
            .Add(new TestVelocity { X = 1, Y = 2, Z = 3 })
            .AddTo(entity, _world);

        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasVel = _world.HasComponent<TestVelocity>(entity);

        TestPosition pos;
        TestVelocity vel;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }
        using (var velRef = _world.GetComponent<TestVelocity>(entity))
        {
            vel = velRef.Value;
        }

        await Assert.That(hasPos).IsTrue();
        await Assert.That(hasVel).IsTrue();
        await Assert.That(pos.X).IsEqualTo(100f);
        await Assert.That(pos.Y).IsEqualTo(200f);
        await Assert.That(pos.Z).IsEqualTo(300f);
        await Assert.That(vel.X).IsEqualTo(1f);
        await Assert.That(vel.Y).IsEqualTo(2f);
        await Assert.That(vel.Z).IsEqualTo(3f);
    }

    [Test]
    public async Task AddTo_MultipleComponentsAtOnce_AddsAll()
    {
        var entity = _world.Spawn();

        EntityBuilder.Create()
            .Add(new TestPosition { X = 10 })
            .Add(new TestVelocity { X = 20 })
            .AddTo(entity, _world);

        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasVel = _world.HasComponent<TestVelocity>(entity);

        float posX, velX;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            posX = posRef.Value.X;
        }
        using (var velRef = _world.GetComponent<TestVelocity>(entity))
        {
            velX = velRef.Value.X;
        }

        await Assert.That(hasPos).IsTrue();
        await Assert.That(hasVel).IsTrue();
        await Assert.That(posX).IsEqualTo(10f);
        await Assert.That(velX).IsEqualTo(20f);
    }

    [Test]
    public async Task AddTo_DuplicateComponent_ThrowsInvalidOperationException()
    {
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Build(_world);

        var action = () => EntityBuilder.Create()
            .Add(new TestPosition { X = 200 })
            .AddTo(entity, _world);

        await Assert.That(action).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task AddTo_EmptyBuilder_NoChange()
    {
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 100 })
            .Build(_world);

        EntityBuilder.Create()
            .AddTo(entity, _world);

        var hasPos = _world.HasComponent<TestPosition>(entity);

        float posX;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            posX = posRef.Value.X;
        }

        await Assert.That(hasPos).IsTrue();
        await Assert.That(posX).IsEqualTo(100f);
    }

    [Test]
    public async Task AddTo_DeadEntity_ThrowsException()
    {
        var entity = _world.Spawn();
        _world.Despawn(entity);

        var action = () => EntityBuilder.Create()
            .Add(new TestPosition { X = 10 })
            .AddTo(entity, _world);

        await Assert.That(action).ThrowsException();
    }

    [Test]
    public async Task Build_TagWithDataComponent_DoesNotCorruptEntityId()
    {
        // Regression test: Zero-size tag components (Size=0) write 1 byte at offset 0,
        // because empty structs have sizeof=1 in C# and GetEntityComponentOffset returns
        // baseOffset(0) + entityIndex * size(0) = 0 regardless of entity index.
        // This corrupts the first byte of entity[0]'s stored ID in the chunk.
        //
        // Bug scenario:
        // 1. Create entity1 (ID=1) at index 0 - its ID stored at offset 0, first byte = 0x01
        // 2. Create entity2 (ID=2) at index 1 - tag write corrupts offset 0 to 0x00
        // 3. Now stored IDs in chunk: [0, 2] instead of [1, 2]
        // 4. Despawn entity1 (index 0) - swap-remove moves entity2 from index 1 to index 0
        // 5. Swap-remove reads entity ID at index 1 to know which entity moved
        // 6. This reads correct ID (2), so entity2's location is updated correctly
        //
        // Actually, the corruption at index 0 affects reading entity1's ID, which is only
        // read when entity1 is at index 0 and we remove a later entity, causing entity1
        // to be swapped to a new position. But RemoveEntity reads from source (last) index,
        // not from the destination (removed) index.
        //
        // The real bug: if we remove entity at index 1, swap-remove copies entity0's data
        // to index 1 and reads entity0's ID to update its location. Entity0's stored ID
        // is corrupted from 1 to 0, so the wrong entity's location is updated.

        // Bump entity ID counter so entities have non-zero first byte IDs
        var dummy = _world.Spawn();
        _world.Despawn(dummy);

        // Create 2 entities with tags
        var entity1 = EntityBuilder.Create()
            .Add(new TestPosition { X = 10, Y = 20, Z = 30 })
            .Add(default(TestTag))
            .Build(_world);  // ID=1 at index 0

        var entity2 = EntityBuilder.Create()
            .Add(new TestPosition { X = 100, Y = 200, Z = 300 })
            .Add(default(TestTag))
            .Build(_world);  // ID=2 at index 1, tag writes at offset 0 corrupting entity1's ID

        // Despawn entity1 (index 0) - swap-remove moves entity2 from index 1 to index 0
        // RemoveEntity reads entity ID from source index (1), which is entity2's ID (2) - correct
        _world.Despawn(entity1);

        // entity2 should have been moved to index 0 and its location updated
        var isAlive2 = _world.IsAlive(entity2);
        await Assert.That(isAlive2).IsTrue();

        TestPosition pos2;
        using (var posRef = _world.GetComponent<TestPosition>(entity2))
        {
            pos2 = posRef.Value;
        }

        await Assert.That(pos2.X).IsEqualTo(100f);
        await Assert.That(pos2.Y).IsEqualTo(200f);
        await Assert.That(pos2.Z).IsEqualTo(300f);
    }

    [Test]
    public async Task Build_DataComponentWithTag_DoesNotCorruptData()
    {
        // Same test but with reversed add order to ensure both paths work
        var entity = EntityBuilder.Create()
            .Add(default(TestTag))  // Tag component with Size=0 added first
            .Add(new TestPosition { X = 100, Y = 200, Z = 300 })
            .Build(_world);

        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasTag = _world.HasComponent<TestTag>(entity);

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }

        await Assert.That(hasPos).IsTrue();
        await Assert.That(hasTag).IsTrue();
        await Assert.That(pos.X).IsEqualTo(100f);
        await Assert.That(pos.Y).IsEqualTo(200f);
        await Assert.That(pos.Z).IsEqualTo(300f);
    }

    [Test]
    public async Task Build_MultipleDataComponentsWithTag_DoesNotCorruptAny()
    {
        // Test with multiple data components to ensure none are corrupted
        var entity = EntityBuilder.Create()
            .Add(new TestPosition { X = 1, Y = 2, Z = 3 })
            .Add(default(TestTag))
            .Add(new TestVelocity { X = 4, Y = 5, Z = 6 })
            .Build(_world);

        var hasPos = _world.HasComponent<TestPosition>(entity);
        var hasTag = _world.HasComponent<TestTag>(entity);
        var hasVel = _world.HasComponent<TestVelocity>(entity);

        TestPosition pos;
        TestVelocity vel;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }
        using (var velRef = _world.GetComponent<TestVelocity>(entity))
        {
            vel = velRef.Value;
        }

        await Assert.That(hasPos).IsTrue();
        await Assert.That(hasTag).IsTrue();
        await Assert.That(hasVel).IsTrue();
        await Assert.That(pos.X).IsEqualTo(1f);
        await Assert.That(pos.Y).IsEqualTo(2f);
        await Assert.That(pos.Z).IsEqualTo(3f);
        await Assert.That(vel.X).IsEqualTo(4f);
        await Assert.That(vel.Y).IsEqualTo(5f);
        await Assert.That(vel.Z).IsEqualTo(6f);
    }
}
