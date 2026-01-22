using System.Diagnostics;

namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates entity creation patterns including empty entities, entities with components, and bulk creation.
/// </summary>
public static class EntityCreationSample
{
    public static (Entity emptyEntity, Entity playerEntity, Entity[] enemies) Run(World world)
    {
        Console.WriteLine("1. Entity Creation");
        Console.WriteLine("----------------------------");

        // Spawn empty entity
        var emptyEntity = world.Spawn();
        Console.WriteLine($"  Created empty entity: {emptyEntity}");
        Console.WriteLine($"  Entity count: {world.EntityCount}");
        Debug.Assert(world.EntityCount == 1);
        Debug.Assert(world.IsAlive(emptyEntity));

        // Create player entity with components and tags using builder
        var playerEntity = EntityBuilder.Create()
            .Add(new Position(100, 200))
            .Add(new Velocity(5, 0))
            .Add(new Health(100))
            .Add(new Name("Hero"))
            .AddTag(default(PlayerTag), world)
            .AddTag(default(IsActive), world)
            .Build(world);
        Console.WriteLine($"  Created player entity: {playerEntity}");
        Console.WriteLine($"  Entity count: {world.EntityCount}");
        Debug.Assert(world.EntityCount == 2);
        Debug.Assert(world.IsAlive(playerEntity));

        // Create multiple enemy entities
        var enemies = new Entity[5];
        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i] = EntityBuilder.Create()
                .Add(new Position(i * 50, 300))
                .Add(new Velocity(-2, 0))
                .Add(new Health(50))
                .AddTag(default(EnemyTag), world)
                .AddTag(default(IsActive), world)
                .Build(world);
        }
        Console.WriteLine($"  Created {enemies.Length} enemy entities");
        Console.WriteLine($"  Entity count: {world.EntityCount}");
        Debug.Assert(world.EntityCount == 7); // 1 empty + 1 player + 5 enemies
        Console.WriteLine();

        return (emptyEntity, playerEntity, enemies);
    }
}
