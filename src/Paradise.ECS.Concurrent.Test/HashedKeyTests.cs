namespace Paradise.ECS.Concurrent.Test;

/// <summary>
/// Tests for HashedKey - a dictionary key wrapper that caches hash codes.
/// </summary>
public class HashedKeyTests
{
    #region Constructor and Value Tests

    [Test]
    public async Task Constructor_CachesHashCode()
    {
        var value = "test";
        var key = new HashedKey<string>(value);

        // Hash should be computed at construction time
        var expectedHash = EqualityComparer<string>.Default.GetHashCode(value);
        var actualHash = key.GetHashCode();
        await Assert.That(actualHash).IsEqualTo(expectedHash);
    }

    [Test]
    public async Task Value_ReturnsOriginalValue()
    {
        var value = "test string";
        var key = new HashedKey<string>(value);

        var actualValue = key.Value;
        await Assert.That(actualValue).IsEqualTo(value);
    }

    [Test]
    public async Task Constructor_WithStruct_CachesHashCode()
    {
        var value = 42;
        var key = new HashedKey<int>(value);

        var expectedHash = EqualityComparer<int>.Default.GetHashCode(value);
        var actualHash = key.GetHashCode();
        await Assert.That(actualHash).IsEqualTo(expectedHash);
    }

    #endregion

    #region Equality Tests

    [Test]
    public async Task Equals_SameValue_ReturnsTrue()
    {
        var key1 = new HashedKey<string>("test");
        var key2 = new HashedKey<string>("test");

        var result = key1.Equals(key2);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Equals_DifferentValue_ReturnsFalse()
    {
        var key1 = new HashedKey<string>("test1");
        var key2 = new HashedKey<string>("test2");

        var result = key1.Equals(key2);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Equals_Object_SameValue_ReturnsTrue()
    {
        var key1 = new HashedKey<string>("test");
        object key2 = new HashedKey<string>("test");

        var result = key1.Equals(key2);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Equals_Object_DifferentType_ReturnsFalse()
    {
        var key = new HashedKey<string>("test");
        object other = "test";

        var result = key.Equals(other);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task Equals_Object_Null_ReturnsFalse()
    {
        var key = new HashedKey<string>("test");

        var result = key.Equals(null);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task OperatorEquals_SameValue_ReturnsTrue()
    {
        var key1 = new HashedKey<string>("test");
        var key2 = new HashedKey<string>("test");

        var result = key1 == key2;
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task OperatorNotEquals_DifferentValue_ReturnsTrue()
    {
        var key1 = new HashedKey<string>("test1");
        var key2 = new HashedKey<string>("test2");

        var result = key1 != key2;
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task GetHashCode_SameValue_ReturnsSameHash()
    {
        var key1 = new HashedKey<string>("test");
        var key2 = new HashedKey<string>("test");

        var hash1 = key1.GetHashCode();
        var hash2 = key2.GetHashCode();
        await Assert.That(hash1).IsEqualTo(hash2);
    }

    #endregion

    #region Conversion Tests

    [Test]
    public async Task ExplicitConversion_FromValue_CreatesKey()
    {
        var value = "test";
        var key = (HashedKey<string>)value;

        var actualValue = key.Value;
        await Assert.That(actualValue).IsEqualTo(value);
    }

    [Test]
    public async Task ImplicitConversion_ToValue_ReturnsValue()
    {
        var key = new HashedKey<string>("test");
        string value = key;

        await Assert.That(value).IsEqualTo("test");
    }

    [Test]
    public async Task ExplicitConversion_WithStruct_CreatesKey()
    {
        int value = 42;
        var key = (HashedKey<int>)value;

        var actualValue = key.Value;
        await Assert.That(actualValue).IsEqualTo(value);
    }

    #endregion

    #region Dictionary Usage Tests

    [Test]
    public async Task Dictionary_LookupByValue_Works()
    {
        var dict = new Dictionary<HashedKey<string>, int>();
        var key1 = new HashedKey<string>("test");
        dict[key1] = 42;

        // Look up with a new key instance with same value
        var key2 = new HashedKey<string>("test");
        var found = dict.TryGetValue(key2, out var value);

        await Assert.That(found).IsTrue();
        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task Dictionary_MultipleKeys_WorkCorrectly()
    {
        var dict = new Dictionary<HashedKey<string>, int>();
        dict[(HashedKey<string>)"key1"] = 1;
        dict[(HashedKey<string>)"key2"] = 2;
        dict[(HashedKey<string>)"key3"] = 3;

        var count = dict.Count;
        var v1 = dict[(HashedKey<string>)"key1"];
        var v2 = dict[(HashedKey<string>)"key2"];
        var v3 = dict[(HashedKey<string>)"key3"];

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(v1).IsEqualTo(1);
        await Assert.That(v2).IsEqualTo(2);
        await Assert.That(v3).IsEqualTo(3);
    }

    [Test]
    public async Task Dictionary_SameKeyTwice_OverwritesValue()
    {
        var dict = new Dictionary<HashedKey<string>, int>();
        dict[(HashedKey<string>)"test"] = 1;
        dict[(HashedKey<string>)"test"] = 2;

        var count = dict.Count;
        var value = dict[(HashedKey<string>)"test"];

        await Assert.That(count).IsEqualTo(1);
        await Assert.That(value).IsEqualTo(2);
    }

    #endregion

    #region ToString Tests

    [Test]
    public async Task ToString_ReturnsFormattedString()
    {
        var key = new HashedKey<string>("test");
        var result = key.ToString();

        await Assert.That(result).IsEqualTo("HashedKey(test)");
    }

    [Test]
    public async Task ToString_WithInt_ReturnsFormattedString()
    {
        var key = new HashedKey<int>(42);
        var result = key.ToString();

        await Assert.That(result).IsEqualTo("HashedKey(42)");
    }

    #endregion

    #region BitSet Integration Tests

    [Test]
    public async Task HashedKey_WithBitSet_WorksAsKey()
    {
        var mask1 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(1);
        var mask2 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(1);

        var key1 = (HashedKey<ImmutableBitSet<Bit64>>)mask1;
        var key2 = (HashedKey<ImmutableBitSet<Bit64>>)mask2;

        var equals = key1.Equals(key2);
        var hash1 = key1.GetHashCode();
        var hash2 = key2.GetHashCode();

        await Assert.That(equals).IsTrue();
        await Assert.That(hash1).IsEqualTo(hash2);
    }

    [Test]
    public async Task Dictionary_WithBitSetKeys_WorksCorrectly()
    {
        var dict = new Dictionary<HashedKey<ImmutableBitSet<Bit64>>, int>();

        var mask1 = ImmutableBitSet<Bit64>.Empty.Set(0);
        var mask2 = ImmutableBitSet<Bit64>.Empty.Set(1);
        var mask3 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(1);

        dict[(HashedKey<ImmutableBitSet<Bit64>>)mask1] = 1;
        dict[(HashedKey<ImmutableBitSet<Bit64>>)mask2] = 2;
        dict[(HashedKey<ImmutableBitSet<Bit64>>)mask3] = 3;

        var count = dict.Count;

        // Look up with fresh masks
        var lookup1 = ImmutableBitSet<Bit64>.Empty.Set(0);
        var lookup2 = ImmutableBitSet<Bit64>.Empty.Set(1);
        var lookup3 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(1);

        var v1 = dict[(HashedKey<ImmutableBitSet<Bit64>>)lookup1];
        var v2 = dict[(HashedKey<ImmutableBitSet<Bit64>>)lookup2];
        var v3 = dict[(HashedKey<ImmutableBitSet<Bit64>>)lookup3];

        await Assert.That(count).IsEqualTo(3);
        await Assert.That(v1).IsEqualTo(1);
        await Assert.That(v2).IsEqualTo(2);
        await Assert.That(v3).IsEqualTo(3);
    }

    #endregion
}
