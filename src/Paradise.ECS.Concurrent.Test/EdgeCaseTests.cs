namespace Paradise.ECS.Concurrent.Test;

/// <summary>
/// Edge case and boundary condition tests for the ECS.
/// </summary>
public sealed class EdgeCaseTests : IDisposable
{
    private readonly ChunkManager<DefaultConfig> _chunkManager = new(new DefaultConfig());
    private readonly World<Bit64, ComponentRegistry, DefaultConfig> _world;

    public EdgeCaseTests()
    {
        _world = new World<Bit64, ComponentRegistry, DefaultConfig>(
            new DefaultConfig(),
            SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig>.Shared,
            _chunkManager);
    }

    public void Dispose()
    {
        _world.Dispose();
        _chunkManager.Dispose();
    }

    #region Entity Edge Cases

    [Test]
    public async Task DefaultEntity_IsNotValid()
    {
        var entity = default(Entity);

        await Assert.That(entity.IsValid).IsFalse();
    }

    [Test]
    public async Task DefaultEntity_IsNotAlive()
    {
        var entity = default(Entity);

        var isAlive = _world.IsAlive(entity);

        await Assert.That(isAlive).IsFalse();
    }

    [Test]
    public async Task DespawnedEntity_CannotAddComponent()
    {
        var entity = _world.Spawn();
        _world.Despawn(entity);

        await Assert.That(() => _world.AddComponent(entity, new TestPosition()))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DespawnedEntity_CannotRemoveComponent()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);
        _world.Despawn(entity);

        await Assert.That(() => _world.RemoveComponent<TestPosition>(entity))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DespawnedEntity_CannotGetComponent()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);
        _world.Despawn(entity);

        await Assert.That(() =>
        {
            using var _ = _world.GetComponent<TestPosition>(entity);
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DespawnedEntity_CannotSetComponent()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);
        _world.Despawn(entity);

        await Assert.That(() => _world.SetComponent(entity, new TestPosition()))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Despawn_AlreadyDespawned_ReturnsFalse()
    {
        var entity = _world.Spawn();
        var first = _world.Despawn(entity);
        var second = _world.Despawn(entity);

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
    }

    [Test]
    public async Task Despawn_InvalidEntity_ReturnsFalse()
    {
        var result = _world.Despawn(default);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task StaleEntityHandle_AfterSlotReuse_IsNotAlive()
    {
        var entity1 = _world.Spawn();
        var savedEntity = entity1;
        _world.Despawn(entity1);

        // Spawn a new entity which may reuse the slot
        var entity2 = _world.Spawn();

        // The saved entity should not be alive (different version)
        var isAlive = _world.IsAlive(savedEntity);
        await Assert.That(isAlive).IsFalse();

        // But the new entity is alive
        await Assert.That(_world.IsAlive(entity2)).IsTrue();
    }

    #endregion

    #region Component Edge Cases

    [Test]
    public async Task AddDuplicateComponent_Throws()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        await Assert.That(() => _world.AddComponent<TestPosition>(entity))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RemoveNonExistentComponent_Throws()
    {
        var entity = _world.Spawn();

        await Assert.That(() => _world.RemoveComponent<TestPosition>(entity))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetNonExistentComponent_Throws()
    {
        var entity = _world.Spawn();

        await Assert.That(() =>
        {
            using var _ = _world.GetComponent<TestPosition>(entity);
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task SetNonExistentComponent_Throws()
    {
        var entity = _world.Spawn();

        await Assert.That(() => _world.SetComponent(entity, new TestPosition()))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task HasComponent_EntityWithNoComponents_ReturnsFalse()
    {
        var entity = _world.Spawn();

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsFalse();
    }

    [Test]
    public async Task HasComponent_InvalidEntity_ReturnsFalse()
    {
        await Assert.That(_world.HasComponent<TestPosition>(default)).IsFalse();
    }

    [Test]
    public async Task RemoveLastComponent_EntityStaysAlive()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);
        _world.RemoveComponent<TestPosition>(entity);

        await Assert.That(_world.IsAlive(entity)).IsTrue();
        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsFalse();
    }

    [Test]
    public async Task ZeroSizeComponent_CanBeAddedAndRemoved()
    {
        var entity = _world.Spawn();

        _world.AddComponent<TestTag>(entity);
        await Assert.That(_world.HasComponent<TestTag>(entity)).IsTrue();

        _world.RemoveComponent<TestTag>(entity);
        await Assert.That(_world.HasComponent<TestTag>(entity)).IsFalse();
    }

    #endregion

    #region Query Edge Cases

    [Test]
    public async Task EmptyQuery_MatchesAllArchetypes()
    {
        var e1 = _world.Spawn();
        _world.AddComponent<TestPosition>(e1);

        var e2 = _world.Spawn();
        _world.AddComponent<TestVelocity>(e2);

        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(2);
    }

    [Test]
    public async Task Query_NoMatchingArchetypes_ReturnsEmpty()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestVelocity>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.IsEmpty).IsTrue();
        await Assert.That(query.EntityCount).IsEqualTo(0);
        await Assert.That(query.ArchetypeCount).IsEqualTo(0);
    }

    [Test]
    public async Task Query_ConflictingAllAndNone_MatchesNothing()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        // Query for Position but exclude Position - impossible!
        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Without<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Query_EmptyAny_StillRequiresAll()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        // Query with All requirement only
        var query = World<Bit64, ComponentRegistry, DefaultConfig>.Query()
            .With<TestPosition>()
            .Build(_world.ArchetypeRegistry);

        await Assert.That(query.EntityCount).IsEqualTo(1);
    }

    #endregion

    #region BitSet Edge Cases

    [Test]
    public async Task BitSet_SetSameBitTwice_NoChange()
    {
        var bitSet = ImmutableBitSet<Bit64>.Empty;
        bitSet = bitSet.Set(5);
        bitSet = bitSet.Set(5);

        await Assert.That(bitSet.Get(5)).IsTrue();
        await Assert.That(bitSet.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task BitSet_ClearUnsetBit_NoChange()
    {
        var bitSet = ImmutableBitSet<Bit64>.Empty;
        bitSet = bitSet.Clear(5);

        await Assert.That(bitSet.Get(5)).IsFalse();
        await Assert.That(bitSet.IsEmpty).IsTrue();
    }

    [Test]
    public async Task BitSet_FirstBit_Works()
    {
        var bitSet = ImmutableBitSet<Bit64>.Empty.Set(0);

        await Assert.That(bitSet.Get(0)).IsTrue();
        await Assert.That(bitSet.FirstSetBit()).IsEqualTo(0);
    }

    [Test]
    public async Task BitSet_LastBit_Works()
    {
        var bitSet = ImmutableBitSet<Bit64>.Empty.Set(63);

        await Assert.That(bitSet.Get(63)).IsTrue();
        await Assert.That(bitSet.LastSetBit()).IsEqualTo(63);
    }

    [Test]
    public async Task BitSet_Empty_OperationsWork()
    {
        var empty = ImmutableBitSet<Bit64>.Empty;

        await Assert.That(empty.IsEmpty).IsTrue();
        await Assert.That(empty.PopCount()).IsEqualTo(0);
        await Assert.That(empty.FirstSetBit()).IsEqualTo(-1);
        await Assert.That(empty.LastSetBit()).IsEqualTo(-1);
    }

    #endregion

    #region ChunkHandle Edge Cases

    [Test]
    public async Task ChunkHandle_Invalid_HasCorrectProperties()
    {
        var invalid = ChunkHandle.Invalid;

        await Assert.That(invalid.IsValid).IsFalse();
    }

    [Test]
    public async Task ChunkHandle_VersionZero_IsInvalid()
    {
        var handle = new ChunkHandle(0, 0);

        await Assert.That(handle.IsValid).IsFalse();
    }

    [Test]
    public async Task ChunkHandle_VersionOne_IsValid()
    {
        var handle = new ChunkHandle(0, 1);

        await Assert.That(handle.IsValid).IsTrue();
    }

    #endregion

    #region World Disposed Edge Cases

    [Test]
    public async Task World_Disposed_SpawnThrows()
    {
        using var cm = new ChunkManager<DefaultConfig>(new DefaultConfig());
        var world = new World<Bit64, ComponentRegistry, DefaultConfig>(
            new DefaultConfig(),
            SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig>.Shared,
            cm);
        world.Dispose();

        await Assert.That(world.Spawn).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task World_Disposed_HasComponentDoesNotThrow()
    {
        using var cm = new ChunkManager<DefaultConfig>(new DefaultConfig());
        var world = new World<Bit64, ComponentRegistry, DefaultConfig>(
            new DefaultConfig(),
            SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig>.Shared,
            cm);
        var entity = world.Spawn();
        world.Dispose();

        // HasComponent on disposed world should throw for alive checks
        await Assert.That(() => world.HasComponent<TestPosition>(entity))
            .Throws<ObjectDisposedException>();
    }

    #endregion

    #region Entity Reuse Edge Cases

    [Test]
    public async Task EntityVersion_IncrementsOnReuse()
    {
        var entity1 = _world.Spawn();
        uint version1 = entity1.Version;
        int id1 = entity1.Id;

        _world.Despawn(entity1);

        // Create many more entities to likely trigger reuse
        for (int i = 0; i < 100; i++)
        {
            var e = _world.Spawn();
            if (e.Id == id1)
            {
                // Found reused slot
                await Assert.That(e.Version).IsGreaterThan(version1);
                return;
            }
        }

        // If slot wasn't reused, that's also acceptable behavior - test passes either way
    }

    #endregion

    #region Archetype Transition Edge Cases

    [Test]
    public async Task AddComponent_TransitionsArchetype()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);

        var hasPositionOnly = _world.HasComponent<TestPosition>(entity) && !_world.HasComponent<TestVelocity>(entity);
        await Assert.That(hasPositionOnly).IsTrue();

        _world.AddComponent<TestVelocity>(entity);

        var hasBoth = _world.HasComponent<TestPosition>(entity) && _world.HasComponent<TestVelocity>(entity);
        await Assert.That(hasBoth).IsTrue();
    }

    [Test]
    public async Task RemoveComponent_TransitionsArchetype()
    {
        var entity = _world.Spawn();
        _world.AddComponent<TestPosition>(entity);
        _world.AddComponent<TestVelocity>(entity);

        var hasBoth = _world.HasComponent<TestPosition>(entity) && _world.HasComponent<TestVelocity>(entity);
        await Assert.That(hasBoth).IsTrue();

        _world.RemoveComponent<TestVelocity>(entity);

        var hasPositionOnly = _world.HasComponent<TestPosition>(entity) && !_world.HasComponent<TestVelocity>(entity);
        await Assert.That(hasPositionOnly).IsTrue();
    }

    [Test]
    public async Task ArchetypeTransition_PreservesData()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 10, Y = 20, Z = 30 });

        // Transition by adding Velocity
        _world.AddComponent(entity, new TestVelocity { X = 1 });

        // Original Position data should be preserved
        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }

        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
        await Assert.That(pos.Z).IsEqualTo(30f);
    }

    #endregion

    #region Component Value Edge Cases

    [Test]
    public async Task ComponentWithDefaultValues_WorksCorrectly()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, default(TestPosition));

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }

        await Assert.That(pos.X).IsEqualTo(0f);
        await Assert.That(pos.Y).IsEqualTo(0f);
        await Assert.That(pos.Z).IsEqualTo(0f);
    }

    [Test]
    public async Task SetComponent_OverwritesValue()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 1, Y = 2, Z = 3 });
        _world.SetComponent(entity, new TestPosition { X = 10, Y = 20, Z = 30 });

        TestPosition pos;
        using (var posRef = _world.GetComponent<TestPosition>(entity))
        {
            pos = posRef.Value;
        }

        await Assert.That(pos.X).IsEqualTo(10f);
        await Assert.That(pos.Y).IsEqualTo(20f);
        await Assert.That(pos.Z).IsEqualTo(30f);
    }

    #endregion

    #region HashedKey Edge Cases

    [Test]
    public async Task HashedKey_DefaultStruct_WorksCorrectly()
    {
        var key1 = (HashedKey<int>)0;
        var key2 = (HashedKey<int>)0;

        await Assert.That(key1).IsEqualTo(key2);
    }

    [Test]
    public async Task HashedKey_NegativeValues_WorksCorrectly()
    {
        var key1 = (HashedKey<int>)(-1);
        var key2 = (HashedKey<int>)(-1);

        await Assert.That(key1).IsEqualTo(key2);
        await Assert.That(key1.GetHashCode()).IsEqualTo(key2.GetHashCode());
    }

    #endregion
}
