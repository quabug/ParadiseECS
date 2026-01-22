namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates a simple game loop simulation with movement updates.
/// </summary>
public static class GameLoopSample
{
    public static void Run(World world, WorldQuery<ImmutableBitSet<Bit64>, ComponentRegistry, GameConfig> movableQuery)
    {
        Console.WriteLine("8. Game Loop Simulation (5 frames)");
        Console.WriteLine("----------------------------");

        for (int frame = 0; frame < 5; frame++)
        {
            Console.WriteLine($"  Frame {frame + 1}:");

            int movedEntities = 0;
            foreach (var entity in movableQuery)
            {
                ref var pos = ref entity.Get<Position>();
                var vel = entity.Get<Velocity>();
                pos = new Position(pos.X + vel.X, pos.Y + vel.Y);
                movedEntities++;

                if (world.HasTag<PlayerTag>(entity.Entity))
                {
                    Console.WriteLine($"    Player moved to {pos}");
                }
            }
            Console.WriteLine($"    Moved {movedEntities} entities total");
        }
        Console.WriteLine();
    }
}
