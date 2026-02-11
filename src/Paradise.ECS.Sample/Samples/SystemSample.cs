namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates system scheduling with inline, queryable entity, and queryable chunk systems.
/// </summary>
public static class SystemSample
{
    public static void Run(World world)
    {
        Console.WriteLine("12. System Scheduling");
        Console.WriteLine("----------------------------");

        // Create some movable entities for the systems to process
        var e1 = world.Spawn();
        world.AddComponent(e1, new Position(10, 20));
        world.AddComponent(e1, new Velocity(1, 2));

        var e2 = world.Spawn();
        world.AddComponent(e2, new Position(100, 200));
        world.AddComponent(e2, new Velocity(-5, 3));

        // ---- Inline Entity Systems ----
        // DAG wave order: GravitySystem (writes Vel) → MovementSystem (reads Vel, writes Pos) → BoundsSystem (writes Pos)
        Console.WriteLine("  Inline entity systems (GravitySystem → MovementSystem → BoundsSystem):");

        var schedule = SystemSchedule.Create(world)
            .Add<MovementSystem>()
            .Add<GravitySystem>()
            .Add<BoundsSystem>()
            .Build<SequentialWaveScheduler>();

        schedule.Run();

        var pos1 = world.GetComponent<Position>(e1);
        var vel1 = world.GetComponent<Velocity>(e1);
        Console.WriteLine($"    Entity 1: Position={pos1}, Velocity={vel1}");

        var pos2 = world.GetComponent<Position>(e2);
        var vel2 = world.GetComponent<Velocity>(e2);
        Console.WriteLine($"    Entity 2: Position={pos2}, Velocity={vel2}");

        // ---- Queryable Entity System ----
        // QueryableMovementSystem uses [QueryAccess<Movable>] to access Position and Velocity
        // through the generated Movable.Data field instead of inline ref fields.
        Console.WriteLine("  Queryable entity system (QueryableMovementSystem):");

        var posBefore1 = world.GetComponent<Position>(e1);
        var posBefore2 = world.GetComponent<Position>(e2);

        var queryableSchedule = SystemSchedule.Create(world)
            .Add<QueryableMovementSystem>()
            .Build<SequentialWaveScheduler>();

        queryableSchedule.Run();

        var posAfter1 = world.GetComponent<Position>(e1);
        var posAfter2 = world.GetComponent<Position>(e2);
        Console.WriteLine($"    Entity 1: {posBefore1} → {posAfter1}");
        Console.WriteLine($"    Entity 2: {posBefore2} → {posAfter2}");

        // ---- Inline Chunk System ----
        Console.WriteLine("  Inline chunk system (GravityBatchSystem):");

        var batchSchedule = SystemSchedule.Create(world)
            .Add<GravityBatchSystem>()
            .Build<SequentialWaveScheduler>();

        var velBefore = world.GetComponent<Velocity>(e1);
        batchSchedule.Run();

        var vel1After = world.GetComponent<Velocity>(e1);
        Console.WriteLine($"    Entity 1 Velocity: {velBefore} → {vel1After}");

        // ---- Queryable Chunk System ----
        // QueryableGravityBatchSystem uses [QueryAccess<Movable>] to access Velocity spans
        // through the generated Movable.ChunkData field instead of inline Span<T> fields.
        Console.WriteLine("  Queryable chunk system (QueryableGravityBatchSystem):");

        var velBeforeQ = world.GetComponent<Velocity>(e1);

        var queryableChunkSchedule = SystemSchedule.Create(world)
            .Add<QueryableGravityBatchSystem>()
            .Build<SequentialWaveScheduler>();

        queryableChunkSchedule.Run();

        var velAfterQ = world.GetComponent<Velocity>(e1);
        Console.WriteLine($"    Entity 1 Velocity: {velBeforeQ} → {velAfterQ}");

        // ---- Health Systems (parallel waves) ----
        // HealthRegenSystem and HealthClampSystem only touch Health,
        // so they share waves with Velocity-only systems.
        Console.WriteLine("  Health systems (parallel with velocity systems):");

        world.AddComponent(e1, new Health(100));
        world.AddComponent(e2, new Health { Current = 50, Max = 80 });

        var healthSchedule = SystemSchedule.Create(world)
            .Add<HealthRegenSystem>()
            .Add<HealthClampSystem>()
            .Add<GravitySystem>()
            .Build<SequentialWaveScheduler>();

        var hBefore1 = world.GetComponent<Health>(e1);
        var hBefore2 = world.GetComponent<Health>(e2);
        healthSchedule.Run();
        var hAfter1 = world.GetComponent<Health>(e1);
        var hAfter2 = world.GetComponent<Health>(e2);
        Console.WriteLine($"    Entity 1 Health: {hBefore1} → {hAfter1}");
        Console.WriteLine($"    Entity 2 Health: {hBefore2} → {hAfter2}");

        // ---- AddAll and Parallel ----
        Console.WriteLine("  Parallel execution (all systems):");

        var allSchedule = SystemSchedule.Create(world)
            .AddAll()
            .Build<ParallelWaveScheduler>();

        allSchedule.Run();
        Console.WriteLine("    All systems executed in parallel successfully");

        // Cleanup test entities
        world.Despawn(e1);
        world.Despawn(e2);

        Console.WriteLine();
    }
}
