using System.Collections;
using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Benchmarks;

// ============================================================================
// Get/Set/Clear Benchmarks
// ============================================================================

[Config(typeof(NativeAotConfig))]
public class BitSetGetBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private ImmutableBitVector<Bit256> _immutableBitVector256;
    private ImmutableBitVector<Bit512> _immutableBitVector512;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    private BitSet<Bit256> _mutableBitSet256;
    private BitSet<Bit512> _mutableBitSet512;
    private BitSet<Bit1024> _mutableBitSet1024;
    private BitVector<Bit256> _mutableBitVector256;
    private BitVector<Bit512> _mutableBitVector512;
    private BitVector<Bit1024> _mutableBitVector1024;

    private BitArray _bitArray64 = null!;
    private BitArray _bitArray256 = null!;
    private BitArray _bitArray512 = null!;
    private BitArray _bitArray1024 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _int64 = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 63);
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(63);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180).Set(255);
        _bit512 = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360).Set(511);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720).Set(1023);
        _immutableBitVector256 = ImmutableBitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180).Set(255);
        _immutableBitVector512 = ImmutableBitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360).Set(511);
        _immutableBitVector1024 = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720).Set(1023);
        _bitVectorT256 = BitVectorT<Bit256>.Empty;
        _bitVectorT256.Set(0); _bitVectorT256.Set(60); _bitVectorT256.Set(120); _bitVectorT256.Set(180); _bitVectorT256.Set(255);
        _bitVectorT512 = BitVectorT<Bit512>.Empty;
        _bitVectorT512.Set(0); _bitVectorT512.Set(120); _bitVectorT512.Set(240); _bitVectorT512.Set(360); _bitVectorT512.Set(511);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024.Set(0); _bitVectorT1024.Set(240); _bitVectorT1024.Set(480); _bitVectorT1024.Set(720); _bitVectorT1024.Set(1023);

        _mutableBitSet256 = BitSet<Bit256>.Empty;
        _mutableBitSet256.Set(0); _mutableBitSet256.Set(60); _mutableBitSet256.Set(120); _mutableBitSet256.Set(180); _mutableBitSet256.Set(255);
        _mutableBitSet512 = BitSet<Bit512>.Empty;
        _mutableBitSet512.Set(0); _mutableBitSet512.Set(120); _mutableBitSet512.Set(240); _mutableBitSet512.Set(360); _mutableBitSet512.Set(511);
        _mutableBitSet1024 = BitSet<Bit1024>.Empty;
        _mutableBitSet1024.Set(0); _mutableBitSet1024.Set(240); _mutableBitSet1024.Set(480); _mutableBitSet1024.Set(720); _mutableBitSet1024.Set(1023);
        _mutableBitVector256 = BitVector<Bit256>.Empty;
        _mutableBitVector256.Set(0); _mutableBitVector256.Set(60); _mutableBitVector256.Set(120); _mutableBitVector256.Set(180); _mutableBitVector256.Set(255);
        _mutableBitVector512 = BitVector<Bit512>.Empty;
        _mutableBitVector512.Set(0); _mutableBitVector512.Set(120); _mutableBitVector512.Set(240); _mutableBitVector512.Set(360); _mutableBitVector512.Set(511);
        _mutableBitVector1024 = BitVector<Bit1024>.Empty;
        _mutableBitVector1024.Set(0); _mutableBitVector1024.Set(240); _mutableBitVector1024.Set(480); _mutableBitVector1024.Set(720); _mutableBitVector1024.Set(1023);

        _bitArray64 = new BitArray(64);
        _bitArray64.Set(0, true); _bitArray64.Set(10, true); _bitArray64.Set(20, true);
        _bitArray64.Set(30, true); _bitArray64.Set(63, true);

        _bitArray256 = new BitArray(256);
        _bitArray256.Set(0, true); _bitArray256.Set(60, true); _bitArray256.Set(120, true);
        _bitArray256.Set(180, true); _bitArray256.Set(255, true);

        _bitArray512 = new BitArray(512);
        _bitArray512.Set(0, true); _bitArray512.Set(120, true); _bitArray512.Set(240, true);
        _bitArray512.Set(360, true); _bitArray512.Set(511, true);

        _bitArray1024 = new BitArray(1024);
        _bitArray1024.Set(0, true); _bitArray1024.Set(240, true); _bitArray1024.Set(480, true);
        _bitArray1024.Set(720, true); _bitArray1024.Set(1023, true);
    }

    // === Get ===
    [Benchmark]
    public bool Int64_Get() => (_int64 & (1L << 30)) != 0;

    [Benchmark]
    public bool Bit64_Get() => _bit64.Get(30);

    [Benchmark]
    public bool BitArray64_Get() => _bitArray64.Get(30);

    [Benchmark(Baseline = true)]
    public bool Bit256_Get() => _bit256.Get(120);

    [Benchmark]
    public bool BitVector256_Get() => _immutableBitVector256.Get(120);

    [Benchmark]
    public bool BitVectorT256_Get() => _bitVectorT256.Get(120);

    [Benchmark]
    public bool MutableBitSet256_Get() => _mutableBitSet256.Get(120);

    [Benchmark]
    public bool MutableBitVector256_Get() => _mutableBitVector256.Get(120);

    [Benchmark]
    public bool BitArray256_Get() => _bitArray256.Get(120);

    [Benchmark]
    public bool Bit512_Get() => _bit512.Get(240);

    [Benchmark]
    public bool BitVector512_Get() => _immutableBitVector512.Get(240);

    [Benchmark]
    public bool BitVectorT512_Get() => _bitVectorT512.Get(240);

    [Benchmark]
    public bool MutableBitSet512_Get() => _mutableBitSet512.Get(240);

    [Benchmark]
    public bool MutableBitVector512_Get() => _mutableBitVector512.Get(240);

    [Benchmark]
    public bool BitArray512_Get() => _bitArray512.Get(240);

    [Benchmark]
    public bool Bit1024_Get() => _bit1024.Get(480);

    [Benchmark]
    public bool BitVector1024_Get() => _immutableBitVector1024.Get(480);

    [Benchmark]
    public bool BitVectorT1024_Get() => _bitVectorT1024.Get(480);

    [Benchmark]
    public bool MutableBitSet1024_Get() => _mutableBitSet1024.Get(480);

    [Benchmark]
    public bool MutableBitVector1024_Get() => _mutableBitVector1024.Get(480);

    [Benchmark]
    public bool BitArray1024_Get() => _bitArray1024.Get(480);
}

[Config(typeof(NativeAotConfig))]
public class BitSetSetBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private ImmutableBitVector<Bit256> _immutableBitVector256;
    private ImmutableBitVector<Bit512> _immutableBitVector512;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    private BitSet<Bit256> _mutableBitSet256;
    private BitSet<Bit512> _mutableBitSet512;
    private BitSet<Bit1024> _mutableBitSet1024;
    private BitVector<Bit256> _mutableBitVector256;
    private BitVector<Bit512> _mutableBitVector512;
    private BitVector<Bit1024> _mutableBitVector1024;

    private BitArray _bitArray64 = null!;
    private BitArray _bitArray256 = null!;
    private BitArray _bitArray512 = null!;
    private BitArray _bitArray1024 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _int64 = 0L;
        _bit64 = ImmutableBitSet<Bit64>.Empty;
        _bit256 = ImmutableBitSet<Bit256>.Empty;
        _bit512 = ImmutableBitSet<Bit512>.Empty;
        _bit1024 = ImmutableBitSet<Bit1024>.Empty;
        _immutableBitVector256 = ImmutableBitVector<Bit256>.Empty;
        _immutableBitVector512 = ImmutableBitVector<Bit512>.Empty;
        _immutableBitVector1024 = ImmutableBitVector<Bit1024>.Empty;
        _bitVectorT256 = BitVectorT<Bit256>.Empty;
        _bitVectorT512 = BitVectorT<Bit512>.Empty;
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty;

        _mutableBitSet256 = BitSet<Bit256>.Empty;
        _mutableBitSet512 = BitSet<Bit512>.Empty;
        _mutableBitSet1024 = BitSet<Bit1024>.Empty;
        _mutableBitVector256 = BitVector<Bit256>.Empty;
        _mutableBitVector512 = BitVector<Bit512>.Empty;
        _mutableBitVector1024 = BitVector<Bit1024>.Empty;

        _bitArray64 = new BitArray(64);
        _bitArray256 = new BitArray(256);
        _bitArray512 = new BitArray(512);
        _bitArray1024 = new BitArray(1024);
    }

    // === Set ===
    [Benchmark]
    public long Int64_Set() => _int64 | (1L << 32);

    [Benchmark]
    public ImmutableBitSet<Bit64> Bit64_Set() => _bit64.Set(32);

    [Benchmark]
    public void BitArray64_Set() => _bitArray64.Set(32, true);

    [Benchmark(Baseline = true)]
    public ImmutableBitSet<Bit256> Bit256_Set() => _bit256.Set(128);

    [Benchmark]
    public ImmutableBitVector<Bit256> BitVector256_Set() => _immutableBitVector256.Set(128);

    [Benchmark]
    public void BitVectorT256_Set() => _bitVectorT256.Set(128);

    [Benchmark]
    public void MutableBitSet256_Set() => _mutableBitSet256.Set(128);

    [Benchmark]
    public void MutableBitVector256_Set() => _mutableBitVector256.Set(128);

    [Benchmark]
    public void BitArray256_Set() => _bitArray256.Set(128, true);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_Set() => _bit512.Set(256);

    [Benchmark]
    public ImmutableBitVector<Bit512> BitVector512_Set() => _immutableBitVector512.Set(256);

    [Benchmark]
    public void BitVectorT512_Set() => _bitVectorT512.Set(256);

    [Benchmark]
    public void MutableBitSet512_Set() => _mutableBitSet512.Set(256);

    [Benchmark]
    public void MutableBitVector512_Set() => _mutableBitVector512.Set(256);

    [Benchmark]
    public void BitArray512_Set() => _bitArray512.Set(256, true);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Set() => _bit1024.Set(512);

    [Benchmark]
    public ImmutableBitVector<Bit1024> BitVector1024_Set() => _immutableBitVector1024.Set(512);

    [Benchmark]
    public void BitVectorT1024_Set() => _bitVectorT1024.Set(512);

    [Benchmark]
    public void MutableBitSet1024_Set() => _mutableBitSet1024.Set(512);

    [Benchmark]
    public void MutableBitVector1024_Set() => _mutableBitVector1024.Set(512);

    [Benchmark]
    public void BitArray1024_Set() => _bitArray1024.Set(512, true);
}

[Config(typeof(NativeAotConfig))]
public class BitSetClearBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private ImmutableBitVector<Bit256> _immutableBitVector256;
    private ImmutableBitVector<Bit512> _immutableBitVector512;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    private BitSet<Bit256> _mutableBitSet256;
    private BitSet<Bit512> _mutableBitSet512;
    private BitSet<Bit1024> _mutableBitSet1024;
    private BitVector<Bit256> _mutableBitVector256;
    private BitVector<Bit512> _mutableBitVector512;
    private BitVector<Bit1024> _mutableBitVector1024;

    private BitArray _bitArray64 = null!;
    private BitArray _bitArray256 = null!;
    private BitArray _bitArray512 = null!;
    private BitArray _bitArray1024 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _int64 = 1L << 32;
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(32);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(128);
        _bit512 = ImmutableBitSet<Bit512>.Empty.Set(256);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(512);
        _immutableBitVector256 = ImmutableBitVector<Bit256>.Empty.Set(128);
        _immutableBitVector512 = ImmutableBitVector<Bit512>.Empty.Set(256);
        _immutableBitVector1024 = ImmutableBitVector<Bit1024>.Empty.Set(512);
        _bitVectorT256 = BitVectorT<Bit256>.Empty;
        _bitVectorT256.Set(128);
        _bitVectorT512 = BitVectorT<Bit512>.Empty;
        _bitVectorT512.Set(256);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024.Set(512);

        _mutableBitSet256 = BitSet<Bit256>.Empty;
        _mutableBitSet256.Set(128);
        _mutableBitSet512 = BitSet<Bit512>.Empty;
        _mutableBitSet512.Set(256);
        _mutableBitSet1024 = BitSet<Bit1024>.Empty;
        _mutableBitSet1024.Set(512);
        _mutableBitVector256 = BitVector<Bit256>.Empty;
        _mutableBitVector256.Set(128);
        _mutableBitVector512 = BitVector<Bit512>.Empty;
        _mutableBitVector512.Set(256);
        _mutableBitVector1024 = BitVector<Bit1024>.Empty;
        _mutableBitVector1024.Set(512);

        _bitArray64 = new BitArray(64);
        _bitArray64.Set(32, true);

        _bitArray256 = new BitArray(256);
        _bitArray256.Set(128, true);

        _bitArray512 = new BitArray(512);
        _bitArray512.Set(256, true);

        _bitArray1024 = new BitArray(1024);
        _bitArray1024.Set(512, true);
    }

    // === Clear ===
    [Benchmark]
    public long Int64_Clear() => _int64 & ~(1L << 32);

    [Benchmark]
    public ImmutableBitSet<Bit64> Bit64_Clear() => _bit64.Clear(32);

    [Benchmark]
    public void BitArray64_Clear() => _bitArray64.Set(32, false);

    [Benchmark(Baseline = true)]
    public ImmutableBitSet<Bit256> Bit256_Clear() => _bit256.Clear(128);

    [Benchmark]
    public ImmutableBitVector<Bit256> BitVector256_Clear() => _immutableBitVector256.Clear(128);

    [Benchmark]
    public void BitVectorT256_Clear() => _bitVectorT256.Clear(128);

    [Benchmark]
    public void MutableBitSet256_Clear() => _mutableBitSet256.Clear(128);

    [Benchmark]
    public void MutableBitVector256_Clear() => _mutableBitVector256.Clear(128);

    [Benchmark]
    public void BitArray256_Clear() => _bitArray256.Set(128, false);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_Clear() => _bit512.Clear(256);

    [Benchmark]
    public ImmutableBitVector<Bit512> BitVector512_Clear() => _immutableBitVector512.Clear(256);

    [Benchmark]
    public void BitVectorT512_Clear() => _bitVectorT512.Clear(256);

    [Benchmark]
    public void MutableBitSet512_Clear() => _mutableBitSet512.Clear(256);

    [Benchmark]
    public void MutableBitVector512_Clear() => _mutableBitVector512.Clear(256);

    [Benchmark]
    public void BitArray512_Clear() => _bitArray512.Set(256, false);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Clear() => _bit1024.Clear(512);

    [Benchmark]
    public ImmutableBitVector<Bit1024> BitVector1024_Clear() => _immutableBitVector1024.Clear(512);

    [Benchmark]
    public void BitVectorT1024_Clear() => _bitVectorT1024.Clear(512);

    [Benchmark]
    public void MutableBitSet1024_Clear() => _mutableBitSet1024.Clear(512);

    [Benchmark]
    public void MutableBitVector1024_Clear() => _mutableBitVector1024.Clear(512);

    [Benchmark]
    public void BitArray1024_Clear() => _bitArray1024.Set(512, false);
}

// ============================================================================
// Bitwise Operation Benchmarks (And, Or, Xor, AndNot)
// ============================================================================

[Config(typeof(NativeAotConfig))]
public class BitSetAndBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private ImmutableBitVector<Bit256> _immutableBitVector256A, _immutableBitVector256B;
    private ImmutableBitVector<Bit512> _immutableBitVector512A, _immutableBitVector512B;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024A, _immutableBitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

    private BitSet<Bit256> _mutableBitSet256A, _mutableBitSet256B;
    private BitSet<Bit512> _mutableBitSet512A, _mutableBitSet512B;
    private BitSet<Bit1024> _mutableBitSet1024A, _mutableBitSet1024B;
    private BitVector<Bit256> _mutableBitVector256A, _mutableBitVector256B;
    private BitVector<Bit512> _mutableBitVector512A, _mutableBitVector512B;
    private BitVector<Bit1024> _mutableBitVector1024A, _mutableBitVector1024B;

    private BitArray _bitArray64A = null!, _bitArray64B = null!;
    private BitArray _bitArray256A = null!, _bitArray256B = null!;
    private BitArray _bitArray512A = null!, _bitArray512B = null!;
    private BitArray _bitArray1024A = null!, _bitArray1024B = null!;

    [GlobalSetup]
    public void Setup()
    {
        _int64A = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50);
        _int64B = (1L << 10) | (1L << 20) | (1L << 40) | (1L << 63);

        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(10).Set(20).Set(40).Set(63);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bit512A = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bit512B = ImmutableBitSet<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _immutableBitVector256A = ImmutableBitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _immutableBitVector256B = ImmutableBitVector<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _immutableBitVector512A = ImmutableBitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _immutableBitVector512B = ImmutableBitVector<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _immutableBitVector1024A = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _immutableBitVector1024B = ImmutableBitVector<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _bitVectorT256A = BitVectorT<Bit256>.Empty;
        _bitVectorT256A.Set(0); _bitVectorT256A.Set(60); _bitVectorT256A.Set(120); _bitVectorT256A.Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty;
        _bitVectorT256B.Set(60); _bitVectorT256B.Set(120); _bitVectorT256B.Set(200); _bitVectorT256B.Set(240);

        _bitVectorT512A = BitVectorT<Bit512>.Empty;
        _bitVectorT512A.Set(0); _bitVectorT512A.Set(120); _bitVectorT512A.Set(240); _bitVectorT512A.Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty;
        _bitVectorT512B.Set(120); _bitVectorT512B.Set(240); _bitVectorT512B.Set(400); _bitVectorT512B.Set(480);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024A.Set(0); _bitVectorT1024A.Set(240); _bitVectorT1024A.Set(480); _bitVectorT1024A.Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024B.Set(240); _bitVectorT1024B.Set(480); _bitVectorT1024B.Set(800); _bitVectorT1024B.Set(960);

        _mutableBitSet256A = BitSet<Bit256>.Empty;
        _mutableBitSet256A.Set(0); _mutableBitSet256A.Set(60); _mutableBitSet256A.Set(120); _mutableBitSet256A.Set(180);
        _mutableBitSet256B = BitSet<Bit256>.Empty;
        _mutableBitSet256B.Set(60); _mutableBitSet256B.Set(120); _mutableBitSet256B.Set(200); _mutableBitSet256B.Set(240);
        _mutableBitSet512A = BitSet<Bit512>.Empty;
        _mutableBitSet512A.Set(0); _mutableBitSet512A.Set(120); _mutableBitSet512A.Set(240); _mutableBitSet512A.Set(360);
        _mutableBitSet512B = BitSet<Bit512>.Empty;
        _mutableBitSet512B.Set(120); _mutableBitSet512B.Set(240); _mutableBitSet512B.Set(400); _mutableBitSet512B.Set(480);
        _mutableBitSet1024A = BitSet<Bit1024>.Empty;
        _mutableBitSet1024A.Set(0); _mutableBitSet1024A.Set(240); _mutableBitSet1024A.Set(480); _mutableBitSet1024A.Set(720);
        _mutableBitSet1024B = BitSet<Bit1024>.Empty;
        _mutableBitSet1024B.Set(240); _mutableBitSet1024B.Set(480); _mutableBitSet1024B.Set(800); _mutableBitSet1024B.Set(960);

        _mutableBitVector256A = BitVector<Bit256>.Empty;
        _mutableBitVector256A.Set(0); _mutableBitVector256A.Set(60); _mutableBitVector256A.Set(120); _mutableBitVector256A.Set(180);
        _mutableBitVector256B = BitVector<Bit256>.Empty;
        _mutableBitVector256B.Set(60); _mutableBitVector256B.Set(120); _mutableBitVector256B.Set(200); _mutableBitVector256B.Set(240);
        _mutableBitVector512A = BitVector<Bit512>.Empty;
        _mutableBitVector512A.Set(0); _mutableBitVector512A.Set(120); _mutableBitVector512A.Set(240); _mutableBitVector512A.Set(360);
        _mutableBitVector512B = BitVector<Bit512>.Empty;
        _mutableBitVector512B.Set(120); _mutableBitVector512B.Set(240); _mutableBitVector512B.Set(400); _mutableBitVector512B.Set(480);
        _mutableBitVector1024A = BitVector<Bit1024>.Empty;
        _mutableBitVector1024A.Set(0); _mutableBitVector1024A.Set(240); _mutableBitVector1024A.Set(480); _mutableBitVector1024A.Set(720);
        _mutableBitVector1024B = BitVector<Bit1024>.Empty;
        _mutableBitVector1024B.Set(240); _mutableBitVector1024B.Set(480); _mutableBitVector1024B.Set(800); _mutableBitVector1024B.Set(960);

        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true); _bitArray64A.Set(30, true); _bitArray64A.Set(40, true); _bitArray64A.Set(50, true);
        _bitArray64B = new BitArray(64);
        _bitArray64B.Set(10, true); _bitArray64B.Set(20, true); _bitArray64B.Set(40, true); _bitArray64B.Set(63, true);

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true); _bitArray256A.Set(180, true);
        _bitArray256B = new BitArray(256);
        _bitArray256B.Set(60, true); _bitArray256B.Set(120, true); _bitArray256B.Set(200, true); _bitArray256B.Set(240, true);

        _bitArray512A = new BitArray(512);
        _bitArray512A.Set(0, true); _bitArray512A.Set(120, true); _bitArray512A.Set(240, true); _bitArray512A.Set(360, true);
        _bitArray512B = new BitArray(512);
        _bitArray512B.Set(120, true); _bitArray512B.Set(240, true); _bitArray512B.Set(400, true); _bitArray512B.Set(480, true);

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true); _bitArray1024A.Set(720, true);
        _bitArray1024B = new BitArray(1024);
        _bitArray1024B.Set(240, true); _bitArray1024B.Set(480, true); _bitArray1024B.Set(800, true); _bitArray1024B.Set(960, true);
    }

    [Benchmark]
    public long Int64_And() => _int64A & _int64B;

    [Benchmark]
    public ImmutableBitSet<Bit64> Bit64_And() => _bit64A.And(_bit64B);

    [Benchmark]
    public BitArray BitArray64_And() => _bitArray64A.And(_bitArray64B);

    [Benchmark(Baseline = true)]
    public ImmutableBitSet<Bit256> Bit256_And() => _bit256A.And(_bit256B);

    [Benchmark]
    public ImmutableBitVector<Bit256> BitVector256_And() => _immutableBitVector256A.And(in _immutableBitVector256B);

    [Benchmark]
    public void BitVectorT256_And() => _bitVectorT256A.And(in _bitVectorT256B);

    [Benchmark]
    public void MutableBitSet256_And() => _mutableBitSet256A.And(in _mutableBitSet256B);

    [Benchmark]
    public void MutableBitVector256_And() => _mutableBitVector256A.And(in _mutableBitVector256B);

    [Benchmark]
    public BitArray BitArray256_And() => _bitArray256A.And(_bitArray256B);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_And() => _bit512A.And(_bit512B);

    [Benchmark]
    public ImmutableBitVector<Bit512> BitVector512_And() => _immutableBitVector512A.And(in _immutableBitVector512B);

    [Benchmark]
    public void BitVectorT512_And() => _bitVectorT512A.And(in _bitVectorT512B);

    [Benchmark]
    public void MutableBitSet512_And() => _mutableBitSet512A.And(in _mutableBitSet512B);

    [Benchmark]
    public void MutableBitVector512_And() => _mutableBitVector512A.And(in _mutableBitVector512B);

    [Benchmark]
    public BitArray BitArray512_And() => _bitArray512A.And(_bitArray512B);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_And() => _bit1024A.And(_bit1024B);

    [Benchmark]
    public ImmutableBitVector<Bit1024> BitVector1024_And() => _immutableBitVector1024A.And(in _immutableBitVector1024B);

    [Benchmark]
    public void BitVectorT1024_And() => _bitVectorT1024A.And(in _bitVectorT1024B);

    [Benchmark]
    public void MutableBitSet1024_And() => _mutableBitSet1024A.And(in _mutableBitSet1024B);

    [Benchmark]
    public void MutableBitVector1024_And() => _mutableBitVector1024A.And(in _mutableBitVector1024B);

    [Benchmark]
    public BitArray BitArray1024_And() => _bitArray1024A.And(_bitArray1024B);
}

[Config(typeof(NativeAotConfig))]
public class BitSetOrBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private ImmutableBitVector<Bit256> _immutableBitVector256A, _immutableBitVector256B;
    private ImmutableBitVector<Bit512> _immutableBitVector512A, _immutableBitVector512B;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024A, _immutableBitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

    private BitSet<Bit256> _mutableBitSet256A, _mutableBitSet256B;
    private BitSet<Bit512> _mutableBitSet512A, _mutableBitSet512B;
    private BitSet<Bit1024> _mutableBitSet1024A, _mutableBitSet1024B;
    private BitVector<Bit256> _mutableBitVector256A, _mutableBitVector256B;
    private BitVector<Bit512> _mutableBitVector512A, _mutableBitVector512B;
    private BitVector<Bit1024> _mutableBitVector1024A, _mutableBitVector1024B;

    private BitArray _bitArray64A = null!, _bitArray64B = null!;
    private BitArray _bitArray256A = null!, _bitArray256B = null!;
    private BitArray _bitArray512A = null!, _bitArray512B = null!;
    private BitArray _bitArray1024A = null!, _bitArray1024B = null!;

    [GlobalSetup]
    public void Setup()
    {
        _int64A = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50);
        _int64B = (1L << 10) | (1L << 20) | (1L << 40) | (1L << 63);

        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(10).Set(20).Set(40).Set(63);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bit512A = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bit512B = ImmutableBitSet<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _immutableBitVector256A = ImmutableBitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _immutableBitVector256B = ImmutableBitVector<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _immutableBitVector512A = ImmutableBitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _immutableBitVector512B = ImmutableBitVector<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _immutableBitVector1024A = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _immutableBitVector1024B = ImmutableBitVector<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _bitVectorT256A = BitVectorT<Bit256>.Empty;
        _bitVectorT256A.Set(0); _bitVectorT256A.Set(60); _bitVectorT256A.Set(120); _bitVectorT256A.Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty;
        _bitVectorT256B.Set(60); _bitVectorT256B.Set(120); _bitVectorT256B.Set(200); _bitVectorT256B.Set(240);

        _bitVectorT512A = BitVectorT<Bit512>.Empty;
        _bitVectorT512A.Set(0); _bitVectorT512A.Set(120); _bitVectorT512A.Set(240); _bitVectorT512A.Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty;
        _bitVectorT512B.Set(120); _bitVectorT512B.Set(240); _bitVectorT512B.Set(400); _bitVectorT512B.Set(480);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024A.Set(0); _bitVectorT1024A.Set(240); _bitVectorT1024A.Set(480); _bitVectorT1024A.Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024B.Set(240); _bitVectorT1024B.Set(480); _bitVectorT1024B.Set(800); _bitVectorT1024B.Set(960);

        _mutableBitSet256A = BitSet<Bit256>.Empty;
        _mutableBitSet256A.Set(0); _mutableBitSet256A.Set(60); _mutableBitSet256A.Set(120); _mutableBitSet256A.Set(180);
        _mutableBitSet256B = BitSet<Bit256>.Empty;
        _mutableBitSet256B.Set(60); _mutableBitSet256B.Set(120); _mutableBitSet256B.Set(200); _mutableBitSet256B.Set(240);
        _mutableBitSet512A = BitSet<Bit512>.Empty;
        _mutableBitSet512A.Set(0); _mutableBitSet512A.Set(120); _mutableBitSet512A.Set(240); _mutableBitSet512A.Set(360);
        _mutableBitSet512B = BitSet<Bit512>.Empty;
        _mutableBitSet512B.Set(120); _mutableBitSet512B.Set(240); _mutableBitSet512B.Set(400); _mutableBitSet512B.Set(480);
        _mutableBitSet1024A = BitSet<Bit1024>.Empty;
        _mutableBitSet1024A.Set(0); _mutableBitSet1024A.Set(240); _mutableBitSet1024A.Set(480); _mutableBitSet1024A.Set(720);
        _mutableBitSet1024B = BitSet<Bit1024>.Empty;
        _mutableBitSet1024B.Set(240); _mutableBitSet1024B.Set(480); _mutableBitSet1024B.Set(800); _mutableBitSet1024B.Set(960);

        _mutableBitVector256A = BitVector<Bit256>.Empty;
        _mutableBitVector256A.Set(0); _mutableBitVector256A.Set(60); _mutableBitVector256A.Set(120); _mutableBitVector256A.Set(180);
        _mutableBitVector256B = BitVector<Bit256>.Empty;
        _mutableBitVector256B.Set(60); _mutableBitVector256B.Set(120); _mutableBitVector256B.Set(200); _mutableBitVector256B.Set(240);
        _mutableBitVector512A = BitVector<Bit512>.Empty;
        _mutableBitVector512A.Set(0); _mutableBitVector512A.Set(120); _mutableBitVector512A.Set(240); _mutableBitVector512A.Set(360);
        _mutableBitVector512B = BitVector<Bit512>.Empty;
        _mutableBitVector512B.Set(120); _mutableBitVector512B.Set(240); _mutableBitVector512B.Set(400); _mutableBitVector512B.Set(480);
        _mutableBitVector1024A = BitVector<Bit1024>.Empty;
        _mutableBitVector1024A.Set(0); _mutableBitVector1024A.Set(240); _mutableBitVector1024A.Set(480); _mutableBitVector1024A.Set(720);
        _mutableBitVector1024B = BitVector<Bit1024>.Empty;
        _mutableBitVector1024B.Set(240); _mutableBitVector1024B.Set(480); _mutableBitVector1024B.Set(800); _mutableBitVector1024B.Set(960);

        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true); _bitArray64A.Set(30, true); _bitArray64A.Set(40, true); _bitArray64A.Set(50, true);
        _bitArray64B = new BitArray(64);
        _bitArray64B.Set(10, true); _bitArray64B.Set(20, true); _bitArray64B.Set(40, true); _bitArray64B.Set(63, true);

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true); _bitArray256A.Set(180, true);
        _bitArray256B = new BitArray(256);
        _bitArray256B.Set(60, true); _bitArray256B.Set(120, true); _bitArray256B.Set(200, true); _bitArray256B.Set(240, true);

        _bitArray512A = new BitArray(512);
        _bitArray512A.Set(0, true); _bitArray512A.Set(120, true); _bitArray512A.Set(240, true); _bitArray512A.Set(360, true);
        _bitArray512B = new BitArray(512);
        _bitArray512B.Set(120, true); _bitArray512B.Set(240, true); _bitArray512B.Set(400, true); _bitArray512B.Set(480, true);

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true); _bitArray1024A.Set(720, true);
        _bitArray1024B = new BitArray(1024);
        _bitArray1024B.Set(240, true); _bitArray1024B.Set(480, true); _bitArray1024B.Set(800, true); _bitArray1024B.Set(960, true);
    }

    [Benchmark]
    public long Int64_Or() => _int64A | _int64B;

    [Benchmark]
    public ImmutableBitSet<Bit64> Bit64_Or() => _bit64A.Or(_bit64B);

    [Benchmark]
    public BitArray BitArray64_Or() => _bitArray64A.Or(_bitArray64B);

    [Benchmark(Baseline = true)]
    public ImmutableBitSet<Bit256> Bit256_Or() => _bit256A.Or(_bit256B);

    [Benchmark]
    public ImmutableBitVector<Bit256> BitVector256_Or() => _immutableBitVector256A.Or(in _immutableBitVector256B);

    [Benchmark]
    public void BitVectorT256_Or() => _bitVectorT256A.Or(in _bitVectorT256B);

    [Benchmark]
    public void MutableBitSet256_Or() => _mutableBitSet256A.Or(in _mutableBitSet256B);

    [Benchmark]
    public void MutableBitVector256_Or() => _mutableBitVector256A.Or(in _mutableBitVector256B);

    [Benchmark]
    public BitArray BitArray256_Or() => _bitArray256A.Or(_bitArray256B);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_Or() => _bit512A.Or(_bit512B);

    [Benchmark]
    public ImmutableBitVector<Bit512> BitVector512_Or() => _immutableBitVector512A.Or(in _immutableBitVector512B);

    [Benchmark]
    public void BitVectorT512_Or() => _bitVectorT512A.Or(in _bitVectorT512B);

    [Benchmark]
    public void MutableBitSet512_Or() => _mutableBitSet512A.Or(in _mutableBitSet512B);

    [Benchmark]
    public void MutableBitVector512_Or() => _mutableBitVector512A.Or(in _mutableBitVector512B);

    [Benchmark]
    public BitArray BitArray512_Or() => _bitArray512A.Or(_bitArray512B);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Or() => _bit1024A.Or(_bit1024B);

    [Benchmark]
    public ImmutableBitVector<Bit1024> BitVector1024_Or() => _immutableBitVector1024A.Or(in _immutableBitVector1024B);

    [Benchmark]
    public void BitVectorT1024_Or() => _bitVectorT1024A.Or(in _bitVectorT1024B);

    [Benchmark]
    public void MutableBitSet1024_Or() => _mutableBitSet1024A.Or(in _mutableBitSet1024B);

    [Benchmark]
    public void MutableBitVector1024_Or() => _mutableBitVector1024A.Or(in _mutableBitVector1024B);

    [Benchmark]
    public BitArray BitArray1024_Or() => _bitArray1024A.Or(_bitArray1024B);
}

[Config(typeof(NativeAotConfig))]
public class BitSetXorBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private ImmutableBitVector<Bit256> _immutableBitVector256A, _immutableBitVector256B;
    private ImmutableBitVector<Bit512> _immutableBitVector512A, _immutableBitVector512B;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024A, _immutableBitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

    private BitSet<Bit256> _mutableBitSet256A, _mutableBitSet256B;
    private BitSet<Bit512> _mutableBitSet512A, _mutableBitSet512B;
    private BitSet<Bit1024> _mutableBitSet1024A, _mutableBitSet1024B;
    private BitVector<Bit256> _mutableBitVector256A, _mutableBitVector256B;
    private BitVector<Bit512> _mutableBitVector512A, _mutableBitVector512B;
    private BitVector<Bit1024> _mutableBitVector1024A, _mutableBitVector1024B;

    private BitArray _bitArray64A = null!, _bitArray64B = null!;
    private BitArray _bitArray256A = null!, _bitArray256B = null!;
    private BitArray _bitArray512A = null!, _bitArray512B = null!;
    private BitArray _bitArray1024A = null!, _bitArray1024B = null!;

    [GlobalSetup]
    public void Setup()
    {
        _int64A = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50);
        _int64B = (1L << 10) | (1L << 20) | (1L << 40) | (1L << 63);

        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(10).Set(20).Set(40).Set(63);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bit512A = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bit512B = ImmutableBitSet<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _immutableBitVector256A = ImmutableBitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _immutableBitVector256B = ImmutableBitVector<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _immutableBitVector512A = ImmutableBitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _immutableBitVector512B = ImmutableBitVector<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _immutableBitVector1024A = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _immutableBitVector1024B = ImmutableBitVector<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _bitVectorT256A = BitVectorT<Bit256>.Empty;
        _bitVectorT256A.Set(0); _bitVectorT256A.Set(60); _bitVectorT256A.Set(120); _bitVectorT256A.Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty;
        _bitVectorT256B.Set(60); _bitVectorT256B.Set(120); _bitVectorT256B.Set(200); _bitVectorT256B.Set(240);

        _bitVectorT512A = BitVectorT<Bit512>.Empty;
        _bitVectorT512A.Set(0); _bitVectorT512A.Set(120); _bitVectorT512A.Set(240); _bitVectorT512A.Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty;
        _bitVectorT512B.Set(120); _bitVectorT512B.Set(240); _bitVectorT512B.Set(400); _bitVectorT512B.Set(480);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024A.Set(0); _bitVectorT1024A.Set(240); _bitVectorT1024A.Set(480); _bitVectorT1024A.Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024B.Set(240); _bitVectorT1024B.Set(480); _bitVectorT1024B.Set(800); _bitVectorT1024B.Set(960);

        _mutableBitSet256A = BitSet<Bit256>.Empty;
        _mutableBitSet256A.Set(0); _mutableBitSet256A.Set(60); _mutableBitSet256A.Set(120); _mutableBitSet256A.Set(180);
        _mutableBitSet256B = BitSet<Bit256>.Empty;
        _mutableBitSet256B.Set(60); _mutableBitSet256B.Set(120); _mutableBitSet256B.Set(200); _mutableBitSet256B.Set(240);
        _mutableBitSet512A = BitSet<Bit512>.Empty;
        _mutableBitSet512A.Set(0); _mutableBitSet512A.Set(120); _mutableBitSet512A.Set(240); _mutableBitSet512A.Set(360);
        _mutableBitSet512B = BitSet<Bit512>.Empty;
        _mutableBitSet512B.Set(120); _mutableBitSet512B.Set(240); _mutableBitSet512B.Set(400); _mutableBitSet512B.Set(480);
        _mutableBitSet1024A = BitSet<Bit1024>.Empty;
        _mutableBitSet1024A.Set(0); _mutableBitSet1024A.Set(240); _mutableBitSet1024A.Set(480); _mutableBitSet1024A.Set(720);
        _mutableBitSet1024B = BitSet<Bit1024>.Empty;
        _mutableBitSet1024B.Set(240); _mutableBitSet1024B.Set(480); _mutableBitSet1024B.Set(800); _mutableBitSet1024B.Set(960);

        _mutableBitVector256A = BitVector<Bit256>.Empty;
        _mutableBitVector256A.Set(0); _mutableBitVector256A.Set(60); _mutableBitVector256A.Set(120); _mutableBitVector256A.Set(180);
        _mutableBitVector256B = BitVector<Bit256>.Empty;
        _mutableBitVector256B.Set(60); _mutableBitVector256B.Set(120); _mutableBitVector256B.Set(200); _mutableBitVector256B.Set(240);
        _mutableBitVector512A = BitVector<Bit512>.Empty;
        _mutableBitVector512A.Set(0); _mutableBitVector512A.Set(120); _mutableBitVector512A.Set(240); _mutableBitVector512A.Set(360);
        _mutableBitVector512B = BitVector<Bit512>.Empty;
        _mutableBitVector512B.Set(120); _mutableBitVector512B.Set(240); _mutableBitVector512B.Set(400); _mutableBitVector512B.Set(480);
        _mutableBitVector1024A = BitVector<Bit1024>.Empty;
        _mutableBitVector1024A.Set(0); _mutableBitVector1024A.Set(240); _mutableBitVector1024A.Set(480); _mutableBitVector1024A.Set(720);
        _mutableBitVector1024B = BitVector<Bit1024>.Empty;
        _mutableBitVector1024B.Set(240); _mutableBitVector1024B.Set(480); _mutableBitVector1024B.Set(800); _mutableBitVector1024B.Set(960);

        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true); _bitArray64A.Set(30, true); _bitArray64A.Set(40, true); _bitArray64A.Set(50, true);
        _bitArray64B = new BitArray(64);
        _bitArray64B.Set(10, true); _bitArray64B.Set(20, true); _bitArray64B.Set(40, true); _bitArray64B.Set(63, true);

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true); _bitArray256A.Set(180, true);
        _bitArray256B = new BitArray(256);
        _bitArray256B.Set(60, true); _bitArray256B.Set(120, true); _bitArray256B.Set(200, true); _bitArray256B.Set(240, true);

        _bitArray512A = new BitArray(512);
        _bitArray512A.Set(0, true); _bitArray512A.Set(120, true); _bitArray512A.Set(240, true); _bitArray512A.Set(360, true);
        _bitArray512B = new BitArray(512);
        _bitArray512B.Set(120, true); _bitArray512B.Set(240, true); _bitArray512B.Set(400, true); _bitArray512B.Set(480, true);

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true); _bitArray1024A.Set(720, true);
        _bitArray1024B = new BitArray(1024);
        _bitArray1024B.Set(240, true); _bitArray1024B.Set(480, true); _bitArray1024B.Set(800, true); _bitArray1024B.Set(960, true);
    }

    [Benchmark]
    public long Int64_Xor() => _int64A ^ _int64B;

    [Benchmark]
    public ImmutableBitSet<Bit64> Bit64_Xor() => _bit64A.Xor(_bit64B);

    [Benchmark]
    public BitArray BitArray64_Xor() => _bitArray64A.Xor(_bitArray64B);

    [Benchmark(Baseline = true)]
    public ImmutableBitSet<Bit256> Bit256_Xor() => _bit256A.Xor(_bit256B);

    [Benchmark]
    public ImmutableBitVector<Bit256> BitVector256_Xor() => _immutableBitVector256A.Xor(in _immutableBitVector256B);

    [Benchmark]
    public void BitVectorT256_Xor() => _bitVectorT256A.Xor(in _bitVectorT256B);

    [Benchmark]
    public void MutableBitSet256_Xor() => _mutableBitSet256A.Xor(in _mutableBitSet256B);

    [Benchmark]
    public void MutableBitVector256_Xor() => _mutableBitVector256A.Xor(in _mutableBitVector256B);

    [Benchmark]
    public BitArray BitArray256_Xor() => _bitArray256A.Xor(_bitArray256B);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_Xor() => _bit512A.Xor(_bit512B);

    [Benchmark]
    public ImmutableBitVector<Bit512> BitVector512_Xor() => _immutableBitVector512A.Xor(in _immutableBitVector512B);

    [Benchmark]
    public void BitVectorT512_Xor() => _bitVectorT512A.Xor(in _bitVectorT512B);

    [Benchmark]
    public void MutableBitSet512_Xor() => _mutableBitSet512A.Xor(in _mutableBitSet512B);

    [Benchmark]
    public void MutableBitVector512_Xor() => _mutableBitVector512A.Xor(in _mutableBitVector512B);

    [Benchmark]
    public BitArray BitArray512_Xor() => _bitArray512A.Xor(_bitArray512B);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Xor() => _bit1024A.Xor(_bit1024B);

    [Benchmark]
    public ImmutableBitVector<Bit1024> BitVector1024_Xor() => _immutableBitVector1024A.Xor(in _immutableBitVector1024B);

    [Benchmark]
    public void BitVectorT1024_Xor() => _bitVectorT1024A.Xor(in _bitVectorT1024B);

    [Benchmark]
    public void MutableBitSet1024_Xor() => _mutableBitSet1024A.Xor(in _mutableBitSet1024B);

    [Benchmark]
    public void MutableBitVector1024_Xor() => _mutableBitVector1024A.Xor(in _mutableBitVector1024B);

    [Benchmark]
    public BitArray BitArray1024_Xor() => _bitArray1024A.Xor(_bitArray1024B);
}

[Config(typeof(NativeAotConfig))]
public class BitSetAndNotBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private ImmutableBitVector<Bit256> _immutableBitVector256A, _immutableBitVector256B;
    private ImmutableBitVector<Bit512> _immutableBitVector512A, _immutableBitVector512B;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024A, _immutableBitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

    private BitSet<Bit256> _mutableBitSet256A, _mutableBitSet256B;
    private BitSet<Bit512> _mutableBitSet512A, _mutableBitSet512B;
    private BitSet<Bit1024> _mutableBitSet1024A, _mutableBitSet1024B;
    private BitVector<Bit256> _mutableBitVector256A, _mutableBitVector256B;
    private BitVector<Bit512> _mutableBitVector512A, _mutableBitVector512B;
    private BitVector<Bit1024> _mutableBitVector1024A, _mutableBitVector1024B;

    private BitArray _bitArray64A = null!, _bitArray64BNot = null!;
    private BitArray _bitArray256A = null!, _bitArray256BNot = null!;
    private BitArray _bitArray512A = null!, _bitArray512BNot = null!;
    private BitArray _bitArray1024A = null!, _bitArray1024BNot = null!;

    [GlobalSetup]
    public void Setup()
    {
        _int64A = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50);
        _int64B = (1L << 10) | (1L << 20) | (1L << 40) | (1L << 63);

        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(10).Set(20).Set(40).Set(63);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bit512A = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bit512B = ImmutableBitSet<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _immutableBitVector256A = ImmutableBitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _immutableBitVector256B = ImmutableBitVector<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _immutableBitVector512A = ImmutableBitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _immutableBitVector512B = ImmutableBitVector<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _immutableBitVector1024A = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _immutableBitVector1024B = ImmutableBitVector<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _bitVectorT256A = BitVectorT<Bit256>.Empty;
        _bitVectorT256A.Set(0); _bitVectorT256A.Set(60); _bitVectorT256A.Set(120); _bitVectorT256A.Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty;
        _bitVectorT256B.Set(60); _bitVectorT256B.Set(120); _bitVectorT256B.Set(200); _bitVectorT256B.Set(240);

        _bitVectorT512A = BitVectorT<Bit512>.Empty;
        _bitVectorT512A.Set(0); _bitVectorT512A.Set(120); _bitVectorT512A.Set(240); _bitVectorT512A.Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty;
        _bitVectorT512B.Set(120); _bitVectorT512B.Set(240); _bitVectorT512B.Set(400); _bitVectorT512B.Set(480);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024A.Set(0); _bitVectorT1024A.Set(240); _bitVectorT1024A.Set(480); _bitVectorT1024A.Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024B.Set(240); _bitVectorT1024B.Set(480); _bitVectorT1024B.Set(800); _bitVectorT1024B.Set(960);

        _mutableBitSet256A = BitSet<Bit256>.Empty;
        _mutableBitSet256A.Set(0); _mutableBitSet256A.Set(60); _mutableBitSet256A.Set(120); _mutableBitSet256A.Set(180);
        _mutableBitSet256B = BitSet<Bit256>.Empty;
        _mutableBitSet256B.Set(60); _mutableBitSet256B.Set(120); _mutableBitSet256B.Set(200); _mutableBitSet256B.Set(240);
        _mutableBitSet512A = BitSet<Bit512>.Empty;
        _mutableBitSet512A.Set(0); _mutableBitSet512A.Set(120); _mutableBitSet512A.Set(240); _mutableBitSet512A.Set(360);
        _mutableBitSet512B = BitSet<Bit512>.Empty;
        _mutableBitSet512B.Set(120); _mutableBitSet512B.Set(240); _mutableBitSet512B.Set(400); _mutableBitSet512B.Set(480);
        _mutableBitSet1024A = BitSet<Bit1024>.Empty;
        _mutableBitSet1024A.Set(0); _mutableBitSet1024A.Set(240); _mutableBitSet1024A.Set(480); _mutableBitSet1024A.Set(720);
        _mutableBitSet1024B = BitSet<Bit1024>.Empty;
        _mutableBitSet1024B.Set(240); _mutableBitSet1024B.Set(480); _mutableBitSet1024B.Set(800); _mutableBitSet1024B.Set(960);

        _mutableBitVector256A = BitVector<Bit256>.Empty;
        _mutableBitVector256A.Set(0); _mutableBitVector256A.Set(60); _mutableBitVector256A.Set(120); _mutableBitVector256A.Set(180);
        _mutableBitVector256B = BitVector<Bit256>.Empty;
        _mutableBitVector256B.Set(60); _mutableBitVector256B.Set(120); _mutableBitVector256B.Set(200); _mutableBitVector256B.Set(240);
        _mutableBitVector512A = BitVector<Bit512>.Empty;
        _mutableBitVector512A.Set(0); _mutableBitVector512A.Set(120); _mutableBitVector512A.Set(240); _mutableBitVector512A.Set(360);
        _mutableBitVector512B = BitVector<Bit512>.Empty;
        _mutableBitVector512B.Set(120); _mutableBitVector512B.Set(240); _mutableBitVector512B.Set(400); _mutableBitVector512B.Set(480);
        _mutableBitVector1024A = BitVector<Bit1024>.Empty;
        _mutableBitVector1024A.Set(0); _mutableBitVector1024A.Set(240); _mutableBitVector1024A.Set(480); _mutableBitVector1024A.Set(720);
        _mutableBitVector1024B = BitVector<Bit1024>.Empty;
        _mutableBitVector1024B.Set(240); _mutableBitVector1024B.Set(480); _mutableBitVector1024B.Set(800); _mutableBitVector1024B.Set(960);

        _bitArray64A = new BitArray(64);
        _bitArray64A.Set(0, true); _bitArray64A.Set(10, true); _bitArray64A.Set(20, true); _bitArray64A.Set(30, true); _bitArray64A.Set(40, true); _bitArray64A.Set(50, true);
        _bitArray64BNot = new BitArray(64);
        _bitArray64BNot.Set(10, true); _bitArray64BNot.Set(20, true); _bitArray64BNot.Set(40, true); _bitArray64BNot.Set(63, true);
        _bitArray64BNot.Not();

        _bitArray256A = new BitArray(256);
        _bitArray256A.Set(0, true); _bitArray256A.Set(60, true); _bitArray256A.Set(120, true); _bitArray256A.Set(180, true);
        _bitArray256BNot = new BitArray(256);
        _bitArray256BNot.Set(60, true); _bitArray256BNot.Set(120, true); _bitArray256BNot.Set(200, true); _bitArray256BNot.Set(240, true);
        _bitArray256BNot.Not();

        _bitArray512A = new BitArray(512);
        _bitArray512A.Set(0, true); _bitArray512A.Set(120, true); _bitArray512A.Set(240, true); _bitArray512A.Set(360, true);
        _bitArray512BNot = new BitArray(512);
        _bitArray512BNot.Set(120, true); _bitArray512BNot.Set(240, true); _bitArray512BNot.Set(400, true); _bitArray512BNot.Set(480, true);
        _bitArray512BNot.Not();

        _bitArray1024A = new BitArray(1024);
        _bitArray1024A.Set(0, true); _bitArray1024A.Set(240, true); _bitArray1024A.Set(480, true); _bitArray1024A.Set(720, true);
        _bitArray1024BNot = new BitArray(1024);
        _bitArray1024BNot.Set(240, true); _bitArray1024BNot.Set(480, true); _bitArray1024BNot.Set(800, true); _bitArray1024BNot.Set(960, true);
        _bitArray1024BNot.Not();
    }

    [Benchmark]
    public long Int64_AndNot() => _int64A & ~_int64B;

    [Benchmark]
    public ImmutableBitSet<Bit64> Bit64_AndNot() => _bit64A.AndNot(_bit64B);

    [Benchmark]
    public BitArray BitArray64_AndNot() => _bitArray64A.And(_bitArray64BNot);

    [Benchmark(Baseline = true)]
    public ImmutableBitSet<Bit256> Bit256_AndNot() => _bit256A.AndNot(_bit256B);

    [Benchmark]
    public ImmutableBitVector<Bit256> BitVector256_AndNot() => _immutableBitVector256A.AndNot(in _immutableBitVector256B);

    [Benchmark]
    public void BitVectorT256_AndNot() => _bitVectorT256A.AndNot(in _bitVectorT256B);

    [Benchmark]
    public void MutableBitSet256_AndNot() => _mutableBitSet256A.AndNot(in _mutableBitSet256B);

    [Benchmark]
    public void MutableBitVector256_AndNot() => _mutableBitVector256A.AndNot(in _mutableBitVector256B);

    [Benchmark]
    public BitArray BitArray256_AndNot() => _bitArray256A.And(_bitArray256BNot);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_AndNot() => _bit512A.AndNot(_bit512B);

    [Benchmark]
    public ImmutableBitVector<Bit512> BitVector512_AndNot() => _immutableBitVector512A.AndNot(in _immutableBitVector512B);

    [Benchmark]
    public void BitVectorT512_AndNot() => _bitVectorT512A.AndNot(in _bitVectorT512B);

    [Benchmark]
    public void MutableBitSet512_AndNot() => _mutableBitSet512A.AndNot(in _mutableBitSet512B);

    [Benchmark]
    public void MutableBitVector512_AndNot() => _mutableBitVector512A.AndNot(in _mutableBitVector512B);

    [Benchmark]
    public BitArray BitArray512_AndNot() => _bitArray512A.And(_bitArray512BNot);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_AndNot() => _bit1024A.AndNot(_bit1024B);

    [Benchmark]
    public ImmutableBitVector<Bit1024> BitVector1024_AndNot() => _immutableBitVector1024A.AndNot(in _immutableBitVector1024B);

    [Benchmark]
    public void BitVectorT1024_AndNot() => _bitVectorT1024A.AndNot(in _bitVectorT1024B);

    [Benchmark]
    public void MutableBitSet1024_AndNot() => _mutableBitSet1024A.AndNot(in _mutableBitSet1024B);

    [Benchmark]
    public void MutableBitVector1024_AndNot() => _mutableBitVector1024A.AndNot(in _mutableBitVector1024B);

    [Benchmark]
    public BitArray BitArray1024_AndNot() => _bitArray1024A.And(_bitArray1024BNot);
}

// ============================================================================
// Query Operation Benchmarks (ContainsAll, ContainsAny, ContainsNone)
// ============================================================================

[Config(typeof(NativeAotConfig))]
public class BitSetContainsAllBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private ImmutableBitVector<Bit256> _immutableBitVector256A, _immutableBitVector256B;
    private ImmutableBitVector<Bit512> _immutableBitVector512A, _immutableBitVector512B;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024A, _immutableBitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

    private BitSet<Bit256> _mutableBitSet256A, _mutableBitSet256B;
    private BitSet<Bit512> _mutableBitSet512A, _mutableBitSet512B;
    private BitSet<Bit1024> _mutableBitSet1024A, _mutableBitSet1024B;
    private BitVector<Bit256> _mutableBitVector256A, _mutableBitVector256B;
    private BitVector<Bit512> _mutableBitVector512A, _mutableBitVector512B;
    private BitVector<Bit1024> _mutableBitVector1024A, _mutableBitVector1024B;

    [GlobalSetup]
    public void Setup()
    {
        // A contains all bits from B
        _int64A = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50) | (1L << 63);
        _int64B = (1L << 10) | (1L << 20) | (1L << 40);

        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50).Set(63);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(10).Set(20).Set(40);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(60).Set(120);

        _bit512A = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360).Set(480);
        _bit512B = ImmutableBitSet<Bit512>.Empty.Set(120).Set(240);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720).Set(960);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(240).Set(480);

        _immutableBitVector256A = ImmutableBitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240);
        _immutableBitVector256B = ImmutableBitVector<Bit256>.Empty.Set(60).Set(120);

        _immutableBitVector512A = ImmutableBitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360).Set(480);
        _immutableBitVector512B = ImmutableBitVector<Bit512>.Empty.Set(120).Set(240);

        _immutableBitVector1024A = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720).Set(960);
        _immutableBitVector1024B = ImmutableBitVector<Bit1024>.Empty.Set(240).Set(480);

        _bitVectorT256A = BitVectorT<Bit256>.Empty;
        _bitVectorT256A.Set(0); _bitVectorT256A.Set(60); _bitVectorT256A.Set(120); _bitVectorT256A.Set(180); _bitVectorT256A.Set(240);
        _bitVectorT256B = BitVectorT<Bit256>.Empty;
        _bitVectorT256B.Set(60); _bitVectorT256B.Set(120);

        _bitVectorT512A = BitVectorT<Bit512>.Empty;
        _bitVectorT512A.Set(0); _bitVectorT512A.Set(120); _bitVectorT512A.Set(240); _bitVectorT512A.Set(360); _bitVectorT512A.Set(480);
        _bitVectorT512B = BitVectorT<Bit512>.Empty;
        _bitVectorT512B.Set(120); _bitVectorT512B.Set(240);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024A.Set(0); _bitVectorT1024A.Set(240); _bitVectorT1024A.Set(480); _bitVectorT1024A.Set(720); _bitVectorT1024A.Set(960);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024B.Set(240); _bitVectorT1024B.Set(480);

        _mutableBitSet256A = BitSet<Bit256>.Empty;
        _mutableBitSet256A.Set(0); _mutableBitSet256A.Set(60); _mutableBitSet256A.Set(120); _mutableBitSet256A.Set(180); _mutableBitSet256A.Set(240);
        _mutableBitSet256B = BitSet<Bit256>.Empty;
        _mutableBitSet256B.Set(60); _mutableBitSet256B.Set(120);
        _mutableBitSet512A = BitSet<Bit512>.Empty;
        _mutableBitSet512A.Set(0); _mutableBitSet512A.Set(120); _mutableBitSet512A.Set(240); _mutableBitSet512A.Set(360); _mutableBitSet512A.Set(480);
        _mutableBitSet512B = BitSet<Bit512>.Empty;
        _mutableBitSet512B.Set(120); _mutableBitSet512B.Set(240);
        _mutableBitSet1024A = BitSet<Bit1024>.Empty;
        _mutableBitSet1024A.Set(0); _mutableBitSet1024A.Set(240); _mutableBitSet1024A.Set(480); _mutableBitSet1024A.Set(720); _mutableBitSet1024A.Set(960);
        _mutableBitSet1024B = BitSet<Bit1024>.Empty;
        _mutableBitSet1024B.Set(240); _mutableBitSet1024B.Set(480);

        _mutableBitVector256A = BitVector<Bit256>.Empty;
        _mutableBitVector256A.Set(0); _mutableBitVector256A.Set(60); _mutableBitVector256A.Set(120); _mutableBitVector256A.Set(180); _mutableBitVector256A.Set(240);
        _mutableBitVector256B = BitVector<Bit256>.Empty;
        _mutableBitVector256B.Set(60); _mutableBitVector256B.Set(120);
        _mutableBitVector512A = BitVector<Bit512>.Empty;
        _mutableBitVector512A.Set(0); _mutableBitVector512A.Set(120); _mutableBitVector512A.Set(240); _mutableBitVector512A.Set(360); _mutableBitVector512A.Set(480);
        _mutableBitVector512B = BitVector<Bit512>.Empty;
        _mutableBitVector512B.Set(120); _mutableBitVector512B.Set(240);
        _mutableBitVector1024A = BitVector<Bit1024>.Empty;
        _mutableBitVector1024A.Set(0); _mutableBitVector1024A.Set(240); _mutableBitVector1024A.Set(480); _mutableBitVector1024A.Set(720); _mutableBitVector1024A.Set(960);
        _mutableBitVector1024B = BitVector<Bit1024>.Empty;
        _mutableBitVector1024B.Set(240); _mutableBitVector1024B.Set(480);
    }

    [Benchmark]
    public bool Int64_ContainsAll() => (_int64A & _int64B) == _int64B;

    [Benchmark]
    public bool Bit64_ContainsAll() => _bit64A.ContainsAll(_bit64B);

    [Benchmark(Baseline = true)]
    public bool Bit256_ContainsAll() => _bit256A.ContainsAll(_bit256B);

    [Benchmark]
    public bool BitVector256_ContainsAll() => _immutableBitVector256A.ContainsAll(in _immutableBitVector256B);

    [Benchmark]
    public bool BitVectorT256_ContainsAll() => _bitVectorT256A.ContainsAll(in _bitVectorT256B);

    [Benchmark]
    public bool MutableBitSet256_ContainsAll() => _mutableBitSet256A.ContainsAll(in _mutableBitSet256B);

    [Benchmark]
    public bool MutableBitVector256_ContainsAll() => _mutableBitVector256A.ContainsAll(in _mutableBitVector256B);

    [Benchmark]
    public bool Bit512_ContainsAll() => _bit512A.ContainsAll(_bit512B);

    [Benchmark]
    public bool BitVector512_ContainsAll() => _immutableBitVector512A.ContainsAll(in _immutableBitVector512B);

    [Benchmark]
    public bool BitVectorT512_ContainsAll() => _bitVectorT512A.ContainsAll(in _bitVectorT512B);

    [Benchmark]
    public bool MutableBitSet512_ContainsAll() => _mutableBitSet512A.ContainsAll(in _mutableBitSet512B);

    [Benchmark]
    public bool MutableBitVector512_ContainsAll() => _mutableBitVector512A.ContainsAll(in _mutableBitVector512B);

    [Benchmark]
    public bool Bit1024_ContainsAll() => _bit1024A.ContainsAll(_bit1024B);

    [Benchmark]
    public bool BitVector1024_ContainsAll() => _immutableBitVector1024A.ContainsAll(in _immutableBitVector1024B);

    [Benchmark]
    public bool BitVectorT1024_ContainsAll() => _bitVectorT1024A.ContainsAll(in _bitVectorT1024B);

    [Benchmark]
    public bool MutableBitSet1024_ContainsAll() => _mutableBitSet1024A.ContainsAll(in _mutableBitSet1024B);

    [Benchmark]
    public bool MutableBitVector1024_ContainsAll() => _mutableBitVector1024A.ContainsAll(in _mutableBitVector1024B);
}

[Config(typeof(NativeAotConfig))]
public class BitSetContainsAnyBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private ImmutableBitVector<Bit256> _immutableBitVector256A, _immutableBitVector256B;
    private ImmutableBitVector<Bit512> _immutableBitVector512A, _immutableBitVector512B;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024A, _immutableBitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

    private BitSet<Bit256> _mutableBitSet256A, _mutableBitSet256B;
    private BitSet<Bit512> _mutableBitSet512A, _mutableBitSet512B;
    private BitSet<Bit1024> _mutableBitSet1024A, _mutableBitSet1024B;
    private BitVector<Bit256> _mutableBitVector256A, _mutableBitVector256B;
    private BitVector<Bit512> _mutableBitVector512A, _mutableBitVector512B;
    private BitVector<Bit1024> _mutableBitVector1024A, _mutableBitVector1024B;

    [GlobalSetup]
    public void Setup()
    {
        // A and B have some overlapping bits
        _int64A = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50);
        _int64B = (1L << 20) | (1L << 45) | (1L << 63);

        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(20).Set(45).Set(63);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(120).Set(200).Set(240);

        _bit512A = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bit512B = ImmutableBitSet<Bit512>.Empty.Set(240).Set(400).Set(480);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(480).Set(800).Set(960);

        _immutableBitVector256A = ImmutableBitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _immutableBitVector256B = ImmutableBitVector<Bit256>.Empty.Set(120).Set(200).Set(240);

        _immutableBitVector512A = ImmutableBitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _immutableBitVector512B = ImmutableBitVector<Bit512>.Empty.Set(240).Set(400).Set(480);

        _immutableBitVector1024A = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _immutableBitVector1024B = ImmutableBitVector<Bit1024>.Empty.Set(480).Set(800).Set(960);

        _bitVectorT256A = BitVectorT<Bit256>.Empty;
        _bitVectorT256A.Set(0); _bitVectorT256A.Set(60); _bitVectorT256A.Set(120); _bitVectorT256A.Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty;
        _bitVectorT256B.Set(120); _bitVectorT256B.Set(200); _bitVectorT256B.Set(240);

        _bitVectorT512A = BitVectorT<Bit512>.Empty;
        _bitVectorT512A.Set(0); _bitVectorT512A.Set(120); _bitVectorT512A.Set(240); _bitVectorT512A.Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty;
        _bitVectorT512B.Set(240); _bitVectorT512B.Set(400); _bitVectorT512B.Set(480);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024A.Set(0); _bitVectorT1024A.Set(240); _bitVectorT1024A.Set(480); _bitVectorT1024A.Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024B.Set(480); _bitVectorT1024B.Set(800); _bitVectorT1024B.Set(960);

        _mutableBitSet256A = BitSet<Bit256>.Empty;
        _mutableBitSet256A.Set(0); _mutableBitSet256A.Set(60); _mutableBitSet256A.Set(120); _mutableBitSet256A.Set(180);
        _mutableBitSet256B = BitSet<Bit256>.Empty;
        _mutableBitSet256B.Set(120); _mutableBitSet256B.Set(200); _mutableBitSet256B.Set(240);
        _mutableBitSet512A = BitSet<Bit512>.Empty;
        _mutableBitSet512A.Set(0); _mutableBitSet512A.Set(120); _mutableBitSet512A.Set(240); _mutableBitSet512A.Set(360);
        _mutableBitSet512B = BitSet<Bit512>.Empty;
        _mutableBitSet512B.Set(240); _mutableBitSet512B.Set(400); _mutableBitSet512B.Set(480);
        _mutableBitSet1024A = BitSet<Bit1024>.Empty;
        _mutableBitSet1024A.Set(0); _mutableBitSet1024A.Set(240); _mutableBitSet1024A.Set(480); _mutableBitSet1024A.Set(720);
        _mutableBitSet1024B = BitSet<Bit1024>.Empty;
        _mutableBitSet1024B.Set(480); _mutableBitSet1024B.Set(800); _mutableBitSet1024B.Set(960);

        _mutableBitVector256A = BitVector<Bit256>.Empty;
        _mutableBitVector256A.Set(0); _mutableBitVector256A.Set(60); _mutableBitVector256A.Set(120); _mutableBitVector256A.Set(180);
        _mutableBitVector256B = BitVector<Bit256>.Empty;
        _mutableBitVector256B.Set(120); _mutableBitVector256B.Set(200); _mutableBitVector256B.Set(240);
        _mutableBitVector512A = BitVector<Bit512>.Empty;
        _mutableBitVector512A.Set(0); _mutableBitVector512A.Set(120); _mutableBitVector512A.Set(240); _mutableBitVector512A.Set(360);
        _mutableBitVector512B = BitVector<Bit512>.Empty;
        _mutableBitVector512B.Set(240); _mutableBitVector512B.Set(400); _mutableBitVector512B.Set(480);
        _mutableBitVector1024A = BitVector<Bit1024>.Empty;
        _mutableBitVector1024A.Set(0); _mutableBitVector1024A.Set(240); _mutableBitVector1024A.Set(480); _mutableBitVector1024A.Set(720);
        _mutableBitVector1024B = BitVector<Bit1024>.Empty;
        _mutableBitVector1024B.Set(480); _mutableBitVector1024B.Set(800); _mutableBitVector1024B.Set(960);
    }

    [Benchmark]
    public bool Int64_ContainsAny() => (_int64A & _int64B) != 0;

    [Benchmark]
    public bool Bit64_ContainsAny() => _bit64A.ContainsAny(_bit64B);

    [Benchmark(Baseline = true)]
    public bool Bit256_ContainsAny() => _bit256A.ContainsAny(_bit256B);

    [Benchmark]
    public bool BitVector256_ContainsAny() => _immutableBitVector256A.ContainsAny(in _immutableBitVector256B);

    [Benchmark]
    public bool BitVectorT256_ContainsAny() => _bitVectorT256A.ContainsAny(in _bitVectorT256B);

    [Benchmark]
    public bool MutableBitSet256_ContainsAny() => _mutableBitSet256A.ContainsAny(in _mutableBitSet256B);

    [Benchmark]
    public bool MutableBitVector256_ContainsAny() => _mutableBitVector256A.ContainsAny(in _mutableBitVector256B);

    [Benchmark]
    public bool Bit512_ContainsAny() => _bit512A.ContainsAny(_bit512B);

    [Benchmark]
    public bool BitVector512_ContainsAny() => _immutableBitVector512A.ContainsAny(in _immutableBitVector512B);

    [Benchmark]
    public bool BitVectorT512_ContainsAny() => _bitVectorT512A.ContainsAny(in _bitVectorT512B);

    [Benchmark]
    public bool MutableBitSet512_ContainsAny() => _mutableBitSet512A.ContainsAny(in _mutableBitSet512B);

    [Benchmark]
    public bool MutableBitVector512_ContainsAny() => _mutableBitVector512A.ContainsAny(in _mutableBitVector512B);

    [Benchmark]
    public bool Bit1024_ContainsAny() => _bit1024A.ContainsAny(_bit1024B);

    [Benchmark]
    public bool BitVector1024_ContainsAny() => _immutableBitVector1024A.ContainsAny(in _immutableBitVector1024B);

    [Benchmark]
    public bool BitVectorT1024_ContainsAny() => _bitVectorT1024A.ContainsAny(in _bitVectorT1024B);

    [Benchmark]
    public bool MutableBitSet1024_ContainsAny() => _mutableBitSet1024A.ContainsAny(in _mutableBitSet1024B);

    [Benchmark]
    public bool MutableBitVector1024_ContainsAny() => _mutableBitVector1024A.ContainsAny(in _mutableBitVector1024B);
}

[Config(typeof(NativeAotConfig))]
public class BitSetContainsNoneBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private ImmutableBitVector<Bit256> _immutableBitVector256A, _immutableBitVector256B;
    private ImmutableBitVector<Bit512> _immutableBitVector512A, _immutableBitVector512B;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024A, _immutableBitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

    private BitSet<Bit256> _mutableBitSet256A, _mutableBitSet256B;
    private BitSet<Bit512> _mutableBitSet512A, _mutableBitSet512B;
    private BitSet<Bit1024> _mutableBitSet1024A, _mutableBitSet1024B;
    private BitVector<Bit256> _mutableBitVector256A, _mutableBitVector256B;
    private BitVector<Bit512> _mutableBitVector512A, _mutableBitVector512B;
    private BitVector<Bit1024> _mutableBitVector1024A, _mutableBitVector1024B;

    [GlobalSetup]
    public void Setup()
    {
        // A and B have no overlapping bits
        _int64A = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50);
        _int64B = (1L << 5) | (1L << 15) | (1L << 25) | (1L << 35) | (1L << 45) | (1L << 55);

        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(5).Set(15).Set(25).Set(35).Set(45).Set(55);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(30).Set(90).Set(150).Set(210);

        _bit512A = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bit512B = ImmutableBitSet<Bit512>.Empty.Set(60).Set(180).Set(300).Set(420);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(120).Set(360).Set(600).Set(840);

        _immutableBitVector256A = ImmutableBitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _immutableBitVector256B = ImmutableBitVector<Bit256>.Empty.Set(30).Set(90).Set(150).Set(210);

        _immutableBitVector512A = ImmutableBitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _immutableBitVector512B = ImmutableBitVector<Bit512>.Empty.Set(60).Set(180).Set(300).Set(420);

        _immutableBitVector1024A = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _immutableBitVector1024B = ImmutableBitVector<Bit1024>.Empty.Set(120).Set(360).Set(600).Set(840);

        _bitVectorT256A = BitVectorT<Bit256>.Empty;
        _bitVectorT256A.Set(0); _bitVectorT256A.Set(60); _bitVectorT256A.Set(120); _bitVectorT256A.Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty;
        _bitVectorT256B.Set(30); _bitVectorT256B.Set(90); _bitVectorT256B.Set(150); _bitVectorT256B.Set(210);

        _bitVectorT512A = BitVectorT<Bit512>.Empty;
        _bitVectorT512A.Set(0); _bitVectorT512A.Set(120); _bitVectorT512A.Set(240); _bitVectorT512A.Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty;
        _bitVectorT512B.Set(60); _bitVectorT512B.Set(180); _bitVectorT512B.Set(300); _bitVectorT512B.Set(420);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024A.Set(0); _bitVectorT1024A.Set(240); _bitVectorT1024A.Set(480); _bitVectorT1024A.Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024B.Set(120); _bitVectorT1024B.Set(360); _bitVectorT1024B.Set(600); _bitVectorT1024B.Set(840);

        _mutableBitSet256A = BitSet<Bit256>.Empty;
        _mutableBitSet256A.Set(0); _mutableBitSet256A.Set(60); _mutableBitSet256A.Set(120); _mutableBitSet256A.Set(180);
        _mutableBitSet256B = BitSet<Bit256>.Empty;
        _mutableBitSet256B.Set(30); _mutableBitSet256B.Set(90); _mutableBitSet256B.Set(150); _mutableBitSet256B.Set(210);
        _mutableBitSet512A = BitSet<Bit512>.Empty;
        _mutableBitSet512A.Set(0); _mutableBitSet512A.Set(120); _mutableBitSet512A.Set(240); _mutableBitSet512A.Set(360);
        _mutableBitSet512B = BitSet<Bit512>.Empty;
        _mutableBitSet512B.Set(60); _mutableBitSet512B.Set(180); _mutableBitSet512B.Set(300); _mutableBitSet512B.Set(420);
        _mutableBitSet1024A = BitSet<Bit1024>.Empty;
        _mutableBitSet1024A.Set(0); _mutableBitSet1024A.Set(240); _mutableBitSet1024A.Set(480); _mutableBitSet1024A.Set(720);
        _mutableBitSet1024B = BitSet<Bit1024>.Empty;
        _mutableBitSet1024B.Set(120); _mutableBitSet1024B.Set(360); _mutableBitSet1024B.Set(600); _mutableBitSet1024B.Set(840);

        _mutableBitVector256A = BitVector<Bit256>.Empty;
        _mutableBitVector256A.Set(0); _mutableBitVector256A.Set(60); _mutableBitVector256A.Set(120); _mutableBitVector256A.Set(180);
        _mutableBitVector256B = BitVector<Bit256>.Empty;
        _mutableBitVector256B.Set(30); _mutableBitVector256B.Set(90); _mutableBitVector256B.Set(150); _mutableBitVector256B.Set(210);
        _mutableBitVector512A = BitVector<Bit512>.Empty;
        _mutableBitVector512A.Set(0); _mutableBitVector512A.Set(120); _mutableBitVector512A.Set(240); _mutableBitVector512A.Set(360);
        _mutableBitVector512B = BitVector<Bit512>.Empty;
        _mutableBitVector512B.Set(60); _mutableBitVector512B.Set(180); _mutableBitVector512B.Set(300); _mutableBitVector512B.Set(420);
        _mutableBitVector1024A = BitVector<Bit1024>.Empty;
        _mutableBitVector1024A.Set(0); _mutableBitVector1024A.Set(240); _mutableBitVector1024A.Set(480); _mutableBitVector1024A.Set(720);
        _mutableBitVector1024B = BitVector<Bit1024>.Empty;
        _mutableBitVector1024B.Set(120); _mutableBitVector1024B.Set(360); _mutableBitVector1024B.Set(600); _mutableBitVector1024B.Set(840);
    }

    [Benchmark]
    public bool Int64_ContainsNone() => (_int64A & _int64B) == 0;

    [Benchmark]
    public bool Bit64_ContainsNone() => _bit64A.ContainsNone(_bit64B);

    [Benchmark(Baseline = true)]
    public bool Bit256_ContainsNone() => _bit256A.ContainsNone(_bit256B);

    [Benchmark]
    public bool BitVector256_ContainsNone() => _immutableBitVector256A.ContainsNone(in _immutableBitVector256B);

    [Benchmark]
    public bool BitVectorT256_ContainsNone() => _bitVectorT256A.ContainsNone(in _bitVectorT256B);

    [Benchmark]
    public bool MutableBitSet256_ContainsNone() => _mutableBitSet256A.ContainsNone(in _mutableBitSet256B);

    [Benchmark]
    public bool MutableBitVector256_ContainsNone() => _mutableBitVector256A.ContainsNone(in _mutableBitVector256B);

    [Benchmark]
    public bool Bit512_ContainsNone() => _bit512A.ContainsNone(_bit512B);

    [Benchmark]
    public bool BitVector512_ContainsNone() => _immutableBitVector512A.ContainsNone(in _immutableBitVector512B);

    [Benchmark]
    public bool BitVectorT512_ContainsNone() => _bitVectorT512A.ContainsNone(in _bitVectorT512B);

    [Benchmark]
    public bool MutableBitSet512_ContainsNone() => _mutableBitSet512A.ContainsNone(in _mutableBitSet512B);

    [Benchmark]
    public bool MutableBitVector512_ContainsNone() => _mutableBitVector512A.ContainsNone(in _mutableBitVector512B);

    [Benchmark]
    public bool Bit1024_ContainsNone() => _bit1024A.ContainsNone(_bit1024B);

    [Benchmark]
    public bool BitVector1024_ContainsNone() => _immutableBitVector1024A.ContainsNone(in _immutableBitVector1024B);

    [Benchmark]
    public bool BitVectorT1024_ContainsNone() => _bitVectorT1024A.ContainsNone(in _bitVectorT1024B);

    [Benchmark]
    public bool MutableBitSet1024_ContainsNone() => _mutableBitSet1024A.ContainsNone(in _mutableBitSet1024B);

    [Benchmark]
    public bool MutableBitVector1024_ContainsNone() => _mutableBitVector1024A.ContainsNone(in _mutableBitVector1024B);
}

// ============================================================================
// Counting/Finding Benchmarks (PopCount, FirstSetBit, LastSetBit, IsEmpty)
// ============================================================================

[Config(typeof(NativeAotConfig))]
public class BitSetPopCountBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private ImmutableBitVector<Bit256> _immutableBitVector256;
    private ImmutableBitVector<Bit512> _immutableBitVector512;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    private BitSet<Bit256> _mutableBitSet256;
    private BitSet<Bit512> _mutableBitSet512;
    private BitSet<Bit1024> _mutableBitSet1024;
    private BitVector<Bit256> _mutableBitVector256;
    private BitVector<Bit512> _mutableBitVector512;
    private BitVector<Bit1024> _mutableBitVector1024;

    [GlobalSetup]
    public void Setup()
    {
        // Set multiple bits spread across the range
        _int64 = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50) | (1L << 60);
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50).Set(60);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(0).Set(30).Set(60).Set(90).Set(120).Set(150).Set(180).Set(210).Set(240);
        _bit512 = ImmutableBitSet<Bit512>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240).Set(300).Set(360).Set(420).Set(480);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(100).Set(200).Set(300).Set(400).Set(500).Set(600).Set(700).Set(800).Set(900).Set(1000);
        _immutableBitVector256 = ImmutableBitVector<Bit256>.Empty.Set(0).Set(30).Set(60).Set(90).Set(120).Set(150).Set(180).Set(210).Set(240);
        _immutableBitVector512 = ImmutableBitVector<Bit512>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240).Set(300).Set(360).Set(420).Set(480);
        _immutableBitVector1024 = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(100).Set(200).Set(300).Set(400).Set(500).Set(600).Set(700).Set(800).Set(900).Set(1000);
        _bitVectorT256 = BitVectorT<Bit256>.Empty;
        _bitVectorT256.Set(0); _bitVectorT256.Set(30); _bitVectorT256.Set(60); _bitVectorT256.Set(90); _bitVectorT256.Set(120); _bitVectorT256.Set(150); _bitVectorT256.Set(180); _bitVectorT256.Set(210); _bitVectorT256.Set(240);
        _bitVectorT512 = BitVectorT<Bit512>.Empty;
        _bitVectorT512.Set(0); _bitVectorT512.Set(60); _bitVectorT512.Set(120); _bitVectorT512.Set(180); _bitVectorT512.Set(240); _bitVectorT512.Set(300); _bitVectorT512.Set(360); _bitVectorT512.Set(420); _bitVectorT512.Set(480);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024.Set(0); _bitVectorT1024.Set(100); _bitVectorT1024.Set(200); _bitVectorT1024.Set(300); _bitVectorT1024.Set(400); _bitVectorT1024.Set(500); _bitVectorT1024.Set(600); _bitVectorT1024.Set(700); _bitVectorT1024.Set(800); _bitVectorT1024.Set(900); _bitVectorT1024.Set(1000);

        _mutableBitSet256 = BitSet<Bit256>.Empty;
        _mutableBitSet256.Set(0); _mutableBitSet256.Set(30); _mutableBitSet256.Set(60); _mutableBitSet256.Set(90); _mutableBitSet256.Set(120); _mutableBitSet256.Set(150); _mutableBitSet256.Set(180); _mutableBitSet256.Set(210); _mutableBitSet256.Set(240);
        _mutableBitSet512 = BitSet<Bit512>.Empty;
        _mutableBitSet512.Set(0); _mutableBitSet512.Set(60); _mutableBitSet512.Set(120); _mutableBitSet512.Set(180); _mutableBitSet512.Set(240); _mutableBitSet512.Set(300); _mutableBitSet512.Set(360); _mutableBitSet512.Set(420); _mutableBitSet512.Set(480);
        _mutableBitSet1024 = BitSet<Bit1024>.Empty;
        _mutableBitSet1024.Set(0); _mutableBitSet1024.Set(100); _mutableBitSet1024.Set(200); _mutableBitSet1024.Set(300); _mutableBitSet1024.Set(400); _mutableBitSet1024.Set(500); _mutableBitSet1024.Set(600); _mutableBitSet1024.Set(700); _mutableBitSet1024.Set(800); _mutableBitSet1024.Set(900); _mutableBitSet1024.Set(1000);
        _mutableBitVector256 = BitVector<Bit256>.Empty;
        _mutableBitVector256.Set(0); _mutableBitVector256.Set(30); _mutableBitVector256.Set(60); _mutableBitVector256.Set(90); _mutableBitVector256.Set(120); _mutableBitVector256.Set(150); _mutableBitVector256.Set(180); _mutableBitVector256.Set(210); _mutableBitVector256.Set(240);
        _mutableBitVector512 = BitVector<Bit512>.Empty;
        _mutableBitVector512.Set(0); _mutableBitVector512.Set(60); _mutableBitVector512.Set(120); _mutableBitVector512.Set(180); _mutableBitVector512.Set(240); _mutableBitVector512.Set(300); _mutableBitVector512.Set(360); _mutableBitVector512.Set(420); _mutableBitVector512.Set(480);
        _mutableBitVector1024 = BitVector<Bit1024>.Empty;
        _mutableBitVector1024.Set(0); _mutableBitVector1024.Set(100); _mutableBitVector1024.Set(200); _mutableBitVector1024.Set(300); _mutableBitVector1024.Set(400); _mutableBitVector1024.Set(500); _mutableBitVector1024.Set(600); _mutableBitVector1024.Set(700); _mutableBitVector1024.Set(800); _mutableBitVector1024.Set(900); _mutableBitVector1024.Set(1000);
    }

    [Benchmark]
    public int Int64_PopCount() => BitOperations.PopCount((ulong)_int64);

    [Benchmark]
    public int Bit64_PopCount() => _bit64.PopCount();

    [Benchmark(Baseline = true)]
    public int Bit256_PopCount() => _bit256.PopCount();

    [Benchmark]
    public int BitVector256_PopCount() => _immutableBitVector256.PopCount();

    [Benchmark]
    public int BitVectorT256_PopCount() => _bitVectorT256.PopCount();

    [Benchmark]
    public int MutableBitSet256_PopCount() => _mutableBitSet256.PopCount();

    [Benchmark]
    public int MutableBitVector256_PopCount() => _mutableBitVector256.PopCount();

    [Benchmark]
    public int Bit512_PopCount() => _bit512.PopCount();

    [Benchmark]
    public int BitVector512_PopCount() => _immutableBitVector512.PopCount();

    [Benchmark]
    public int BitVectorT512_PopCount() => _bitVectorT512.PopCount();

    [Benchmark]
    public int MutableBitSet512_PopCount() => _mutableBitSet512.PopCount();

    [Benchmark]
    public int MutableBitVector512_PopCount() => _mutableBitVector512.PopCount();

    [Benchmark]
    public int Bit1024_PopCount() => _bit1024.PopCount();

    [Benchmark]
    public int BitVector1024_PopCount() => _immutableBitVector1024.PopCount();

    [Benchmark]
    public int BitVectorT1024_PopCount() => _bitVectorT1024.PopCount();

    [Benchmark]
    public int MutableBitSet1024_PopCount() => _mutableBitSet1024.PopCount();

    [Benchmark]
    public int MutableBitVector1024_PopCount() => _mutableBitVector1024.PopCount();
}

[Config(typeof(NativeAotConfig))]
public class BitSetFirstSetBitBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private ImmutableBitVector<Bit256> _immutableBitVector256;
    private ImmutableBitVector<Bit512> _immutableBitVector512;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    private BitSet<Bit256> _mutableBitSet256;
    private BitSet<Bit512> _mutableBitSet512;
    private BitSet<Bit1024> _mutableBitSet1024;
    private BitVector<Bit256> _mutableBitVector256;
    private BitVector<Bit512> _mutableBitVector512;
    private BitVector<Bit1024> _mutableBitVector1024;

    [GlobalSetup]
    public void Setup()
    {
        // First bit is somewhere in the middle to test search
        _int64 = (1L << 25) | (1L << 40) | (1L << 50);
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(25).Set(40).Set(50);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(100).Set(150).Set(200);
        _bit512 = ImmutableBitSet<Bit512>.Empty.Set(200).Set(300).Set(400);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(400).Set(600).Set(800);
        _immutableBitVector256 = ImmutableBitVector<Bit256>.Empty.Set(100).Set(150).Set(200);
        _immutableBitVector512 = ImmutableBitVector<Bit512>.Empty.Set(200).Set(300).Set(400);
        _immutableBitVector1024 = ImmutableBitVector<Bit1024>.Empty.Set(400).Set(600).Set(800);
        _bitVectorT256 = BitVectorT<Bit256>.Empty;
        _bitVectorT256.Set(100); _bitVectorT256.Set(150); _bitVectorT256.Set(200);
        _bitVectorT512 = BitVectorT<Bit512>.Empty;
        _bitVectorT512.Set(200); _bitVectorT512.Set(300); _bitVectorT512.Set(400);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024.Set(400); _bitVectorT1024.Set(600); _bitVectorT1024.Set(800);

        _mutableBitSet256 = BitSet<Bit256>.Empty;
        _mutableBitSet256.Set(100); _mutableBitSet256.Set(150); _mutableBitSet256.Set(200);
        _mutableBitSet512 = BitSet<Bit512>.Empty;
        _mutableBitSet512.Set(200); _mutableBitSet512.Set(300); _mutableBitSet512.Set(400);
        _mutableBitSet1024 = BitSet<Bit1024>.Empty;
        _mutableBitSet1024.Set(400); _mutableBitSet1024.Set(600); _mutableBitSet1024.Set(800);
        _mutableBitVector256 = BitVector<Bit256>.Empty;
        _mutableBitVector256.Set(100); _mutableBitVector256.Set(150); _mutableBitVector256.Set(200);
        _mutableBitVector512 = BitVector<Bit512>.Empty;
        _mutableBitVector512.Set(200); _mutableBitVector512.Set(300); _mutableBitVector512.Set(400);
        _mutableBitVector1024 = BitVector<Bit1024>.Empty;
        _mutableBitVector1024.Set(400); _mutableBitVector1024.Set(600); _mutableBitVector1024.Set(800);
    }

    [Benchmark]
    public int Int64_FirstSetBit() => BitOperations.TrailingZeroCount(_int64);

    [Benchmark]
    public int Bit64_FirstSetBit() => _bit64.FirstSetBit();

    [Benchmark(Baseline = true)]
    public int Bit256_FirstSetBit() => _bit256.FirstSetBit();

    [Benchmark]
    public int BitVector256_FirstSetBit() => _immutableBitVector256.FirstSetBit();

    [Benchmark]
    public int BitVectorT256_FirstSetBit() => _bitVectorT256.FirstSetBit();

    [Benchmark]
    public int MutableBitSet256_FirstSetBit() => _mutableBitSet256.FirstSetBit();

    [Benchmark]
    public int MutableBitVector256_FirstSetBit() => _mutableBitVector256.FirstSetBit();

    [Benchmark]
    public int Bit512_FirstSetBit() => _bit512.FirstSetBit();

    [Benchmark]
    public int BitVector512_FirstSetBit() => _immutableBitVector512.FirstSetBit();

    [Benchmark]
    public int BitVectorT512_FirstSetBit() => _bitVectorT512.FirstSetBit();

    [Benchmark]
    public int MutableBitSet512_FirstSetBit() => _mutableBitSet512.FirstSetBit();

    [Benchmark]
    public int MutableBitVector512_FirstSetBit() => _mutableBitVector512.FirstSetBit();

    [Benchmark]
    public int Bit1024_FirstSetBit() => _bit1024.FirstSetBit();

    [Benchmark]
    public int BitVector1024_FirstSetBit() => _immutableBitVector1024.FirstSetBit();

    [Benchmark]
    public int BitVectorT1024_FirstSetBit() => _bitVectorT1024.FirstSetBit();

    [Benchmark]
    public int MutableBitSet1024_FirstSetBit() => _mutableBitSet1024.FirstSetBit();

    [Benchmark]
    public int MutableBitVector1024_FirstSetBit() => _mutableBitVector1024.FirstSetBit();
}

[Config(typeof(NativeAotConfig))]
public class BitSetLastSetBitBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private ImmutableBitVector<Bit256> _immutableBitVector256;
    private ImmutableBitVector<Bit512> _immutableBitVector512;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    private BitSet<Bit256> _mutableBitSet256;
    private BitSet<Bit512> _mutableBitSet512;
    private BitSet<Bit1024> _mutableBitSet1024;
    private BitVector<Bit256> _mutableBitVector256;
    private BitVector<Bit512> _mutableBitVector512;
    private BitVector<Bit1024> _mutableBitVector1024;

    [GlobalSetup]
    public void Setup()
    {
        // Last bit is somewhere in the middle to test search
        _int64 = (1L << 10) | (1L << 25) | (1L << 40);
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(10).Set(25).Set(40);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(50).Set(100).Set(150);
        _bit512 = ImmutableBitSet<Bit512>.Empty.Set(100).Set(200).Set(300);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(200).Set(400).Set(600);
        _immutableBitVector256 = ImmutableBitVector<Bit256>.Empty.Set(50).Set(100).Set(150);
        _immutableBitVector512 = ImmutableBitVector<Bit512>.Empty.Set(100).Set(200).Set(300);
        _immutableBitVector1024 = ImmutableBitVector<Bit1024>.Empty.Set(200).Set(400).Set(600);
        _bitVectorT256 = BitVectorT<Bit256>.Empty;
        _bitVectorT256.Set(50); _bitVectorT256.Set(100); _bitVectorT256.Set(150);
        _bitVectorT512 = BitVectorT<Bit512>.Empty;
        _bitVectorT512.Set(100); _bitVectorT512.Set(200); _bitVectorT512.Set(300);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024.Set(200); _bitVectorT1024.Set(400); _bitVectorT1024.Set(600);

        _mutableBitSet256 = BitSet<Bit256>.Empty;
        _mutableBitSet256.Set(50); _mutableBitSet256.Set(100); _mutableBitSet256.Set(150);
        _mutableBitSet512 = BitSet<Bit512>.Empty;
        _mutableBitSet512.Set(100); _mutableBitSet512.Set(200); _mutableBitSet512.Set(300);
        _mutableBitSet1024 = BitSet<Bit1024>.Empty;
        _mutableBitSet1024.Set(200); _mutableBitSet1024.Set(400); _mutableBitSet1024.Set(600);
        _mutableBitVector256 = BitVector<Bit256>.Empty;
        _mutableBitVector256.Set(50); _mutableBitVector256.Set(100); _mutableBitVector256.Set(150);
        _mutableBitVector512 = BitVector<Bit512>.Empty;
        _mutableBitVector512.Set(100); _mutableBitVector512.Set(200); _mutableBitVector512.Set(300);
        _mutableBitVector1024 = BitVector<Bit1024>.Empty;
        _mutableBitVector1024.Set(200); _mutableBitVector1024.Set(400); _mutableBitVector1024.Set(600);
    }

    [Benchmark]
    public int Int64_LastSetBit() => 63 - BitOperations.LeadingZeroCount((ulong)_int64);

    [Benchmark]
    public int Bit64_LastSetBit() => _bit64.LastSetBit();

    [Benchmark(Baseline = true)]
    public int Bit256_LastSetBit() => _bit256.LastSetBit();

    [Benchmark]
    public int BitVector256_LastSetBit() => _immutableBitVector256.LastSetBit();

    [Benchmark]
    public int BitVectorT256_LastSetBit() => _bitVectorT256.LastSetBit();

    [Benchmark]
    public int MutableBitSet256_LastSetBit() => _mutableBitSet256.LastSetBit();

    [Benchmark]
    public int MutableBitVector256_LastSetBit() => _mutableBitVector256.LastSetBit();

    [Benchmark]
    public int Bit512_LastSetBit() => _bit512.LastSetBit();

    [Benchmark]
    public int BitVector512_LastSetBit() => _immutableBitVector512.LastSetBit();

    [Benchmark]
    public int BitVectorT512_LastSetBit() => _bitVectorT512.LastSetBit();

    [Benchmark]
    public int MutableBitSet512_LastSetBit() => _mutableBitSet512.LastSetBit();

    [Benchmark]
    public int MutableBitVector512_LastSetBit() => _mutableBitVector512.LastSetBit();

    [Benchmark]
    public int Bit1024_LastSetBit() => _bit1024.LastSetBit();

    [Benchmark]
    public int BitVector1024_LastSetBit() => _immutableBitVector1024.LastSetBit();

    [Benchmark]
    public int BitVectorT1024_LastSetBit() => _bitVectorT1024.LastSetBit();

    [Benchmark]
    public int MutableBitSet1024_LastSetBit() => _mutableBitSet1024.LastSetBit();

    [Benchmark]
    public int MutableBitVector1024_LastSetBit() => _mutableBitVector1024.LastSetBit();
}

[Config(typeof(NativeAotConfig))]
public class BitSetIsEmptyBenchmarks
{
    private long _int64Empty;
    private long _int64NonEmpty;
    private ImmutableBitSet<Bit64> _bit64Empty;
    private ImmutableBitSet<Bit64> _bit64NonEmpty;
    private ImmutableBitSet<Bit256> _bit256Empty;
    private ImmutableBitSet<Bit256> _bit256NonEmpty;
    private ImmutableBitSet<Bit512> _bit512Empty;
    private ImmutableBitSet<Bit512> _bit512NonEmpty;
    private ImmutableBitSet<Bit1024> _bit1024Empty;
    private ImmutableBitSet<Bit1024> _bit1024NonEmpty;
    private ImmutableBitVector<Bit256> _immutableBitVector256Empty;
    private ImmutableBitVector<Bit256> _immutableBitVector256NonEmpty;
    private ImmutableBitVector<Bit512> _immutableBitVector512Empty;
    private ImmutableBitVector<Bit512> _immutableBitVector512NonEmpty;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024Empty;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024NonEmpty;
    private BitVectorT<Bit256> _bitVectorT256Empty;
    private BitVectorT<Bit256> _bitVectorT256NonEmpty;
    private BitVectorT<Bit512> _bitVectorT512Empty;
    private BitVectorT<Bit512> _bitVectorT512NonEmpty;
    private BitVectorT<Bit1024> _bitVectorT1024Empty;
    private BitVectorT<Bit1024> _bitVectorT1024NonEmpty;

    private BitSet<Bit256> _mutableBitSet256Empty;
    private BitSet<Bit256> _mutableBitSet256NonEmpty;
    private BitSet<Bit512> _mutableBitSet512Empty;
    private BitSet<Bit512> _mutableBitSet512NonEmpty;
    private BitSet<Bit1024> _mutableBitSet1024Empty;
    private BitSet<Bit1024> _mutableBitSet1024NonEmpty;
    private BitVector<Bit256> _mutableBitVector256Empty;
    private BitVector<Bit256> _mutableBitVector256NonEmpty;
    private BitVector<Bit512> _mutableBitVector512Empty;
    private BitVector<Bit512> _mutableBitVector512NonEmpty;
    private BitVector<Bit1024> _mutableBitVector1024Empty;
    private BitVector<Bit1024> _mutableBitVector1024NonEmpty;

    [GlobalSetup]
    public void Setup()
    {
        _int64Empty = 0L;
        _int64NonEmpty = 1L << 63;

        _bit64Empty = ImmutableBitSet<Bit64>.Empty;
        _bit64NonEmpty = ImmutableBitSet<Bit64>.Empty.Set(63);  // Last bit

        _bit256Empty = ImmutableBitSet<Bit256>.Empty;
        _bit256NonEmpty = ImmutableBitSet<Bit256>.Empty.Set(255);  // Last bit

        _bit512Empty = ImmutableBitSet<Bit512>.Empty;
        _bit512NonEmpty = ImmutableBitSet<Bit512>.Empty.Set(511);  // Last bit

        _bit1024Empty = ImmutableBitSet<Bit1024>.Empty;
        _bit1024NonEmpty = ImmutableBitSet<Bit1024>.Empty.Set(1023);  // Last bit

        _immutableBitVector256Empty = ImmutableBitVector<Bit256>.Empty;
        _immutableBitVector256NonEmpty = ImmutableBitVector<Bit256>.Empty.Set(255);  // Last bit

        _immutableBitVector512Empty = ImmutableBitVector<Bit512>.Empty;
        _immutableBitVector512NonEmpty = ImmutableBitVector<Bit512>.Empty.Set(511);  // Last bit

        _immutableBitVector1024Empty = ImmutableBitVector<Bit1024>.Empty;
        _immutableBitVector1024NonEmpty = ImmutableBitVector<Bit1024>.Empty.Set(1023);  // Last bit

        _bitVectorT256Empty = BitVectorT<Bit256>.Empty;
        _bitVectorT256NonEmpty = BitVectorT<Bit256>.Empty;
        _bitVectorT256NonEmpty.Set(255);  // Last bit

        _bitVectorT512Empty = BitVectorT<Bit512>.Empty;
        _bitVectorT512NonEmpty = BitVectorT<Bit512>.Empty;
        _bitVectorT512NonEmpty.Set(511);  // Last bit

        _bitVectorT1024Empty = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024NonEmpty = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024NonEmpty.Set(1023);  // Last bit

        _mutableBitSet256Empty = BitSet<Bit256>.Empty;
        _mutableBitSet256NonEmpty = BitSet<Bit256>.Empty;
        _mutableBitSet256NonEmpty.Set(255);
        _mutableBitSet512Empty = BitSet<Bit512>.Empty;
        _mutableBitSet512NonEmpty = BitSet<Bit512>.Empty;
        _mutableBitSet512NonEmpty.Set(511);
        _mutableBitSet1024Empty = BitSet<Bit1024>.Empty;
        _mutableBitSet1024NonEmpty = BitSet<Bit1024>.Empty;
        _mutableBitSet1024NonEmpty.Set(1023);

        _mutableBitVector256Empty = BitVector<Bit256>.Empty;
        _mutableBitVector256NonEmpty = BitVector<Bit256>.Empty;
        _mutableBitVector256NonEmpty.Set(255);
        _mutableBitVector512Empty = BitVector<Bit512>.Empty;
        _mutableBitVector512NonEmpty = BitVector<Bit512>.Empty;
        _mutableBitVector512NonEmpty.Set(511);
        _mutableBitVector1024Empty = BitVector<Bit1024>.Empty;
        _mutableBitVector1024NonEmpty = BitVector<Bit1024>.Empty;
        _mutableBitVector1024NonEmpty.Set(1023);
    }

    [Benchmark]
    public bool Int64_IsEmpty_Empty() => _int64Empty == 0L;

    [Benchmark]
    public bool Int64_IsEmpty_NonEmpty() => _int64NonEmpty == 0L;

    [Benchmark]
    public bool Bit64_IsEmpty_Empty() => _bit64Empty.IsEmpty;

    [Benchmark]
    public bool Bit64_IsEmpty_NonEmpty() => _bit64NonEmpty.IsEmpty;

    [Benchmark(Baseline = true)]
    public bool Bit256_IsEmpty_Empty() => _bit256Empty.IsEmpty;

    [Benchmark]
    public bool Bit256_IsEmpty_NonEmpty() => _bit256NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitVector256_IsEmpty_Empty() => _immutableBitVector256Empty.IsEmpty;

    [Benchmark]
    public bool BitVector256_IsEmpty_NonEmpty() => _immutableBitVector256NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitVectorT256_IsEmpty_Empty() => _bitVectorT256Empty.IsEmpty;

    [Benchmark]
    public bool BitVectorT256_IsEmpty_NonEmpty() => _bitVectorT256NonEmpty.IsEmpty;

    [Benchmark]
    public bool MutableBitSet256_IsEmpty_Empty() => _mutableBitSet256Empty.IsEmpty;

    [Benchmark]
    public bool MutableBitSet256_IsEmpty_NonEmpty() => _mutableBitSet256NonEmpty.IsEmpty;

    [Benchmark]
    public bool MutableBitVector256_IsEmpty_Empty() => _mutableBitVector256Empty.IsEmpty;

    [Benchmark]
    public bool MutableBitVector256_IsEmpty_NonEmpty() => _mutableBitVector256NonEmpty.IsEmpty;

    [Benchmark]
    public bool Bit512_IsEmpty_Empty() => _bit512Empty.IsEmpty;

    [Benchmark]
    public bool Bit512_IsEmpty_NonEmpty() => _bit512NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitVector512_IsEmpty_Empty() => _immutableBitVector512Empty.IsEmpty;

    [Benchmark]
    public bool BitVector512_IsEmpty_NonEmpty() => _immutableBitVector512NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitVectorT512_IsEmpty_Empty() => _bitVectorT512Empty.IsEmpty;

    [Benchmark]
    public bool BitVectorT512_IsEmpty_NonEmpty() => _bitVectorT512NonEmpty.IsEmpty;

    [Benchmark]
    public bool MutableBitSet512_IsEmpty_Empty() => _mutableBitSet512Empty.IsEmpty;

    [Benchmark]
    public bool MutableBitSet512_IsEmpty_NonEmpty() => _mutableBitSet512NonEmpty.IsEmpty;

    [Benchmark]
    public bool MutableBitVector512_IsEmpty_Empty() => _mutableBitVector512Empty.IsEmpty;

    [Benchmark]
    public bool MutableBitVector512_IsEmpty_NonEmpty() => _mutableBitVector512NonEmpty.IsEmpty;

    [Benchmark]
    public bool Bit1024_IsEmpty_Empty() => _bit1024Empty.IsEmpty;

    [Benchmark]
    public bool Bit1024_IsEmpty_NonEmpty() => _bit1024NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitVector1024_IsEmpty_Empty() => _immutableBitVector1024Empty.IsEmpty;

    [Benchmark]
    public bool BitVector1024_IsEmpty_NonEmpty() => _immutableBitVector1024NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitVectorT1024_IsEmpty_Empty() => _bitVectorT1024Empty.IsEmpty;

    [Benchmark]
    public bool BitVectorT1024_IsEmpty_NonEmpty() => _bitVectorT1024NonEmpty.IsEmpty;

    [Benchmark]
    public bool MutableBitSet1024_IsEmpty_Empty() => _mutableBitSet1024Empty.IsEmpty;

    [Benchmark]
    public bool MutableBitSet1024_IsEmpty_NonEmpty() => _mutableBitSet1024NonEmpty.IsEmpty;

    [Benchmark]
    public bool MutableBitVector1024_IsEmpty_Empty() => _mutableBitVector1024Empty.IsEmpty;

    [Benchmark]
    public bool MutableBitVector1024_IsEmpty_NonEmpty() => _mutableBitVector1024NonEmpty.IsEmpty;
}

// ============================================================================
// Equality/Hashing Benchmarks
// ============================================================================

[Config(typeof(NativeAotConfig))]
public class BitSetEqualsBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private ImmutableBitVector<Bit256> _immutableBitVector256A, _immutableBitVector256B;
    private ImmutableBitVector<Bit512> _immutableBitVector512A, _immutableBitVector512B;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024A, _immutableBitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

    private BitSet<Bit256> _mutableBitSet256A, _mutableBitSet256B;
    private BitSet<Bit512> _mutableBitSet512A, _mutableBitSet512B;
    private BitSet<Bit1024> _mutableBitSet1024A, _mutableBitSet1024B;
    private BitVector<Bit256> _mutableBitVector256A, _mutableBitVector256B;
    private BitVector<Bit512> _mutableBitVector512A, _mutableBitVector512B;
    private BitVector<Bit1024> _mutableBitVector1024A, _mutableBitVector1024B;

    [GlobalSetup]
    public void Setup()
    {
        _int64A = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50) | (1L << 63);
        _int64B = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50) | (1L << 63);

        _bit64A = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50).Set(63);
        _bit64B = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50).Set(63);

        _bit256A = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit256B = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);

        _bit512A = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bit512B = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);

        _bit1024A = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bit1024B = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);

        _immutableBitVector256A = ImmutableBitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _immutableBitVector256B = ImmutableBitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);

        _immutableBitVector512A = ImmutableBitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _immutableBitVector512B = ImmutableBitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);

        _immutableBitVector1024A = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _immutableBitVector1024B = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);

        _bitVectorT256A = BitVectorT<Bit256>.Empty;
        _bitVectorT256A.Set(0); _bitVectorT256A.Set(60); _bitVectorT256A.Set(120); _bitVectorT256A.Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty;
        _bitVectorT256B.Set(0); _bitVectorT256B.Set(60); _bitVectorT256B.Set(120); _bitVectorT256B.Set(180);

        _bitVectorT512A = BitVectorT<Bit512>.Empty;
        _bitVectorT512A.Set(0); _bitVectorT512A.Set(120); _bitVectorT512A.Set(240); _bitVectorT512A.Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty;
        _bitVectorT512B.Set(0); _bitVectorT512B.Set(120); _bitVectorT512B.Set(240); _bitVectorT512B.Set(360);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024A.Set(0); _bitVectorT1024A.Set(240); _bitVectorT1024A.Set(480); _bitVectorT1024A.Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024B.Set(0); _bitVectorT1024B.Set(240); _bitVectorT1024B.Set(480); _bitVectorT1024B.Set(720);

        _mutableBitSet256A = BitSet<Bit256>.Empty;
        _mutableBitSet256A.Set(0); _mutableBitSet256A.Set(60); _mutableBitSet256A.Set(120); _mutableBitSet256A.Set(180);
        _mutableBitSet256B = BitSet<Bit256>.Empty;
        _mutableBitSet256B.Set(0); _mutableBitSet256B.Set(60); _mutableBitSet256B.Set(120); _mutableBitSet256B.Set(180);
        _mutableBitSet512A = BitSet<Bit512>.Empty;
        _mutableBitSet512A.Set(0); _mutableBitSet512A.Set(120); _mutableBitSet512A.Set(240); _mutableBitSet512A.Set(360);
        _mutableBitSet512B = BitSet<Bit512>.Empty;
        _mutableBitSet512B.Set(0); _mutableBitSet512B.Set(120); _mutableBitSet512B.Set(240); _mutableBitSet512B.Set(360);
        _mutableBitSet1024A = BitSet<Bit1024>.Empty;
        _mutableBitSet1024A.Set(0); _mutableBitSet1024A.Set(240); _mutableBitSet1024A.Set(480); _mutableBitSet1024A.Set(720);
        _mutableBitSet1024B = BitSet<Bit1024>.Empty;
        _mutableBitSet1024B.Set(0); _mutableBitSet1024B.Set(240); _mutableBitSet1024B.Set(480); _mutableBitSet1024B.Set(720);

        _mutableBitVector256A = BitVector<Bit256>.Empty;
        _mutableBitVector256A.Set(0); _mutableBitVector256A.Set(60); _mutableBitVector256A.Set(120); _mutableBitVector256A.Set(180);
        _mutableBitVector256B = BitVector<Bit256>.Empty;
        _mutableBitVector256B.Set(0); _mutableBitVector256B.Set(60); _mutableBitVector256B.Set(120); _mutableBitVector256B.Set(180);
        _mutableBitVector512A = BitVector<Bit512>.Empty;
        _mutableBitVector512A.Set(0); _mutableBitVector512A.Set(120); _mutableBitVector512A.Set(240); _mutableBitVector512A.Set(360);
        _mutableBitVector512B = BitVector<Bit512>.Empty;
        _mutableBitVector512B.Set(0); _mutableBitVector512B.Set(120); _mutableBitVector512B.Set(240); _mutableBitVector512B.Set(360);
        _mutableBitVector1024A = BitVector<Bit1024>.Empty;
        _mutableBitVector1024A.Set(0); _mutableBitVector1024A.Set(240); _mutableBitVector1024A.Set(480); _mutableBitVector1024A.Set(720);
        _mutableBitVector1024B = BitVector<Bit1024>.Empty;
        _mutableBitVector1024B.Set(0); _mutableBitVector1024B.Set(240); _mutableBitVector1024B.Set(480); _mutableBitVector1024B.Set(720);
    }

    [Benchmark]
    public bool Int64_Equals() => _int64A == _int64B;

    [Benchmark]
    public bool Bit64_Equals() => _bit64A.Equals(_bit64B);

    [Benchmark(Baseline = true)]
    public bool Bit256_Equals() => _bit256A.Equals(_bit256B);

    [Benchmark]
    public bool BitVector256_Equals() => _immutableBitVector256A.Equals(in _immutableBitVector256B);

    [Benchmark]
    public bool BitVectorT256_Equals() => _bitVectorT256A.Equals(in _bitVectorT256B);

    [Benchmark]
    public bool MutableBitSet256_Equals() => _mutableBitSet256A.Equals(_mutableBitSet256B);

    [Benchmark]
    public bool MutableBitVector256_Equals() => _mutableBitVector256A.Equals(in _mutableBitVector256B);

    [Benchmark]
    public bool Bit512_Equals() => _bit512A.Equals(_bit512B);

    [Benchmark]
    public bool BitVector512_Equals() => _immutableBitVector512A.Equals(in _immutableBitVector512B);

    [Benchmark]
    public bool BitVectorT512_Equals() => _bitVectorT512A.Equals(in _bitVectorT512B);

    [Benchmark]
    public bool MutableBitSet512_Equals() => _mutableBitSet512A.Equals(_mutableBitSet512B);

    [Benchmark]
    public bool MutableBitVector512_Equals() => _mutableBitVector512A.Equals(in _mutableBitVector512B);

    [Benchmark]
    public bool Bit1024_Equals() => _bit1024A.Equals(_bit1024B);

    [Benchmark]
    public bool BitVector1024_Equals() => _immutableBitVector1024A.Equals(in _immutableBitVector1024B);

    [Benchmark]
    public bool BitVectorT1024_Equals() => _bitVectorT1024A.Equals(in _bitVectorT1024B);

    [Benchmark]
    public bool MutableBitSet1024_Equals() => _mutableBitSet1024A.Equals(_mutableBitSet1024B);

    [Benchmark]
    public bool MutableBitVector1024_Equals() => _mutableBitVector1024A.Equals(in _mutableBitVector1024B);
}

[Config(typeof(NativeAotConfig))]
public class BitSetGetHashCodeBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private ImmutableBitVector<Bit256> _immutableBitVector256;
    private ImmutableBitVector<Bit512> _immutableBitVector512;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    private BitSet<Bit256> _mutableBitSet256;
    private BitSet<Bit512> _mutableBitSet512;
    private BitSet<Bit1024> _mutableBitSet1024;
    private BitVector<Bit256> _mutableBitVector256;
    private BitVector<Bit512> _mutableBitVector512;
    private BitVector<Bit1024> _mutableBitVector1024;

    [GlobalSetup]
    public void Setup()
    {
        _int64 = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50) | (1L << 63);
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50).Set(63);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit512 = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _immutableBitVector256 = ImmutableBitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _immutableBitVector512 = ImmutableBitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _immutableBitVector1024 = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVectorT256 = BitVectorT<Bit256>.Empty;
        _bitVectorT256.Set(0); _bitVectorT256.Set(60); _bitVectorT256.Set(120); _bitVectorT256.Set(180);
        _bitVectorT512 = BitVectorT<Bit512>.Empty;
        _bitVectorT512.Set(0); _bitVectorT512.Set(120); _bitVectorT512.Set(240); _bitVectorT512.Set(360);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024.Set(0); _bitVectorT1024.Set(240); _bitVectorT1024.Set(480); _bitVectorT1024.Set(720);

        _mutableBitSet256 = BitSet<Bit256>.Empty;
        _mutableBitSet256.Set(0); _mutableBitSet256.Set(60); _mutableBitSet256.Set(120); _mutableBitSet256.Set(180);
        _mutableBitSet512 = BitSet<Bit512>.Empty;
        _mutableBitSet512.Set(0); _mutableBitSet512.Set(120); _mutableBitSet512.Set(240); _mutableBitSet512.Set(360);
        _mutableBitSet1024 = BitSet<Bit1024>.Empty;
        _mutableBitSet1024.Set(0); _mutableBitSet1024.Set(240); _mutableBitSet1024.Set(480); _mutableBitSet1024.Set(720);
        _mutableBitVector256 = BitVector<Bit256>.Empty;
        _mutableBitVector256.Set(0); _mutableBitVector256.Set(60); _mutableBitVector256.Set(120); _mutableBitVector256.Set(180);
        _mutableBitVector512 = BitVector<Bit512>.Empty;
        _mutableBitVector512.Set(0); _mutableBitVector512.Set(120); _mutableBitVector512.Set(240); _mutableBitVector512.Set(360);
        _mutableBitVector1024 = BitVector<Bit1024>.Empty;
        _mutableBitVector1024.Set(0); _mutableBitVector1024.Set(240); _mutableBitVector1024.Set(480); _mutableBitVector1024.Set(720);
    }

    [Benchmark]
    public int Int64_GetHashCode() => _int64.GetHashCode();

    [Benchmark]
    public int Bit64_GetHashCode() => _bit64.GetHashCode();

    [Benchmark(Baseline = true)]
    public int Bit256_GetHashCode() => _bit256.GetHashCode();

    [Benchmark]
    public int BitVector256_GetHashCode() => _immutableBitVector256.GetHashCode();

    [Benchmark]
    public int BitVectorT256_GetHashCode() => _bitVectorT256.GetHashCode();

    [Benchmark]
    public int MutableBitSet256_GetHashCode() => _mutableBitSet256.GetHashCode();

    [Benchmark]
    public int MutableBitVector256_GetHashCode() => _mutableBitVector256.GetHashCode();

    [Benchmark]
    public int Bit512_GetHashCode() => _bit512.GetHashCode();

    [Benchmark]
    public int BitVector512_GetHashCode() => _immutableBitVector512.GetHashCode();

    [Benchmark]
    public int BitVectorT512_GetHashCode() => _bitVectorT512.GetHashCode();

    [Benchmark]
    public int MutableBitSet512_GetHashCode() => _mutableBitSet512.GetHashCode();

    [Benchmark]
    public int MutableBitVector512_GetHashCode() => _mutableBitVector512.GetHashCode();

    [Benchmark]
    public int Bit1024_GetHashCode() => _bit1024.GetHashCode();

    [Benchmark]
    public int BitVector1024_GetHashCode() => _immutableBitVector1024.GetHashCode();

    [Benchmark]
    public int BitVectorT1024_GetHashCode() => _bitVectorT1024.GetHashCode();

    [Benchmark]
    public int MutableBitSet1024_GetHashCode() => _mutableBitSet1024.GetHashCode();

    [Benchmark]
    public int MutableBitVector1024_GetHashCode() => _mutableBitVector1024.GetHashCode();
}

// ============================================================================
// Enumeration Benchmarks
// ============================================================================

[Config(typeof(NativeAotConfig))]
public class BitSetEnumerationBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private ImmutableBitVector<Bit256> _immutableBitVector256;
    private ImmutableBitVector<Bit512> _immutableBitVector512;
    private ImmutableBitVector<Bit1024> _immutableBitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    private BitSet<Bit256> _mutableBitSet256;
    private BitSet<Bit512> _mutableBitSet512;
    private BitSet<Bit1024> _mutableBitSet1024;
    private BitVector<Bit256> _mutableBitVector256;
    private BitVector<Bit512> _mutableBitVector512;
    private BitVector<Bit1024> _mutableBitVector1024;

    [GlobalSetup]
    public void Setup()
    {
        // Set multiple bits spread across the range
        _int64 = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50) | (1L << 60);
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50).Set(60);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(0).Set(30).Set(60).Set(90).Set(120).Set(150).Set(180).Set(210).Set(240);
        _bit512 = ImmutableBitSet<Bit512>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240).Set(300).Set(360).Set(420).Set(480);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(100).Set(200).Set(300).Set(400).Set(500).Set(600).Set(700).Set(800).Set(900).Set(1000);
        _immutableBitVector256 = ImmutableBitVector<Bit256>.Empty.Set(0).Set(30).Set(60).Set(90).Set(120).Set(150).Set(180).Set(210).Set(240);
        _immutableBitVector512 = ImmutableBitVector<Bit512>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240).Set(300).Set(360).Set(420).Set(480);
        _immutableBitVector1024 = ImmutableBitVector<Bit1024>.Empty.Set(0).Set(100).Set(200).Set(300).Set(400).Set(500).Set(600).Set(700).Set(800).Set(900).Set(1000);
        _bitVectorT256 = BitVectorT<Bit256>.Empty;
        _bitVectorT256.Set(0); _bitVectorT256.Set(30); _bitVectorT256.Set(60); _bitVectorT256.Set(90); _bitVectorT256.Set(120); _bitVectorT256.Set(150); _bitVectorT256.Set(180); _bitVectorT256.Set(210); _bitVectorT256.Set(240);
        _bitVectorT512 = BitVectorT<Bit512>.Empty;
        _bitVectorT512.Set(0); _bitVectorT512.Set(60); _bitVectorT512.Set(120); _bitVectorT512.Set(180); _bitVectorT512.Set(240); _bitVectorT512.Set(300); _bitVectorT512.Set(360); _bitVectorT512.Set(420); _bitVectorT512.Set(480);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024.Set(0); _bitVectorT1024.Set(100); _bitVectorT1024.Set(200); _bitVectorT1024.Set(300); _bitVectorT1024.Set(400); _bitVectorT1024.Set(500); _bitVectorT1024.Set(600); _bitVectorT1024.Set(700); _bitVectorT1024.Set(800); _bitVectorT1024.Set(900); _bitVectorT1024.Set(1000);

        _mutableBitSet256 = BitSet<Bit256>.Empty;
        _mutableBitSet256.Set(0); _mutableBitSet256.Set(30); _mutableBitSet256.Set(60); _mutableBitSet256.Set(90); _mutableBitSet256.Set(120); _mutableBitSet256.Set(150); _mutableBitSet256.Set(180); _mutableBitSet256.Set(210); _mutableBitSet256.Set(240);
        _mutableBitSet512 = BitSet<Bit512>.Empty;
        _mutableBitSet512.Set(0); _mutableBitSet512.Set(60); _mutableBitSet512.Set(120); _mutableBitSet512.Set(180); _mutableBitSet512.Set(240); _mutableBitSet512.Set(300); _mutableBitSet512.Set(360); _mutableBitSet512.Set(420); _mutableBitSet512.Set(480);
        _mutableBitSet1024 = BitSet<Bit1024>.Empty;
        _mutableBitSet1024.Set(0); _mutableBitSet1024.Set(100); _mutableBitSet1024.Set(200); _mutableBitSet1024.Set(300); _mutableBitSet1024.Set(400); _mutableBitSet1024.Set(500); _mutableBitSet1024.Set(600); _mutableBitSet1024.Set(700); _mutableBitSet1024.Set(800); _mutableBitSet1024.Set(900); _mutableBitSet1024.Set(1000);
        _mutableBitVector256 = BitVector<Bit256>.Empty;
        _mutableBitVector256.Set(0); _mutableBitVector256.Set(30); _mutableBitVector256.Set(60); _mutableBitVector256.Set(90); _mutableBitVector256.Set(120); _mutableBitVector256.Set(150); _mutableBitVector256.Set(180); _mutableBitVector256.Set(210); _mutableBitVector256.Set(240);
        _mutableBitVector512 = BitVector<Bit512>.Empty;
        _mutableBitVector512.Set(0); _mutableBitVector512.Set(60); _mutableBitVector512.Set(120); _mutableBitVector512.Set(180); _mutableBitVector512.Set(240); _mutableBitVector512.Set(300); _mutableBitVector512.Set(360); _mutableBitVector512.Set(420); _mutableBitVector512.Set(480);
        _mutableBitVector1024 = BitVector<Bit1024>.Empty;
        _mutableBitVector1024.Set(0); _mutableBitVector1024.Set(100); _mutableBitVector1024.Set(200); _mutableBitVector1024.Set(300); _mutableBitVector1024.Set(400); _mutableBitVector1024.Set(500); _mutableBitVector1024.Set(600); _mutableBitVector1024.Set(700); _mutableBitVector1024.Set(800); _mutableBitVector1024.Set(900); _mutableBitVector1024.Set(1000);
    }

    [Benchmark]
    public int Int64_Enumerate()
    {
        int sum = 0;
        long bits = _int64;
        while (bits != 0)
        {
            int bit = BitOperations.TrailingZeroCount(bits);
            sum += bit;
            bits &= bits - 1;  // Clear lowest set bit
        }
        return sum;
    }

    [Benchmark]
    public int Bit64_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _bit64)
            sum += bit;
        return sum;
    }

    [Benchmark(Baseline = true)]
    public int Bit256_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _bit256)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int BitVector256_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _immutableBitVector256)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int BitVectorT256_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _bitVectorT256)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int MutableBitSet256_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _mutableBitSet256)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int MutableBitVector256_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _mutableBitVector256)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int Bit512_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _bit512)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int BitVector512_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _immutableBitVector512)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int BitVectorT512_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _bitVectorT512)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int MutableBitSet512_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _mutableBitSet512)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int MutableBitVector512_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _mutableBitVector512)
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
    public int BitVector1024_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _immutableBitVector1024)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int BitVectorT1024_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _bitVectorT1024)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int MutableBitSet1024_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _mutableBitSet1024)
            sum += bit;
        return sum;
    }

    [Benchmark]
    public int MutableBitVector1024_Enumerate()
    {
        int sum = 0;
        foreach (var bit in _mutableBitVector1024)
            sum += bit;
        return sum;
    }
}
