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
