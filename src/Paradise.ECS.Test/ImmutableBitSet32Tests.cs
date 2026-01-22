namespace Paradise.ECS.Test;

/// <summary>
/// Tests for <see cref="ImmutableBitSet32"/>.
/// </summary>
public sealed class ImmutableBitSet32Tests
{
    [Test]
    public async Task Capacity_Is32()
    {
        await Assert.That(ImmutableBitSet32.Capacity).IsEqualTo(32);
    }

    [Test]
    public async Task Empty_HasAllBitsClear()
    {
        var bitset = ImmutableBitSet32.Empty;

        await Assert.That(bitset.IsEmpty).IsTrue();
        await Assert.That(bitset.PopCount()).IsEqualTo(0);
    }

    [Test]
    public async Task Empty_IsDefault()
    {
        var bitset = default(ImmutableBitSet32);

        await Assert.That(bitset).IsEqualTo(ImmutableBitSet32.Empty);
        await Assert.That(bitset.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Set_SetsBit()
    {
        var bitset = ImmutableBitSet32.Empty.Set(5);

        await Assert.That(bitset.Get(5)).IsTrue();
        await Assert.That(bitset.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task Set_IsImmutable()
    {
        var original = ImmutableBitSet32.Empty;
        var modified = original.Set(5);

        await Assert.That(original.IsEmpty).IsTrue();
        await Assert.That(modified.Get(5)).IsTrue();
    }

    [Test]
    public async Task Set_MultipleBits()
    {
        var bitset = ImmutableBitSet32.Empty.Set(0).Set(15).Set(31);

        await Assert.That(bitset.Get(0)).IsTrue();
        await Assert.That(bitset.Get(15)).IsTrue();
        await Assert.That(bitset.Get(31)).IsTrue();
        await Assert.That(bitset.PopCount()).IsEqualTo(3);
    }

    [Test]
    public async Task Set_ThrowsForNegativeIndex()
    {
        var bitset = ImmutableBitSet32.Empty;

        await Assert.That(() => bitset.Set(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Set_ThrowsForIndexAtCapacity()
    {
        var bitset = ImmutableBitSet32.Empty;

        await Assert.That(() => bitset.Set(32)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Set_ThrowsForIndexAboveCapacity()
    {
        var bitset = ImmutableBitSet32.Empty;

        await Assert.That(() => bitset.Set(100)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Clear_ClearsBit()
    {
        var bitset = ImmutableBitSet32.Empty.Set(5).Clear(5);

        await Assert.That(bitset.Get(5)).IsFalse();
        await Assert.That(bitset.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Clear_IsImmutable()
    {
        var original = ImmutableBitSet32.Empty.Set(5);
        var modified = original.Clear(5);

        await Assert.That(original.Get(5)).IsTrue();
        await Assert.That(modified.Get(5)).IsFalse();
    }

    [Test]
    public async Task Clear_ThrowsForNegativeIndex()
    {
        var bitset = ImmutableBitSet32.Empty;

        await Assert.That(() => bitset.Clear(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Clear_ThrowsForIndexAtCapacity()
    {
        var bitset = ImmutableBitSet32.Empty;

        await Assert.That(() => bitset.Clear(32)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Get_ReturnsFalseForUnsetBit()
    {
        var bitset = ImmutableBitSet32.Empty;

        await Assert.That(bitset.Get(0)).IsFalse();
        await Assert.That(bitset.Get(31)).IsFalse();
    }

    [Test]
    public async Task Get_ThrowsForNegativeIndex()
    {
        var bitset = ImmutableBitSet32.Empty;

        await Assert.That(() => bitset.Get(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Get_ThrowsForIndexAtCapacity()
    {
        var bitset = ImmutableBitSet32.Empty;

        await Assert.That(() => bitset.Get(32)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task And_ReturnsIntersection()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1).Set(2);
        var b = ImmutableBitSet32.Empty.Set(1).Set(2).Set(3);

        var result = a.And(b);

        await Assert.That(result.Get(0)).IsFalse();
        await Assert.That(result.Get(1)).IsTrue();
        await Assert.That(result.Get(2)).IsTrue();
        await Assert.That(result.Get(3)).IsFalse();
        await Assert.That(result.PopCount()).IsEqualTo(2);
    }

    [Test]
    public async Task And_WithEmpty_ReturnsEmpty()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty;

        var result = a.And(b);

        await Assert.That(result.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Or_ReturnsUnion()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty.Set(2).Set(3);

        var result = a.Or(b);

        await Assert.That(result.Get(0)).IsTrue();
        await Assert.That(result.Get(1)).IsTrue();
        await Assert.That(result.Get(2)).IsTrue();
        await Assert.That(result.Get(3)).IsTrue();
        await Assert.That(result.PopCount()).IsEqualTo(4);
    }

    [Test]
    public async Task Or_WithEmpty_ReturnsSelf()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty;

        var result = a.Or(b);

        await Assert.That(result).IsEqualTo(a);
    }

    [Test]
    public async Task Xor_ReturnsSymmetricDifference()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1).Set(2);
        var b = ImmutableBitSet32.Empty.Set(1).Set(2).Set(3);

        var result = a.Xor(b);

        await Assert.That(result.Get(0)).IsTrue();
        await Assert.That(result.Get(1)).IsFalse();
        await Assert.That(result.Get(2)).IsFalse();
        await Assert.That(result.Get(3)).IsTrue();
        await Assert.That(result.PopCount()).IsEqualTo(2);
    }

    [Test]
    public async Task Xor_WithSelf_ReturnsEmpty()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1).Set(2);

        var result = a.Xor(a);

        await Assert.That(result.IsEmpty).IsTrue();
    }

    [Test]
    public async Task AndNot_ReturnsDifference()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1).Set(2);
        var b = ImmutableBitSet32.Empty.Set(1).Set(2);

        var result = a.AndNot(b);

        await Assert.That(result.Get(0)).IsTrue();
        await Assert.That(result.Get(1)).IsFalse();
        await Assert.That(result.Get(2)).IsFalse();
        await Assert.That(result.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task AndNot_WithEmpty_ReturnsSelf()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty;

        var result = a.AndNot(b);

        await Assert.That(result).IsEqualTo(a);
    }

    [Test]
    public async Task ContainsAll_ReturnsTrueWhenSuperset()
    {
        var superset = ImmutableBitSet32.Empty.Set(0).Set(1).Set(2);
        var subset = ImmutableBitSet32.Empty.Set(0).Set(1);

        await Assert.That(superset.ContainsAll(subset)).IsTrue();
        await Assert.That(subset.ContainsAll(superset)).IsFalse();
    }

    [Test]
    public async Task ContainsAll_ReturnsTrueForEmpty()
    {
        var bitset = ImmutableBitSet32.Empty.Set(0).Set(1);
        var empty = ImmutableBitSet32.Empty;

        await Assert.That(bitset.ContainsAll(empty)).IsTrue();
        await Assert.That(empty.ContainsAll(empty)).IsTrue();
    }

    [Test]
    public async Task ContainsAll_ReturnsTrueForSelf()
    {
        var bitset = ImmutableBitSet32.Empty.Set(0).Set(1);

        await Assert.That(bitset.ContainsAll(bitset)).IsTrue();
    }

    [Test]
    public async Task ContainsAny_ReturnsTrueWhenOverlap()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty.Set(1).Set(2);
        var c = ImmutableBitSet32.Empty.Set(3).Set(4);

        await Assert.That(a.ContainsAny(b)).IsTrue();
        await Assert.That(a.ContainsAny(c)).IsFalse();
    }

    [Test]
    public async Task ContainsAny_ReturnsFalseForEmpty()
    {
        var bitset = ImmutableBitSet32.Empty.Set(0).Set(1);
        var empty = ImmutableBitSet32.Empty;

        await Assert.That(bitset.ContainsAny(empty)).IsFalse();
        await Assert.That(empty.ContainsAny(bitset)).IsFalse();
    }

    [Test]
    public async Task ContainsNone_ReturnsTrueWhenNoOverlap()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty.Set(2).Set(3);

        await Assert.That(a.ContainsNone(b)).IsTrue();
    }

    [Test]
    public async Task ContainsNone_ReturnsFalseWhenOverlap()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty.Set(1).Set(2);

        await Assert.That(a.ContainsNone(b)).IsFalse();
    }

    [Test]
    public async Task ContainsNone_ReturnsTrueForEmpty()
    {
        var bitset = ImmutableBitSet32.Empty.Set(0).Set(1);
        var empty = ImmutableBitSet32.Empty;

        await Assert.That(bitset.ContainsNone(empty)).IsTrue();
    }

    [Test]
    public async Task PopCount_ReturnsNumberOfSetBits()
    {
        var empty = ImmutableBitSet32.Empty;
        var one = ImmutableBitSet32.Empty.Set(5);
        var five = ImmutableBitSet32.Empty.Set(0).Set(1).Set(2).Set(15).Set(31);

        await Assert.That(empty.PopCount()).IsEqualTo(0);
        await Assert.That(one.PopCount()).IsEqualTo(1);
        await Assert.That(five.PopCount()).IsEqualTo(5);
    }

    [Test]
    public async Task FirstSetBit_ReturnsLowestSetBit()
    {
        var bitset = ImmutableBitSet32.Empty.Set(5).Set(10).Set(2);

        await Assert.That(bitset.FirstSetBit()).IsEqualTo(2);
    }

    [Test]
    public async Task FirstSetBit_ReturnsNegativeOneWhenEmpty()
    {
        var bitset = ImmutableBitSet32.Empty;

        await Assert.That(bitset.FirstSetBit()).IsEqualTo(-1);
    }

    [Test]
    public async Task FirstSetBit_ReturnsBit0WhenSet()
    {
        var bitset = ImmutableBitSet32.Empty.Set(0);

        await Assert.That(bitset.FirstSetBit()).IsEqualTo(0);
    }

    [Test]
    public async Task FirstSetBit_ReturnsBit31WhenOnlyHighestSet()
    {
        var bitset = ImmutableBitSet32.Empty.Set(31);

        await Assert.That(bitset.FirstSetBit()).IsEqualTo(31);
    }

    [Test]
    public async Task LastSetBit_ReturnsHighestSetBit()
    {
        var bitset = ImmutableBitSet32.Empty.Set(5).Set(10).Set(2);

        await Assert.That(bitset.LastSetBit()).IsEqualTo(10);
    }

    [Test]
    public async Task LastSetBit_ReturnsNegativeOneWhenEmpty()
    {
        var bitset = ImmutableBitSet32.Empty;

        await Assert.That(bitset.LastSetBit()).IsEqualTo(-1);
    }

    [Test]
    public async Task LastSetBit_ReturnsBit31WhenSet()
    {
        var bitset = ImmutableBitSet32.Empty.Set(31);

        await Assert.That(bitset.LastSetBit()).IsEqualTo(31);
    }

    [Test]
    public async Task LastSetBit_ReturnsBit0WhenOnlyLowestSet()
    {
        var bitset = ImmutableBitSet32.Empty.Set(0);

        await Assert.That(bitset.LastSetBit()).IsEqualTo(0);
    }

    [Test]
    public async Task Operators_And_WorksCorrectly()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty.Set(1).Set(2);

        var result = a & b;

        await Assert.That(result.PopCount()).IsEqualTo(1);
        await Assert.That(result.Get(1)).IsTrue();
    }

    [Test]
    public async Task Operators_Or_WorksCorrectly()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty.Set(1).Set(2);

        var result = a | b;

        await Assert.That(result.PopCount()).IsEqualTo(3);
    }

    [Test]
    public async Task Operators_Xor_WorksCorrectly()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty.Set(1).Set(2);

        var result = a ^ b;

        await Assert.That(result.PopCount()).IsEqualTo(2);
        await Assert.That(result.Get(0)).IsTrue();
        await Assert.That(result.Get(2)).IsTrue();
    }

    [Test]
    public async Task Equals_ReturnsTrueForEqualBitsets()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty.Set(0).Set(1);

        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a == b).IsTrue();
    }

    [Test]
    public async Task Equals_ReturnsFalseForDifferentBitsets()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty.Set(0).Set(2);

        await Assert.That(a.Equals(b)).IsFalse();
        await Assert.That(a != b).IsTrue();
    }

    [Test]
    public async Task GetHashCode_IsSameForEqualBitsets()
    {
        var a = ImmutableBitSet32.Empty.Set(0).Set(1);
        var b = ImmutableBitSet32.Empty.Set(0).Set(1);

        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task GetHashCode_IsDifferentForDifferentBitsets()
    {
        var a = ImmutableBitSet32.Empty.Set(0);
        var b = ImmutableBitSet32.Empty.Set(1);

        await Assert.That(a.GetHashCode()).IsNotEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task Enumerator_IteratesSetBits()
    {
        var bitset = ImmutableBitSet32.Empty.Set(0).Set(5).Set(10).Set(31);
        var indices = new List<int>();

        foreach (var index in bitset)
        {
            indices.Add(index);
        }

        await Assert.That(indices.Count).IsEqualTo(4);
        await Assert.That(indices[0]).IsEqualTo(0);
        await Assert.That(indices[1]).IsEqualTo(5);
        await Assert.That(indices[2]).IsEqualTo(10);
        await Assert.That(indices[3]).IsEqualTo(31);
    }

    [Test]
    public async Task Enumerator_EmptyBitset_IteratesNothing()
    {
        var bitset = ImmutableBitSet32.Empty;
        var indices = new List<int>();

        foreach (var index in bitset)
        {
            indices.Add(index);
        }

        await Assert.That(indices.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ToString_IncludesPopCount()
    {
        var bitset = ImmutableBitSet32.Empty.Set(0).Set(1).Set(2);
        var str = bitset.ToString();

        await Assert.That(str).Contains("3 bits set");
    }

    [Test]
    public async Task ToString_IncludesHexValue()
    {
        var bitset = ImmutableBitSet32.Empty.Set(0);
        var str = bitset.ToString();

        await Assert.That(str).Contains("0x00000001");
    }

    [Test]
    public async Task BoundaryBits_CanSetAndGetBit0()
    {
        var bitset = ImmutableBitSet32.Empty.Set(0);

        await Assert.That(bitset.Get(0)).IsTrue();
        await Assert.That(bitset.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task BoundaryBits_CanSetAndGetBit31()
    {
        var bitset = ImmutableBitSet32.Empty.Set(31);

        await Assert.That(bitset.Get(31)).IsTrue();
        await Assert.That(bitset.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task AllBitsSet_HasCorrectPopCount()
    {
        var bitset = ImmutableBitSet32.Empty;
        for (int i = 0; i < 32; i++)
        {
            bitset = bitset.Set(i);
        }

        await Assert.That(bitset.PopCount()).IsEqualTo(32);
        await Assert.That(bitset.FirstSetBit()).IsEqualTo(0);
        await Assert.That(bitset.LastSetBit()).IsEqualTo(31);
    }
}
