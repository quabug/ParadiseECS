namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates and verifies the generated types from source generators.
/// </summary>
public static class GeneratedTypesSample
{
    public static void Run()
    {
        Console.WriteLine("10. Verifying Generated Types");
        Console.WriteLine("----------------------------");

        // Verify the ComponentMask alias works
        Console.WriteLine($"  ComponentMask type: {typeof(ComponentMask).FullName}");

        // Verify the World alias works (TaggedWorld when Paradise.ECS.Tag is referenced)
        Console.WriteLine($"  World type: {typeof(World).FullName}");

        // Verify component type IDs are assigned
        Console.WriteLine($"  Position.TypeId: {Position.TypeId}");
        Console.WriteLine($"  Velocity.TypeId: {Velocity.TypeId}");
        Console.WriteLine($"  Health.TypeId: {Health.TypeId}");

        // Verify tag IDs are assigned
        Console.WriteLine($"  IsActive.TagId: {IsActive.TagId}");
        Console.WriteLine($"  IsVisible.TagId: {IsVisible.TagId}");
        Console.WriteLine($"  IsDamageable.TagId: {IsDamageable.TagId}");
        Console.WriteLine($"  PlayerTag.TagId: {PlayerTag.TagId}");
        Console.WriteLine($"  EnemyTag.TagId: {EnemyTag.TagId}");

        // Verify component GUIDs
        Console.WriteLine($"  Position.Guid: {Position.Guid}");

        // Verify component sizes
        Console.WriteLine($"  Position.Size: {Position.Size} bytes");
        Console.WriteLine($"  Health.Size: {Health.Size} bytes");

        // Verify ComponentRegistry
        Console.WriteLine($"  ComponentRegistry.TypeInfos.Length: {ComponentRegistry.TypeInfos.Length}");
        Console.WriteLine();
    }
}
