using Microsoft.Coyote.Specifications;
using Microsoft.Coyote.SystematicTesting;
using Paradise.ECS.CoyoteTest;

namespace Paradise.ECS.Concurrent.ConcurrentTest;

// ============================================================================
// Test Components
// ============================================================================

[Component("2E9B1C4B-F50F-4D76-A16F-478E3098F4AA", Id = 0)]
public partial struct CoyotePosition
{
    public float X, Y, Z;
}

[Component("EF293D66-2FE4-4078-9523-9798121EC2CE", Id = 1)]
public partial struct CoyoteVelocity
{
    public float X, Y, Z;
}

[Component("2056C049-F782-4369-B172-669BB00513B8", Id = 2)]
public partial struct CoyoteHealth
{
    public int Current;
}

// ============================================================================
// Test Systems
// ============================================================================

/// <summary>
/// Writes Position only. Adds 1 to all axes.
/// </summary>
public ref partial struct IncrementPositionSystem : IEntitySystem
{
    public ref CoyotePosition Position;

    public void Execute()
    {
        Position = new CoyotePosition { X = Position.X + 1, Y = Position.Y + 1, Z = Position.Z + 1 };
    }
}

/// <summary>
/// Writes Velocity only. Doubles Y.
/// No component overlap with IncrementPositionSystem → same wave.
/// </summary>
public ref partial struct ScaleVelocitySystem : IEntitySystem
{
    public ref CoyoteVelocity Velocity;

    public void Execute()
    {
        Velocity = new CoyoteVelocity { X = Velocity.X, Y = Velocity.Y * 2, Z = Velocity.Z };
    }
}

/// <summary>
/// Reads Velocity, writes Position. Conflicts with both systems above → later wave.
/// </summary>
[After<IncrementPositionSystem>]
[After<ScaleVelocitySystem>]
public ref partial struct ApplyVelocitySystem : IEntitySystem
{
    public ref CoyotePosition Position;
    public ref readonly CoyoteVelocity Velocity;

    public void Execute()
    {
        Position = new CoyotePosition
        {
            X = Position.X + Velocity.X,
            Y = Position.Y + Velocity.Y,
            Z = Position.Z + Velocity.Z
        };
    }
}

/// <summary>
/// Writes Health only. Independent of position/velocity → same wave as IncrementPosition and ScaleVelocity.
/// </summary>
public ref partial struct HealSystem : IEntitySystem
{
    public ref CoyoteHealth Health;

    public void Execute()
    {
        Health = new CoyoteHealth { Current = Health.Current + 10 };
    }
}

// ============================================================================
// Coyote Tests
// ============================================================================

/// <summary>
/// Concurrent tests for parallel system execution using Coyote systematic testing.
/// Verifies that <see cref="SystemSchedule{TMask,TConfig}.Run"/> with <see cref="ParallelWaveScheduler"/>
/// produces correct results under all thread interleavings — especially the flattened chunk-level parallelism.
/// </summary>
public static class ParallelSystemTests
{
    /// <summary>
    /// Creates a world populated with entities that have Position + Velocity + Health.
    /// Returns (sharedWorld, world, entityCount) for the test to use.
    /// </summary>
    private static (SharedWorld shared, World world) SetupWorld(int entityCount)
    {
        var shared = SharedWorldFactory.Create();
        var world = shared.CreateWorld();

        for (int i = 0; i < entityCount; i++)
        {
            var e = world.Spawn();
            world.AddComponent(e, new CoyotePosition { X = i, Y = i * 10, Z = 0 });
            world.AddComponent(e, new CoyoteVelocity { X = 1, Y = 2, Z = 3 });
            world.AddComponent(e, new CoyoteHealth { Current = 100 });
        }

        return (shared, world);
    }

    /// <summary>
    /// Runs a schedule sequentially and collects all component values for comparison.
    /// </summary>
    private static (float[] posX, float[] velY, int[] health) CollectResults(World world, int entityCount)
    {
        var posX = new float[entityCount];
        var velY = new float[entityCount];
        var health = new int[entityCount];

        for (int i = 0; i < entityCount; i++)
        {
            var entity = new Entity(i, 1);
            posX[i] = world.GetComponent<CoyotePosition>(entity).X;
            velY[i] = world.GetComponent<CoyoteVelocity>(entity).Y;
            health[i] = world.GetComponent<CoyoteHealth>(entity).Current;
        }

        return (posX, velY, health);
    }

    /// <summary>
    /// RunParallel with independent systems (no component overlap) must produce
    /// the same results as RunSequential.
    /// </summary>
    [Test]
    public static void ParallelMatchesSequential_IndependentSystems()
    {
        const int entityCount = 50;

        // Sequential baseline
        var (seqShared, seqWorld) = SetupWorld(entityCount);
        var seqSchedule = SystemSchedule.Create(seqWorld)
            .Add<IncrementPositionSystem>()
            .Add<ScaleVelocitySystem>()
            .Add<HealSystem>()
            .Build<SequentialWaveScheduler>();
        seqSchedule.Run();
        var (seqPosX, seqVelY, seqHealth) = CollectResults(seqWorld, entityCount);
        seqShared.Dispose();

        // Parallel run
        var (parShared, parWorld) = SetupWorld(entityCount);
        var parSchedule = SystemSchedule.Create(parWorld)
            .Add<IncrementPositionSystem>()
            .Add<ScaleVelocitySystem>()
            .Add<HealSystem>()
            .Build<ParallelWaveScheduler>();
        parSchedule.Run();
        var (parPosX, parVelY, parHealth) = CollectResults(parWorld, entityCount);
        parShared.Dispose();

        // Compare
        for (int i = 0; i < entityCount; i++)
        {
            Specification.Assert(parPosX[i] == seqPosX[i],
                $"Position.X mismatch at entity {i}: expected {seqPosX[i]}, got {parPosX[i]}");
            Specification.Assert(parVelY[i] == seqVelY[i],
                $"Velocity.Y mismatch at entity {i}: expected {seqVelY[i]}, got {parVelY[i]}");
            Specification.Assert(parHealth[i] == seqHealth[i],
                $"Health mismatch at entity {i}: expected {seqHealth[i]}, got {parHealth[i]}");
        }
    }

    /// <summary>
    /// Parallel with dependent systems (multi-wave) must produce the same results as sequential.
    /// IncrementPosition + ScaleVelocity run in wave 0, then ApplyVelocity in wave 1.
    /// </summary>
    [Test]
    public static void ParallelMatchesSequential_MultiWave()
    {
        const int entityCount = 50;

        // Sequential baseline
        var (seqShared, seqWorld) = SetupWorld(entityCount);
        var seqSchedule = SystemSchedule.Create(seqWorld)
            .Add<IncrementPositionSystem>()
            .Add<ScaleVelocitySystem>()
            .Add<ApplyVelocitySystem>()
            .Build<SequentialWaveScheduler>();
        seqSchedule.Run();
        var (seqPosX, seqVelY, _) = CollectResults(seqWorld, entityCount);
        seqShared.Dispose();

        // Parallel run
        var (parShared, parWorld) = SetupWorld(entityCount);
        var parSchedule = SystemSchedule.Create(parWorld)
            .Add<IncrementPositionSystem>()
            .Add<ScaleVelocitySystem>()
            .Add<ApplyVelocitySystem>()
            .Build<ParallelWaveScheduler>();
        parSchedule.Run();
        var (parPosX, parVelY, _) = CollectResults(parWorld, entityCount);
        parShared.Dispose();

        for (int i = 0; i < entityCount; i++)
        {
            Specification.Assert(parPosX[i] == seqPosX[i],
                $"Position.X mismatch at entity {i}: expected {seqPosX[i]}, got {parPosX[i]}");
            Specification.Assert(parVelY[i] == seqVelY[i],
                $"Velocity.Y mismatch at entity {i}: expected {seqVelY[i]}, got {parVelY[i]}");
        }
    }

    /// <summary>
    /// Multiple RunParallel iterations accumulate correctly.
    /// </summary>
    [Test]
    public static void ParallelMultipleIterations_AccumulatesCorrectly()
    {
        const int entityCount = 30;
        const int iterations = 5;

        // Sequential baseline
        var (seqShared, seqWorld) = SetupWorld(entityCount);
        var seqSchedule = SystemSchedule.Create(seqWorld)
            .Add<IncrementPositionSystem>()
            .Add<HealSystem>()
            .Build<SequentialWaveScheduler>();
        for (int iter = 0; iter < iterations; iter++)
            seqSchedule.Run();
        var (seqPosX, _, seqHealth) = CollectResults(seqWorld, entityCount);
        seqShared.Dispose();

        // Parallel run
        var (parShared, parWorld) = SetupWorld(entityCount);
        var parSchedule = SystemSchedule.Create(parWorld)
            .Add<IncrementPositionSystem>()
            .Add<HealSystem>()
            .Build<ParallelWaveScheduler>();
        for (int iter = 0; iter < iterations; iter++)
            parSchedule.Run();
        var (parPosX, _, parHealth) = CollectResults(parWorld, entityCount);
        parShared.Dispose();

        for (int i = 0; i < entityCount; i++)
        {
            Specification.Assert(parPosX[i] == seqPosX[i],
                $"Position.X mismatch at entity {i} after {iterations} iters: expected {seqPosX[i]}, got {parPosX[i]}");
            Specification.Assert(parHealth[i] == seqHealth[i],
                $"Health mismatch at entity {i} after {iterations} iters: expected {seqHealth[i]}, got {parHealth[i]}");
        }
    }

    /// <summary>
    /// All systems together via AddAll, parallel vs sequential.
    /// </summary>
    [Test]
    public static void ParallelMatchesSequential_AllSystems()
    {
        const int entityCount = 50;

        // Sequential baseline
        var (seqShared, seqWorld) = SetupWorld(entityCount);
        var seqSchedule = SystemSchedule.Create(seqWorld)
            .AddAll()
            .Build<SequentialWaveScheduler>();
        seqSchedule.Run();
        var (seqPosX, seqVelY, seqHealth) = CollectResults(seqWorld, entityCount);
        seqShared.Dispose();

        // Parallel run
        var (parShared, parWorld) = SetupWorld(entityCount);
        var parSchedule = SystemSchedule.Create(parWorld)
            .AddAll()
            .Build<ParallelWaveScheduler>();
        parSchedule.Run();
        var (parPosX, parVelY, parHealth) = CollectResults(parWorld, entityCount);
        parShared.Dispose();

        for (int i = 0; i < entityCount; i++)
        {
            Specification.Assert(parPosX[i] == seqPosX[i],
                $"Position.X mismatch at entity {i}: expected {seqPosX[i]}, got {parPosX[i]}");
            Specification.Assert(parVelY[i] == seqVelY[i],
                $"Velocity.Y mismatch at entity {i}: expected {seqVelY[i]}, got {parVelY[i]}");
            Specification.Assert(parHealth[i] == seqHealth[i],
                $"Health mismatch at entity {i}: expected {seqHealth[i]}, got {parHealth[i]}");
        }
    }

    /// <summary>
    /// Stress test: many entities across many chunks, all run in parallel.
    /// </summary>
    [Test]
    public static void ParallelStress_ManyEntities()
    {
        const int entityCount = 200;

        var (shared, world) = SetupWorld(entityCount);
        var schedule = SystemSchedule.Create(world)
            .Add<IncrementPositionSystem>()
            .Add<ScaleVelocitySystem>()
            .Add<ApplyVelocitySystem>()
            .Add<HealSystem>()
            .Build<ParallelWaveScheduler>();

        // Run multiple iterations
        for (int iter = 0; iter < 3; iter++)
            schedule.Run();

        // Verify no corruption: all entities should have consistent state
        for (int i = 0; i < entityCount; i++)
        {
            var entity = new Entity(i, 1);
            var pos = world.GetComponent<CoyotePosition>(entity);
            var health = world.GetComponent<CoyoteHealth>(entity);

            // Health should have increased by 10 per iteration
            Specification.Assert(health.Current == 100 + 30,
                $"Entity {i} health corrupted: expected 130, got {health.Current}");

            // Position values should be finite (not NaN or Inf from corrupted reads)
            Specification.Assert(!float.IsNaN(pos.X) && !float.IsInfinity(pos.X),
                $"Entity {i} position corrupted: X={pos.X}");
        }

        shared.Dispose();
    }
}
