using System.Diagnostics;

namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates structural changes including adding and removing components from entities.
/// </summary>
public static class StructuralChangesSample
{
    public static void Run(World world, Entity emptyEntity)
    {
        Console.WriteLine("4. Structural Changes");
        Console.WriteLine("----------------------------");

        // Add component to empty entity
        world.AddComponent(emptyEntity, new Position(0, 0));
        Console.WriteLine($"  Added Position to empty entity");
        Console.WriteLine($"  Empty entity has Position: {world.HasComponent<Position>(emptyEntity)}");
        Debug.Assert(world.HasComponent<Position>(emptyEntity));

        // Remove component
        world.RemoveComponent<Position>(emptyEntity);
        Console.WriteLine($"  Removed Position from entity");
        Console.WriteLine($"  Entity has Position: {world.HasComponent<Position>(emptyEntity)}");
        Debug.Assert(!world.HasComponent<Position>(emptyEntity));
        Console.WriteLine();
    }
}
