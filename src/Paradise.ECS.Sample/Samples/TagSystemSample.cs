using System.Diagnostics;

namespace Paradise.ECS.Sample.Samples;

/// <summary>
/// Demonstrates tag system operations including add, remove, and checking tag presence.
/// </summary>
public static class TagSystemSample
{
    public static void Run(World world, Entity playerEntity)
    {
        Console.WriteLine("3. Tag System Operations");
        Console.WriteLine("----------------------------");

        // Add/remove tags
        Console.WriteLine($"  Player has IsActive tag: {world.HasTag<IsActive>(playerEntity)}");
        Console.WriteLine($"  Player has IsVisible tag: {world.HasTag<IsVisible>(playerEntity)}");
        Debug.Assert(world.HasTag<IsActive>(playerEntity));
        Debug.Assert(!world.HasTag<IsVisible>(playerEntity));

        world.AddTag<IsVisible>(playerEntity);
        Console.WriteLine($"  Added IsVisible tag to player");
        Console.WriteLine($"  Player has IsVisible tag: {world.HasTag<IsVisible>(playerEntity)}");
        Debug.Assert(world.HasTag<IsVisible>(playerEntity));

        world.RemoveTag<IsVisible>(playerEntity);
        Console.WriteLine($"  Removed IsVisible tag from player");
        Console.WriteLine($"  Player has IsVisible tag: {world.HasTag<IsVisible>(playerEntity)}");
        Debug.Assert(!world.HasTag<IsVisible>(playerEntity));

        // Get and set tag masks
        var playerTags = world.GetTags(playerEntity);
        Console.WriteLine($"  Player tag mask: IsActive={playerTags.Get(IsActive.TagId)}, IsVisible={playerTags.Get(IsVisible.TagId)}");
        Console.WriteLine();
    }
}
