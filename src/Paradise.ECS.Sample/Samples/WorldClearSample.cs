using System.Diagnostics;

namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates clearing all entities from the world.
/// </summary>
public static class WorldClearSample
{
    public static void Run(World world, Entity playerEntity)
    {
        Console.WriteLine("11. World Clear");
        Console.WriteLine("----------------------------");

        Console.WriteLine($"  Entity count before clear: {world.EntityCount}");
        world.Clear();
        Console.WriteLine($"  World cleared");
        Console.WriteLine($"  Entity count after clear: {world.EntityCount}");
        Console.WriteLine($"  Player is alive after clear: {world.IsAlive(playerEntity)}");
        Debug.Assert(world.EntityCount == 0);
        Debug.Assert(!world.IsAlive(playerEntity));
        Console.WriteLine();
    }
}
