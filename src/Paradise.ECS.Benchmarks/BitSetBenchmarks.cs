using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Paradise.ECS;

namespace Paradise.ECS.Benchmarks;

/// <summary>
/// Helper extensions for BitArray operations matching ImmutableBitSet API.
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
}

/// <summary>
/// Helper extensions for BitVector32 operations.
/// </summary>
public static class BitVector32Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsAll(this BitVector32 self, BitVector32 other)
    {
        // Check if all bits in 'other' are also in 'self'
        return (other.Data & ~self.Data) == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsNone(this BitVector32 self, BitVector32 other)
    {
        return (self.Data & other.Data) == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BitVector32 SetBit(this BitVector32 vector, int bit)
    {
        return new BitVector32(vector.Data | (1 << bit));
    }
}

[MemoryDiagnoser]
[ShortRunJob]
public class BitSetBenchmarks
{
    // InlineArray bitsets
    private ImmutableBitSet<Bit64> _bit64A;
    private ImmutableBitSet<Bit64> _bit64B;
    private ImmutableBitSet<Bit128> _bit128A;
    private ImmutableBitSet<Bit128> _bit128B;
    private ImmutableBitSet<Bit256> _bit256A;
    private ImmutableBitSet<Bit256> _bit256B;
    private ImmutableBitSet<Bit512> _bit512A;
    private ImmutableBitSet<Bit512> _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A;
    private ImmutableBitSet<Bit1024> _bit1024B;

    // BitArray (same sizes)
    private BitArray _bitArray64A = null!;
    private BitArray _bitArray64B = null!;
    private BitArray _bitArray128A = null!;
    private BitArray _bitArray128B = null!;
    private BitArray _bitArray256A = null!;
    private BitArray _bitArray256B = null!;
    private BitArray _bitArray512A = null!;
    private BitArray _bitArray512B = null!;
    private BitArray _bitArray1024A = null!;
    private BitArray _bitArray1024B = null!;

    // BitVector32 (only 32 bits - for reference)
    private BitVector32 _bitVector32A;
    private BitVector32 _bitVector32B;

    [GlobalSetup]
    public void Setup()
    {
        // Setup InlineArray bitsets with some bits set
        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(5).Set(15).Set(25);

        _bit128A = ImmutableBitSet<Bit128>.Empty.Set(0).Set(30).Set(60).Set(90);
        _bit128B = ImmutableBitSet<Bit128>.Empty.Set(15).Set(45).Set(75).Set(105);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(30).Set(90).Set(150).Set(210);

        _bit512A = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bit512B = ImmutableBitSet<Bit512>.Empty.Set(60).Set(180).Set(300).Set(420);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(120).Set(360).Set(600).Set(840);

        // Setup BitArray with same bits
        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true); _bitArray64A.Set(30, true);
        _bitArray64B = new BitArray(64);
        _bitArray64B.Set(5, true); _bitArray64B.Set(15, true); _bitArray64B.Set(25, true);

        _bitArray128A = new BitArray(128);
        _bitArray128A.Set(0, true); _bitArray128A.Set(30, true); _bitArray128A.Set(60, true); _bitArray128A.Set(90, true);
        _bitArray128B = new BitArray(128);
        _bitArray128B.Set(15, true); _bitArray128B.Set(45, true); _bitArray128B.Set(75, true); _bitArray128B.Set(105, true);

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true); _bitArray256A.Set(180, true);
        _bitArray256B = new BitArray(256);
        _bitArray256B.Set(30, true); _bitArray256B.Set(90, true); _bitArray256B.Set(150, true); _bitArray256B.Set(210, true);

        _bitArray512A = new BitArray(512);
        _bitArray512A.Set(0, true); _bitArray512A.Set(120, true); _bitArray512A.Set(240, true); _bitArray512A.Set(360, true);
        _bitArray512B = new BitArray(512);
        _bitArray512B.Set(60, true); _bitArray512B.Set(180, true); _bitArray512B.Set(300, true); _bitArray512B.Set(420, true);

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true); _bitArray1024A.Set(720, true);
        _bitArray1024B = new BitArray(1024);
        _bitArray1024B.Set(120, true); _bitArray1024B.Set(360, true); _bitArray1024B.Set(600, true); _bitArray1024B.Set(840, true);

        // Setup BitVector32 (only 32 bits)
        _bitVector32A = new BitVector32(0).SetBit(0).SetBit(10).SetBit(20).SetBit(30);
        _bitVector32B = new BitVector32(0).SetBit(5).SetBit(15).SetBit(25);
    }

    // === BitVector32 (32 bits only - baseline for small comparisons) ===

    [Benchmark]
    public bool BitVector32_ContainsAll() => _bitVector32A.ContainsAll(_bitVector32B);

    [Benchmark]
    public bool BitVector32_ContainsNone() => _bitVector32A.ContainsNone(_bitVector32B);

    // === Bit64 vs BitArray[64] ===

    [Benchmark(Baseline = true)]
    public bool Bit64_ContainsAll() => _bit64A.ContainsAll(_bit64B);

    [Benchmark]
    public bool BitArray64_ContainsAll() => _bitArray64A.ContainsAll(_bitArray64B);

    [Benchmark]
    public bool Bit64_ContainsNone() => _bit64A.ContainsNone(_bit64B);

    [Benchmark]
    public bool BitArray64_ContainsNone() => _bitArray64A.ContainsNone(_bitArray64B);

    // === Bit128 vs BitArray[128] ===

    [Benchmark]
    public bool Bit128_ContainsAll() => _bit128A.ContainsAll(_bit128B);

    [Benchmark]
    public bool BitArray128_ContainsAll() => _bitArray128A.ContainsAll(_bitArray128B);

    [Benchmark]
    public bool Bit128_ContainsNone() => _bit128A.ContainsNone(_bit128B);

    [Benchmark]
    public bool BitArray128_ContainsNone() => _bitArray128A.ContainsNone(_bitArray128B);

    // === Bit256 vs BitArray[256] ===

    [Benchmark]
    public bool Bit256_ContainsAll() => _bit256A.ContainsAll(_bit256B);

    [Benchmark]
    public bool BitArray256_ContainsAll() => _bitArray256A.ContainsAll(_bitArray256B);

    [Benchmark]
    public bool Bit256_ContainsNone() => _bit256A.ContainsNone(_bit256B);

    [Benchmark]
    public bool BitArray256_ContainsNone() => _bitArray256A.ContainsNone(_bitArray256B);

    // === Bit512 vs BitArray[512] ===

    [Benchmark]
    public bool Bit512_ContainsAll() => _bit512A.ContainsAll(_bit512B);

    [Benchmark]
    public bool BitArray512_ContainsAll() => _bitArray512A.ContainsAll(_bitArray512B);

    [Benchmark]
    public bool Bit512_ContainsNone() => _bit512A.ContainsNone(_bit512B);

    [Benchmark]
    public bool BitArray512_ContainsNone() => _bitArray512A.ContainsNone(_bitArray512B);

    // === Bit1024 vs BitArray[1024] ===

    [Benchmark]
    public bool Bit1024_ContainsAll() => _bit1024A.ContainsAll(_bit1024B);

    [Benchmark]
    public bool BitArray1024_ContainsAll() => _bitArray1024A.ContainsAll(_bitArray1024B);

    [Benchmark]
    public bool Bit1024_ContainsNone() => _bit1024A.ContainsNone(_bit1024B);

    [Benchmark]
    public bool BitArray1024_ContainsNone() => _bitArray1024A.ContainsNone(_bitArray1024B);
}

/// <summary>
/// Benchmarks for Set operation (allocation-heavy for BitArray).
/// </summary>
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

    private BitVector32 _bitVector32;

    [GlobalSetup]
    public void Setup()
    {
        _bit64 = ImmutableBitSet<Bit64>.Empty;
        _bit256 = ImmutableBitSet<Bit256>.Empty;
        _bit1024 = ImmutableBitSet<Bit1024>.Empty;

        _bitArray64 = new BitArray(64);
        _bitArray256 = new BitArray(256);
        _bitArray1024 = new BitArray(1024);

        _bitVector32 = new BitVector32(0);
    }

    [Benchmark]
    public BitVector32 BitVector32_Set() => _bitVector32.SetBit(16);

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
