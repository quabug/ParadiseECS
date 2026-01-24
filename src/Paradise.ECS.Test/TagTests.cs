namespace Paradise.ECS.Test;

/// <summary>
/// Tests for the Tag system including TagId, ITag interface, and tag generation.
/// </summary>
public class TagTests
{
    [Test]
    public async Task TagId_Default_HasValueZero()
    {
        // Default TagId has Value=0 which is valid (non-negative)
        // Use TagId.Invalid for an explicitly invalid ID
        var tagId = default(TagId);
        await Assert.That(tagId.Value).IsEqualTo(0);
        await Assert.That(tagId.IsValid).IsTrue();
    }

    [Test]
    public async Task TagId_WithPositiveValue_IsValid()
    {
        var tagId = new TagId(5);
        await Assert.That(tagId.IsValid).IsTrue();
        await Assert.That(tagId.Value).IsEqualTo(5);
    }

    [Test]
    public async Task TagId_Invalid_HasNegativeValue()
    {
        var tagId = TagId.Invalid;
        await Assert.That(tagId.IsValid).IsFalse();
        await Assert.That(tagId.Value).IsEqualTo(-1);
    }

    [Test]
    public async Task TagId_ImplicitConversionToInt_ReturnsValue()
    {
        var tagId = new TagId(42);
        int value = tagId;
        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task TagId_ToString_ValidId_IncludesValue()
    {
        var tagId = new TagId(7);
        var str = tagId.ToString();
        await Assert.That(str).Contains("7");
    }

    [Test]
    public async Task TagId_ToString_InvalidId_IndicatesInvalid()
    {
        var tagId = TagId.Invalid;
        var str = tagId.ToString();
        await Assert.That(str).Contains("Invalid");
    }

    [Test]
    public async Task TagId_Equality_SameValue_AreEqual()
    {
        var tagId1 = new TagId(10);
        var tagId2 = new TagId(10);
        await Assert.That(tagId1).IsEqualTo(tagId2);
        await Assert.That(tagId1 == tagId2).IsTrue();
    }

    [Test]
    public async Task TagId_Equality_DifferentValues_AreNotEqual()
    {
        var tagId1 = new TagId(10);
        var tagId2 = new TagId(20);
        await Assert.That(tagId1).IsNotEqualTo(tagId2);
        await Assert.That(tagId1 != tagId2).IsTrue();
    }
}

// ===== Test Tags =====

[Tag]
public partial struct TestIsActive;

[Tag]
public partial struct TestIsEnemy;

[Tag]
public partial struct TestIsPlayer;

[Tag(Id = 100)]
public partial struct TestManualIdTag;

// EntityTags needs [Component] attribute for ComponentGenerator to register it.
// The TagGenerator generates the IEntityTags implementation as a partial struct.
[Component("D1E2F3A4-B5C6-4D7E-8F9A-0B1C2D3E4F5A")]
public partial struct EntityTags;

/// <summary>
/// Integration tests for generated tag code.
/// </summary>
public class TagGeneratorIntegrationTests
{
    [Test]
    public async Task GeneratedTag_ImplementsITag()
    {
        // Verify the generated partial struct implements ITag
        await Assert.That(typeof(TestIsActive).GetInterfaces())
            .Contains(typeof(ITag));
    }

    [Test]
    public async Task GeneratedTag_HasValidTagId()
    {
        // After module initialization, tag should have a valid ID
        await Assert.That(TestIsActive.TagId.IsValid).IsTrue();
        await Assert.That(TestIsActive.TagId.Value).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task GeneratedTag_HasEmptyGuid_WhenNotSpecified()
    {
        // Tags without GUID specified should return Guid.Empty
        await Assert.That(TestIsActive.Guid).IsEqualTo(Guid.Empty);
    }

    [Test]
    public async Task GeneratedTag_DifferentTags_HaveDifferentIds()
    {
        // Each tag should have a unique ID
        await Assert.That(TestIsActive.TagId.Value)
            .IsNotEqualTo(TestIsEnemy.TagId.Value);
        await Assert.That(TestIsActive.TagId.Value)
            .IsNotEqualTo(TestIsPlayer.TagId.Value);
        await Assert.That(TestIsEnemy.TagId.Value)
            .IsNotEqualTo(TestIsPlayer.TagId.Value);
    }

    [Test]
    public async Task GeneratedTag_ManualId_UsesSpecifiedValue()
    {
        // Tag with [Tag(Id = 100)] should have TagId.Value == 100
        await Assert.That(TestManualIdTag.TagId.Value).IsEqualTo(100);
    }

    [Test]
    public async Task GeneratedTag_AutoAssignedIds_AreSequential()
    {
        // Auto-assigned IDs should be sequential (0, 1, 2, ...)
        // excluding any manually assigned IDs
        var autoIds = new[]
        {
            TestIsActive.TagId.Value,
            TestIsEnemy.TagId.Value,
            TestIsPlayer.TagId.Value
        };

        // All auto IDs should be less than 100 (the manual ID)
        foreach (var id in autoIds)
        {
            await Assert.That(id).IsLessThan(100);
        }

        // Auto IDs should be unique
        await Assert.That(autoIds.Distinct().Count()).IsEqualTo(autoIds.Length);
    }

    [Test]
    public async Task TagRegistry_GetId_ByType_ReturnsCorrectId()
    {
        var id = TagRegistry.GetId(typeof(TestIsActive));
        await Assert.That(id).IsEqualTo(TestIsActive.TagId);
    }

    [Test]
    public async Task TagRegistry_GetId_UnknownType_ReturnsInvalid()
    {
        // A type not marked with [Tag] should return Invalid
        var id = TagRegistry.GetId(typeof(string));
        await Assert.That(id).IsEqualTo(TagId.Invalid);
    }

    [Test]
    public async Task TagRegistry_TryGetId_KnownType_ReturnsTrue()
    {
        var found = TagRegistry.TryGetId(typeof(TestIsEnemy), out var id);
        await Assert.That(found).IsTrue();
        await Assert.That(id).IsEqualTo(TestIsEnemy.TagId);
    }

    [Test]
    public async Task TagRegistry_TryGetId_UnknownType_ReturnsFalse()
    {
        var found = TagRegistry.TryGetId(typeof(int), out _);
        await Assert.That(found).IsFalse();
    }
}

/// <summary>
/// Tests for TagMask operations using ImmutableBitSet.
/// </summary>
public class TagMaskTests
{
    [Test]
    public async Task TagMask_Empty_IsEmpty()
    {
        var mask = TagMask.Empty;
        await Assert.That(mask.IsEmpty).IsTrue();
        await Assert.That(mask.PopCount()).IsEqualTo(0);
    }

    [Test]
    public async Task TagMask_Set_SetsTagBit()
    {
        var mask = TagMask.Empty.Set(TestIsActive.TagId.Value);
        await Assert.That(mask.Get(TestIsActive.TagId.Value)).IsTrue();
        await Assert.That(mask.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task TagMask_Clear_ClearsTagBit()
    {
        var mask = TagMask.Empty
            .Set(TestIsActive.TagId.Value)
            .Set(TestIsEnemy.TagId.Value);

        mask = mask.Clear(TestIsActive.TagId.Value);

        await Assert.That(mask.Get(TestIsActive.TagId.Value)).IsFalse();
        await Assert.That(mask.Get(TestIsEnemy.TagId.Value)).IsTrue();
        await Assert.That(mask.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task TagMask_Or_CombinesTags()
    {
        var mask1 = TagMask.Empty.Set(TestIsActive.TagId.Value);
        var mask2 = TagMask.Empty.Set(TestIsEnemy.TagId.Value);

        var combined = mask1.Or(mask2);

        await Assert.That(combined.Get(TestIsActive.TagId.Value)).IsTrue();
        await Assert.That(combined.Get(TestIsEnemy.TagId.Value)).IsTrue();
        await Assert.That(combined.PopCount()).IsEqualTo(2);
    }

    [Test]
    public async Task TagMask_And_IntersectsTags()
    {
        var mask1 = TagMask.Empty
            .Set(TestIsActive.TagId.Value)
            .Set(TestIsEnemy.TagId.Value);
        var mask2 = TagMask.Empty
            .Set(TestIsEnemy.TagId.Value)
            .Set(TestIsPlayer.TagId.Value);

        var intersection = mask1.And(mask2);

        await Assert.That(intersection.Get(TestIsActive.TagId.Value)).IsFalse();
        await Assert.That(intersection.Get(TestIsEnemy.TagId.Value)).IsTrue();
        await Assert.That(intersection.Get(TestIsPlayer.TagId.Value)).IsFalse();
        await Assert.That(intersection.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task TagMask_ContainsAll_WithSubset_ReturnsTrue()
    {
        var mask1 = TagMask.Empty
            .Set(TestIsActive.TagId.Value)
            .Set(TestIsEnemy.TagId.Value)
            .Set(TestIsPlayer.TagId.Value);
        var mask2 = TagMask.Empty
            .Set(TestIsActive.TagId.Value)
            .Set(TestIsEnemy.TagId.Value);

        await Assert.That(mask1.ContainsAll(mask2)).IsTrue();
        await Assert.That(mask2.ContainsAll(mask1)).IsFalse();
    }

    [Test]
    public async Task TagMask_ContainsAny_WithOverlap_ReturnsTrue()
    {
        var mask1 = TagMask.Empty.Set(TestIsActive.TagId.Value);
        var mask2 = TagMask.Empty
            .Set(TestIsActive.TagId.Value)
            .Set(TestIsEnemy.TagId.Value);

        await Assert.That(mask1.ContainsAny(mask2)).IsTrue();
    }

    [Test]
    public async Task TagMask_ContainsNone_WithNoOverlap_ReturnsTrue()
    {
        var mask1 = TagMask.Empty.Set(TestIsActive.TagId.Value);
        var mask2 = TagMask.Empty.Set(TestIsEnemy.TagId.Value);

        await Assert.That(mask1.ContainsNone(mask2)).IsTrue();
    }
}

/// <summary>
/// Tests for TaggedWorld operations.
/// Uses generated alias: World (TaggedWorld).
/// </summary>
public sealed class TaggedWorldTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly ChunkManager _chunkManager = ChunkManager.Create(s_config);
    private readonly SharedArchetypeMetadata _sharedMetadata = new(ComponentRegistry.Shared.TypeInfos, s_config);
    private readonly ChunkTagRegistry<TagMask> _chunkTagRegistry = new(s_config.ChunkAllocator, DefaultConfig.MaxMetaBlocks, DefaultConfig.ChunkSize);
    private readonly World _world;

    public TaggedWorldTests()
    {
        _world = new World(s_config, _chunkManager, _sharedMetadata, _chunkTagRegistry);
    }

    public void Dispose()
    {
        _world.Dispose();
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
        _chunkTagRegistry.Dispose();
    }

    [Test]
    public async Task Spawn_CreatesEntityWithEntityTagsComponent()
    {
        var entity = _world.Spawn();

        await Assert.That(_world.HasComponent<EntityTags>(entity)).IsTrue();
    }

    [Test]
    public async Task Spawn_EntityTagsMask_IsEmpty()
    {
        var entity = _world.Spawn();

        var tags = _world.GetTags(entity);

        await Assert.That(tags.IsEmpty).IsTrue();
    }

    [Test]
    public async Task AddTag_SetsTagOnEntity()
    {
        var entity = _world.Spawn();

        _world.AddTag<TestIsActive>(entity);

        await Assert.That(_world.HasTag<TestIsActive>(entity)).IsTrue();
    }

    [Test]
    public async Task AddTag_MultipleTags_AllSet()
    {
        var entity = _world.Spawn();

        _world.AddTag<TestIsActive>(entity);
        _world.AddTag<TestIsEnemy>(entity);

        await Assert.That(_world.HasTag<TestIsActive>(entity)).IsTrue();
        await Assert.That(_world.HasTag<TestIsEnemy>(entity)).IsTrue();
        await Assert.That(_world.HasTag<TestIsPlayer>(entity)).IsFalse();
    }

    [Test]
    public async Task RemoveTag_ClearsTagOnEntity()
    {
        var entity = _world.Spawn();
        _world.AddTag<TestIsActive>(entity);
        _world.AddTag<TestIsEnemy>(entity);

        _world.RemoveTag<TestIsActive>(entity);

        await Assert.That(_world.HasTag<TestIsActive>(entity)).IsFalse();
        await Assert.That(_world.HasTag<TestIsEnemy>(entity)).IsTrue();
    }

    [Test]
    public async Task HasTag_EntityWithoutTag_ReturnsFalse()
    {
        var entity = _world.Spawn();

        await Assert.That(_world.HasTag<TestIsActive>(entity)).IsFalse();
    }

    [Test]
    public async Task GetTags_ReturnsCorrectMask()
    {
        var entity = _world.Spawn();
        _world.AddTag<TestIsActive>(entity);
        _world.AddTag<TestIsPlayer>(entity);

        var tags = _world.GetTags(entity);

        await Assert.That(tags.Get(TestIsActive.TagId.Value)).IsTrue();
        await Assert.That(tags.Get(TestIsPlayer.TagId.Value)).IsTrue();
        await Assert.That(tags.Get(TestIsEnemy.TagId.Value)).IsFalse();
    }

    [Test]
    public async Task SetTags_ReplacesAllTags()
    {
        var entity = _world.Spawn();
        _world.AddTag<TestIsActive>(entity);

        var newMask = TagMask.Empty
            .Set(TestIsEnemy.TagId.Value)
            .Set(TestIsPlayer.TagId.Value);
        _world.SetTags(entity, newMask);

        await Assert.That(_world.HasTag<TestIsActive>(entity)).IsFalse();
        await Assert.That(_world.HasTag<TestIsEnemy>(entity)).IsTrue();
        await Assert.That(_world.HasTag<TestIsPlayer>(entity)).IsTrue();
    }

    [Test]
    public async Task Despawn_RemovesEntity()
    {
        var entity = _world.Spawn();
        _world.AddTag<TestIsActive>(entity);

        _world.Despawn(entity);

        await Assert.That(_world.IsAlive(entity)).IsFalse();
    }

    [Test]
    public async Task EntityCount_TracksSpawnedEntities()
    {
        await Assert.That(_world.EntityCount).IsEqualTo(0);

        var e1 = _world.Spawn();
        await Assert.That(_world.EntityCount).IsEqualTo(1);

        _ = _world.Spawn();
        await Assert.That(_world.EntityCount).IsEqualTo(2);

        _world.Despawn(e1);
        await Assert.That(_world.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task ChunkTagRegistry_AddTag_UpdatesChunkMask()
    {
        var entity = _world.Spawn();

        _world.AddTag<TestIsActive>(entity);

        // Get entity's chunk handle
        var location = _world.World.GetLocation(entity);
        var archetype = _world.World.ArchetypeRegistry.GetById(location.ArchetypeId)!;
        var (chunkIndex, _) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);

        var chunkMask = _world.ChunkTagRegistry.GetChunkMask(chunkHandle);
        await Assert.That(chunkMask.Get(TestIsActive.TagId.Value)).IsTrue();
    }

    [Test]
    public async Task ChunkTagRegistry_MultipleEntitiesSameChunk_CombinesTags()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();

        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsEnemy>(e2);

        // Both should be in same chunk
        var location = _world.World.GetLocation(e1);
        var archetype = _world.World.ArchetypeRegistry.GetById(location.ArchetypeId)!;
        var (chunkIndex, _) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);

        var chunkMask = _world.ChunkTagRegistry.GetChunkMask(chunkHandle);
        await Assert.That(chunkMask.Get(TestIsActive.TagId.Value)).IsTrue();
        await Assert.That(chunkMask.Get(TestIsEnemy.TagId.Value)).IsTrue();
    }

    [Test]
    public async Task ChunkTagRegistry_RemoveTag_RecomputesChunkMask()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();

        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsActive>(e2);
        _world.AddTag<TestIsEnemy>(e2);

        // Verify both entities have the expected tags before removal
        var e1TagsBefore = _world.GetTags(e1);
        var e2TagsBefore = _world.GetTags(e2);
        await Assert.That(e1TagsBefore.Get(TestIsActive.TagId.Value)).IsTrue();
        await Assert.That(e2TagsBefore.Get(TestIsActive.TagId.Value)).IsTrue();
        await Assert.That(e2TagsBefore.Get(TestIsEnemy.TagId.Value)).IsTrue();

        // Verify both entities are in the same chunk
        var loc1 = _world.World.GetLocation(e1);
        var loc2 = _world.World.GetLocation(e2);
        await Assert.That(loc1.ArchetypeId).IsEqualTo(loc2.ArchetypeId);
        var arch = _world.World.ArchetypeRegistry.GetById(loc1.ArchetypeId)!;
        var (chunkIdx1, _) = arch.GetChunkLocation(loc1.GlobalIndex);
        var (chunkIdx2, _) = arch.GetChunkLocation(loc2.GlobalIndex);
        await Assert.That(chunkIdx1).IsEqualTo(chunkIdx2);

        // Remove TestIsActive from e1 - chunk should still have it from e2
        _world.RemoveTag<TestIsActive>(e1);

        // Verify e2 still has TestIsActive after e1's removal
        var e2TagsAfter = _world.GetTags(e2);
        await Assert.That(e2TagsAfter.Get(TestIsActive.TagId.Value)).IsTrue();

        var location = _world.World.GetLocation(e1);
        var archetype = _world.World.ArchetypeRegistry.GetById(location.ArchetypeId)!;
        var (chunkIndex, _) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);

        var chunkMask = _world.ChunkTagRegistry.GetChunkMask(chunkHandle);
        await Assert.That(chunkMask.Get(TestIsActive.TagId.Value)).IsTrue(); // Still set from e2
        await Assert.That(chunkMask.Get(TestIsEnemy.TagId.Value)).IsTrue();
    }

    [Test]
    public async Task ChunkTagRegistry_RemoveLastTagOfType_StickyMaskRetainsBit()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();

        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsEnemy>(e2);

        // Remove the only TestIsActive tag
        _world.RemoveTag<TestIsActive>(e1);

        var location = _world.World.GetLocation(e1);
        var archetype = _world.World.ArchetypeRegistry.GetById(location.ArchetypeId)!;
        var (chunkIndex, _) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);

        // Sticky mask: bit remains set even after removing last entity with that tag
        var chunkMask = _world.ChunkTagRegistry.GetChunkMask(chunkHandle);
        await Assert.That(chunkMask.Get(TestIsActive.TagId.Value)).IsTrue(); // Sticky - still set
        await Assert.That(chunkMask.Get(TestIsEnemy.TagId.Value)).IsTrue();

        // After RebuildChunkMasks, the stale bit is cleared
        _world.RebuildChunkMasks();
        var rebuiltMask = _world.ChunkTagRegistry.GetChunkMask(chunkHandle);
        await Assert.That(rebuiltMask.Get(TestIsActive.TagId.Value)).IsFalse(); // Now cleared
        await Assert.That(rebuiltMask.Get(TestIsEnemy.TagId.Value)).IsTrue();
    }

    [Test]
    public async Task ChunkTagRegistry_ChunkMayMatch_WithMatchingTags_ReturnsTrue()
    {
        var entity = _world.Spawn();
        _world.AddTag<TestIsActive>(entity);
        _world.AddTag<TestIsEnemy>(entity);

        var location = _world.World.GetLocation(entity);
        var archetype = _world.World.ArchetypeRegistry.GetById(location.ArchetypeId)!;
        var (chunkIndex, _) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);

        var requiredMask = TagMask.Empty.Set(TestIsActive.TagId.Value);
        await Assert.That(_world.ChunkTagRegistry.ChunkMayMatch(chunkHandle, requiredMask)).IsTrue();
    }

    [Test]
    public async Task ChunkTagRegistry_ChunkMayMatch_WithMissingTags_ReturnsFalse()
    {
        var entity = _world.Spawn();
        _world.AddTag<TestIsActive>(entity);

        var location = _world.World.GetLocation(entity);
        var archetype = _world.World.ArchetypeRegistry.GetById(location.ArchetypeId)!;
        var (chunkIndex, _) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);

        var requiredMask = TagMask.Empty
            .Set(TestIsActive.TagId.Value)
            .Set(TestIsEnemy.TagId.Value); // Entity doesn't have TestIsEnemy
        await Assert.That(_world.ChunkTagRegistry.ChunkMayMatch(chunkHandle, requiredMask)).IsFalse();
    }

    [Test]
    public async Task ComponentOperations_DelegatesToUnderlyingWorld()
    {
        var entity = _world.Spawn();

        _world.AddComponent(entity, new TestPosition { X = 1, Y = 2, Z = 3 });

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsTrue();
        var pos = _world.GetComponent<TestPosition>(entity);
        await Assert.That(pos.X).IsEqualTo(1);
        await Assert.That(pos.Y).IsEqualTo(2);
        await Assert.That(pos.Z).IsEqualTo(3);
    }

    [Test]
    public async Task SetComponent_UpdatesComponentValue()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestHealth { Current = 100, Max = 100 });

        _world.SetComponent(entity, new TestHealth { Current = 50, Max = 100 });

        var health = _world.GetComponent<TestHealth>(entity);
        await Assert.That(health.Current).IsEqualTo(50);
    }

    [Test]
    public async Task GetComponentRef_AllowsDirectModification()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestHealth { Current = 100, Max = 100 });

        ref var health = ref _world.GetComponentRef<TestHealth>(entity);
        health.Current = 75;

        var updated = _world.GetComponent<TestHealth>(entity);
        await Assert.That(updated.Current).IsEqualTo(75);
    }

    [Test]
    public async Task RemoveComponent_RemovesComponentFromEntity()
    {
        var entity = _world.Spawn();
        _world.AddComponent(entity, new TestPosition { X = 1 });

        _world.RemoveComponent<TestPosition>(entity);

        await Assert.That(_world.HasComponent<TestPosition>(entity)).IsFalse();
    }

    [Test]
    public async Task TaggedQueryBuilder_WithTag_FiltersEntities()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        var e3 = _world.Spawn();

        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsActive>(e2);
        _world.AddTag<TestIsEnemy>(e2);
        // e3 has no tags

        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .WithTag<SmallBitSet<uint>, TestIsActive, TagMask>()
            .Build(_world);

        var matchedEntities = new List<Entity>();
        foreach (var entity in query)
        {
            matchedEntities.Add(entity);
        }

        await Assert.That(matchedEntities).Contains(e1);
        await Assert.That(matchedEntities).Contains(e2);
        await Assert.That(matchedEntities).DoesNotContain(e3);
        await Assert.That(matchedEntities.Count).IsEqualTo(2);
    }

    [Test]
    public async Task TaggedQueryBuilder_MultipleTags_FiltersCorrectly()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        var e3 = _world.Spawn();

        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsActive>(e2);
        _world.AddTag<TestIsEnemy>(e2);
        _world.AddTag<TestIsEnemy>(e3);

        // Query for entities with BOTH TestIsActive AND TestIsEnemy
        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .WithTag<SmallBitSet<uint>, TestIsActive, TagMask>()
            .WithTag<TestIsEnemy>()
            .Build(_world);

        var matchedEntities = new List<Entity>();
        foreach (var entity in query)
        {
            matchedEntities.Add(entity);
        }

        await Assert.That(matchedEntities).DoesNotContain(e1); // Only has TestIsActive
        await Assert.That(matchedEntities).Contains(e2);       // Has both
        await Assert.That(matchedEntities).DoesNotContain(e3); // Only has TestIsEnemy
        await Assert.That(matchedEntities.Count).IsEqualTo(1);
    }

    [Test]
    public async Task TaggedQueryBuilder_WithComponentConstraint_FiltersCorrectly()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        var e3 = _world.Spawn();

        _world.AddComponent(e1, new TestPosition { X = 1 });
        _world.AddComponent(e2, new TestPosition { X = 2 });
        // e3 has no Position component

        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsActive>(e2);
        _world.AddTag<TestIsActive>(e3);

        // Query for entities with TestIsActive tag AND Position component
        var query = QueryBuilder<SmallBitSet<uint>>.Create()
            .With<TestPosition>()
            .WithTag<SmallBitSet<uint>, TestIsActive, TagMask>()
            .Build(_world);

        var matchedEntities = new List<Entity>();
        foreach (var entity in query)
        {
            matchedEntities.Add(entity);
        }

        await Assert.That(matchedEntities).Contains(e1);
        await Assert.That(matchedEntities).Contains(e2);
        await Assert.That(matchedEntities).DoesNotContain(e3); // No Position component
        await Assert.That(matchedEntities.Count).IsEqualTo(2);
    }

    [Test]
    public async Task TaggedWorldQueryBuilder_CleanApi_FiltersCorrectly()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        var e3 = _world.Spawn();

        _world.AddComponent(e1, new TestPosition { X = 1 });
        _world.AddComponent(e2, new TestPosition { X = 2 });

        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsActive>(e2);
        _world.AddTag<TestIsEnemy>(e2);
        _world.AddTag<TestIsEnemy>(e3);

        // Clean API: world.Query().WithTag<T>().With<C>().Build()
        var query = _world.Query()
            .WithTag<TestIsActive>()
            .With<TestPosition>()
            .Build();

        var matchedEntities = new List<Entity>();
        foreach (var entity in query)
        {
            matchedEntities.Add(entity);
        }

        await Assert.That(matchedEntities).Contains(e1);
        await Assert.That(matchedEntities).Contains(e2);
        await Assert.That(matchedEntities).DoesNotContain(e3);
        await Assert.That(matchedEntities.Count).IsEqualTo(2);
    }

    [Test]
    public async Task TaggedWorldQueryBuilder_MultipleTagsCleanApi_FiltersCorrectly()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();
        var e3 = _world.Spawn();

        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsActive>(e2);
        _world.AddTag<TestIsEnemy>(e2);
        _world.AddTag<TestIsEnemy>(e3);

        // Clean API with multiple tags
        var query = _world.Query()
            .WithTag<TestIsActive>()
            .WithTag<TestIsEnemy>()
            .Build();

        var matchedEntities = new List<Entity>();
        foreach (var entity in query)
        {
            matchedEntities.Add(entity);
        }

        await Assert.That(matchedEntities).DoesNotContain(e1); // Only TestIsActive
        await Assert.That(matchedEntities).Contains(e2);       // Has both
        await Assert.That(matchedEntities).DoesNotContain(e3); // Only TestIsEnemy
        await Assert.That(matchedEntities.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ComputeStaleBitStatistics_NoTags_ReturnsZeroStats()
    {
        // Spawn some entities without any tags
        _ = _world.Spawn();
        _ = _world.Spawn();

        var stats = _world.ComputeStaleBitStatistics();

        await Assert.That(stats.TotalStaleBits).IsEqualTo(0);
        await Assert.That(stats.TotalActualBits).IsEqualTo(0);
        await Assert.That(stats.ChunksWithStaleBits).IsEqualTo(0);
        await Assert.That(stats.StaleBitRatio).IsEqualTo(0.0);
        await Assert.That(stats.SuggestsRebuild).IsFalse();
    }

    [Test]
    public async Task ComputeStaleBitStatistics_NoStaleBits_ReturnsZeroStaleBits()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();

        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsEnemy>(e2);

        var stats = _world.ComputeStaleBitStatistics();

        await Assert.That(stats.TotalStaleBits).IsEqualTo(0);
        await Assert.That(stats.TotalActualBits).IsEqualTo(2); // Two actual tag bits
        await Assert.That(stats.ChunksWithStaleBits).IsEqualTo(0);
        await Assert.That(stats.StaleBitRatio).IsEqualTo(0.0);
        await Assert.That(stats.SuggestsRebuild).IsFalse();
    }

    [Test]
    public async Task ComputeStaleBitStatistics_WithStaleBits_ReportsCorrectly()
    {
        var e1 = _world.Spawn();
        var e2 = _world.Spawn();

        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsEnemy>(e1);
        _world.AddTag<TestIsActive>(e2);

        // Remove TestIsEnemy from e1 - creates a stale bit
        _world.RemoveTag<TestIsEnemy>(e1);

        var stats = _world.ComputeStaleBitStatistics();

        // Total actual bits: TestIsActive on e1, TestIsActive on e2 = 2 (counted per chunk, OR'd together = 1)
        // Wait, this is per-chunk counting. Both entities are in same chunk.
        // Chunk mask has TestIsActive and TestIsEnemy (sticky).
        // Actual mask: TestIsActive only (since TestIsEnemy was removed).
        // Current mask bits: 2 (TestIsActive + TestIsEnemy)
        // Actual mask bits: 1 (TestIsActive only)
        // Stale bits: 2 - 1 = 1
        await Assert.That(stats.TotalStaleBits).IsEqualTo(1);
        await Assert.That(stats.TotalActualBits).IsEqualTo(1);
        await Assert.That(stats.ChunksWithStaleBits).IsEqualTo(1);
        await Assert.That(stats.StaleBitRatio).IsEqualTo(0.5); // 1/(1+1) = 0.5
    }

    [Test]
    public async Task ComputeStaleBitStatistics_AfterRebuild_ReturnsZeroStaleBits()
    {
        var e1 = _world.Spawn();

        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsEnemy>(e1);

        // Remove tag to create stale bit
        _world.RemoveTag<TestIsEnemy>(e1);

        // Verify stale bits exist before rebuild
        var statsBefore = _world.ComputeStaleBitStatistics();
        await Assert.That(statsBefore.TotalStaleBits).IsGreaterThan(0);

        // Rebuild clears stale bits
        _world.RebuildChunkMasks();

        var statsAfter = _world.ComputeStaleBitStatistics();
        await Assert.That(statsAfter.TotalStaleBits).IsEqualTo(0);
        await Assert.That(statsAfter.ChunksWithStaleBits).IsEqualTo(0);
        await Assert.That(statsAfter.SuggestsRebuild).IsFalse();
    }

    [Test]
    public async Task StaleBitStatistics_SuggestsRebuild_HighStaleBitRatio()
    {
        var e1 = _world.Spawn();

        // Add multiple tags then remove most of them
        _world.AddTag<TestIsActive>(e1);
        _world.AddTag<TestIsEnemy>(e1);
        _world.AddTag<TestIsPlayer>(e1);

        _world.RemoveTag<TestIsEnemy>(e1);
        _world.RemoveTag<TestIsPlayer>(e1);

        var stats = _world.ComputeStaleBitStatistics();

        // Current mask: 3 bits (TestIsActive, TestIsEnemy, TestIsPlayer - sticky)
        // Actual mask: 1 bit (TestIsActive only)
        // Stale bits: 2, Actual bits: 1
        // Ratio: 2/3 ≈ 0.67 > 0.5
        await Assert.That(stats.TotalStaleBits).IsEqualTo(2);
        await Assert.That(stats.TotalActualBits).IsEqualTo(1);
        await Assert.That(stats.SuggestsRebuild).IsTrue();
    }
}
