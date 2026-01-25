using System.Diagnostics;

namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates component-based queries using QueryBuilder.
/// </summary>
public static class ComponentQuerySample
{
    public static Query<SmallBitSet<uint>, GameConfig, Archetype<SmallBitSet<uint>, GameConfig>> Run(World world)
    {
        Console.WriteLine("6. Component-based Query");
        Console.WriteLine("----------------------------");

        // Create a query for movable entities
        var movableQuery = QueryBuilder
            .Create()
            .With<Position>()
            .With<Velocity>()
            .Build(world.World.ArchetypeRegistry);

        Console.WriteLine($"  Movable query entity count: {movableQuery.EntityCount}");
        Debug.Assert(movableQuery.EntityCount == 5); // player + 4 enemies (1 despawned)

        foreach (var entityId in movableQuery)
        {
            var entity = world.World.GetEntity(entityId);
            var pos = world.GetComponent<Position>(entity);
            Console.WriteLine($"    Entity {entity.Id}: Position={pos}");
        }
        Console.WriteLine();

        return movableQuery;
    }
}
