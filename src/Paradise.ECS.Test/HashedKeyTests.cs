namespace Paradise.ECS.Test;

/// <summary>
/// Tests for <see cref="HashedKey{T}"/>.
/// </summary>
public sealed class HashedKeyTests
{
    [Test]
    public async Task Value_ReturnsWrappedValue()
    {
        var key = new HashedKey<int>(42);

        await Assert.That(key.Value).IsEqualTo(42);
    }

    [Test]
    public async Task GetHashCode_ReturnsCachedHash()
    {
        var key = new HashedKey<string>("test");

        var hash1 = key.GetHashCode();
        var hash2 = key.GetHashCode();

        await Assert.That(hash1).IsEqualTo(hash2);
    }

    [Test]
    public async Task GetHashCode_MatchesUnderlyingValue()
    {
        var value = "test";
        var key = new HashedKey<string>(value);

        var expectedHash = EqualityComparer<string>.Default.GetHashCode(value);

        await Assert.That(key.GetHashCode()).IsEqualTo(expectedHash);
    }

    [Test]
    public async Task Equals_ReturnsTrueForSameValue()
    {
        var key1 = new HashedKey<int>(42);
        var key2 = new HashedKey<int>(42);

        await Assert.That(key1.Equals(key2)).IsTrue();
        await Assert.That(key1 == key2).IsTrue();
    }

    [Test]
    public async Task Equals_ReturnsFalseForDifferentValues()
    {
        var key1 = new HashedKey<int>(42);
        var key2 = new HashedKey<int>(99);

        await Assert.That(key1.Equals(key2)).IsFalse();
        await Assert.That(key1 != key2).IsTrue();
    }

    [Test]
    public async Task Equals_Object_ReturnsTrueForEqualHashedKey()
    {
        var key1 = new HashedKey<int>(42);
        object key2 = new HashedKey<int>(42);

        await Assert.That(key1.Equals(key2)).IsTrue();
    }

    [Test]
    public async Task Equals_Object_ReturnsFalseForNonHashedKey()
    {
        var key = new HashedKey<int>(42);

        await Assert.That(key.Equals("not a hashed key")).IsFalse();
        await Assert.That(key.Equals(null)).IsFalse();
    }

    [Test]
    public async Task ExplicitCast_CreatesHashedKey()
    {
        var key = (HashedKey<int>)42;

        await Assert.That(key.Value).IsEqualTo(42);
    }

    [Test]
    public async Task ImplicitCast_ReturnsValue()
    {
        var key = new HashedKey<int>(42);
        int value = key;

        await Assert.That(value).IsEqualTo(42);
    }

    [Test]
    public async Task ToString_IncludesValue()
    {
        var key = new HashedKey<int>(42);
        var str = key.ToString();

        await Assert.That(str).Contains("42");
    }

    [Test]
    public async Task Dictionary_WorksWithHashedKey()
    {
        var dict = new Dictionary<HashedKey<string>, int>();

        dict[(HashedKey<string>)"key1"] = 1;
        dict[(HashedKey<string>)"key2"] = 2;

        await Assert.That(dict[(HashedKey<string>)"key1"]).IsEqualTo(1);
        await Assert.That(dict[(HashedKey<string>)"key2"]).IsEqualTo(2);
    }

    [Test]
    public async Task HashSet_WorksWithHashedKey()
    {
        var set = new HashSet<HashedKey<int>>
        {
            (HashedKey<int>)1,
            (HashedKey<int>)2,
            (HashedKey<int>)1 // Duplicate
        };

        await Assert.That(set.Count).IsEqualTo(2);
        await Assert.That(set.Contains((HashedKey<int>)1)).IsTrue();
        await Assert.That(set.Contains((HashedKey<int>)2)).IsTrue();
    }

    [Test]
    public async Task NullableString_HandledCorrectly()
    {
        // HashedKey handles null values in Equals method
        var key1 = new HashedKey<string>(null!);
        var key2 = new HashedKey<string>(null!);
        var key3 = new HashedKey<string>("not null");

        await Assert.That(key1.Equals(key2)).IsTrue();
        await Assert.That(key1.Equals(key3)).IsFalse();
    }

    [Test]
    public async Task ComplexType_WorksCorrectly()
    {
        var bitset1 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(1);
        var bitset2 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(1);
        var bitset3 = ImmutableBitSet<Bit64>.Empty.Set(2);

        var key1 = new HashedKey<ImmutableBitSet<Bit64>>(bitset1);
        var key2 = new HashedKey<ImmutableBitSet<Bit64>>(bitset2);
        var key3 = new HashedKey<ImmutableBitSet<Bit64>>(bitset3);

        await Assert.That(key1.Equals(key2)).IsTrue();
        await Assert.That(key1.Equals(key3)).IsFalse();
    }
}
