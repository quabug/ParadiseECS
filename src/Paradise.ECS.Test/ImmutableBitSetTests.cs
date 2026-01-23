namespace Paradise.ECS.Test;

/// <summary>
/// Tests for <see cref="ImmutableBitSet{TBits}"/>.
/// </summary>
public sealed class ImmutableBitSetTests
{
    [Test]
    public async Task Empty_HasAllBitsClear()
    {
        var bitset = SmallBitSet<ulong>.Empty;

        await Assert.That(bitset.IsEmpty).IsTrue();
        await Assert.That(bitset.PopCount()).IsEqualTo(0);
    }

    [Test]
    public async Task Set_SetsBit()
    {
        var bitset = SmallBitSet<ulong>.Empty.Set(5);

        await Assert.That(bitset.Get(5)).IsTrue();
        await Assert.That(bitset.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task Set_IsImmutable()
    {
        var original = SmallBitSet<ulong>.Empty;
        var modified = original.Set(5);

        await Assert.That(original.IsEmpty).IsTrue();
        await Assert.That(modified.Get(5)).IsTrue();
    }

    [Test]
    public async Task Clear_ClearsBit()
    {
        var bitset = SmallBitSet<ulong>.Empty.Set(5).Clear(5);

        await Assert.That(bitset.Get(5)).IsFalse();
        await Assert.That(bitset.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Get_ReturnsFalseForUnsetBit()
    {
        var bitset = SmallBitSet<ulong>.Empty;

        await Assert.That(bitset.Get(0)).IsFalse();
        await Assert.That(bitset.Get(63)).IsFalse();
    }

    [Test]
    public async Task Get_ThrowsForNegativeIndex()
    {
        var bitset = SmallBitSet<ulong>.Empty;

        await Assert.That(() => bitset.Get(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Get_ThrowsForIndexAtCapacity()
    {
        var bitset = SmallBitSet<ulong>.Empty;
        int capacity = SmallBitSet<ulong>.Capacity;

        await Assert.That(() => bitset.Get(capacity)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Set_ThrowsForNegativeIndex()
    {
        var bitset = SmallBitSet<ulong>.Empty;

        await Assert.That(() => bitset.Set(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task And_ReturnsIntersection()
    {
        var a = SmallBitSet<ulong>.Empty.Set(0).Set(1).Set(2);
        var b = SmallBitSet<ulong>.Empty.Set(1).Set(2).Set(3);

        var result = a.And(b);

        await Assert.That(result.Get(0)).IsFalse();
        await Assert.That(result.Get(1)).IsTrue();
        await Assert.That(result.Get(2)).IsTrue();
        await Assert.That(result.Get(3)).IsFalse();
        await Assert.That(result.PopCount()).IsEqualTo(2);
    }

    [Test]
    public async Task Or_ReturnsUnion()
    {
        var a = SmallBitSet<ulong>.Empty.Set(0).Set(1);
        var b = SmallBitSet<ulong>.Empty.Set(2).Set(3);

        var result = a.Or(b);

        await Assert.That(result.Get(0)).IsTrue();
        await Assert.That(result.Get(1)).IsTrue();
        await Assert.That(result.Get(2)).IsTrue();
        await Assert.That(result.Get(3)).IsTrue();
        await Assert.That(result.PopCount()).IsEqualTo(4);
    }

    [Test]
    public async Task Xor_ReturnsSymmetricDifference()
    {
        var a = SmallBitSet<ulong>.Empty.Set(0).Set(1).Set(2);
        var b = SmallBitSet<ulong>.Empty.Set(1).Set(2).Set(3);

        var result = a.Xor(b);

        await Assert.That(result.Get(0)).IsTrue();
        await Assert.That(result.Get(1)).IsFalse();
        await Assert.That(result.Get(2)).IsFalse();
        await Assert.That(result.Get(3)).IsTrue();
        await Assert.That(result.PopCount()).IsEqualTo(2);
    }

    [Test]
    public async Task AndNot_ReturnsDifference()
    {
        var a = SmallBitSet<ulong>.Empty.Set(0).Set(1).Set(2);
        var b = SmallBitSet<ulong>.Empty.Set(1).Set(2);

        var result = a.AndNot(b);

        await Assert.That(result.Get(0)).IsTrue();
        await Assert.That(result.Get(1)).IsFalse();
        await Assert.That(result.Get(2)).IsFalse();
        await Assert.That(result.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task ContainsAll_ReturnsTrueWhenSuperset()
    {
        var superset = SmallBitSet<ulong>.Empty.Set(0).Set(1).Set(2);
        var subset = SmallBitSet<ulong>.Empty.Set(0).Set(1);

        await Assert.That(superset.ContainsAll(subset)).IsTrue();
        await Assert.That(subset.ContainsAll(superset)).IsFalse();
    }

    [Test]
    public async Task ContainsAny_ReturnsTrueWhenOverlap()
    {
        var a = SmallBitSet<ulong>.Empty.Set(0).Set(1);
        var b = SmallBitSet<ulong>.Empty.Set(1).Set(2);
        var c = SmallBitSet<ulong>.Empty.Set(3).Set(4);

        await Assert.That(a.ContainsAny(b)).IsTrue();
        await Assert.That(a.ContainsAny(c)).IsFalse();
    }

    [Test]
    public async Task ContainsNone_ReturnsTrueWhenNoOverlap()
    {
        var a = SmallBitSet<ulong>.Empty.Set(0).Set(1);
        var b = SmallBitSet<ulong>.Empty.Set(2).Set(3);

        await Assert.That(a.ContainsNone(b)).IsTrue();
    }

    [Test]
    public async Task FirstSetBit_ReturnsLowestSetBit()
    {
        var bitset = SmallBitSet<ulong>.Empty.Set(5).Set(10).Set(2);

        await Assert.That(bitset.FirstSetBit()).IsEqualTo(2);
    }

    [Test]
    public async Task FirstSetBit_ReturnsNegativeOneWhenEmpty()
    {
        var bitset = SmallBitSet<ulong>.Empty;

        await Assert.That(bitset.FirstSetBit()).IsEqualTo(-1);
    }

    [Test]
    public async Task LastSetBit_ReturnsHighestSetBit()
    {
        var bitset = SmallBitSet<ulong>.Empty.Set(5).Set(10).Set(2);

        await Assert.That(bitset.LastSetBit()).IsEqualTo(10);
    }

    [Test]
    public async Task LastSetBit_ReturnsNegativeOneWhenEmpty()
    {
        var bitset = SmallBitSet<ulong>.Empty;

        await Assert.That(bitset.LastSetBit()).IsEqualTo(-1);
    }

    [Test]
    public async Task Methods_WorkCorrectly()
    {
        var a = SmallBitSet<ulong>.Empty.Set(0).Set(1);
        var b = SmallBitSet<ulong>.Empty.Set(1).Set(2);

        var andResult = a.And(b);
        var orResult = a.Or(b);
        var xorResult = a.Xor(b);

        await Assert.That(andResult.PopCount()).IsEqualTo(1);
        await Assert.That(orResult.PopCount()).IsEqualTo(3);
        await Assert.That(xorResult.PopCount()).IsEqualTo(2);
    }

    [Test]
    public async Task Equals_ReturnsTrueForEqualBitsets()
    {
        var a = SmallBitSet<ulong>.Empty.Set(0).Set(1);
        var b = SmallBitSet<ulong>.Empty.Set(0).Set(1);

        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task Equals_ReturnsFalseForDifferentBitsets()
    {
        var a = SmallBitSet<ulong>.Empty.Set(0).Set(1);
        var b = SmallBitSet<ulong>.Empty.Set(0).Set(2);

        await Assert.That(a.Equals(b)).IsFalse();
        await Assert.That(a != b).IsTrue();
    }

    [Test]
    public async Task GetHashCode_IsSameForEqualBitsets()
    {
        var a = SmallBitSet<ulong>.Empty.Set(0).Set(1);
        var b = SmallBitSet<ulong>.Empty.Set(0).Set(1);

        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task Capacity_MatchesStorageSize()
    {
        await Assert.That(SmallBitSet<ulong>.Capacity).IsEqualTo(64);
        await Assert.That(ImmutableBitSet<Bit128>.Capacity).IsEqualTo(128);
        await Assert.That(ImmutableBitSet<Bit256>.Capacity).IsEqualTo(256);
        await Assert.That(ImmutableBitSet<Bit512>.Capacity).IsEqualTo(512);
        await Assert.That(ImmutableBitSet<Bit1024>.Capacity).IsEqualTo(1024);
    }

    [Test]
    public async Task ForEach_IteratesSetBits()
    {
        var bitset = SmallBitSet<ulong>.Empty.Set(0).Set(5).Set(10).Set(63);
        var indices = new List<int>();
        var action = new CollectIndicesAction(indices);

        bitset.ForEach(ref action);

        await Assert.That(indices.Count).IsEqualTo(4);
        await Assert.That(indices[0]).IsEqualTo(0);
        await Assert.That(indices[1]).IsEqualTo(5);
        await Assert.That(indices[2]).IsEqualTo(10);
        await Assert.That(indices[3]).IsEqualTo(63);
    }

    private struct CollectIndicesAction(List<int> indices) : IBitAction
    {
        public void Invoke(int bitIndex) => indices.Add(bitIndex);
    }

    [Test]
    public async Task Bit128_WorksAcrossMultipleBuckets()
    {
        var bitset = ImmutableBitSet<Bit128>.Empty.Set(0).Set(64).Set(127);

        await Assert.That(bitset.Get(0)).IsTrue();
        await Assert.That(bitset.Get(64)).IsTrue();
        await Assert.That(bitset.Get(127)).IsTrue();
        await Assert.That(bitset.PopCount()).IsEqualTo(3);
    }

    [Test]
    public async Task ToString_IncludesPopCount()
    {
        var bitset = SmallBitSet<ulong>.Empty.Set(0).Set(1).Set(2);
        var str = bitset.ToString();

        await Assert.That(str).Contains("3 bits set");
    }
}
