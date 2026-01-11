using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Paradise.ECS;

namespace Paradise.ECS.Benchmarks;

/// <summary>
/// Helper extensions for BitArray operations matching ImmutableBitSet API.
/// Note: BitArray operations mutate in-place, so we clone for immutable semantics.
/// </summary>
public static class BitArrayExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BitArray SetBit(this BitArray array, int bit)
    {
        var clone = new BitArray(array);
        clone.Set(bit, true);
        return clone;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BitArray ClearBit(this BitArray array, int bit)
    {
        var clone = new BitArray(array);
        clone.Set(bit, false);
        return clone;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BitArray AndImmutable(this BitArray array, BitArray other)
    {
        var clone = new BitArray(array);
        clone.And(other);
        return clone;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BitArray OrImmutable(this BitArray array, BitArray other)
    {
        var clone = new BitArray(array);
        clone.Or(other);
        return clone;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BitArray XorImmutable(this BitArray array, BitArray other)
    {
        var clone = new BitArray(array);
        clone.Xor(other);
        return clone;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BitArray AndNotImmutable(this BitArray array, BitArray other)
    {
        var clone = new BitArray(array);
        var notOther = new BitArray(other).Not();
        clone.And(notOther);
        return clone;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsAll(this BitArray self, BitArray other)
    {
        for (int i = 0; i < self.Length; i++)
        {
            if (other[i] && !self[i])
                return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsNone(this BitArray self, BitArray other)
    {
        for (int i = 0; i < self.Length; i++)
        {
            if (self[i] && other[i])
                return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsAny(this BitArray self, BitArray other)
    {
        for (int i = 0; i < self.Length; i++)
        {
            if (self[i] && other[i])
                return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEmpty(this BitArray array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i]) return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopCount(this BitArray array)
    {
        int count = 0;
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i]) count++;
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FirstSetBit(this BitArray array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i]) return i;
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastSetBit(this BitArray array)
    {
        for (int i = array.Length - 1; i >= 0; i--)
        {
            if (array[i]) return i;
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool BitArrayEquals(this BitArray self, BitArray other)
    {
        if (self.Length != other.Length) return false;
        for (int i = 0; i < self.Length; i++)
        {
            if (self[i] != other[i]) return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int BitArrayGetHashCode(this BitArray array)
    {
        var hash = new HashCode();
        for (int i = 0; i < array.Length; i++)
        {
            hash.Add(array[i]);
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// Enumerates set bit indices (to match ImmutableBitSet.GetEnumerator behavior).
    /// </summary>
    public static IEnumerable<int> EnumerateSetBits(this BitArray array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i]) yield return i;
        }
    }
}

// ============================================================================
// Get/Set/Clear Benchmarks
// ============================================================================

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetGetBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit1024> _bit1024;

    private BitArray _bitArray64 = null!;
    private BitArray _bitArray256 = null!;
    private BitArray _bitArray1024 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(63);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180).Set(255);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720).Set(1023);

        _bitArray64 = new BitArray(64);
        _bitArray64.Set(0, true); _bitArray64.Set(10, true); _bitArray64.Set(20, true);
        _bitArray64.Set(30, true); _bitArray64.Set(63, true);

        _bitArray256 = new BitArray(256);
        _bitArray256.Set(0, true); _bitArray256.Set(60, true); _bitArray256.Set(120, true);
        _bitArray256.Set(180, true); _bitArray256.Set(255, true);

        _bitArray1024 = new BitArray(1024);
        _bitArray1024.Set(0, true); _bitArray1024.Set(240, true); _bitArray1024.Set(480, true);
        _bitArray1024.Set(720, true); _bitArray1024.Set(1023, true);
    }

    // === Get ===
    [Benchmark(Baseline = true)]
    public bool Bit64_Get() => _bit64.Get(30);

    [Benchmark]
    public bool BitArray64_Get() => _bitArray64.Get(30);

    [Benchmark]
    public bool Bit256_Get() => _bit256.Get(120);

    [Benchmark]
    public bool BitArray256_Get() => _bitArray256.Get(120);

    [Benchmark]
    public bool Bit1024_Get() => _bit1024.Get(480);

    [Benchmark]
    public bool BitArray1024_Get() => _bitArray1024.Get(480);
}

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetSetBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit1024> _bit1024;

    private BitArray _bitArray64 = null!;
    private BitArray _bitArray256 = null!;
    private BitArray _bitArray1024 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bit64 = ImmutableBitSet<Bit64>.Empty;
        _bit256 = ImmutableBitSet<Bit256>.Empty;
        _bit1024 = ImmutableBitSet<Bit1024>.Empty;

        _bitArray64 = new BitArray(64);
        _bitArray256 = new BitArray(256);
        _bitArray1024 = new BitArray(1024);
    }

    // === Set (immutable - returns new instance) ===
    [Benchmark(Baseline = true)]
    public ImmutableBitSet<Bit64> Bit64_Set() => _bit64.Set(32);

    [Benchmark]
    public BitArray BitArray64_Set() => _bitArray64.SetBit(32);

    [Benchmark]
    public ImmutableBitSet<Bit256> Bit256_Set() => _bit256.Set(128);

    [Benchmark]
    public BitArray BitArray256_Set() => _bitArray256.SetBit(128);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Set() => _bit1024.Set(512);

    [Benchmark]
    public BitArray BitArray1024_Set() => _bitArray1024.SetBit(512);
}

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetClearBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit1024> _bit1024;

    private BitArray _bitArray64 = null!;
    private BitArray _bitArray256 = null!;
    private BitArray _bitArray1024 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(32);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(128);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(512);

        _bitArray64 = new BitArray(64);
        _bitArray64.Set(32, true);

        _bitArray256 = new BitArray(256);
        _bitArray256.Set(128, true);

        _bitArray1024 = new BitArray(1024);
        _bitArray1024.Set(512, true);
    }

    // === Clear (immutable - returns new instance) ===
    [Benchmark(Baseline = true)]
    public ImmutableBitSet<Bit64> Bit64_Clear() => _bit64.Clear(32);

    [Benchmark]
    public BitArray BitArray64_Clear() => _bitArray64.ClearBit(32);

    [Benchmark]
    public ImmutableBitSet<Bit256> Bit256_Clear() => _bit256.Clear(128);

    [Benchmark]
    public BitArray BitArray256_Clear() => _bitArray256.ClearBit(128);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Clear() => _bit1024.Clear(512);

    [Benchmark]
    public BitArray BitArray1024_Clear() => _bitArray1024.ClearBit(512);
}

// ============================================================================
// Bitwise Operation Benchmarks (And, Or, Xor, AndNot)
// ============================================================================

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetAndBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;

    private BitArray _bitArray64A = null!, _bitArray64B = null!;
    private BitArray _bitArray256A = null!, _bitArray256B = null!;
    private BitArray _bitArray1024A = null!, _bitArray1024B = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(10).Set(20).Set(40).Set(50);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true); _bitArray64A.Set(30, true);
        _bitArray64B = new BitArray(64);
        _bitArray64B.Set(10, true); _bitArray64B.Set(20, true); _bitArray64B.Set(40, true); _bitArray64B.Set(50, true);

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true); _bitArray256A.Set(180, true);
        _bitArray256B = new BitArray(256);
        _bitArray256B.Set(60, true); _bitArray256B.Set(120, true); _bitArray256B.Set(200, true); _bitArray256B.Set(240, true);

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true); _bitArray1024A.Set(720, true);
        _bitArray1024B = new BitArray(1024);
        _bitArray1024B.Set(240, true); _bitArray1024B.Set(480, true); _bitArray1024B.Set(800, true); _bitArray1024B.Set(960, true);
    }

    [Benchmark(Baseline = true)]
    public ImmutableBitSet<Bit64> Bit64_And() => _bit64A.And(_bit64B);

    [Benchmark]
    public BitArray BitArray64_And() => _bitArray64A.AndImmutable(_bitArray64B);

    [Benchmark]
    public ImmutableBitSet<Bit256> Bit256_And() => _bit256A.And(_bit256B);

    [Benchmark]
    public BitArray BitArray256_And() => _bitArray256A.AndImmutable(_bitArray256B);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_And() => _bit1024A.And(_bit1024B);

    [Benchmark]
    public BitArray BitArray1024_And() => _bitArray1024A.AndImmutable(_bitArray1024B);
}

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetOrBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;

    private BitArray _bitArray64A = null!, _bitArray64B = null!;
    private BitArray _bitArray256A = null!, _bitArray256B = null!;
    private BitArray _bitArray1024A = null!, _bitArray1024B = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(10).Set(20).Set(40).Set(50);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true); _bitArray64A.Set(30, true);
        _bitArray64B = new BitArray(64);
        _bitArray64B.Set(10, true); _bitArray64B.Set(20, true); _bitArray64B.Set(40, true); _bitArray64B.Set(50, true);

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true); _bitArray256A.Set(180, true);
        _bitArray256B = new BitArray(256);
        _bitArray256B.Set(60, true); _bitArray256B.Set(120, true); _bitArray256B.Set(200, true); _bitArray256B.Set(240, true);

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true); _bitArray1024A.Set(720, true);
        _bitArray1024B = new BitArray(1024);
        _bitArray1024B.Set(240, true); _bitArray1024B.Set(480, true); _bitArray1024B.Set(800, true); _bitArray1024B.Set(960, true);
    }

    [Benchmark(Baseline = true)]
    public ImmutableBitSet<Bit64> Bit64_Or() => _bit64A.Or(_bit64B);

    [Benchmark]
    public BitArray BitArray64_Or() => _bitArray64A.OrImmutable(_bitArray64B);

    [Benchmark]
    public ImmutableBitSet<Bit256> Bit256_Or() => _bit256A.Or(_bit256B);

    [Benchmark]
    public BitArray BitArray256_Or() => _bitArray256A.OrImmutable(_bitArray256B);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Or() => _bit1024A.Or(_bit1024B);

    [Benchmark]
    public BitArray BitArray1024_Or() => _bitArray1024A.OrImmutable(_bitArray1024B);
}

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetXorBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;

    private BitArray _bitArray64A = null!, _bitArray64B = null!;
    private BitArray _bitArray256A = null!, _bitArray256B = null!;
    private BitArray _bitArray1024A = null!, _bitArray1024B = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(10).Set(20).Set(40).Set(50);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true); _bitArray64A.Set(30, true);
        _bitArray64B = new BitArray(64);
        _bitArray64B.Set(10, true); _bitArray64B.Set(20, true); _bitArray64B.Set(40, true); _bitArray64B.Set(50, true);

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true); _bitArray256A.Set(180, true);
        _bitArray256B = new BitArray(256);
        _bitArray256B.Set(60, true); _bitArray256B.Set(120, true); _bitArray256B.Set(200, true); _bitArray256B.Set(240, true);

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true); _bitArray1024A.Set(720, true);
        _bitArray1024B = new BitArray(1024);
        _bitArray1024B.Set(240, true); _bitArray1024B.Set(480, true); _bitArray1024B.Set(800, true); _bitArray1024B.Set(960, true);
    }

    [Benchmark(Baseline = true)]
    public ImmutableBitSet<Bit64> Bit64_Xor() => _bit64A.Xor(_bit64B);

    [Benchmark]
    public BitArray BitArray64_Xor() => _bitArray64A.XorImmutable(_bitArray64B);

    [Benchmark]
    public ImmutableBitSet<Bit256> Bit256_Xor() => _bit256A.Xor(_bit256B);

    [Benchmark]
    public BitArray BitArray256_Xor() => _bitArray256A.XorImmutable(_bitArray256B);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Xor() => _bit1024A.Xor(_bit1024B);

    [Benchmark]
    public BitArray BitArray1024_Xor() => _bitArray1024A.XorImmutable(_bitArray1024B);
}

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetAndNotBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;

    private BitArray _bitArray64A = null!, _bitArray64B = null!;
    private BitArray _bitArray256A = null!, _bitArray256B = null!;
    private BitArray _bitArray1024A = null!, _bitArray1024B = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(10).Set(20).Set(40).Set(50);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true); _bitArray64A.Set(30, true);
        _bitArray64B = new BitArray(64);
        _bitArray64B.Set(10, true); _bitArray64B.Set(20, true); _bitArray64B.Set(40, true); _bitArray64B.Set(50, true);

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true); _bitArray256A.Set(180, true);
        _bitArray256B = new BitArray(256);
        _bitArray256B.Set(60, true); _bitArray256B.Set(120, true); _bitArray256B.Set(200, true); _bitArray256B.Set(240, true);

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true); _bitArray1024A.Set(720, true);
        _bitArray1024B = new BitArray(1024);
        _bitArray1024B.Set(240, true); _bitArray1024B.Set(480, true); _bitArray1024B.Set(800, true); _bitArray1024B.Set(960, true);
    }

    [Benchmark(Baseline = true)]
    public ImmutableBitSet<Bit64> Bit64_AndNot() => _bit64A.AndNot(_bit64B);

    [Benchmark]
    public BitArray BitArray64_AndNot() => _bitArray64A.AndNotImmutable(_bitArray64B);

    [Benchmark]
    public ImmutableBitSet<Bit256> Bit256_AndNot() => _bit256A.AndNot(_bit256B);

    [Benchmark]
    public BitArray BitArray256_AndNot() => _bitArray256A.AndNotImmutable(_bitArray256B);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_AndNot() => _bit1024A.AndNot(_bit1024B);

    [Benchmark]
    public BitArray BitArray1024_AndNot() => _bitArray1024A.AndNotImmutable(_bitArray1024B);
}

// ============================================================================
// Query Operation Benchmarks (ContainsAll, ContainsAny, ContainsNone)
// ============================================================================

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetContainsAllBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;

    private BitArray _bitArray64A = null!, _bitArray64B = null!;
    private BitArray _bitArray256A = null!, _bitArray256B = null!;
    private BitArray _bitArray1024A = null!, _bitArray1024B = null!;

    [GlobalSetup]
    public void Setup()
    {
        // A contains all bits from B
        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(10).Set(20);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(60).Set(120);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720).Set(960);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(240).Set(480);

        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true);
        _bitArray64A.Set(30, true); _bitArray64A.Set(40, true);
        _bitArray64B = new BitArray(64);
        _bitArray64B.Set(10, true); _bitArray64B.Set(20, true);

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true);
        _bitArray256A.Set(180, true); _bitArray256A.Set(240, true);
        _bitArray256B = new BitArray(256);
        _bitArray256B.Set(60, true); _bitArray256B.Set(120, true);

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true);
        _bitArray1024A.Set(720, true); _bitArray1024A.Set(960, true);
        _bitArray1024B = new BitArray(1024);
        _bitArray1024B.Set(240, true); _bitArray1024B.Set(480, true);
    }

    [Benchmark(Baseline = true)]
    public bool Bit64_ContainsAll() => _bit64A.ContainsAll(_bit64B);

    [Benchmark]
    public bool BitArray64_ContainsAll() => _bitArray64A.ContainsAll(_bitArray64B);

    [Benchmark]
    public bool Bit256_ContainsAll() => _bit256A.ContainsAll(_bit256B);

    [Benchmark]
    public bool BitArray256_ContainsAll() => _bitArray256A.ContainsAll(_bitArray256B);

    [Benchmark]
    public bool Bit1024_ContainsAll() => _bit1024A.ContainsAll(_bit1024B);

    [Benchmark]
    public bool BitArray1024_ContainsAll() => _bitArray1024A.ContainsAll(_bitArray1024B);
}

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetContainsAnyBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;

    private BitArray _bitArray64A = null!, _bitArray64B = null!;
    private BitArray _bitArray256A = null!, _bitArray256B = null!;
    private BitArray _bitArray1024A = null!, _bitArray1024B = null!;

    [GlobalSetup]
    public void Setup()
    {
        // A and B have some overlapping bits
        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(20).Set(40).Set(50);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(120).Set(200).Set(240);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(480).Set(800).Set(960);

        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true); _bitArray64A.Set(30, true);
        _bitArray64B = new BitArray(64);
        _bitArray64B.Set(20, true); _bitArray64B.Set(40, true); _bitArray64B.Set(50, true);

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true); _bitArray256A.Set(180, true);
        _bitArray256B = new BitArray(256);
        _bitArray256B.Set(120, true); _bitArray256B.Set(200, true); _bitArray256B.Set(240, true);

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true); _bitArray1024A.Set(720, true);
        _bitArray1024B = new BitArray(1024);
        _bitArray1024B.Set(480, true); _bitArray1024B.Set(800, true); _bitArray1024B.Set(960, true);
    }

    [Benchmark(Baseline = true)]
    public bool Bit64_ContainsAny() => _bit64A.ContainsAny(_bit64B);

    [Benchmark]
    public bool BitArray64_ContainsAny() => _bitArray64A.ContainsAny(_bitArray64B);

    [Benchmark]
    public bool Bit256_ContainsAny() => _bit256A.ContainsAny(_bit256B);

    [Benchmark]
    public bool BitArray256_ContainsAny() => _bitArray256A.ContainsAny(_bitArray256B);

    [Benchmark]
    public bool Bit1024_ContainsAny() => _bit1024A.ContainsAny(_bit1024B);

    [Benchmark]
    public bool BitArray1024_ContainsAny() => _bitArray1024A.ContainsAny(_bitArray1024B);
}

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetContainsNoneBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;

    private BitArray _bitArray64A = null!, _bitArray64B = null!;
    private BitArray _bitArray256A = null!, _bitArray256B = null!;
    private BitArray _bitArray1024A = null!, _bitArray1024B = null!;

    [GlobalSetup]
    public void Setup()
    {
        // A and B have no overlapping bits
        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(5).Set(15).Set(25);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(30).Set(90).Set(150).Set(210);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(120).Set(360).Set(600).Set(840);

        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true); _bitArray64A.Set(30, true);
        _bitArray64B = new BitArray(64);
        _bitArray64B.Set(5, true); _bitArray64B.Set(15, true); _bitArray64B.Set(25, true);

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true); _bitArray256A.Set(180, true);
        _bitArray256B = new BitArray(256);
        _bitArray256B.Set(30, true); _bitArray256B.Set(90, true); _bitArray256B.Set(150, true); _bitArray256B.Set(210, true);

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true); _bitArray1024A.Set(720, true);
        _bitArray1024B = new BitArray(1024);
        _bitArray1024B.Set(120, true); _bitArray1024B.Set(360, true); _bitArray1024B.Set(600, true); _bitArray1024B.Set(840, true);
    }

    [Benchmark(Baseline = true)]
    public bool Bit64_ContainsNone() => _bit64A.ContainsNone(_bit64B);

    [Benchmark]
    public bool BitArray64_ContainsNone() => _bitArray64A.ContainsNone(_bitArray64B);

    [Benchmark]
    public bool Bit256_ContainsNone() => _bit256A.ContainsNone(_bit256B);

    [Benchmark]
    public bool BitArray256_ContainsNone() => _bitArray256A.ContainsNone(_bitArray256B);

    [Benchmark]
    public bool Bit1024_ContainsNone() => _bit1024A.ContainsNone(_bit1024B);

    [Benchmark]
    public bool BitArray1024_ContainsNone() => _bitArray1024A.ContainsNone(_bitArray1024B);
}

// ============================================================================
// Counting/Finding Benchmarks (PopCount, FirstSetBit, LastSetBit, IsEmpty)
// ============================================================================

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetPopCountBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit1024> _bit1024;

    private BitArray _bitArray64 = null!;
    private BitArray _bitArray256 = null!;
    private BitArray _bitArray1024 = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Set multiple bits spread across the range
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50).Set(60);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(0).Set(30).Set(60).Set(90).Set(120).Set(150).Set(180).Set(210).Set(240);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(100).Set(200).Set(300).Set(400).Set(500).Set(600).Set(700).Set(800).Set(900).Set(1000);

        _bitArray64 = new BitArray(64);
        _bitArray64.Set(0, true); _bitArray64.Set(10, true); _bitArray64.Set(20, true);
        _bitArray64.Set(30, true); _bitArray64.Set(40, true); _bitArray64.Set(50, true); _bitArray64.Set(60, true);

        _bitArray256 = new BitArray(256);
        _bitArray256.Set(0, true); _bitArray256.Set(30, true); _bitArray256.Set(60, true);
        _bitArray256.Set(90, true); _bitArray256.Set(120, true); _bitArray256.Set(150, true);
        _bitArray256.Set(180, true); _bitArray256.Set(210, true); _bitArray256.Set(240, true);

        _bitArray1024 = new BitArray(1024);
        _bitArray1024.Set(0, true); _bitArray1024.Set(100, true); _bitArray1024.Set(200, true);
        _bitArray1024.Set(300, true); _bitArray1024.Set(400, true); _bitArray1024.Set(500, true);
        _bitArray1024.Set(600, true); _bitArray1024.Set(700, true); _bitArray1024.Set(800, true);
        _bitArray1024.Set(900, true); _bitArray1024.Set(1000, true);
    }

    [Benchmark(Baseline = true)]
    public int Bit64_PopCount() => _bit64.PopCount();

    [Benchmark]
    public int BitArray64_PopCount() => _bitArray64.PopCount();

    [Benchmark]
    public int Bit256_PopCount() => _bit256.PopCount();

    [Benchmark]
    public int BitArray256_PopCount() => _bitArray256.PopCount();

    [Benchmark]
    public int Bit1024_PopCount() => _bit1024.PopCount();

    [Benchmark]
    public int BitArray1024_PopCount() => _bitArray1024.PopCount();
}

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetFirstSetBitBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit1024> _bit1024;

    private BitArray _bitArray64 = null!;
    private BitArray _bitArray256 = null!;
    private BitArray _bitArray1024 = null!;

    [GlobalSetup]
    public void Setup()
    {
        // First bit is somewhere in the middle to test search
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(25).Set(40).Set(50);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(100).Set(150).Set(200);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(400).Set(600).Set(800);

        _bitArray64 = new BitArray(64);
        _bitArray64.Set(25, true); _bitArray64.Set(40, true); _bitArray64.Set(50, true);

        _bitArray256 = new BitArray(256);
        _bitArray256.Set(100, true); _bitArray256.Set(150, true); _bitArray256.Set(200, true);

        _bitArray1024 = new BitArray(1024);
        _bitArray1024.Set(400, true); _bitArray1024.Set(600, true); _bitArray1024.Set(800, true);
    }

    [Benchmark(Baseline = true)]
    public int Bit64_FirstSetBit() => _bit64.FirstSetBit();

    [Benchmark]
    public int BitArray64_FirstSetBit() => _bitArray64.FirstSetBit();

    [Benchmark]
    public int Bit256_FirstSetBit() => _bit256.FirstSetBit();

    [Benchmark]
    public int BitArray256_FirstSetBit() => _bitArray256.FirstSetBit();

    [Benchmark]
    public int Bit1024_FirstSetBit() => _bit1024.FirstSetBit();

    [Benchmark]
    public int BitArray1024_FirstSetBit() => _bitArray1024.FirstSetBit();
}

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetLastSetBitBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit1024> _bit1024;

    private BitArray _bitArray64 = null!;
    private BitArray _bitArray256 = null!;
    private BitArray _bitArray1024 = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Last bit is somewhere in the middle to test search
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(10).Set(25).Set(40);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(50).Set(100).Set(150);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(200).Set(400).Set(600);

        _bitArray64 = new BitArray(64);
        _bitArray64.Set(10, true); _bitArray64.Set(25, true); _bitArray64.Set(40, true);

        _bitArray256 = new BitArray(256);
        _bitArray256.Set(50, true); _bitArray256.Set(100, true); _bitArray256.Set(150, true);

        _bitArray1024 = new BitArray(1024);
        _bitArray1024.Set(200, true); _bitArray1024.Set(400, true); _bitArray1024.Set(600, true);
    }

    [Benchmark(Baseline = true)]
    public int Bit64_LastSetBit() => _bit64.LastSetBit();

    [Benchmark]
    public int BitArray64_LastSetBit() => _bitArray64.LastSetBit();

    [Benchmark]
    public int Bit256_LastSetBit() => _bit256.LastSetBit();

    [Benchmark]
    public int BitArray256_LastSetBit() => _bitArray256.LastSetBit();

    [Benchmark]
    public int Bit1024_LastSetBit() => _bit1024.LastSetBit();

    [Benchmark]
    public int BitArray1024_LastSetBit() => _bitArray1024.LastSetBit();
}

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetIsEmptyBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64Empty;
    private ImmutableBitSet<Bit64> _bit64NonEmpty;
    private ImmutableBitSet<Bit256> _bit256Empty;
    private ImmutableBitSet<Bit256> _bit256NonEmpty;
    private ImmutableBitSet<Bit1024> _bit1024Empty;
    private ImmutableBitSet<Bit1024> _bit1024NonEmpty;

    private BitArray _bitArray64Empty = null!;
    private BitArray _bitArray64NonEmpty = null!;
    private BitArray _bitArray256Empty = null!;
    private BitArray _bitArray256NonEmpty = null!;
    private BitArray _bitArray1024Empty = null!;
    private BitArray _bitArray1024NonEmpty = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bit64Empty = ImmutableBitSet<Bit64>.Empty;
        _bit64NonEmpty = ImmutableBitSet<Bit64>.Empty.Set(63);  // Last bit

        _bit256Empty = ImmutableBitSet<Bit256>.Empty;
        _bit256NonEmpty = ImmutableBitSet<Bit256>.Empty.Set(255);  // Last bit

        _bit1024Empty = ImmutableBitSet<Bit1024>.Empty;
        _bit1024NonEmpty = ImmutableBitSet<Bit1024>.Empty.Set(1023);  // Last bit

        _bitArray64Empty = new BitArray(64);
        _bitArray64NonEmpty = new BitArray(64);
        _bitArray64NonEmpty.Set(63, true);

        _bitArray256Empty = new BitArray(256);
        _bitArray256NonEmpty = new BitArray(256);
        _bitArray256NonEmpty.Set(255, true);

        _bitArray1024Empty = new BitArray(1024);
        _bitArray1024NonEmpty = new BitArray(1024);
        _bitArray1024NonEmpty.Set(1023, true);
    }

    [Benchmark(Baseline = true)]
    public bool Bit64_IsEmpty_Empty() => _bit64Empty.IsEmpty;

    [Benchmark]
    public bool BitArray64_IsEmpty_Empty() => _bitArray64Empty.IsEmpty();

    [Benchmark]
    public bool Bit64_IsEmpty_NonEmpty() => _bit64NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitArray64_IsEmpty_NonEmpty() => _bitArray64NonEmpty.IsEmpty();

    [Benchmark]
    public bool Bit1024_IsEmpty_Empty() => _bit1024Empty.IsEmpty;

    [Benchmark]
    public bool BitArray1024_IsEmpty_Empty() => _bitArray1024Empty.IsEmpty();

    [Benchmark]
    public bool Bit1024_IsEmpty_NonEmpty() => _bit1024NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitArray1024_IsEmpty_NonEmpty() => _bitArray1024NonEmpty.IsEmpty();
}

// ============================================================================
// Equality/Hashing Benchmarks
// ============================================================================

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetEqualsBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;

    private BitArray _bitArray64A = null!, _bitArray64B = null!;
    private BitArray _bitArray256A = null!, _bitArray256B = null!;
    private BitArray _bitArray1024A = null!, _bitArray1024B = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);

        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true); _bitArray64A.Set(30, true);
        _bitArray64B = new BitArray(64);
        _bitArray64B.Set(0, true); _bitArray64B.Set(10, true); _bitArray64B.Set(20, true); _bitArray64B.Set(30, true);

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true); _bitArray256A.Set(180, true);
        _bitArray256B = new BitArray(256);
        _bitArray256B.Set(0, true); _bitArray256B.Set(60, true); _bitArray256B.Set(120, true); _bitArray256B.Set(180, true);

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true); _bitArray1024A.Set(720, true);
        _bitArray1024B = new BitArray(1024);
        _bitArray1024B.Set(0, true); _bitArray1024B.Set(240, true); _bitArray1024B.Set(480, true); _bitArray1024B.Set(720, true);
    }

    [Benchmark(Baseline = true)]
    public bool Bit64_Equals() => _bit64A.Equals(_bit64B);

    [Benchmark]
    public bool BitArray64_Equals() => _bitArray64A.BitArrayEquals(_bitArray64B);

    [Benchmark]
    public bool Bit256_Equals() => _bit256A.Equals(_bit256B);

    [Benchmark]
    public bool BitArray256_Equals() => _bitArray256A.BitArrayEquals(_bitArray256B);

    [Benchmark]
    public bool Bit1024_Equals() => _bit1024A.Equals(_bit1024B);

    [Benchmark]
    public bool BitArray1024_Equals() => _bitArray1024A.BitArrayEquals(_bitArray1024B);
}

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetGetHashCodeBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit1024> _bit1024;

    private BitArray _bitArray64 = null!;
    private BitArray _bitArray256 = null!;
    private BitArray _bitArray1024 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);

        _bitArray64 = new BitArray(64);
        _bitArray64.Set(0, true); _bitArray64.Set(10, true); _bitArray64.Set(20, true); _bitArray64.Set(30, true);

        _bitArray256 = new BitArray(256);
        _bitArray256.Set(0, true); _bitArray256.Set(60, true); _bitArray256.Set(120, true); _bitArray256.Set(180, true);

        _bitArray1024 = new BitArray(1024);
        _bitArray1024.Set(0, true); _bitArray1024.Set(240, true); _bitArray1024.Set(480, true); _bitArray1024.Set(720, true);
    }

    [Benchmark(Baseline = true)]
    public int Bit64_GetHashCode() => _bit64.GetHashCode();

    [Benchmark]
    public int BitArray64_GetHashCode() => _bitArray64.BitArrayGetHashCode();

    [Benchmark]
    public int Bit256_GetHashCode() => _bit256.GetHashCode();

    [Benchmark]
    public int BitArray256_GetHashCode() => _bitArray256.BitArrayGetHashCode();

    [Benchmark]
    public int Bit1024_GetHashCode() => _bit1024.GetHashCode();

    [Benchmark]
    public int BitArray1024_GetHashCode() => _bitArray1024.BitArrayGetHashCode();
}

// ============================================================================
// Enumeration Benchmarks
// ============================================================================

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetEnumerationBenchmarks
{
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit1024> _bit1024;

    private BitArray _bitArray64 = null!;
    private BitArray _bitArray256 = null!;
    private BitArray _bitArray1024 = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Set multiple bits spread across the range
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50).Set(60);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(0).Set(30).Set(60).Set(90).Set(120).Set(150).Set(180).Set(210).Set(240);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(100).Set(200).Set(300).Set(400).Set(500).Set(600).Set(700).Set(800).Set(900).Set(1000);

        _bitArray64 = new BitArray(64);
        _bitArray64.Set(0, true); _bitArray64.Set(10, true); _bitArray64.Set(20, true);
        _bitArray64.Set(30, true); _bitArray64.Set(40, true); _bitArray64.Set(50, true); _bitArray64.Set(60, true);

        _bitArray256 = new BitArray(256);
        _bitArray256.Set(0, true); _bitArray256.Set(30, true); _bitArray256.Set(60, true);
        _bitArray256.Set(90, true); _bitArray256.Set(120, true); _bitArray256.Set(150, true);
        _bitArray256.Set(180, true); _bitArray256.Set(210, true); _bitArray256.Set(240, true);

        _bitArray1024 = new BitArray(1024);
        _bitArray1024.Set(0, true); _bitArray1024.Set(100, true); _bitArray1024.Set(200, true);
        _bitArray1024.Set(300, true); _bitArray1024.Set(400, true); _bitArray1024.Set(500, true);
        _bitArray1024.Set(600, true); _bitArray1024.Set(700, true); _bitArray1024.Set(800, true);
        _bitArray1024.Set(900, true); _bitArray1024.Set(1000, true);
    }

    [Benchmark(Baseline = true)]
    public int Bit64_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _bit64)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int BitArray64_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _bitArray64.EnumerateSetBits())
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int Bit256_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _bit256)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int BitArray256_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _bitArray256.EnumerateSetBits())
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int Bit1024_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _bit1024)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int BitArray1024_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _bitArray1024.EnumerateSetBits())
            sum += bit;
        return sum;
    }
}
