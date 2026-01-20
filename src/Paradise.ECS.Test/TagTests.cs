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

        var combined = mask1 | mask2;

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

        var intersection = mask1 & mask2;

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
/// </summary>
public sealed class TaggedWorldTests : IDisposable
{
    private static readonly DefaultConfig s_config = new();
    private readonly ChunkManager _chunkManager = ChunkManager.Create(s_config);
    private readonly SharedArchetypeMetadata<Bit64, ComponentRegistry, DefaultConfig> _sharedMetadata = new(s_config);
    private readonly World<Bit64, ComponentRegistry, DefaultConfig> _world;
    private readonly ChunkTagRegistry<TagMask> _chunkTagRegistry;
    private readonly TaggedWorld<Bit64, ComponentRegistry, DefaultConfig, EntityTags, TagMask> _taggedWorld;

    public TaggedWorldTests()
    {
        _world = new World<Bit64, ComponentRegistry, DefaultConfig>(s_config, _sharedMetadata, _chunkManager);
        _chunkTagRegistry = new ChunkTagRegistry<TagMask>(s_config.ChunkAllocator, DefaultConfig.MaxMetaBlocks, DefaultConfig.ChunkSize);
        _taggedWorld = new TaggedWorld<Bit64, ComponentRegistry, DefaultConfig, EntityTags, TagMask>(_world, _chunkTagRegistry);
    }

    public void Dispose()
    {
        _chunkTagRegistry.Dispose();
        _sharedMetadata.Dispose();
        _chunkManager.Dispose();
    }

    [Test]
    public async Task Spawn_CreatesEntityWithEntityTagsComponent()
    {
        var entity = _taggedWorld.Spawn();

        await Assert.That(_world.HasComponent<EntityTags>(entity)).IsTrue();
    }

    [Test]
    public async Task Spawn_EntityTagsMask_IsEmpty()
    {
        var entity = _taggedWorld.Spawn();

        var tags = _taggedWorld.GetTags(entity);

        await Assert.That(tags.IsEmpty).IsTrue();
    }

    [Test]
    public async Task AddTag_SetsTagOnEntity()
    {
        var entity = _taggedWorld.Spawn();

        _taggedWorld.AddTag<TestIsActive>(entity);

        await Assert.That(_taggedWorld.HasTag<TestIsActive>(entity)).IsTrue();
    }

    [Test]
    public async Task AddTag_MultipleTags_AllSet()
    {
        var entity = _taggedWorld.Spawn();

        _taggedWorld.AddTag<TestIsActive>(entity);
        _taggedWorld.AddTag<TestIsEnemy>(entity);

        await Assert.That(_taggedWorld.HasTag<TestIsActive>(entity)).IsTrue();
        await Assert.That(_taggedWorld.HasTag<TestIsEnemy>(entity)).IsTrue();
        await Assert.That(_taggedWorld.HasTag<TestIsPlayer>(entity)).IsFalse();
    }

    [Test]
    public async Task RemoveTag_ClearsTagOnEntity()
    {
        var entity = _taggedWorld.Spawn();
        _taggedWorld.AddTag<TestIsActive>(entity);
        _taggedWorld.AddTag<TestIsEnemy>(entity);

        _taggedWorld.RemoveTag<TestIsActive>(entity);

        await Assert.That(_taggedWorld.HasTag<TestIsActive>(entity)).IsFalse();
        await Assert.That(_taggedWorld.HasTag<TestIsEnemy>(entity)).IsTrue();
    }

    [Test]
    public async Task HasTag_EntityWithoutTag_ReturnsFalse()
    {
        var entity = _taggedWorld.Spawn();

        await Assert.That(_taggedWorld.HasTag<TestIsActive>(entity)).IsFalse();
    }

    [Test]
    public async Task GetTags_ReturnsCorrectMask()
    {
        var entity = _taggedWorld.Spawn();
        _taggedWorld.AddTag<TestIsActive>(entity);
        _taggedWorld.AddTag<TestIsPlayer>(entity);

        var tags = _taggedWorld.GetTags(entity);

        await Assert.That(tags.Get(TestIsActive.TagId.Value)).IsTrue();
        await Assert.That(tags.Get(TestIsPlayer.TagId.Value)).IsTrue();
        await Assert.That(tags.Get(TestIsEnemy.TagId.Value)).IsFalse();
    }

    [Test]
    public async Task SetTags_ReplacesAllTags()
    {
        var entity = _taggedWorld.Spawn();
        _taggedWorld.AddTag<TestIsActive>(entity);

        var newMask = TagMask.Empty
            .Set(TestIsEnemy.TagId.Value)
            .Set(TestIsPlayer.TagId.Value);
        _taggedWorld.SetTags(entity, newMask);

        await Assert.That(_taggedWorld.HasTag<TestIsActive>(entity)).IsFalse();
        await Assert.That(_taggedWorld.HasTag<TestIsEnemy>(entity)).IsTrue();
        await Assert.That(_taggedWorld.HasTag<TestIsPlayer>(entity)).IsTrue();
    }

    [Test]
    public async Task Despawn_RemovesEntity()
    {
        var entity = _taggedWorld.Spawn();
        _taggedWorld.AddTag<TestIsActive>(entity);

        _taggedWorld.Despawn(entity);

        await Assert.That(_taggedWorld.IsAlive(entity)).IsFalse();
    }

    [Test]
    public async Task EntityCount_TracksSpawnedEntities()
    {
        await Assert.That(_taggedWorld.EntityCount).IsEqualTo(0);

        var e1 = _taggedWorld.Spawn();
        await Assert.That(_taggedWorld.EntityCount).IsEqualTo(1);

        _ = _taggedWorld.Spawn();
        await Assert.That(_taggedWorld.EntityCount).IsEqualTo(2);

        _taggedWorld.Despawn(e1);
        await Assert.That(_taggedWorld.EntityCount).IsEqualTo(1);
    }

    [Test]
    public async Task ChunkTagRegistry_AddTag_UpdatesChunkMask()
    {
        var entity = _taggedWorld.Spawn();

        _taggedWorld.AddTag<TestIsActive>(entity);

        // Get entity's chunk handle
        var location = _world.GetLocation(entity);
        var archetype = _world.Registry.GetById(location.ArchetypeId)!;
        var (chunkIndex, _) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);

        var chunkMask = _chunkTagRegistry.GetChunkMask(chunkHandle);
        await Assert.That(chunkMask.Get(TestIsActive.TagId.Value)).IsTrue();
    }

    [Test]
    public async Task ChunkTagRegistry_MultipleEntitiesSameChunk_CombinesTags()
    {
        var e1 = _taggedWorld.Spawn();
        var e2 = _taggedWorld.Spawn();

        _taggedWorld.AddTag<TestIsActive>(e1);
        _taggedWorld.AddTag<TestIsEnemy>(e2);

        // Both should be in same chunk
        var location = _world.GetLocation(e1);
        var archetype = _world.Registry.GetById(location.ArchetypeId)!;
        var (chunkIndex, _) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);

        var chunkMask = _chunkTagRegistry.GetChunkMask(chunkHandle);
        await Assert.That(chunkMask.Get(TestIsActive.TagId.Value)).IsTrue();
        await Assert.That(chunkMask.Get(TestIsEnemy.TagId.Value)).IsTrue();
    }

    [Test]
    public async Task ChunkTagRegistry_RemoveTag_RecomputesChunkMask()
    {
        var e1 = _taggedWorld.Spawn();
        var e2 = _taggedWorld.Spawn();

        _taggedWorld.AddTag<TestIsActive>(e1);
        _taggedWorld.AddTag<TestIsActive>(e2);
        _taggedWorld.AddTag<TestIsEnemy>(e2);

        // Verify both entities have the expected tags before removal
        var e1TagsBefore = _taggedWorld.GetTags(e1);
        var e2TagsBefore = _taggedWorld.GetTags(e2);
        await Assert.That(e1TagsBefore.Get(TestIsActive.TagId.Value)).IsTrue();
        await Assert.That(e2TagsBefore.Get(TestIsActive.TagId.Value)).IsTrue();
        await Assert.That(e2TagsBefore.Get(TestIsEnemy.TagId.Value)).IsTrue();

        // Verify both entities are in the same chunk
        var loc1 = _world.GetLocation(e1);
        var loc2 = _world.GetLocation(e2);
        await Assert.That(loc1.ArchetypeId).IsEqualTo(loc2.ArchetypeId);
        var arch = _world.Registry.GetById(loc1.ArchetypeId)!;
        var (chunkIdx1, _) = arch.GetChunkLocation(loc1.GlobalIndex);
        var (chunkIdx2, _) = arch.GetChunkLocation(loc2.GlobalIndex);
        await Assert.That(chunkIdx1).IsEqualTo(chunkIdx2);

        // Remove TestIsActive from e1 - chunk should still have it from e2
        _taggedWorld.RemoveTag<TestIsActive>(e1);

        // Verify e2 still has TestIsActive after e1's removal
        var e2TagsAfter = _taggedWorld.GetTags(e2);
        await Assert.That(e2TagsAfter.Get(TestIsActive.TagId.Value)).IsTrue();

        var location = _world.GetLocation(e1);
        var archetype = _world.Registry.GetById(location.ArchetypeId)!;
        var (chunkIndex, _) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);

        var chunkMask = _chunkTagRegistry.GetChunkMask(chunkHandle);
        await Assert.That(chunkMask.Get(TestIsActive.TagId.Value)).IsTrue(); // Still set from e2
        await Assert.That(chunkMask.Get(TestIsEnemy.TagId.Value)).IsTrue();
    }

    [Test]
    public async Task ChunkTagRegistry_RemoveLastTagOfType_ClearsFromChunkMask()
    {
        var e1 = _taggedWorld.Spawn();
        var e2 = _taggedWorld.Spawn();

        _taggedWorld.AddTag<TestIsActive>(e1);
        _taggedWorld.AddTag<TestIsEnemy>(e2);

        // Remove the only TestIsActive tag
        _taggedWorld.RemoveTag<TestIsActive>(e1);

        var location = _world.GetLocation(e1);
        var archetype = _world.Registry.GetById(location.ArchetypeId)!;
        var (chunkIndex, _) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);

        var chunkMask = _chunkTagRegistry.GetChunkMask(chunkHandle);
        await Assert.That(chunkMask.Get(TestIsActive.TagId.Value)).IsFalse(); // No longer set
        await Assert.That(chunkMask.Get(TestIsEnemy.TagId.Value)).IsTrue();
    }

    [Test]
    public async Task ChunkTagRegistry_ChunkMayMatch_WithMatchingTags_ReturnsTrue()
    {
        var entity = _taggedWorld.Spawn();
        _taggedWorld.AddTag<TestIsActive>(entity);
        _taggedWorld.AddTag<TestIsEnemy>(entity);

        var location = _world.GetLocation(entity);
        var archetype = _world.Registry.GetById(location.ArchetypeId)!;
        var (chunkIndex, _) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);

        var requiredMask = TagMask.Empty.Set(TestIsActive.TagId.Value);
        await Assert.That(_chunkTagRegistry.ChunkMayMatch(chunkHandle, requiredMask)).IsTrue();
    }

    [Test]
    public async Task ChunkTagRegistry_ChunkMayMatch_WithMissingTags_ReturnsFalse()
    {
        var entity = _taggedWorld.Spawn();
        _taggedWorld.AddTag<TestIsActive>(entity);

        var location = _world.GetLocation(entity);
        var archetype = _world.Registry.GetById(location.ArchetypeId)!;
        var (chunkIndex, _) = archetype.GetChunkLocation(location.GlobalIndex);
        var chunkHandle = archetype.GetChunk(chunkIndex);

        var requiredMask = TagMask.Empty
            .Set(TestIsActive.TagId.Value)
            .Set(TestIsEnemy.TagId.Value); // Entity doesn't have TestIsEnemy
        await Assert.That(_chunkTagRegistry.ChunkMayMatch(chunkHandle, requiredMask)).IsFalse();
    }

    [Test]
    public async Task ComponentOperations_DelegatesToUnderlyingWorld()
    {
        var entity = _taggedWorld.Spawn();

        _taggedWorld.AddComponent(entity, new TestPosition { X = 1, Y = 2, Z = 3 });

        await Assert.That(_taggedWorld.HasComponent<TestPosition>(entity)).IsTrue();
        var pos = _taggedWorld.GetComponent<TestPosition>(entity);
        await Assert.That(pos.X).IsEqualTo(1);
        await Assert.That(pos.Y).IsEqualTo(2);
        await Assert.That(pos.Z).IsEqualTo(3);
    }

    [Test]
    public async Task SetComponent_UpdatesComponentValue()
    {
        var entity = _taggedWorld.Spawn();
        _taggedWorld.AddComponent(entity, new TestHealth { Current = 100, Max = 100 });

        _taggedWorld.SetComponent(entity, new TestHealth { Current = 50, Max = 100 });

        var health = _taggedWorld.GetComponent<TestHealth>(entity);
        await Assert.That(health.Current).IsEqualTo(50);
    }

    [Test]
    public async Task GetComponentRef_AllowsDirectModification()
    {
        var entity = _taggedWorld.Spawn();
        _taggedWorld.AddComponent(entity, new TestHealth { Current = 100, Max = 100 });

        ref var health = ref _taggedWorld.GetComponentRef<TestHealth>(entity);
        health.Current = 75;

        var updated = _taggedWorld.GetComponent<TestHealth>(entity);
        await Assert.That(updated.Current).IsEqualTo(75);
    }

    [Test]
    public async Task RemoveComponent_RemovesComponentFromEntity()
    {
        var entity = _taggedWorld.Spawn();
        _taggedWorld.AddComponent(entity, new TestPosition { X = 1 });

        _taggedWorld.RemoveComponent<TestPosition>(entity);

        await Assert.That(_taggedWorld.HasComponent<TestPosition>(entity)).IsFalse();
    }
}
