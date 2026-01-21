namespace Paradise.ECS.IntegrationTest.Tests;

/// <summary>
/// Integration tests for generated queryable types (Player, Enemy, Movable, etc.).
/// </summary>
public sealed class QueryableIterationIntegrationTests : IntegrationTestBase
{
    private Entity _playerWithVel;
    private Entity _playerNoVel;
    private Entity _namedPlayer;
    private Entity _movingEnemy;
    private Entity _stationaryEnemy;
    private Entity _healthOnly;

    [Before(Test)]
    public void SetupEntities()
    {
        // Player with velocity
        _playerWithVel = EntityBuilder.Create()
            .Add(new Position(10, 20))
            .Add(new Velocity(1, 2))
            .Add(new Health(100))
            .AddTag(default(PlayerTag), World)
            .Build(World);

        // Player without velocity
        _playerNoVel = EntityBuilder.Create()
            .Add(new Position(30, 40))
            .Add(new Health(80))
            .AddTag(default(PlayerTag), World)
            .Build(World);

        // Named player (excluded from Player query due to Without<Name>)
        _namedPlayer = EntityBuilder.Create()
            .Add(new Position(50, 60))
            .Add(new Health(90))
            .AddTag(default(PlayerTag), World)
            .Add(new Name("Hero"))
            .Build(World);

        // Moving enemy with velocity
        _movingEnemy = EntityBuilder.Create()
            .Add(new Position(100, 110))
            .Add(new Velocity(3, 4))
            .Add(new Health(50))
            .AddTag(default(EnemyTag), World)
            .Build(World);

        // Stationary enemy without velocity
        _stationaryEnemy = EntityBuilder.Create()
            .Add(new Position(120, 130))
            .Add(new Health(40))
            .AddTag(default(EnemyTag), World)
            .Build(World);

        // Health-only entity
        _healthOnly = EntityBuilder.Create()
            .Add(new Health(25))
            .Build(World);
    }

    [Test]
    public async Task PlayerQuery_ExcludesNamedPlayer()
    {
        var query = Player.Query.Build(World.World);

        // Excludes named player due to Without<Name>
        await Assert.That(query.EntityCount).IsEqualTo(4);
    }

    [Test]
    public async Task PlayerQuery_HasOptionalVelocity()
    {
        var query = Player.Query.Build(World.World);

        int withVelocity = 0;
        int withoutVelocity = 0;
        foreach (var p in query)
        {
            if (p.HasVelocity)
                withVelocity++;
            else
                withoutVelocity++;
        }

        await Assert.That(withVelocity).IsEqualTo(2);
        await Assert.That(withoutVelocity).IsEqualTo(2);
    }

    [Test]
    public async Task PlayerQuery_CanModifyPosition()
    {
        var query = Player.Query.Build(World.World);

        foreach (var p in query)
        {
            p.Position = new Position(p.Position.X + 100, p.Position.Y + 100);
        }

        var pos = World.GetComponent<Position>(_playerWithVel);
        await Assert.That(pos.X).IsEqualTo(110f);
        await Assert.That(pos.Y).IsEqualTo(120f);
    }

    [Test]
    public async Task EnemyQuery_WithAnyVelocity_MatchesOnlyEntitiesWithVelocity()
    {
        var query = Enemy.Query.Build(World.World);

        // Only entities WITH Velocity match due to Any<Velocity>
        await Assert.That(query.EntityCount).IsEqualTo(2);
    }

    [Test]
    public async Task MovableQuery_MatchesEntitiesWithBothComponents()
    {
        var query = Movable.Query.Build(World.World);

        // Only entities with BOTH Position AND Velocity
        await Assert.That(query.EntityCount).IsEqualTo(2);
    }

    [Test]
    public async Task DamageableQuery_MatchesAllEntitiesWithHealth()
    {
        var query = Damageable.Query.Build(World.World);

        // All 6 entities have Health
        await Assert.That(query.EntityCount).IsEqualTo(6);
    }

    [Test]
    public async Task DamageableQuery_OptionalPositionIsReadOnly()
    {
        var query = Damageable.Query.Build(World.World);

        int withPosition = 0;
        int withoutPosition = 0;
        foreach (var d in query)
        {
            if (d.HasPosition)
                withPosition++;
            else
                withoutPosition++;
        }

        await Assert.That(withPosition).IsEqualTo(5);
        await Assert.That(withoutPosition).IsEqualTo(1);
    }

    [Test]
    public async Task NamedEntityQuery_MatchesOnlyNamedEntity()
    {
        var query = NamedEntity.Query.Build(World.World);

        await Assert.That(query.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task PositionedQuery_MatchesAllWithPosition()
    {
        var query = Positioned.Query.Build(World.World);

        // All except healthOnly
        await Assert.That(query.EntityCount).IsEqualTo(5);
    }

    [Test]
    public async Task MovementSystem_UpdatesPositions()
    {
        for (int frame = 0; frame < 3; frame++)
        {
            var query = Movable.Query.Build(World.World);
            foreach (var entity in query)
            {
                entity.Position = new Position(
                    entity.Position.X + entity.Velocity.X,
                    entity.Position.Y + entity.Velocity.Y);
            }
        }

        // After 3 frames: 10 + (1*3) = 13, 20 + (2*3) = 26
        var pos = World.GetComponent<Position>(_playerWithVel);
        await Assert.That(pos.X).IsEqualTo(13f);
        await Assert.That(pos.Y).IsEqualTo(26f);
    }

    [Test]
    public async Task QueryReuse_CanIterateMultipleTimes()
    {
        var query = Positioned.Query.Build(World.World);

        int count1 = 0;
        foreach (var _ in query)
        {
            count1++;
        }

        int count2 = 0;
        foreach (var _ in query)
        {
            count2++;
        }

        await Assert.That(count1).IsEqualTo(5);
        await Assert.That(count2).IsEqualTo(5);
    }
}
