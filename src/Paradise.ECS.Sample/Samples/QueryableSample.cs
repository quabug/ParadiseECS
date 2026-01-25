using System.Diagnostics;

namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates using generated [Queryable] types for type-safe entity iteration.
/// Queryables provide compile-time verified component access via generated Data structs.
/// </summary>
public static class QueryableSample
{
    public static void Run(World world)
    {
        Console.WriteLine("11. Queryable Types");
        Console.WriteLine("----------------------------");

        // =========================================================================
        // Entity-level iteration using generated Movable queryable
        // Movable is defined as: [With<Position>] [With<Velocity>]
        // =========================================================================
        Console.WriteLine("  Entity-level iteration (Movable.Query):");

        var movableQuery = Movable.Query(world.World);
        Console.WriteLine($"    Movable entity count: {movableQuery.EntityCount}");
        // Note: Player was overwritten in EntityOverwriteSample and lost Velocity,
        // so only 4 enemies match (1 enemy was despawned in EntityLifecycleSample)
        Debug.Assert(movableQuery.EntityCount == 4);

        foreach (var data in movableQuery)
        {
            // Access components via generated properties - no runtime checks needed
            ref var pos = ref data.Position;
            ref readonly var vel = ref data.Velocity;
            Console.WriteLine($"      Position: {pos}, Velocity: {vel}");
        }
        Console.WriteLine();

        // =========================================================================
        // Optional component access using Damageable queryable
        // Damageable is defined as: [With<Health>] [Optional<Position>(IsReadOnly = true)]
        // =========================================================================
        Console.WriteLine("  Optional component access (Damageable.Query):");

        var damageableQuery = Damageable.Query(world.World);
        Console.WriteLine($"    Damageable entity count: {damageableQuery.EntityCount}");

        foreach (var data in damageableQuery)
        {
            ref readonly var health = ref data.Health;

            // Check if optional Position component exists before accessing
            if (data.HasPosition)
            {
                ref readonly var pos = ref data.GetPosition();
                Console.WriteLine($"      Health: {health}, Position: {pos}");
            }
            else
            {
                Console.WriteLine($"      Health: {health}, Position: (none)");
            }
        }
        Console.WriteLine();

        // =========================================================================
        // Chunk-level iteration for batch processing using Movable.ChunkQuery
        // Provides Span<T> access for cache-efficient bulk operations
        // =========================================================================
        Console.WriteLine("  Chunk-level iteration (Movable.ChunkQuery):");

        var chunkQuery = Movable.ChunkQuery(world.World);
        Console.WriteLine($"    Total entities across chunks: {chunkQuery.EntityCount}");

        int chunkIndex = 0;
        foreach (var chunk in chunkQuery)
        {
            Console.WriteLine($"    Chunk {chunkIndex}: {chunk.EntityCount} entities");

            // Access component spans for vectorizable operations
            var positions = chunk.Positions;
            var velocities = chunk.Velocitys;  // Note: auto-pluralized property name

            // Example: batch update positions using velocity
            for (int i = 0; i < chunk.EntityCount; i++)
            {
                Console.WriteLine($"      [{i}] Position: {positions[i]}, Velocity: {velocities[i]}");
            }

            chunkIndex++;
        }
        Console.WriteLine();
    }
}
