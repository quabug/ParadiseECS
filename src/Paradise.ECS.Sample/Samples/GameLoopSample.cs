namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates a simple game loop simulation with movement updates.
/// </summary>
public static class GameLoopSample
{
    public static void Run(World world, Query<SmallBitSet<uint>, GameConfig, Archetype<SmallBitSet<uint>, GameConfig>> movableQuery)
    {
        Console.WriteLine("8. Game Loop Simulation (5 frames)");
        Console.WriteLine("----------------------------");

        for (int frame = 0; frame < 5; frame++)
        {
            Console.WriteLine($"  Frame {frame + 1}:");

            int movedEntities = 0;
            foreach (var entityId in movableQuery)
            {
                var entity = world.World.GetEntity(entityId);
                ref var pos = ref world.GetComponent<Position>(entity);
                var vel = world.GetComponent<Velocity>(entity);
                pos = new Position(pos.X + vel.X, pos.Y + vel.Y);
                movedEntities++;

                if (world.HasTag<PlayerTag>(entity))
                {
                    Console.WriteLine($"    Player moved to {pos}");
                }
            }
            Console.WriteLine($"    Moved {movedEntities} entities total");
        }
        Console.WriteLine();
    }
}
