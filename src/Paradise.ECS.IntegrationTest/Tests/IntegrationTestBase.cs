namespace Paradise.ECS.IntegrationTest.Tests;

/// <summary>
/// Base class for integration tests providing shared world setup and teardown.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly World World = new();

    public virtual void Dispose()
    {
        World.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a player entity with standard components.
    /// </summary>
    protected Entity CreatePlayer(float x = 100, float y = 200, int health = 100, string name = "Hero")
    {
        return EntityBuilder.Create()
            .Add(new Position(x, y))
            .Add(new Velocity(5, 0))
            .Add(new Health(health))
            .Add(new Name(name))
            .AddTag(default(PlayerTag), World)
            .Build(World);
    }

    /// <summary>
    /// Creates an enemy entity with standard components.
    /// </summary>
    protected Entity CreateEnemy(float x = 0, float y = 300, int health = 50)
    {
        return EntityBuilder.Create()
            .Add(new Position(x, y))
            .Add(new Velocity(-2, 0))
            .Add(new Health(health))
            .AddTag(default(EnemyTag), World)
            .Build(World);
    }
}
