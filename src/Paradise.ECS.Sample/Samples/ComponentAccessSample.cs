using System.Diagnostics;

namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates component read/write operations and component existence checks.
/// </summary>
public static class ComponentAccessSample
{
    public static void Run(World world, Entity playerEntity)
    {
        Console.WriteLine("2. Component Access");
        Console.WriteLine("----------------------------");

        // Read components
        var playerPos = world.GetComponent<Position>(playerEntity);
        var playerHealth = world.GetComponent<Health>(playerEntity);
        var playerName = world.GetComponent<Name>(playerEntity);
        Console.WriteLine($"  Player position: {playerPos}");
        Console.WriteLine($"  Player health: {playerHealth}");
        Console.WriteLine($"  Player name: {playerName}");
        Debug.Assert(playerPos.X == 100 && playerPos.Y == 200);
        Debug.Assert(playerHealth.Current == 100 && playerHealth.Max == 100);
        Debug.Assert(playerName.ToString() == "Hero");

        // Modify component
        world.SetComponent(playerEntity, new Position(150, 250));
        playerPos = world.GetComponent<Position>(playerEntity);
        Console.WriteLine($"  Updated player position: {playerPos}");
        Debug.Assert(playerPos.X == 150 && playerPos.Y == 250);

        // Check for components
        Console.WriteLine($"  Player has Position: {world.HasComponent<Position>(playerEntity)}");
        Console.WriteLine($"  Player has PlayerTag: {world.HasTag<PlayerTag>(playerEntity)}");
        Console.WriteLine($"  Player has EnemyTag: {world.HasTag<EnemyTag>(playerEntity)}");
        Debug.Assert(world.HasComponent<Position>(playerEntity));
        Debug.Assert(world.HasTag<PlayerTag>(playerEntity));
        Debug.Assert(!world.HasTag<EnemyTag>(playerEntity));
        Console.WriteLine();
    }
}
