namespace Paradise.ECS.IntegrationTest.Tests;

/// <summary>
/// Integration tests for the tag system.
/// </summary>
public sealed class TagSystemIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task AddTag_SingleTag_TagPresent()
    {
        var entity = World.Spawn();

        World.AddTag<IsActive>(entity);

        await Assert.That(World.HasTag<IsActive>(entity)).IsTrue();
        await Assert.That(World.HasTag<IsVisible>(entity)).IsFalse();
    }

    [Test]
    public async Task AddTag_MultipleTags_AllPresent()
    {
        var entity = World.Spawn();

        World.AddTag<IsActive>(entity);
        World.AddTag<IsVisible>(entity);

        await Assert.That(World.HasTag<IsActive>(entity)).IsTrue();
        await Assert.That(World.HasTag<IsVisible>(entity)).IsTrue();
        await Assert.That(World.HasTag<IsDamageable>(entity)).IsFalse();
    }

    [Test]
    public async Task RemoveTag_RemovesSpecificTag()
    {
        var entity = World.Spawn();
        World.AddTag<IsActive>(entity);
        World.AddTag<IsVisible>(entity);

        World.RemoveTag<IsActive>(entity);

        await Assert.That(World.HasTag<IsActive>(entity)).IsFalse();
        await Assert.That(World.HasTag<IsVisible>(entity)).IsTrue();
    }

    [Test]
    public async Task GetTags_ReturnsCorrectMask()
    {
        var entity = World.Spawn();
        World.AddTag<IsActive>(entity);
        World.AddTag<IsDamageable>(entity);

        var tags = World.GetTags(entity);

        await Assert.That(tags.Get(IsActive.TagId)).IsTrue();
        await Assert.That(tags.Get(IsDamageable.TagId)).IsTrue();
        await Assert.That(tags.Get(IsVisible.TagId)).IsFalse();
    }

    [Test]
    public async Task SetTags_ReplacesAllTags()
    {
        var entity = World.Spawn();
        World.AddTag<IsMarkedForDestroy>(entity);
        await Assert.That(World.HasTag<IsMarkedForDestroy>(entity)).IsTrue();

        var newMask = default(TagMask).Set(IsActive.TagId).Set(IsDamageable.TagId);
        World.SetTags(entity, newMask);

        await Assert.That(World.HasTag<IsActive>(entity)).IsTrue();
        await Assert.That(World.HasTag<IsDamageable>(entity)).IsTrue();
        await Assert.That(World.HasTag<IsMarkedForDestroy>(entity)).IsFalse();
    }

    [Test]
    public async Task EntityBuilder_AddTag_TagsPresentOnCreation()
    {
        var entity = EntityBuilder.Create()
            .Add(new Position(100, 200))
            .Add(new Health(50))
            .AddTag(default(IsActive), World)
            .AddTag(default(IsVisible), World)
            .Build(World);

        await Assert.That(World.HasTag<IsActive>(entity)).IsTrue();
        await Assert.That(World.HasTag<IsVisible>(entity)).IsTrue();
        await Assert.That(World.HasTag<IsDamageable>(entity)).IsFalse();
        await Assert.That(World.GetComponent<Position>(entity).X).IsEqualTo(100f);
    }

    [Test]
    public async Task TagIds_AreGenerated()
    {
        // Tags are assigned IDs alphabetically
        await Assert.That(IsActive.TagId.Value).IsGreaterThanOrEqualTo(0);
        await Assert.That(IsDamageable.TagId.Value).IsGreaterThanOrEqualTo(0);
        await Assert.That(IsMarkedForDestroy.TagId.Value).IsGreaterThanOrEqualTo(0);
        await Assert.That(IsVisible.TagId.Value).IsGreaterThanOrEqualTo(0);
    }
}
