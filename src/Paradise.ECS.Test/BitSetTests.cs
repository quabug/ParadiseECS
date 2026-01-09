namespace Paradise.ECS.Test;

public abstract class BitSetTests<TBits> where TBits : unmanaged, IStorage
{
    protected abstract int ExpectedCapacity { get; }
    protected abstract int[] GetBoundaryIndices();

    [Test]
    public async Task Capacity_ReturnsExpectedValue()
    {
        await Assert.That(BitSet<TBits>.Capacity).IsEqualTo(ExpectedCapacity);
    }

    [Test]
    public async Task Empty_IsEmpty()
    {
        var empty = BitSet<TBits>.Empty;
        await Assert.That(empty.IsEmpty).IsTrue();
        await Assert.That(empty.PopCount()).IsEqualTo(0);
    }

    [Test]
    public async Task Set_AndGet_WorksForBoundaryIndices()
    {
        foreach (var index in GetBoundaryIndices())
        {
            var bitset = BitSet<TBits>.Empty.Set(index);
            await Assert.That(bitset.Get(index)).IsTrue();
            await Assert.That(bitset.PopCount()).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Set_OutOfRange_ReturnsUnchanged()
    {
        var bitset = BitSet<TBits>.Empty;
        var result = bitset.Set(ExpectedCapacity);
        await Assert.That(result.IsEmpty).IsTrue();
    }

    [Test]
    public async Task Get_OutOfRange_ReturnsFalse()
    {
        var bitset = BitSet<TBits>.Empty.Set(0);
        await Assert.That(bitset.Get(ExpectedCapacity)).IsFalse();
        await Assert.That(bitset.Get(-1)).IsFalse();
    }

    [Test]
    public async Task Clear_RemovesBit()
    {
        var bitset = BitSet<TBits>.Empty.Set(10).Set(20);
        var cleared = bitset.Clear(10);
        await Assert.That(cleared.Get(10)).IsFalse();
        await Assert.That(cleared.Get(20)).IsTrue();
        await Assert.That(cleared.PopCount()).IsEqualTo(1);
    }

    [Test]
    public async Task And_ReturnsIntersection()
    {
        var a = BitSet<TBits>.Empty.Set(1).Set(2).Set(3);
        var b = BitSet<TBits>.Empty.Set(2).Set(3).Set(4);
        var result = a.And(b);
        await Assert.That(result.Get(1)).IsFalse();
        await Assert.That(result.Get(2)).IsTrue();
        await Assert.That(result.Get(3)).IsTrue();
        await Assert.That(result.Get(4)).IsFalse();
    }

    [Test]
    public async Task Or_ReturnsUnion()
    {
        var a = BitSet<TBits>.Empty.Set(1).Set(2);
        var b = BitSet<TBits>.Empty.Set(2).Set(3);
        var result = a.Or(b);
        await Assert.That(result.Get(1)).IsTrue();
        await Assert.That(result.Get(2)).IsTrue();
        await Assert.That(result.Get(3)).IsTrue();
    }

    [Test]
    public async Task AndNot_ReturnsDifference()
    {
        var a = BitSet<TBits>.Empty.Set(1).Set(2).Set(3);
        var b = BitSet<TBits>.Empty.Set(2);
        var result = a.AndNot(b);
        await Assert.That(result.Get(1)).IsTrue();
        await Assert.That(result.Get(2)).IsFalse();
        await Assert.That(result.Get(3)).IsTrue();
    }

    [Test]
    public async Task ContainsAll_ReturnsTrueWhenSupersetOrEqual()
    {
        var a = BitSet<TBits>.Empty.Set(1).Set(2).Set(3);
        var b = BitSet<TBits>.Empty.Set(1).Set(2);
        await Assert.That(a.ContainsAll(b)).IsTrue();
        await Assert.That(a.ContainsAll(a)).IsTrue();
        await Assert.That(b.ContainsAll(a)).IsFalse();
    }

    [Test]
    public async Task ContainsAny_ReturnsTrueWhenOverlap()
    {
        var a = BitSet<TBits>.Empty.Set(1).Set(2);
        var b = BitSet<TBits>.Empty.Set(2).Set(3);
        var c = BitSet<TBits>.Empty.Set(4).Set(5);
        await Assert.That(a.ContainsAny(b)).IsTrue();
        await Assert.That(a.ContainsAny(c)).IsFalse();
    }

    [Test]
    public async Task ContainsNone_ReturnsTrueWhenNoOverlap()
    {
        var a = BitSet<TBits>.Empty.Set(1).Set(2);
        var b = BitSet<TBits>.Empty.Set(3).Set(4);
        var c = BitSet<TBits>.Empty.Set(2).Set(5);
        await Assert.That(a.ContainsNone(b)).IsTrue();
        await Assert.That(a.ContainsNone(c)).IsFalse();
    }

    [Test]
    public async Task PopCount_CountsSetBits()
    {
        var bitset = BitSet<TBits>.Empty;
        foreach (var index in GetBoundaryIndices())
        {
            bitset = bitset.Set(index);
        }
        await Assert.That(bitset.PopCount()).IsEqualTo(GetBoundaryIndices().Length);
    }

    [Test]
    public async Task Equality_Works()
    {
        var a = BitSet<TBits>.Empty.Set(1).Set(10);
        var b = BitSet<TBits>.Empty.Set(1).Set(10);
        var c = BitSet<TBits>.Empty.Set(1).Set(11);
        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
        await Assert.That(a != c).IsTrue();
    }

    [Test]
    public async Task ContainsAll_AcrossBuckets()
    {
        if (ExpectedCapacity < 128) return;

        var full = BitSet<TBits>.Empty.Set(0).Set(64);
        var partial = BitSet<TBits>.Empty.Set(64);
        await Assert.That(full.ContainsAll(partial)).IsTrue();
        await Assert.That(partial.ContainsAll(full)).IsFalse();
    }

    [Test]
    public async Task ContainsAny_AcrossBuckets()
    {
        if (ExpectedCapacity < 128) return;

        var a = BitSet<TBits>.Empty.Set(0);
        var b = BitSet<TBits>.Empty.Set(64);
        var c = BitSet<TBits>.Empty.Set(0).Set(64);
        await Assert.That(a.ContainsAny(b)).IsFalse();
        await Assert.That(a.ContainsAny(c)).IsTrue();
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

[InheritsTests]
public class BitSet2048Tests : BitSetTests<Bit2048>
{
    protected override int ExpectedCapacity => 2048;
    protected override int[] GetBoundaryIndices() => [0, 63, 64, 511, 512, 1023, 1024, 1535, 1536, 2047];
}
