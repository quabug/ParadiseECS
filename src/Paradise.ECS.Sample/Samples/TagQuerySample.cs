using System.Diagnostics;

namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates tag-based queries using TaggedQueryBuilder.
/// </summary>
public static class TagQuerySample
{
    public static void Run(World world, Entity playerEntity)
    {
        Console.WriteLine("7. Tag-based Query (TaggedQueryBuilder)");
        Console.WriteLine("----------------------------");

        // Query entities with IsActive tag (simple syntax via world.Query())
        var activeQuery = world.Query()
            .WithTag<IsActive>()
            .Build();

        Console.WriteLine($"  Active entities query:");
        int activeCount = 0;
        foreach (var entity in activeQuery)
        {
            Console.WriteLine($"    Entity {entity.Entity.Id} is active");
            activeCount++;
        }
        Console.WriteLine($"  Total active entities: {activeCount}");
        Debug.Assert(activeCount == 5); // player + 4 enemies (1 despawned)

        // Query entities with multiple tags
        world.AddTag<IsVisible>(playerEntity);
        var activeAndVisibleQuery = world.Query()
            .WithTag<IsActive>()
            .WithTag<IsVisible>()
            .Build();

        Console.WriteLine($"  Active AND visible entities:");
        int activeVisibleCount = 0;
        foreach (var entity in activeAndVisibleQuery)
        {
            Console.WriteLine($"    Entity {entity.Entity.Id} is active and visible");
            activeVisibleCount++;
        }
        Console.WriteLine($"  Total: {activeVisibleCount}");
        Debug.Assert(activeVisibleCount == 1); // only player

        // Query with both tags and components
        var activeMovableQuery = world.Query()
            .WithTag<IsActive>()
            .With<Position>()
            .With<Velocity>()
            .Build();

        Console.WriteLine($"  Active entities with Position and Velocity:");
        int activeMovableCount = 0;
        foreach (var entity in activeMovableQuery)
        {
            var pos = entity.Get<Position>();
            var vel = entity.Get<Velocity>();
            Console.WriteLine($"    Entity {entity.Entity.Id}: pos={pos}, vel={vel}");
            activeMovableCount++;
        }
        Console.WriteLine($"  Total: {activeMovableCount}");
        Debug.Assert(activeMovableCount == 5);
        Console.WriteLine();
    }
}
