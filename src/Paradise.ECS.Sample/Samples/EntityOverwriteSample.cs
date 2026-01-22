using System.Diagnostics;

namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates overwriting entity components using EntityBuilder.Overwrite().
/// </summary>
public static class EntityOverwriteSample
{
    public static void Run(World world, Entity playerEntity)
    {
        Console.WriteLine("9. Entity Overwrite");
        Console.WriteLine("----------------------------");

        Console.WriteLine($"  Player before overwrite - Position: {world.GetComponent<Position>(playerEntity)}");
        Console.WriteLine($"  Player has Velocity: {world.HasComponent<Velocity>(playerEntity)}");
        Debug.Assert(world.HasComponent<Velocity>(playerEntity));

        EntityBuilder.Create()
            .Add(new Position(0, 0))
            .Add(new Health(200))
            .AddTag(default(PlayerTag), world)
            .Overwrite(playerEntity, world);

        Console.WriteLine($"  Player after overwrite - Position: {world.GetComponent<Position>(playerEntity)}");
        Console.WriteLine($"  Player has Velocity: {world.HasComponent<Velocity>(playerEntity)}");
        Console.WriteLine($"  Player health: {world.GetComponent<Health>(playerEntity)}");
        var overwrittenPos = world.GetComponent<Position>(playerEntity);
        var overwrittenHealth = world.GetComponent<Health>(playerEntity);
        Debug.Assert(overwrittenPos.X == 0 && overwrittenPos.Y == 0);
        Debug.Assert(!world.HasComponent<Velocity>(playerEntity));
        Debug.Assert(overwrittenHealth.Current == 200 && overwrittenHealth.Max == 200);
        Console.WriteLine();
    }
}
