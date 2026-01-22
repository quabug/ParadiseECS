using System.Diagnostics;

namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates component-based queries using QueryBuilder.
/// </summary>
public static class ComponentQuerySample
{
    public static WorldQuery<ImmutableBitSet<Bit64>, ComponentRegistry, GameConfig> Run(World world)
    {
        Console.WriteLine("6. Component-based Query");
        Console.WriteLine("----------------------------");

        // Create a query for movable entities
        var movableQuery = QueryBuilder
            .Create()
            .With<Position>()
            .With<Velocity>()
            .Build(world);

        Console.WriteLine($"  Movable query entity count: {movableQuery.EntityCount}");
        Debug.Assert(movableQuery.EntityCount == 5); // player + 4 enemies (1 despawned)

        foreach (var entity in movableQuery)
        {
            var pos = entity.Get<Position>();
            Console.WriteLine($"    Entity {entity.Entity.Id}: Position={pos}");
        }
        Console.WriteLine();

        return movableQuery;
    }
}
