namespace Paradise.ECS.Test;

/// <summary>
/// Tests for World.CloneTo() functionality.
/// </summary>
public sealed class WorldCloneTests : IDisposable
{
    private readonly SharedWorld<SmallBitSet<ulong>, DefaultConfig> _sharedWorld;
    private readonly World<SmallBitSet<ulong>, DefaultConfig> _sourceWorld;
    private readonly World<SmallBitSet<ulong>, DefaultConfig> _targetWorld;

    public WorldCloneTests()
    {
        _sharedWorld = new SharedWorld<SmallBitSet<ulong>, DefaultConfig>(ComponentRegistry.Shared.TypeInfos);
        _sourceWorld = _sharedWorld.CreateWorld();
        _targetWorld = _sharedWorld.CreateWorld();
    }

    public void Dispose()
    {
        _sharedWorld.Dispose();
    }

    #region Basic Clone Tests

    [Test]
    public async Task CopyFrom_CopiesAllEntities()
    {
        // Arrange
        _sourceWorld.Spawn();
        _sourceWorld.Spawn();
        _sourceWorld.Spawn();

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Assert
        await Assert.That(_targetWorld.EntityCount).IsEqualTo(3);
    }

    [Test]
    public async Task CopyFrom_CopiesComponentData()
    {
        // Arrange
        var entity = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(entity, new TestPosition { X = 10.5f, Y = 20.5f, Z = 30.5f });
        _sourceWorld.AddComponent(entity, new TestVelocity { X = 1.0f, Y = 2.0f, Z = 3.0f });

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Assert - Entity handles from source world are valid in target after clone
        var pos = _targetWorld.GetComponent<TestPosition>(entity);
        var vel = _targetWorld.GetComponent<TestVelocity>(entity);

        await Assert.That(pos.X).IsEqualTo(10.5f);
        await Assert.That(pos.Y).IsEqualTo(20.5f);
        await Assert.That(pos.Z).IsEqualTo(30.5f);
        await Assert.That(vel.X).IsEqualTo(1.0f);
        await Assert.That(vel.Y).IsEqualTo(2.0f);
        await Assert.That(vel.Z).IsEqualTo(3.0f);
    }

    [Test]
    public async Task CopyFrom_EntityHandlesRemainValid()
    {
        // Arrange
        var entity1 = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(entity1, new TestPosition { X = 100, Y = 200 });
        var entity2 = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(entity2, new TestHealth { Current = 50, Max = 100 });

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Assert - Original entity handles work in target world
        await Assert.That(_targetWorld.IsAlive(entity1)).IsTrue();
        await Assert.That(_targetWorld.IsAlive(entity2)).IsTrue();
        await Assert.That(_targetWorld.HasComponent<TestPosition>(entity1)).IsTrue();
        await Assert.That(_targetWorld.HasComponent<TestHealth>(entity2)).IsTrue();
    }

    [Test]
    public async Task CopyFrom_ClearsTargetFirst()
    {
        // Arrange - Add entities to both worlds with different components
        var sourceEntity = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(sourceEntity, new TestPosition { X = 1, Y = 2 });

        // Create target entities - the first one will have same ID as sourceEntity
        var targetEntity1 = _targetWorld.Spawn();
        _targetWorld.AddComponent(targetEntity1, new TestVelocity { X = 5, Y = 6 }); // Different component type
        var targetEntity2 = _targetWorld.Spawn();
        _targetWorld.AddComponent(targetEntity2, new TestHealth { Current = 100, Max = 100 });
        var targetEntity3 = _targetWorld.Spawn();

        await Assert.That(_targetWorld.EntityCount).IsEqualTo(3);

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Assert - Target should only have source entities
        await Assert.That(_targetWorld.EntityCount).IsEqualTo(1);
        await Assert.That(_targetWorld.IsAlive(sourceEntity)).IsTrue();

        // Verify the data is from source (TestPosition), not original target (TestVelocity)
        // After clone, entity at ID 0 has Position from source, not Velocity from target
        await Assert.That(_targetWorld.HasComponent<TestPosition>(sourceEntity)).IsTrue();
        await Assert.That(_targetWorld.HasComponent<TestVelocity>(sourceEntity)).IsFalse();
        var pos = _targetWorld.GetComponent<TestPosition>(sourceEntity);
        await Assert.That(pos.X).IsEqualTo(1);
        await Assert.That(pos.Y).IsEqualTo(2);

        // targetEntity2 and targetEntity3 have IDs > 0, so they shouldn't exist after clone
        await Assert.That(_targetWorld.IsAlive(targetEntity2)).IsFalse();
        await Assert.That(_targetWorld.IsAlive(targetEntity3)).IsFalse();
    }

    [Test]
    public async Task CopyFrom_EmptyWorld()
    {
        // Arrange - Source is empty, target has entities
        _targetWorld.Spawn();
        _targetWorld.Spawn();
        await Assert.That(_targetWorld.EntityCount).IsEqualTo(2);

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Assert
        await Assert.That(_targetWorld.EntityCount).IsEqualTo(0);
    }

    [Test]
    public async Task CopyFrom_MultipleArchetypes()
    {
        // Arrange - Create entities with different component combinations
        var entity1 = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(entity1, new TestPosition { X = 1, Y = 1 });

        var entity2 = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(entity2, new TestVelocity { X = 2, Y = 2 });

        var entity3 = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(entity3, new TestPosition { X = 3, Y = 3 });
        _sourceWorld.AddComponent(entity3, new TestVelocity { X = 4, Y = 4 });

        var entity4 = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(entity4, new TestHealth { Current = 100, Max = 100 });

        var entity5 = _sourceWorld.Spawn(); // Empty archetype

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Assert
        await Assert.That(_targetWorld.EntityCount).IsEqualTo(5);

        await Assert.That(_targetWorld.HasComponent<TestPosition>(entity1)).IsTrue();
        await Assert.That(_targetWorld.HasComponent<TestVelocity>(entity1)).IsFalse();

        await Assert.That(_targetWorld.HasComponent<TestVelocity>(entity2)).IsTrue();
        await Assert.That(_targetWorld.HasComponent<TestPosition>(entity2)).IsFalse();

        await Assert.That(_targetWorld.HasComponent<TestPosition>(entity3)).IsTrue();
        await Assert.That(_targetWorld.HasComponent<TestVelocity>(entity3)).IsTrue();

        await Assert.That(_targetWorld.HasComponent<TestHealth>(entity4)).IsTrue();

        await Assert.That(_targetWorld.HasComponent<TestPosition>(entity5)).IsFalse();
        await Assert.That(_targetWorld.HasComponent<TestVelocity>(entity5)).IsFalse();
        await Assert.That(_targetWorld.HasComponent<TestHealth>(entity5)).IsFalse();

        // Verify component values
        var pos1 = _targetWorld.GetComponent<TestPosition>(entity1);
        await Assert.That(pos1.X).IsEqualTo(1);

        var pos3 = _targetWorld.GetComponent<TestPosition>(entity3);
        var vel3 = _targetWorld.GetComponent<TestVelocity>(entity3);
        await Assert.That(pos3.X).IsEqualTo(3);
        await Assert.That(vel3.X).IsEqualTo(4);

        var health4 = _targetWorld.GetComponent<TestHealth>(entity4);
        await Assert.That(health4.Current).IsEqualTo(100);
    }

    #endregion

    #region Entity Lifecycle Tests

    [Test]
    public async Task CopyFrom_PreservesFreeSlots()
    {
        // Arrange - Create and despawn some entities to create free slots
        var e1 = _sourceWorld.Spawn();
        var e2 = _sourceWorld.Spawn();
        var e3 = _sourceWorld.Spawn();

        _sourceWorld.Despawn(e2); // Create a free slot in the middle

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Assert
        await Assert.That(_targetWorld.EntityCount).IsEqualTo(2);
        await Assert.That(_targetWorld.IsAlive(e1)).IsTrue();
        await Assert.That(_targetWorld.IsAlive(e2)).IsFalse(); // e2 was despawned
        await Assert.That(_targetWorld.IsAlive(e3)).IsTrue();
    }

    [Test]
    public async Task CopyFrom_PreservesEntityVersions()
    {
        // Arrange - Create entity, despawn it, create new one to bump version
        var e1 = _sourceWorld.Spawn();
        _sourceWorld.Despawn(e1);

        var e2 = _sourceWorld.Spawn(); // Should reuse e1's slot with bumped version
        _sourceWorld.AddComponent(e2, new TestPosition { X = 42, Y = 42 });

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Assert
        await Assert.That(_targetWorld.IsAlive(e1)).IsFalse(); // Old version should be invalid
        await Assert.That(_targetWorld.IsAlive(e2)).IsTrue(); // New version should be valid
        var pos = _targetWorld.GetComponent<TestPosition>(e2);
        await Assert.That(pos.X).IsEqualTo(42);
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task CopyFrom_ThrowsOnNullSource()
    {
        await Assert.That(() => _targetWorld.CopyFrom(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task CopyFrom_ThrowsOnDifferentSharedWorld()
    {
        // Arrange - Create a world from a different SharedWorld
        using var otherSharedWorld = new SharedWorld<SmallBitSet<ulong>, DefaultConfig>(ComponentRegistry.Shared.TypeInfos);
        var otherWorld = otherSharedWorld.CreateWorld();

        // Act & Assert - Copying from a world with different SharedMetadata should throw
        await Assert.That(() => _targetWorld.CopyFrom(otherWorld)).Throws<InvalidOperationException>();
    }

    #endregion

    #region Multi-Chunk Tests

    [Test]
    public async Task CopyFrom_CopiesMultipleChunks()
    {
        // Arrange - Create enough entities to span multiple chunks
        // With 16KB chunks and typical component sizes, we need many entities
        const int entityCount = 1000;
        var entities = new Entity[entityCount];

        for (int i = 0; i < entityCount; i++)
        {
            entities[i] = _sourceWorld.Spawn();
            _sourceWorld.AddComponent(entities[i], new TestPosition { X = i, Y = i * 2, Z = i * 3 });
        }

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Assert
        await Assert.That(_targetWorld.EntityCount).IsEqualTo(entityCount);

        // Verify some entities across the range
        var pos0 = _targetWorld.GetComponent<TestPosition>(entities[0]);
        await Assert.That(pos0.X).IsEqualTo(0);

        var pos500 = _targetWorld.GetComponent<TestPosition>(entities[500]);
        await Assert.That(pos500.X).IsEqualTo(500);

        var pos999 = _targetWorld.GetComponent<TestPosition>(entities[999]);
        await Assert.That(pos999.X).IsEqualTo(999);
    }

    #endregion

    #region Query Tests

    [Test]
    public async Task CopyFrom_QueriesWorkOnClonedWorld()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            var entity = _sourceWorld.Spawn();
            _sourceWorld.AddComponent(entity, new TestPosition { X = i, Y = i });
        }

        for (int i = 0; i < 3; i++)
        {
            var entity = _sourceWorld.Spawn();
            _sourceWorld.AddComponent(entity, new TestVelocity { X = i, Y = i });
        }

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Assert - Query should work on cloned world
        var posQuery = QueryBuilder<SmallBitSet<ulong>>.Create().With<TestPosition>().Build(_targetWorld);
        var velQuery = QueryBuilder<SmallBitSet<ulong>>.Create().With<TestVelocity>().Build(_targetWorld);

        int posCount = 0;
        foreach (var _ in posQuery)
            posCount++;

        int velCount = 0;
        foreach (var _ in velQuery)
            velCount++;

        await Assert.That(posCount).IsEqualTo(5);
        await Assert.That(velCount).IsEqualTo(3);
    }

    #endregion

    #region Isolation Tests

    [Test]
    public async Task CopyFrom_SourceAndTargetAreIndependent()
    {
        // Arrange
        var entity = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(entity, new TestPosition { X = 10, Y = 20 });

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Modify source after clone
        _sourceWorld.SetComponent(entity, new TestPosition { X = 100, Y = 200 });

        // Assert - Target should have original values
        var sourcePos = _sourceWorld.GetComponent<TestPosition>(entity);
        var targetPos = _targetWorld.GetComponent<TestPosition>(entity);

        await Assert.That(sourcePos.X).IsEqualTo(100);
        await Assert.That(sourcePos.Y).IsEqualTo(200);
        await Assert.That(targetPos.X).IsEqualTo(10);
        await Assert.That(targetPos.Y).IsEqualTo(20);
    }

    [Test]
    public async Task CopyFrom_ModifyingTargetDoesNotAffectSource()
    {
        // Arrange
        var entity = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(entity, new TestPosition { X = 10, Y = 20 });

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Modify target after clone
        _targetWorld.SetComponent(entity, new TestPosition { X = 100, Y = 200 });

        // Assert - Source should have original values
        var sourcePos = _sourceWorld.GetComponent<TestPosition>(entity);
        var targetPos = _targetWorld.GetComponent<TestPosition>(entity);

        await Assert.That(sourcePos.X).IsEqualTo(10);
        await Assert.That(sourcePos.Y).IsEqualTo(20);
        await Assert.That(targetPos.X).IsEqualTo(100);
        await Assert.That(targetPos.Y).IsEqualTo(200);
    }

    [Test]
    public async Task CopyFrom_NewEntitiesInTargetAreIndependent()
    {
        // Arrange
        var sourceEntity = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(sourceEntity, new TestPosition { X = 1, Y = 2 });

        // Act
        _targetWorld.CopyFrom(_sourceWorld);

        // Create new entity in target after clone
        var newEntity = _targetWorld.Spawn();
        _targetWorld.AddComponent(newEntity, new TestVelocity { X = 5, Y = 6 });

        // Assert
        await Assert.That(_sourceWorld.EntityCount).IsEqualTo(1);
        await Assert.That(_targetWorld.EntityCount).IsEqualTo(2);
        await Assert.That(_sourceWorld.IsAlive(newEntity)).IsFalse();
        await Assert.That(_targetWorld.IsAlive(newEntity)).IsTrue();
    }

    #endregion

    #region Multiple Clone Tests

    [Test]
    public async Task CopyFrom_CanCloneMultipleTimes()
    {
        // Arrange
        var entity = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(entity, new TestPosition { X = 1, Y = 1 });

        // First clone
        _targetWorld.CopyFrom(_sourceWorld);
        await Assert.That(_targetWorld.EntityCount).IsEqualTo(1);

        // Modify source
        _sourceWorld.SetComponent(entity, new TestPosition { X = 2, Y = 2 });
        var entity2 = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(entity2, new TestVelocity { X = 3, Y = 3 });

        // Second clone
        _targetWorld.CopyFrom(_sourceWorld);

        // Assert - Target should have updated state
        await Assert.That(_targetWorld.EntityCount).IsEqualTo(2);
        var pos = _targetWorld.GetComponent<TestPosition>(entity);
        await Assert.That(pos.X).IsEqualTo(2);
        await Assert.That(pos.Y).IsEqualTo(2);
    }

    [Test]
    public async Task CopyFrom_CanCloneToMultipleTargets()
    {
        // Arrange
        var entity = _sourceWorld.Spawn();
        _sourceWorld.AddComponent(entity, new TestPosition { X = 10, Y = 20 });

        var target2 = _sharedWorld.CreateWorld();

        // Act
        _targetWorld.CopyFrom(_sourceWorld);
        target2.CopyFrom(_sourceWorld);

        // Assert - Both targets should have the same data
        await Assert.That(_targetWorld.EntityCount).IsEqualTo(1);
        await Assert.That(target2.EntityCount).IsEqualTo(1);

        var pos1 = _targetWorld.GetComponent<TestPosition>(entity);
        var pos2 = target2.GetComponent<TestPosition>(entity);

        await Assert.That(pos1.X).IsEqualTo(10);
        await Assert.That(pos2.X).IsEqualTo(10);
    }

    #endregion
}
