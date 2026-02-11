namespace Paradise.ECS.Test;

// ============================================================================
// Test System Definitions
// ============================================================================

/// <summary>
/// Test system: adds velocity to position per entity, using underscore-prefixed fields.
/// Regression test for ToCamelCase stripping underscore prefix in generated constructor.
/// </summary>
#pragma warning disable IDE1006 // Naming rule violation — underscore prefix intentional for regression testing
public ref partial struct TestUnderscoreFieldSystem : IEntitySystem
{
    public ref TestPosition _Position;
    public ref readonly TestVelocity _Velocity;

    public void Execute()
    {
        _Position = new TestPosition { X = _Position.X + _Velocity.X, Y = _Position.Y + _Velocity.Y, Z = _Position.Z + _Velocity.Z };
    }
}
#pragma warning restore IDE1006

/// <summary>
/// Test system: adds velocity to position per entity.
/// </summary>
public ref partial struct TestMovementSystem : IEntitySystem
{
    public ref TestPosition Position;
    public ref readonly TestVelocity Velocity;

    public void Execute()
    {
        Position = new TestPosition { X = Position.X + Velocity.X, Y = Position.Y + Velocity.Y, Z = Position.Z + Velocity.Z };
    }
}

/// <summary>
/// Test system: multiplies velocity Y by 2.
/// </summary>
public ref partial struct TestGravitySystem : IEntitySystem
{
    public ref TestVelocity Velocity;

    public void Execute()
    {
        Velocity = new TestVelocity { X = Velocity.X, Y = Velocity.Y * 2, Z = Velocity.Z };
    }
}

/// <summary>
/// Test system: runs after TestMovementSystem.
/// </summary>
[After<TestMovementSystem>]
public ref partial struct TestBoundsSystem : IEntitySystem
{
    public ref TestPosition Position;

    public void Execute()
    {
        Position = new TestPosition
        {
            X = Math.Clamp(Position.X, 0, 100),
            Y = Math.Clamp(Position.Y, 0, 100),
            Z = Math.Clamp(Position.Z, 0, 100),
        };
    }
}

/// <summary>
/// Test chunk system: batch multiplies velocity Y by 2.
/// </summary>
public ref partial struct TestGravityBatchSystem : IChunkSystem
{
    public Span<TestVelocity> Velocities;

    public void ExecuteChunk()
    {
        for (int i = 0; i < Velocities.Length; i++)
            Velocities[i] = new TestVelocity { X = Velocities[i].X, Y = Velocities[i].Y * 2, Z = Velocities[i].Z };
    }
}

/// <summary>
/// Test system: only reads health (no writes).
/// </summary>
public ref partial struct TestReadOnlyHealthSystem : IEntitySystem
{
    public ref readonly TestHealth Health;

    public void Execute()
    {
        // Read-only system - just observes health
        _ = Health.Current;
    }
}

// ============================================================================
// Tests
// ============================================================================

/// <summary>
/// Tests for the System API: scheduling, execution, and DAG ordering.
/// </summary>
public sealed class SystemTests : IDisposable
{
    private readonly SharedWorld _sharedWorld;
    private readonly World _world;

    public SystemTests()
    {
        _sharedWorld = SharedWorldFactory.Create();
        _world = _sharedWorld.CreateWorld();
    }

    public void Dispose()
    {
        _sharedWorld.Dispose();
    }

    // ---- Entity System Tests ----

    [Test]
    public async Task EntitySystem_Execute_UpdatesComponents()
    {
        var e = _world.Spawn();
        _world.AddComponent(e, new TestPosition { X = 10, Y = 20, Z = 0 });
        _world.AddComponent(e, new TestVelocity { X = 1, Y = 2, Z = 0 });

        var schedule = SystemSchedule.Create(_world)
            .Add<TestMovementSystem>()
            .Build<SequentialWaveScheduler>();

        schedule.Run();

        // TestGravitySystem not in schedule, so vel is unchanged
        // But TestMovementSystem reads Velocity and writes Position
        // Due to DAG ordering, if GravitySystem were present, it would run first.
        // Here only MovementSystem runs.
        var pos = _world.GetComponent<TestPosition>(e);
        await Assert.That(pos.X).IsEqualTo(11f);
        await Assert.That(pos.Y).IsEqualTo(22f);
    }

    [Test]
    public async Task EntitySystem_MultipleEntities_ProcessesAll()
    {
        var e1 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 1, Y = 0, Z = 0 });
        _world.AddComponent(e1, new TestVelocity { X = 10, Y = 0, Z = 0 });

        var e2 = _world.Spawn();
        _world.AddComponent(e2, new TestPosition { X = 2, Y = 0, Z = 0 });
        _world.AddComponent(e2, new TestVelocity { X = 20, Y = 0, Z = 0 });

        var e3 = _world.Spawn();
        _world.AddComponent(e3, new TestPosition { X = 3, Y = 0, Z = 0 });
        _world.AddComponent(e3, new TestVelocity { X = 30, Y = 0, Z = 0 });

        var schedule = SystemSchedule.Create(_world)
            .Add<TestMovementSystem>()
            .Build<SequentialWaveScheduler>();

        schedule.Run();

        await Assert.That(_world.GetComponent<TestPosition>(e1).X).IsEqualTo(11f);
        await Assert.That(_world.GetComponent<TestPosition>(e2).X).IsEqualTo(22f);
        await Assert.That(_world.GetComponent<TestPosition>(e3).X).IsEqualTo(33f);
    }

    [Test]
    public async Task EntitySystem_NoMatchingEntities_DoesNotCrash()
    {
        // Spawn entity without Velocity (MovementSystem requires both Position and Velocity)
        var e = _world.Spawn();
        _world.AddComponent(e, new TestPosition { X = 10, Y = 20, Z = 0 });

        var schedule = SystemSchedule.Create(_world)
            .Add<TestMovementSystem>()
            .Build<SequentialWaveScheduler>();

        schedule.Run();

        // Position should be unchanged
        var pos = _world.GetComponent<TestPosition>(e);
        await Assert.That(pos.X).IsEqualTo(10f);
    }

    // ---- Chunk System Tests ----

    [Test]
    public async Task ChunkSystem_ExecuteChunk_ProcessesBatch()
    {
        var e1 = _world.Spawn();
        _world.AddComponent(e1, new TestVelocity { X = 1, Y = 5, Z = 0 });

        var e2 = _world.Spawn();
        _world.AddComponent(e2, new TestVelocity { X = 2, Y = 10, Z = 0 });

        var schedule = SystemSchedule.Create(_world)
            .Add<TestGravityBatchSystem>()
            .Build<SequentialWaveScheduler>();

        schedule.Run();

        await Assert.That(_world.GetComponent<TestVelocity>(e1).Y).IsEqualTo(10f);
        await Assert.That(_world.GetComponent<TestVelocity>(e2).Y).IsEqualTo(20f);
    }

    // ---- DAG Ordering Tests ----

    [Test]
    public async Task DagOrdering_AfterAttribute_EnforcesOrder()
    {
        // BoundsSystem has [After<TestMovementSystem>] so movement runs first, then bounds clamp
        var e = _world.Spawn();
        _world.AddComponent(e, new TestPosition { X = 90, Y = 0, Z = 0 });
        _world.AddComponent(e, new TestVelocity { X = 20, Y = 0, Z = 0 });

        var schedule = SystemSchedule.Create(_world)
            .Add<TestMovementSystem>()
            .Add<TestBoundsSystem>()
            .Build<SequentialWaveScheduler>();

        schedule.Run();

        // Movement: 90 + 20 = 110, then Bounds: clamp(110, 0, 100) = 100
        var pos = _world.GetComponent<TestPosition>(e);
        await Assert.That(pos.X).IsEqualTo(100f);
    }

    [Test]
    public async Task DagOrdering_ConflictDetection_SeparatesConflictingSystems()
    {
        // GravitySystem writes Velocity, MovementSystem reads Velocity → different waves
        var e = _world.Spawn();
        _world.AddComponent(e, new TestPosition { X = 0, Y = 0, Z = 0 });
        _world.AddComponent(e, new TestVelocity { X = 0, Y = 5, Z = 0 });

        var schedule = SystemSchedule.Create(_world)
            .Add<TestGravitySystem>()
            .Add<TestMovementSystem>()
            .Build<SequentialWaveScheduler>();

        schedule.Run();

        // GravitySystem doubles vel.Y: 5 → 10
        // Then MovementSystem adds vel to pos: 0 + 10 = 10
        var pos = _world.GetComponent<TestPosition>(e);
        await Assert.That(pos.Y).IsEqualTo(10f);
    }

    // ---- Schedule Tests ----

    [Test]
    public async Task Schedule_AddAll_RunsAllSystems()
    {
        var e = _world.Spawn();
        _world.AddComponent(e, new TestPosition { X = 0, Y = 0, Z = 0 });
        _world.AddComponent(e, new TestVelocity { X = 1, Y = 1, Z = 0 });

        var schedule = SystemSchedule.Create(_world)
            .AddAll()
            .Build<SequentialWaveScheduler>();

        // Should not throw
        schedule.Run();

        // Position should have changed (at least MovementSystem ran)
        var pos = _world.GetComponent<TestPosition>(e);
        await Assert.That(pos.X).IsNotEqualTo(0f);
    }

    [Test]
    public async Task Schedule_RunParallel_ProducesSameResultsAsSequential()
    {
        // Create entities in one world, run sequential; create same in another, run parallel; compare
        var e1 = _world.Spawn();
        _world.AddComponent(e1, new TestPosition { X = 10, Y = 20, Z = 0 });
        _world.AddComponent(e1, new TestVelocity { X = 1, Y = 2, Z = 0 });

        var schedule = SystemSchedule.Create(_world)
            .Add<TestMovementSystem>()
            .Add<TestGravitySystem>()
            .Build<SequentialWaveScheduler>();

        schedule.Run();
        var seqPos = _world.GetComponent<TestPosition>(e1);
        var seqVel = _world.GetComponent<TestVelocity>(e1);

        // Reset
        _world.Clear();
        var e2 = _world.Spawn();
        _world.AddComponent(e2, new TestPosition { X = 10, Y = 20, Z = 0 });
        _world.AddComponent(e2, new TestVelocity { X = 1, Y = 2, Z = 0 });

        var schedule2 = SystemSchedule.Create(_world)
            .Add<TestMovementSystem>()
            .Add<TestGravitySystem>()
            .Build<ParallelWaveScheduler>();

        schedule2.Run();
        var parPos = _world.GetComponent<TestPosition>(e2);
        var parVel = _world.GetComponent<TestVelocity>(e2);

        await Assert.That(parPos.X).IsEqualTo(seqPos.X);
        await Assert.That(parPos.Y).IsEqualTo(seqPos.Y);
        await Assert.That(parVel.Y).IsEqualTo(seqVel.Y);
    }

    [Test]
    public async Task Schedule_EmptySchedule_DoesNotCrash()
    {
        var seqSchedule = SystemSchedule.Create(_world)
            .Build<SequentialWaveScheduler>();
        var parSchedule = SystemSchedule.Create(_world)
            .Build<ParallelWaveScheduler>();

        seqSchedule.Run();
        parSchedule.Run();

        // just verifying no exception thrown
        await Assert.That(_world.EntityCount).IsGreaterThanOrEqualTo(0);
    }

    // ---- Underscore Field Regression Test ----

    [Test]
    public async Task UnderscoreFieldSystem_Execute_UpdatesComponents()
    {
        var e = _world.Spawn();
        _world.AddComponent(e, new TestPosition { X = 10, Y = 20, Z = 0 });
        _world.AddComponent(e, new TestVelocity { X = 1, Y = 2, Z = 0 });

        var schedule = SystemSchedule.Create(_world)
            .Add<TestUnderscoreFieldSystem>()
            .Build<SequentialWaveScheduler>();

        schedule.Run();

        var pos = _world.GetComponent<TestPosition>(e);
        await Assert.That(pos.X).IsEqualTo(11f);
        await Assert.That(pos.Y).IsEqualTo(22f);
    }

    // ---- Subset Scheduling Tests ----

    [Test]
    public async Task Schedule_SubsetOnly_ComputesWavesForAddedSystems()
    {
        // Only add MovementSystem (no GravitySystem), verify it runs correctly in isolation
        var e = _world.Spawn();
        _world.AddComponent(e, new TestPosition { X = 10, Y = 20, Z = 0 });
        _world.AddComponent(e, new TestVelocity { X = 1, Y = 2, Z = 0 });

        var schedule = SystemSchedule.Create(_world)
            .Add<TestMovementSystem>()
            .Build<SequentialWaveScheduler>();

        schedule.Run();

        // Only movement ran, no gravity doubling
        var vel = _world.GetComponent<TestVelocity>(e);
        await Assert.That(vel.Y).IsEqualTo(2f);
        var pos = _world.GetComponent<TestPosition>(e);
        await Assert.That(pos.X).IsEqualTo(11f);
        await Assert.That(pos.Y).IsEqualTo(22f);
    }

    [Test]
    public async Task Schedule_SubsetWithDeps_IgnoresMissingDep()
    {
        // BoundsSystem has [After<MovementSystem>], but we only add BoundsSystem — dep is skipped
        var e = _world.Spawn();
        _world.AddComponent(e, new TestPosition { X = 200, Y = 0, Z = 0 });

        var schedule = SystemSchedule.Create(_world)
            .Add<TestBoundsSystem>()
            .Build<SequentialWaveScheduler>();

        schedule.Run();

        // BoundsSystem clamps to [0,100]
        var pos = _world.GetComponent<TestPosition>(e);
        await Assert.That(pos.X).IsEqualTo(100f);
    }

    [Test]
    public async Task Schedule_BuildWithCustomDagScheduler_Works()
    {
        var e = _world.Spawn();
        _world.AddComponent(e, new TestPosition { X = 0, Y = 0, Z = 0 });
        _world.AddComponent(e, new TestVelocity { X = 5, Y = 5, Z = 0 });

        var schedule = SystemSchedule.Create(_world)
            .Add<TestMovementSystem>()
            .Build(new DefaultDagScheduler(), new SequentialWaveScheduler());

        schedule.Run();

        var pos = _world.GetComponent<TestPosition>(e);
        await Assert.That(pos.X).IsEqualTo(5f);
        await Assert.That(pos.Y).IsEqualTo(5f);
    }

    // ---- SystemRegistry Tests ----

    [Test]
    public async Task SystemRegistry_Count_ReturnsCorrectValue()
    {
        await Assert.That(SystemRegistry.Count).IsGreaterThanOrEqualTo(5);
    }

    [Test]
    public async Task SystemRegistry_Metadata_HasCorrectSystemNames()
    {
        var metadata = SystemRegistry.Metadata;
        var names = new List<string>();
        for (int i = 0; i < metadata.Length; i++)
            names.Add(metadata[i].TypeName);

        await Assert.That(names).Contains("Paradise.ECS.Test.TestMovementSystem");
        await Assert.That(names).Contains("Paradise.ECS.Test.TestGravitySystem");
    }

    [Test]
    public async Task SystemRegistry_Metadata_HasAfterSystemIds()
    {
        // TestBoundsSystem has [After<TestMovementSystem>]
        var metadata = SystemRegistry.Metadata;
        var boundsAfterIds = System.Collections.Immutable.ImmutableArray<int>.Empty;
        var movementId = -1;
        var found = false;
        for (int i = 0; i < metadata.Length; i++)
        {
            if (metadata[i].TypeName == "Paradise.ECS.Test.TestBoundsSystem")
            {
                boundsAfterIds = metadata[i].AfterSystemIds;
                found = true;
            }
            if (metadata[i].TypeName == "Paradise.ECS.Test.TestMovementSystem")
                movementId = metadata[i].SystemId;
        }

        await Assert.That(found).IsTrue();
        await Assert.That(movementId).IsGreaterThanOrEqualTo(0);
        await Assert.That(boundsAfterIds).Contains(movementId);
    }
}
