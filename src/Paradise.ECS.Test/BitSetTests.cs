using System.Runtime.CompilerServices;

namespace Paradise.ECS.Test;

// Invalid storage type for testing validation (size not multiple of sizeof(ulong))
[InlineArray(1)]
public struct InvalidBit : IStorage
{
    private int _element0; // 4 bytes, not a multiple of 8
}

public abstract class BitSetTests<TBits> where TBits : unmanaged, IStorage
{
    protected abstract int ExpectedCapacity { get; }
    protected abstract int[] GetBoundaryIndices();

    [Test]
    public async Task Capacity_ReturnsExpectedValue()
    {
        await Assert.That(ImmutableBitSet<TBits>.Capacity).IsEqualTo(ExpectedCapacity);
    }

    [Test]
    public async Task Empty_IsEmpty()
    {
        var empty = ImmutableBitSet<TBits>.Empty;
        await Assert.That(empty.IsEmpty).IsTrue();
        await Assert.That(empty.PopCount()).IsEqualTo(0);
    }

    [Test]
    public async Task Set_AndGet_WorksForBoundaryIndices()
    {
        foreach (var index in GetBoundaryIndices())
        {
            var bitset = ImmutableBitSet<TBits>.Empty.Set(index);
            await Assert.That(bitset.Get(index)).IsTrue();
            await Assert.That(bitset.PopCount()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Set_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var bitset = ImmutableBitSet<TBits>.Empty;
        await Assert.That(() => bitset.Set(ExpectedCapacity)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => bitset.Set(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Get_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var bitset = ImmutableBitSet<TBits>.Empty.Set(0);
        await Assert.That(() => bitset.Get(ExpectedCapacity)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => bitset.Get(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Clear_OutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var bitset = ImmutableBitSet<TBits>.Empty.Set(0);
        await Assert.That(() => bitset.Clear(ExpectedCapacity)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => bitset.Clear(-1)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Clear_RemovesBit()
    {
        var bitset = ImmutableBitSet<TBits>.Empty.Set(10).Set(20);
        var cleared = bitset.Clear(10);
        await Assert.That(cleared.Get(10)).IsFalse();
        await Assert.That(cleared.Get(20)).IsTrue();
        await Assert.That(cleared.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task And_ReturnsIntersection()
    {
        var a = ImmutableBitSet<TBits>.Empty.Set(1).Set(2).Set(3);
        var b = ImmutableBitSet<TBits>.Empty.Set(2).Set(3).Set(4);
        var result = a.And(b);
        await Assert.That(result.Get(1)).IsFalse();
        await Assert.That(result.Get(2)).IsTrue();
        await Assert.That(result.Get(3)).IsTrue();
        await Assert.That(result.Get(4)).IsFalse();
    }

    [Test]
    public async Task Or_ReturnsUnion()
    {
        var a = ImmutableBitSet<TBits>.Empty.Set(1).Set(2);
        var b = ImmutableBitSet<TBits>.Empty.Set(2).Set(3);
        var result = a.Or(b);
        await Assert.That(result.Get(1)).IsTrue();
        await Assert.That(result.Get(2)).IsTrue();
        await Assert.That(result.Get(3)).IsTrue();
    }

    [Test]
    public async Task AndNot_ReturnsDifference()
    {
        var a = ImmutableBitSet<TBits>.Empty.Set(1).Set(2).Set(3);
        var b = ImmutableBitSet<TBits>.Empty.Set(2);
        var result = a.AndNot(b);
        await Assert.That(result.Get(1)).IsTrue();
        await Assert.That(result.Get(2)).IsFalse();
        await Assert.That(result.Get(3)).IsTrue();
    }

    [Test]
    public async Task Xor_ReturnsSymmetricDifference()
    {
        var a = ImmutableBitSet<TBits>.Empty.Set(1).Set(2);
        var b = ImmutableBitSet<TBits>.Empty.Set(2).Set(3);
        var result = a.Xor(b);
        await Assert.That(result.Get(1)).IsTrue();
        await Assert.That(result.Get(2)).IsFalse();
        await Assert.That(result.Get(3)).IsTrue();
    }

    [Test]
    public async Task BitwiseAndOperator_EqualsAndMethod()
    {
        var a = ImmutableBitSet<TBits>.Empty.Set(1).Set(2).Set(3);
        var b = ImmutableBitSet<TBits>.Empty.Set(2).Set(3).Set(4);
        await Assert.That(a & b).IsEqualTo(a.And(b));
    }

    [Test]
    public async Task BitwiseOrOperator_EqualsOrMethod()
    {
        var a = ImmutableBitSet<TBits>.Empty.Set(1).Set(2);
        var b = ImmutableBitSet<TBits>.Empty.Set(2).Set(3);
        await Assert.That(a | b).IsEqualTo(a.Or(b));
    }

    [Test]
    public async Task BitwiseXorOperator_EqualsXorMethod()
    {
        var a = ImmutableBitSet<TBits>.Empty.Set(1).Set(2);
        var b = ImmutableBitSet<TBits>.Empty.Set(2).Set(3);
        await Assert.That(a ^ b).IsEqualTo(a.Xor(b));
    }

    [Test]
    public async Task ContainsAll_ReturnsTrueWhenSupersetOrEqual()
    {
        var a = ImmutableBitSet<TBits>.Empty.Set(1).Set(2).Set(3);
        var b = ImmutableBitSet<TBits>.Empty.Set(1).Set(2);
        await Assert.That(a.ContainsAll(b)).IsTrue();
        await Assert.That(a.ContainsAll(a)).IsTrue();
        await Assert.That(b.ContainsAll(a)).IsFalse();
    }

    [Test]
    public async Task ContainsAny_ReturnsTrueWhenOverlap()
    {
        var a = ImmutableBitSet<TBits>.Empty.Set(1).Set(2);
        var b = ImmutableBitSet<TBits>.Empty.Set(2).Set(3);
        var c = ImmutableBitSet<TBits>.Empty.Set(4).Set(5);
        await Assert.That(a.ContainsAny(b)).IsTrue();
        await Assert.That(a.ContainsAny(c)).IsFalse();
    }

    [Test]
    public async Task ContainsNone_ReturnsTrueWhenNoOverlap()
    {
        var a = ImmutableBitSet<TBits>.Empty.Set(1).Set(2);
        var b = ImmutableBitSet<TBits>.Empty.Set(3).Set(4);
        var c = ImmutableBitSet<TBits>.Empty.Set(2).Set(5);
        await Assert.That(a.ContainsNone(b)).IsTrue();
        await Assert.That(a.ContainsNone(c)).IsFalse();
    }

    [Test]
    public async Task PopCount_CountsSetBits()
    {
        var bitset = ImmutableBitSet<TBits>.Empty;
        foreach (var index in GetBoundaryIndices())
        {
            bitset = bitset.Set(index);
        }
        await Assert.That(bitset.PopCount()).IsEqualTo(GetBoundaryIndices().Length);
    }

    [Test]
    public async Task Equality_Works()
    {
        var a = ImmutableBitSet<TBits>.Empty.Set(1).Set(10);
        var b = ImmutableBitSet<TBits>.Empty.Set(1).Set(10);
        var c = ImmutableBitSet<TBits>.Empty.Set(1).Set(11);
        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
        await Assert.That(a != c).IsTrue();
    }

    [Test]
    public async Task ContainsAll_AcrossBuckets()
    {
        if (ExpectedCapacity < 128) return;

        var full = ImmutableBitSet<TBits>.Empty.Set(0).Set(64);
        var partial = ImmutableBitSet<TBits>.Empty.Set(64);
        await Assert.That(full.ContainsAll(partial)).IsTrue();
        await Assert.That(partial.ContainsAll(full)).IsFalse();
    }

    [Test]
    public async Task ContainsAny_AcrossBuckets()
    {
        if (ExpectedCapacity < 128) return;

        var a = ImmutableBitSet<TBits>.Empty.Set(0);
        var b = ImmutableBitSet<TBits>.Empty.Set(64);
        var c = ImmutableBitSet<TBits>.Empty.Set(0).Set(64);
        await Assert.That(a.ContainsAny(b)).IsFalse();
        await Assert.That(a.ContainsAny(c)).IsTrue();
    }

    [Test]
    public async Task FirstSetBit_EmptyBitset_ReturnsNegativeOne()
    {
        var empty = ImmutableBitSet<TBits>.Empty;
        await Assert.That(empty.FirstSetBit()).IsEqualTo(-1);
    }

    [Test]
    public async Task FirstSetBit_SingleBit_ReturnsCorrectIndex()
    {
        foreach (var index in GetBoundaryIndices())
        {
            var bitset = ImmutableBitSet<TBits>.Empty.Set(index);
            await Assert.That(bitset.FirstSetBit()).IsEqualTo(index);
        }
    }

    [Test]
    public async Task FirstSetBit_MultipleBits_ReturnsLowest()
    {
        var indices = GetBoundaryIndices();
        if (indices.Length < 2) return;

        var bitset = ImmutableBitSet<TBits>.Empty;
        foreach (var index in indices)
        {
            bitset = bitset.Set(index);
        }
        await Assert.That(bitset.FirstSetBit()).IsEqualTo(indices.Min());
    }

    [Test]
    public async Task LastSetBit_EmptyBitset_ReturnsNegativeOne()
    {
        var empty = ImmutableBitSet<TBits>.Empty;
        await Assert.That(empty.LastSetBit()).IsEqualTo(-1);
    }

    [Test]
    public async Task LastSetBit_SingleBit_ReturnsCorrectIndex()
    {
        foreach (var index in GetBoundaryIndices())
        {
            var bitset = ImmutableBitSet<TBits>.Empty.Set(index);
            await Assert.That(bitset.LastSetBit()).IsEqualTo(index);
        }
    }

    [Test]
    public async Task LastSetBit_MultipleBits_ReturnsHighest()
    {
        var indices = GetBoundaryIndices();
        if (indices.Length < 2) return;

        var bitset = ImmutableBitSet<TBits>.Empty;
        foreach (var index in indices)
        {
            bitset = bitset.Set(index);
        }
        await Assert.That(bitset.LastSetBit()).IsEqualTo(indices.Max());
    }

    [Test]
    public async Task GetEnumerator_EmptyBitset_YieldsNothing()
    {
        var empty = ImmutableBitSet<TBits>.Empty;
        var indices = new List<int>();
        foreach (int index in empty)
        {
            indices.Add(index);
        }
        await Assert.That(indices.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetEnumerator_SingleBit_YieldsSingleIndex()
    {
        foreach (var expectedIndex in GetBoundaryIndices())
        {
            var bitset = ImmutableBitSet<TBits>.Empty.Set(expectedIndex);
            var indices = new List<int>();
            foreach (int index in bitset)
            {
                indices.Add(index);
            }
            await Assert.That(indices.Count).IsEqualTo(1);
            await Assert.That(indices[0]).IsEqualTo(expectedIndex);
        }
    }

    [Test]
    public async Task GetEnumerator_MultipleBits_YieldsAllIndicesInOrder()
    {
        var expectedIndices = GetBoundaryIndices();
        var bitset = ImmutableBitSet<TBits>.Empty;
        foreach (var index in expectedIndices)
        {
            bitset = bitset.Set(index);
        }

        var actualIndices = new List<int>();
        foreach (int index in bitset)
        {
            actualIndices.Add(index);
        }

        // Should yield all indices in ascending order
        var sortedExpected = expectedIndices.OrderBy(x => x).ToArray();
        await Assert.That(actualIndices.Count).IsEqualTo(sortedExpected.Length);
        for (int i = 0; i < sortedExpected.Length; i++)
        {
            await Assert.That(actualIndices[i]).IsEqualTo(sortedExpected[i]);
        }
    }

    [Test]
    public async Task GetEnumerator_ConsecutiveBits_YieldsAllIndices()
    {
        // Test consecutive bits within a single ulong bucket
        var bitset = ImmutableBitSet<TBits>.Empty.Set(0).Set(1).Set(2).Set(3);
        var indices = new List<int>();
        foreach (int index in bitset)
        {
            indices.Add(index);
        }

        await Assert.That(indices.Count).IsEqualTo(4);
        await Assert.That(indices[0]).IsEqualTo(0);
        await Assert.That(indices[1]).IsEqualTo(1);
        await Assert.That(indices[2]).IsEqualTo(2);
        await Assert.That(indices[3]).IsEqualTo(3);
    }
}

[InheritsTests]
public class BitSet64Tests : BitSetTests<Bit64>
{
    protected override int ExpectedCapacity => 64;
    protected override int[] GetBoundaryIndices() => [0, 1, 31, 32, 62, 63];
}

[InheritsTests]
public class BitSet128Tests : BitSetTests<Bit128>
{
    protected override int ExpectedCapacity => 128;
    protected override int[] GetBoundaryIndices() => [0, 1, 63, 64, 65, 126, 127];
}

[InheritsTests]
public class BitSet256Tests : BitSetTests<Bit256>
{
    protected override int ExpectedCapacity => 256;
    protected override int[] GetBoundaryIndices() => [0, 63, 64, 127, 128, 191, 192, 255];
}

[InheritsTests]
public class BitSet512Tests : BitSetTests<Bit512>
{
    protected override int ExpectedCapacity => 512;
    protected override int[] GetBoundaryIndices() => [0, 63, 64, 127, 128, 255, 256, 383, 384, 511];
}

[InheritsTests]
public class BitSet1024Tests : BitSetTests<Bit1024>
{
    protected override int ExpectedCapacity => 1024;
    protected override int[] GetBoundaryIndices() => [0, 63, 64, 255, 256, 511, 512, 767, 768, 1023];
}

public class BitSetAdditionalTests
{
    [Test]
    public async Task GetHashCode_SameBitsets_ReturnSameHash()
    {
        var a = ImmutableBitSet<Bit64>.Empty.Set(1).Set(10).Set(63);
        var b = ImmutableBitSet<Bit64>.Empty.Set(1).Set(10).Set(63);
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task GetHashCode_DifferentBitsets_ReturnDifferentHash()
    {
        var a = ImmutableBitSet<Bit64>.Empty.Set(1);
        var b = ImmutableBitSet<Bit64>.Empty.Set(2);
        // Different bitsets should have different hash codes (in most cases)
        await Assert.That(a.GetHashCode()).IsNotEqualTo(b.GetHashCode());
    }

    [Test]
    public async Task ToString_ReturnsExpectedFormat()
    {
        var bitset = ImmutableBitSet<Bit64>.Empty.Set(1).Set(10).Set(20);
        var result = bitset.ToString();
        await Assert.That(result).Contains("Bit64");
        await Assert.That(result).Contains("3 bits set");
    }

    [Test]
    public async Task ToString_EmptyBitset_ShowsZeroBits()
    {
        var bitset = ImmutableBitSet<Bit128>.Empty;
        var result = bitset.ToString();
        await Assert.That(result).Contains("0 bits set");
    }

    [Test]
    public async Task InvalidStorage_ThrowsInvalidOperationException()
    {
        // Using an invalid storage type should throw during static initialization
        await Assert.That(() =>
        {
            // Access Capacity to trigger static initialization
            _ = ImmutableBitSet<InvalidBit>.Capacity;
        }).Throws<TypeInitializationException>();
    }
}
