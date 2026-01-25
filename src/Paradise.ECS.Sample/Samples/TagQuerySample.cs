using System.Diagnostics;

namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates tag-based queries using QueryBuilder with manual tag filtering.
/// </summary>
public static class TagQuerySample
{
    public static void Run(World world, Entity playerEntity)
    {
        Console.WriteLine("7. Tag-based Query (Manual Tag Filtering)");
        Console.WriteLine("----------------------------");

        // Query all entities with EntityTags component (all spawned entities)
        var allEntitiesQuery = QueryBuilder
            .Create()
            .With<EntityTags>()
            .Build(world.ArchetypeRegistry);

        // Define required tag masks
        var activeTagMask = TagMask.Empty.Set(IsActive.TagId);
        var visibleTagMask = TagMask.Empty.Set(IsVisible.TagId);
        var activeAndVisibleMask = activeTagMask.Or(visibleTagMask);

        Console.WriteLine($"  Active entities query:");
        int activeCount = 0;
        foreach (var entityId in allEntitiesQuery)
        {
            var entity = world.World.GetEntity(entityId);
            var tags = world.GetTags(entity);
            if (tags.ContainsAll(activeTagMask))
            {
                Console.WriteLine($"    Entity {entity.Id} is active");
                activeCount++;
            }
        }
        Console.WriteLine($"  Total active entities: {activeCount}");
        Debug.Assert(activeCount == 5); // player + 4 enemies (1 despawned)

        // Query entities with multiple tags
        world.AddTag<IsVisible>(playerEntity);

        Console.WriteLine($"  Active AND visible entities:");
        int activeVisibleCount = 0;
        foreach (var entityId in allEntitiesQuery)
        {
            var entity = world.World.GetEntity(entityId);
            var tags = world.GetTags(entity);
            if (tags.ContainsAll(activeAndVisibleMask))
            {
                Console.WriteLine($"    Entity {entity.Id} is active and visible");
                activeVisibleCount++;
            }
        }
        Console.WriteLine($"  Total: {activeVisibleCount}");
        Debug.Assert(activeVisibleCount == 1); // only player

        // Query with both tags and components
        var movableQuery = QueryBuilder
            .Create()
            .With<Position>()
            .With<Velocity>()
            .Build(world.ArchetypeRegistry);

        Console.WriteLine($"  Active entities with Position and Velocity:");
        int activeMovableCount = 0;
        foreach (var entityId in movableQuery)
        {
            var entity = world.World.GetEntity(entityId);
            var tags = world.GetTags(entity);
            if (tags.ContainsAll(activeTagMask))
            {
                var pos = world.GetComponent<Position>(entity);
                var vel = world.GetComponent<Velocity>(entity);
                Console.WriteLine($"    Entity {entity.Id}: pos={pos}, vel={vel}");
                activeMovableCount++;
            }
        }
        Console.WriteLine($"  Total: {activeMovableCount}");
        Debug.Assert(activeMovableCount == 5);
        Console.WriteLine();
    }
}
