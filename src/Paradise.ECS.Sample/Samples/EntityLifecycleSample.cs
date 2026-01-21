using System.Diagnostics;

namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates entity lifecycle including despawning and checking alive status.
/// </summary>
public static class EntityLifecycleSample
{
    public static void Run(World world, Entity[] enemies)
    {
        Console.WriteLine("5. Entity Lifecycle");
        Console.WriteLine("----------------------------");

        Console.WriteLine($"  Entity count before despawn: {world.EntityCount}");
        Console.WriteLine($"  Enemy[0] is alive: {world.IsAlive(enemies[0])}");
        Debug.Assert(world.EntityCount == 7);
        Debug.Assert(world.IsAlive(enemies[0]));

        // Despawn an enemy
        world.Despawn(enemies[0]);
        Console.WriteLine($"  Despawned enemy[0]");
        Console.WriteLine($"  Enemy[0] is alive: {world.IsAlive(enemies[0])}");
        Console.WriteLine($"  Entity count after despawn: {world.EntityCount}");
        Debug.Assert(!world.IsAlive(enemies[0]));
        Debug.Assert(world.EntityCount == 6);

        // Try to despawn again (should return false)
        var despawnedAgain = world.Despawn(enemies[0]);
        Console.WriteLine($"  Despawn enemy[0] again: {despawnedAgain}");
        Debug.Assert(!despawnedAgain);
        Console.WriteLine();
    }
}
