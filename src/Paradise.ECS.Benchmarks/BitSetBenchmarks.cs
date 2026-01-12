using System.Collections;
using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Benchmarks;

// ============================================================================
// Get/Set/Clear Benchmarks
// ============================================================================

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetGetBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private BitVector<Bit256> _bitVector256;
    private BitVector<Bit512> _bitVector512;
    private BitVector<Bit1024> _bitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

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
        _bitVector256 = BitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180).Set(255);
        _bitVector512 = BitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360).Set(511);
        _bitVector1024 = BitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720).Set(1023);
        _bitVectorT256 = BitVectorT<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180).Set(255);
        _bitVectorT512 = BitVectorT<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360).Set(511);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720).Set(1023);

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
    public bool BitVector256_Get() => _bitVector256.Get(120);

    [Benchmark]
    public bool BitVectorT256_Get() => _bitVectorT256.Get(120);

    [Benchmark]
    public bool BitArray256_Get() => _bitArray256.Get(120);

    [Benchmark]
    public bool Bit512_Get() => _bit512.Get(240);

    [Benchmark]
    public bool BitVector512_Get() => _bitVector512.Get(240);

    [Benchmark]
    public bool BitVectorT512_Get() => _bitVectorT512.Get(240);

    [Benchmark]
    public bool BitArray512_Get() => _bitArray512.Get(240);

    [Benchmark]
    public bool Bit1024_Get() => _bit1024.Get(480);

    [Benchmark]
    public bool BitVector1024_Get() => _bitVector1024.Get(480);

    [Benchmark]
    public bool BitVectorT1024_Get() => _bitVectorT1024.Get(480);

    [Benchmark]
    public bool BitArray1024_Get() => _bitArray1024.Get(480);
}

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetSetBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private BitVector<Bit256> _bitVector256;
    private BitVector<Bit512> _bitVector512;
    private BitVector<Bit1024> _bitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

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
        _bitVector256 = BitVector<Bit256>.Empty;
        _bitVector512 = BitVector<Bit512>.Empty;
        _bitVector1024 = BitVector<Bit1024>.Empty;
        _bitVectorT256 = BitVectorT<Bit256>.Empty;
        _bitVectorT512 = BitVectorT<Bit512>.Empty;
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty;

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
    public BitVector<Bit256> BitVector256_Set() => _bitVector256.Set(128);

    [Benchmark]
    public BitVectorT<Bit256> BitVectorT256_Set() => _bitVectorT256.Set(128);

    [Benchmark]
    public void BitArray256_Set() => _bitArray256.Set(128, true);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_Set() => _bit512.Set(256);

    [Benchmark]
    public BitVector<Bit512> BitVector512_Set() => _bitVector512.Set(256);

    [Benchmark]
    public BitVectorT<Bit512> BitVectorT512_Set() => _bitVectorT512.Set(256);

    [Benchmark]
    public void BitArray512_Set() => _bitArray512.Set(256, true);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Set() => _bit1024.Set(512);

    [Benchmark]
    public BitVector<Bit1024> BitVector1024_Set() => _bitVector1024.Set(512);

    [Benchmark]
    public BitVectorT<Bit1024> BitVectorT1024_Set() => _bitVectorT1024.Set(512);

    [Benchmark]
    public void BitArray1024_Set() => _bitArray1024.Set(512, true);
}

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetClearBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private BitVector<Bit256> _bitVector256;
    private BitVector<Bit512> _bitVector512;
    private BitVector<Bit1024> _bitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

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
        _bitVector256 = BitVector<Bit256>.Empty.Set(128);
        _bitVector512 = BitVector<Bit512>.Empty.Set(256);
        _bitVector1024 = BitVector<Bit1024>.Empty.Set(512);
        _bitVectorT256 = BitVectorT<Bit256>.Empty.Set(128);
        _bitVectorT512 = BitVectorT<Bit512>.Empty.Set(256);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty.Set(512);

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
    public BitVector<Bit256> BitVector256_Clear() => _bitVector256.Clear(128);

    [Benchmark]
    public BitVectorT<Bit256> BitVectorT256_Clear() => _bitVectorT256.Clear(128);

    [Benchmark]
    public void BitArray256_Clear() => _bitArray256.Set(128, false);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_Clear() => _bit512.Clear(256);

    [Benchmark]
    public BitVector<Bit512> BitVector512_Clear() => _bitVector512.Clear(256);

    [Benchmark]
    public BitVectorT<Bit512> BitVectorT512_Clear() => _bitVectorT512.Clear(256);

    [Benchmark]
    public void BitArray512_Clear() => _bitArray512.Set(256, false);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Clear() => _bit1024.Clear(512);

    [Benchmark]
    public BitVector<Bit1024> BitVector1024_Clear() => _bitVector1024.Clear(512);

    [Benchmark]
    public BitVectorT<Bit1024> BitVectorT1024_Clear() => _bitVectorT1024.Clear(512);

    [Benchmark]
    public void BitArray1024_Clear() => _bitArray1024.Set(512, false);
}

// ============================================================================
// Bitwise Operation Benchmarks (And, Or, Xor, AndNot)
// ============================================================================

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetAndBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private BitVector<Bit256> _bitVector256A, _bitVector256B;
    private BitVector<Bit512> _bitVector512A, _bitVector512B;
    private BitVector<Bit1024> _bitVector1024A, _bitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

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

        _bitVector256A = BitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVector256B = BitVector<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bitVector512A = BitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVector512B = BitVector<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _bitVector1024A = BitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVector1024B = BitVector<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _bitVectorT256A = BitVectorT<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bitVectorT512A = BitVectorT<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

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
    public BitVector<Bit256> BitVector256_And() => _bitVector256A.And(in _bitVector256B);

    [Benchmark]
    public BitVectorT<Bit256> BitVectorT256_And() => _bitVectorT256A.And(in _bitVectorT256B);

    [Benchmark]
    public BitArray BitArray256_And() => _bitArray256A.And(_bitArray256B);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_And() => _bit512A.And(_bit512B);

    [Benchmark]
    public BitVector<Bit512> BitVector512_And() => _bitVector512A.And(in _bitVector512B);

    [Benchmark]
    public BitVectorT<Bit512> BitVectorT512_And() => _bitVectorT512A.And(in _bitVectorT512B);

    [Benchmark]
    public BitArray BitArray512_And() => _bitArray512A.And(_bitArray512B);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_And() => _bit1024A.And(_bit1024B);

    [Benchmark]
    public BitVector<Bit1024> BitVector1024_And() => _bitVector1024A.And(in _bitVector1024B);

    [Benchmark]
    public BitVectorT<Bit1024> BitVectorT1024_And() => _bitVectorT1024A.And(in _bitVectorT1024B);

    [Benchmark]
    public BitArray BitArray1024_And() => _bitArray1024A.And(_bitArray1024B);
}

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetOrBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private BitVector<Bit256> _bitVector256A, _bitVector256B;
    private BitVector<Bit512> _bitVector512A, _bitVector512B;
    private BitVector<Bit1024> _bitVector1024A, _bitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

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

        _bitVector256A = BitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVector256B = BitVector<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bitVector512A = BitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVector512B = BitVector<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _bitVector1024A = BitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVector1024B = BitVector<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _bitVectorT256A = BitVectorT<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bitVectorT512A = BitVectorT<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

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
    public BitVector<Bit256> BitVector256_Or() => _bitVector256A.Or(in _bitVector256B);

    [Benchmark]
    public BitVectorT<Bit256> BitVectorT256_Or() => _bitVectorT256A.Or(in _bitVectorT256B);

    [Benchmark]
    public BitArray BitArray256_Or() => _bitArray256A.Or(_bitArray256B);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_Or() => _bit512A.Or(_bit512B);

    [Benchmark]
    public BitVector<Bit512> BitVector512_Or() => _bitVector512A.Or(in _bitVector512B);

    [Benchmark]
    public BitVectorT<Bit512> BitVectorT512_Or() => _bitVectorT512A.Or(in _bitVectorT512B);

    [Benchmark]
    public BitArray BitArray512_Or() => _bitArray512A.Or(_bitArray512B);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Or() => _bit1024A.Or(_bit1024B);

    [Benchmark]
    public BitVector<Bit1024> BitVector1024_Or() => _bitVector1024A.Or(in _bitVector1024B);

    [Benchmark]
    public BitVectorT<Bit1024> BitVectorT1024_Or() => _bitVectorT1024A.Or(in _bitVectorT1024B);

    [Benchmark]
    public BitArray BitArray1024_Or() => _bitArray1024A.Or(_bitArray1024B);
}

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetXorBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private BitVector<Bit256> _bitVector256A, _bitVector256B;
    private BitVector<Bit512> _bitVector512A, _bitVector512B;
    private BitVector<Bit1024> _bitVector1024A, _bitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

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

        _bitVector256A = BitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVector256B = BitVector<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bitVector512A = BitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVector512B = BitVector<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _bitVector1024A = BitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVector1024B = BitVector<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _bitVectorT256A = BitVectorT<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bitVectorT512A = BitVectorT<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

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
    public BitVector<Bit256> BitVector256_Xor() => _bitVector256A.Xor(in _bitVector256B);

    [Benchmark]
    public BitVectorT<Bit256> BitVectorT256_Xor() => _bitVectorT256A.Xor(in _bitVectorT256B);

    [Benchmark]
    public BitArray BitArray256_Xor() => _bitArray256A.Xor(_bitArray256B);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_Xor() => _bit512A.Xor(_bit512B);

    [Benchmark]
    public BitVector<Bit512> BitVector512_Xor() => _bitVector512A.Xor(in _bitVector512B);

    [Benchmark]
    public BitVectorT<Bit512> BitVectorT512_Xor() => _bitVectorT512A.Xor(in _bitVectorT512B);

    [Benchmark]
    public BitArray BitArray512_Xor() => _bitArray512A.Xor(_bitArray512B);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Xor() => _bit1024A.Xor(_bit1024B);

    [Benchmark]
    public BitVector<Bit1024> BitVector1024_Xor() => _bitVector1024A.Xor(in _bitVector1024B);

    [Benchmark]
    public BitVectorT<Bit1024> BitVectorT1024_Xor() => _bitVectorT1024A.Xor(in _bitVectorT1024B);

    [Benchmark]
    public BitArray BitArray1024_Xor() => _bitArray1024A.Xor(_bitArray1024B);
}

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetAndNotBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private BitVector<Bit256> _bitVector256A, _bitVector256B;
    private BitVector<Bit512> _bitVector512A, _bitVector512B;
    private BitVector<Bit1024> _bitVector1024A, _bitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

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

        _bitVector256A = BitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVector256B = BitVector<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bitVector512A = BitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVector512B = BitVector<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _bitVector1024A = BitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVector1024B = BitVector<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

        _bitVectorT256A = BitVectorT<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty.Set(60).Set(120).Set(200).Set(240);

        _bitVectorT512A = BitVectorT<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty.Set(120).Set(240).Set(400).Set(480);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty.Set(240).Set(480).Set(800).Set(960);

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
    public BitVector<Bit256> BitVector256_AndNot() => _bitVector256A.AndNot(in _bitVector256B);

    [Benchmark]
    public BitVectorT<Bit256> BitVectorT256_AndNot() => _bitVectorT256A.AndNot(in _bitVectorT256B);

    [Benchmark]
    public BitArray BitArray256_AndNot() => _bitArray256A.And(_bitArray256BNot);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_AndNot() => _bit512A.AndNot(_bit512B);

    [Benchmark]
    public BitVector<Bit512> BitVector512_AndNot() => _bitVector512A.AndNot(in _bitVector512B);

    [Benchmark]
    public BitVectorT<Bit512> BitVectorT512_AndNot() => _bitVectorT512A.AndNot(in _bitVectorT512B);

    [Benchmark]
    public BitArray BitArray512_AndNot() => _bitArray512A.And(_bitArray512BNot);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_AndNot() => _bit1024A.AndNot(_bit1024B);

    [Benchmark]
    public BitVector<Bit1024> BitVector1024_AndNot() => _bitVector1024A.AndNot(in _bitVector1024B);

    [Benchmark]
    public BitVectorT<Bit1024> BitVectorT1024_AndNot() => _bitVectorT1024A.AndNot(in _bitVectorT1024B);

    [Benchmark]
    public BitArray BitArray1024_AndNot() => _bitArray1024A.And(_bitArray1024BNot);
}

// ============================================================================
// Query Operation Benchmarks (ContainsAll, ContainsAny, ContainsNone)
// ============================================================================

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetContainsAllBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private BitVector<Bit256> _bitVector256A, _bitVector256B;
    private BitVector<Bit512> _bitVector512A, _bitVector512B;
    private BitVector<Bit1024> _bitVector1024A, _bitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

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

        _bitVector256A = BitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240);
        _bitVector256B = BitVector<Bit256>.Empty.Set(60).Set(120);

        _bitVector512A = BitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360).Set(480);
        _bitVector512B = BitVector<Bit512>.Empty.Set(120).Set(240);

        _bitVector1024A = BitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720).Set(960);
        _bitVector1024B = BitVector<Bit1024>.Empty.Set(240).Set(480);

        _bitVectorT256A = BitVectorT<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240);
        _bitVectorT256B = BitVectorT<Bit256>.Empty.Set(60).Set(120);

        _bitVectorT512A = BitVectorT<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360).Set(480);
        _bitVectorT512B = BitVectorT<Bit512>.Empty.Set(120).Set(240);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720).Set(960);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty.Set(240).Set(480);
    }

    [Benchmark]
    public bool Int64_ContainsAll() => (_int64A & _int64B) == _int64B;

    [Benchmark]
    public bool Bit64_ContainsAll() => _bit64A.ContainsAll(_bit64B);

    [Benchmark(Baseline = true)]
    public bool Bit256_ContainsAll() => _bit256A.ContainsAll(_bit256B);

    [Benchmark]
    public bool BitVector256_ContainsAll() => _bitVector256A.ContainsAll(in _bitVector256B);

    [Benchmark]
    public bool BitVectorT256_ContainsAll() => _bitVectorT256A.ContainsAll(in _bitVectorT256B);

    [Benchmark]
    public bool Bit512_ContainsAll() => _bit512A.ContainsAll(_bit512B);

    [Benchmark]
    public bool BitVector512_ContainsAll() => _bitVector512A.ContainsAll(in _bitVector512B);

    [Benchmark]
    public bool BitVectorT512_ContainsAll() => _bitVectorT512A.ContainsAll(in _bitVectorT512B);

    [Benchmark]
    public bool Bit1024_ContainsAll() => _bit1024A.ContainsAll(_bit1024B);

    [Benchmark]
    public bool BitVector1024_ContainsAll() => _bitVector1024A.ContainsAll(in _bitVector1024B);

    [Benchmark]
    public bool BitVectorT1024_ContainsAll() => _bitVectorT1024A.ContainsAll(in _bitVectorT1024B);
}

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetContainsAnyBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private BitVector<Bit256> _bitVector256A, _bitVector256B;
    private BitVector<Bit512> _bitVector512A, _bitVector512B;
    private BitVector<Bit1024> _bitVector1024A, _bitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

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

        _bitVector256A = BitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVector256B = BitVector<Bit256>.Empty.Set(120).Set(200).Set(240);

        _bitVector512A = BitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVector512B = BitVector<Bit512>.Empty.Set(240).Set(400).Set(480);

        _bitVector1024A = BitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVector1024B = BitVector<Bit1024>.Empty.Set(480).Set(800).Set(960);

        _bitVectorT256A = BitVectorT<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty.Set(120).Set(200).Set(240);

        _bitVectorT512A = BitVectorT<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty.Set(240).Set(400).Set(480);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty.Set(480).Set(800).Set(960);
    }

    [Benchmark]
    public bool Int64_ContainsAny() => (_int64A & _int64B) != 0;

    [Benchmark]
    public bool Bit64_ContainsAny() => _bit64A.ContainsAny(_bit64B);

    [Benchmark(Baseline = true)]
    public bool Bit256_ContainsAny() => _bit256A.ContainsAny(_bit256B);

    [Benchmark]
    public bool BitVector256_ContainsAny() => _bitVector256A.ContainsAny(in _bitVector256B);

    [Benchmark]
    public bool BitVectorT256_ContainsAny() => _bitVectorT256A.ContainsAny(in _bitVectorT256B);

    [Benchmark]
    public bool Bit512_ContainsAny() => _bit512A.ContainsAny(_bit512B);

    [Benchmark]
    public bool BitVector512_ContainsAny() => _bitVector512A.ContainsAny(in _bitVector512B);

    [Benchmark]
    public bool BitVectorT512_ContainsAny() => _bitVectorT512A.ContainsAny(in _bitVectorT512B);

    [Benchmark]
    public bool Bit1024_ContainsAny() => _bit1024A.ContainsAny(_bit1024B);

    [Benchmark]
    public bool BitVector1024_ContainsAny() => _bitVector1024A.ContainsAny(in _bitVector1024B);

    [Benchmark]
    public bool BitVectorT1024_ContainsAny() => _bitVectorT1024A.ContainsAny(in _bitVectorT1024B);
}

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetContainsNoneBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private BitVector<Bit256> _bitVector256A, _bitVector256B;
    private BitVector<Bit512> _bitVector512A, _bitVector512B;
    private BitVector<Bit1024> _bitVector1024A, _bitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

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

        _bitVector256A = BitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVector256B = BitVector<Bit256>.Empty.Set(30).Set(90).Set(150).Set(210);

        _bitVector512A = BitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVector512B = BitVector<Bit512>.Empty.Set(60).Set(180).Set(300).Set(420);

        _bitVector1024A = BitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVector1024B = BitVector<Bit1024>.Empty.Set(120).Set(360).Set(600).Set(840);

        _bitVectorT256A = BitVectorT<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty.Set(30).Set(90).Set(150).Set(210);

        _bitVectorT512A = BitVectorT<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty.Set(60).Set(180).Set(300).Set(420);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty.Set(120).Set(360).Set(600).Set(840);
    }

    [Benchmark]
    public bool Int64_ContainsNone() => (_int64A & _int64B) == 0;

    [Benchmark]
    public bool Bit64_ContainsNone() => _bit64A.ContainsNone(_bit64B);

    [Benchmark(Baseline = true)]
    public bool Bit256_ContainsNone() => _bit256A.ContainsNone(_bit256B);

    [Benchmark]
    public bool BitVector256_ContainsNone() => _bitVector256A.ContainsNone(in _bitVector256B);

    [Benchmark]
    public bool BitVectorT256_ContainsNone() => _bitVectorT256A.ContainsNone(in _bitVectorT256B);

    [Benchmark]
    public bool Bit512_ContainsNone() => _bit512A.ContainsNone(_bit512B);

    [Benchmark]
    public bool BitVector512_ContainsNone() => _bitVector512A.ContainsNone(in _bitVector512B);

    [Benchmark]
    public bool BitVectorT512_ContainsNone() => _bitVectorT512A.ContainsNone(in _bitVectorT512B);

    [Benchmark]
    public bool Bit1024_ContainsNone() => _bit1024A.ContainsNone(_bit1024B);

    [Benchmark]
    public bool BitVector1024_ContainsNone() => _bitVector1024A.ContainsNone(in _bitVector1024B);

    [Benchmark]
    public bool BitVectorT1024_ContainsNone() => _bitVectorT1024A.ContainsNone(in _bitVectorT1024B);
}

// ============================================================================
// Counting/Finding Benchmarks (PopCount, FirstSetBit, LastSetBit, IsEmpty)
// ============================================================================

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetPopCountBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private BitVector<Bit256> _bitVector256;
    private BitVector<Bit512> _bitVector512;
    private BitVector<Bit1024> _bitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    [GlobalSetup]
    public void Setup()
    {
        // Set multiple bits spread across the range
        _int64 = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50) | (1L << 60);
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50).Set(60);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(0).Set(30).Set(60).Set(90).Set(120).Set(150).Set(180).Set(210).Set(240);
        _bit512 = ImmutableBitSet<Bit512>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240).Set(300).Set(360).Set(420).Set(480);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(100).Set(200).Set(300).Set(400).Set(500).Set(600).Set(700).Set(800).Set(900).Set(1000);
        _bitVector256 = BitVector<Bit256>.Empty.Set(0).Set(30).Set(60).Set(90).Set(120).Set(150).Set(180).Set(210).Set(240);
        _bitVector512 = BitVector<Bit512>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240).Set(300).Set(360).Set(420).Set(480);
        _bitVector1024 = BitVector<Bit1024>.Empty.Set(0).Set(100).Set(200).Set(300).Set(400).Set(500).Set(600).Set(700).Set(800).Set(900).Set(1000);
        _bitVectorT256 = BitVectorT<Bit256>.Empty.Set(0).Set(30).Set(60).Set(90).Set(120).Set(150).Set(180).Set(210).Set(240);
        _bitVectorT512 = BitVectorT<Bit512>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240).Set(300).Set(360).Set(420).Set(480);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty.Set(0).Set(100).Set(200).Set(300).Set(400).Set(500).Set(600).Set(700).Set(800).Set(900).Set(1000);
    }

    [Benchmark]
    public int Int64_PopCount() => BitOperations.PopCount((ulong)_int64);

    [Benchmark]
    public int Bit64_PopCount() => _bit64.PopCount();

    [Benchmark(Baseline = true)]
    public int Bit256_PopCount() => _bit256.PopCount();

    [Benchmark]
    public int BitVector256_PopCount() => _bitVector256.PopCount();

    [Benchmark]
    public int BitVectorT256_PopCount() => _bitVectorT256.PopCount();

    [Benchmark]
    public int Bit512_PopCount() => _bit512.PopCount();

    [Benchmark]
    public int BitVector512_PopCount() => _bitVector512.PopCount();

    [Benchmark]
    public int BitVectorT512_PopCount() => _bitVectorT512.PopCount();

    [Benchmark]
    public int Bit1024_PopCount() => _bit1024.PopCount();

    [Benchmark]
    public int BitVector1024_PopCount() => _bitVector1024.PopCount();

    [Benchmark]
    public int BitVectorT1024_PopCount() => _bitVectorT1024.PopCount();
}

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetFirstSetBitBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private BitVector<Bit256> _bitVector256;
    private BitVector<Bit512> _bitVector512;
    private BitVector<Bit1024> _bitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    [GlobalSetup]
    public void Setup()
    {
        // First bit is somewhere in the middle to test search
        _int64 = (1L << 25) | (1L << 40) | (1L << 50);
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(25).Set(40).Set(50);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(100).Set(150).Set(200);
        _bit512 = ImmutableBitSet<Bit512>.Empty.Set(200).Set(300).Set(400);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(400).Set(600).Set(800);
        _bitVector256 = BitVector<Bit256>.Empty.Set(100).Set(150).Set(200);
        _bitVector512 = BitVector<Bit512>.Empty.Set(200).Set(300).Set(400);
        _bitVector1024 = BitVector<Bit1024>.Empty.Set(400).Set(600).Set(800);
        _bitVectorT256 = BitVectorT<Bit256>.Empty.Set(100).Set(150).Set(200);
        _bitVectorT512 = BitVectorT<Bit512>.Empty.Set(200).Set(300).Set(400);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty.Set(400).Set(600).Set(800);
    }

    [Benchmark]
    public int Int64_FirstSetBit() => BitOperations.TrailingZeroCount(_int64);

    [Benchmark]
    public int Bit64_FirstSetBit() => _bit64.FirstSetBit();

    [Benchmark(Baseline = true)]
    public int Bit256_FirstSetBit() => _bit256.FirstSetBit();

    [Benchmark]
    public int BitVector256_FirstSetBit() => _bitVector256.FirstSetBit();

    [Benchmark]
    public int BitVectorT256_FirstSetBit() => _bitVectorT256.FirstSetBit();

    [Benchmark]
    public int Bit512_FirstSetBit() => _bit512.FirstSetBit();

    [Benchmark]
    public int BitVector512_FirstSetBit() => _bitVector512.FirstSetBit();

    [Benchmark]
    public int BitVectorT512_FirstSetBit() => _bitVectorT512.FirstSetBit();

    [Benchmark]
    public int Bit1024_FirstSetBit() => _bit1024.FirstSetBit();

    [Benchmark]
    public int BitVector1024_FirstSetBit() => _bitVector1024.FirstSetBit();

    [Benchmark]
    public int BitVectorT1024_FirstSetBit() => _bitVectorT1024.FirstSetBit();
}

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetLastSetBitBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private BitVector<Bit256> _bitVector256;
    private BitVector<Bit512> _bitVector512;
    private BitVector<Bit1024> _bitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    [GlobalSetup]
    public void Setup()
    {
        // Last bit is somewhere in the middle to test search
        _int64 = (1L << 10) | (1L << 25) | (1L << 40);
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(10).Set(25).Set(40);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(50).Set(100).Set(150);
        _bit512 = ImmutableBitSet<Bit512>.Empty.Set(100).Set(200).Set(300);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(200).Set(400).Set(600);
        _bitVector256 = BitVector<Bit256>.Empty.Set(50).Set(100).Set(150);
        _bitVector512 = BitVector<Bit512>.Empty.Set(100).Set(200).Set(300);
        _bitVector1024 = BitVector<Bit1024>.Empty.Set(200).Set(400).Set(600);
        _bitVectorT256 = BitVectorT<Bit256>.Empty.Set(50).Set(100).Set(150);
        _bitVectorT512 = BitVectorT<Bit512>.Empty.Set(100).Set(200).Set(300);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty.Set(200).Set(400).Set(600);
    }

    [Benchmark]
    public int Int64_LastSetBit() => 63 - BitOperations.LeadingZeroCount((ulong)_int64);

    [Benchmark]
    public int Bit64_LastSetBit() => _bit64.LastSetBit();

    [Benchmark(Baseline = true)]
    public int Bit256_LastSetBit() => _bit256.LastSetBit();

    [Benchmark]
    public int BitVector256_LastSetBit() => _bitVector256.LastSetBit();

    [Benchmark]
    public int BitVectorT256_LastSetBit() => _bitVectorT256.LastSetBit();

    [Benchmark]
    public int Bit512_LastSetBit() => _bit512.LastSetBit();

    [Benchmark]
    public int BitVector512_LastSetBit() => _bitVector512.LastSetBit();

    [Benchmark]
    public int BitVectorT512_LastSetBit() => _bitVectorT512.LastSetBit();

    [Benchmark]
    public int Bit1024_LastSetBit() => _bit1024.LastSetBit();

    [Benchmark]
    public int BitVector1024_LastSetBit() => _bitVector1024.LastSetBit();

    [Benchmark]
    public int BitVectorT1024_LastSetBit() => _bitVectorT1024.LastSetBit();
}

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
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
    private BitVector<Bit256> _bitVector256Empty;
    private BitVector<Bit256> _bitVector256NonEmpty;
    private BitVector<Bit512> _bitVector512Empty;
    private BitVector<Bit512> _bitVector512NonEmpty;
    private BitVector<Bit1024> _bitVector1024Empty;
    private BitVector<Bit1024> _bitVector1024NonEmpty;
    private BitVectorT<Bit256> _bitVectorT256Empty;
    private BitVectorT<Bit256> _bitVectorT256NonEmpty;
    private BitVectorT<Bit512> _bitVectorT512Empty;
    private BitVectorT<Bit512> _bitVectorT512NonEmpty;
    private BitVectorT<Bit1024> _bitVectorT1024Empty;
    private BitVectorT<Bit1024> _bitVectorT1024NonEmpty;

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

        _bitVector256Empty = BitVector<Bit256>.Empty;
        _bitVector256NonEmpty = BitVector<Bit256>.Empty.Set(255);  // Last bit

        _bitVector512Empty = BitVector<Bit512>.Empty;
        _bitVector512NonEmpty = BitVector<Bit512>.Empty.Set(511);  // Last bit

        _bitVector1024Empty = BitVector<Bit1024>.Empty;
        _bitVector1024NonEmpty = BitVector<Bit1024>.Empty.Set(1023);  // Last bit

        _bitVectorT256Empty = BitVectorT<Bit256>.Empty;
        _bitVectorT256NonEmpty = BitVectorT<Bit256>.Empty.Set(255);  // Last bit

        _bitVectorT512Empty = BitVectorT<Bit512>.Empty;
        _bitVectorT512NonEmpty = BitVectorT<Bit512>.Empty.Set(511);  // Last bit

        _bitVectorT1024Empty = BitVectorT<Bit1024>.Empty;
        _bitVectorT1024NonEmpty = BitVectorT<Bit1024>.Empty.Set(1023);  // Last bit
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
    public bool BitVector256_IsEmpty_Empty() => _bitVector256Empty.IsEmpty;

    [Benchmark]
    public bool BitVector256_IsEmpty_NonEmpty() => _bitVector256NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitVectorT256_IsEmpty_Empty() => _bitVectorT256Empty.IsEmpty;

    [Benchmark]
    public bool BitVectorT256_IsEmpty_NonEmpty() => _bitVectorT256NonEmpty.IsEmpty;

    [Benchmark]
    public bool Bit512_IsEmpty_Empty() => _bit512Empty.IsEmpty;

    [Benchmark]
    public bool Bit512_IsEmpty_NonEmpty() => _bit512NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitVector512_IsEmpty_Empty() => _bitVector512Empty.IsEmpty;

    [Benchmark]
    public bool BitVector512_IsEmpty_NonEmpty() => _bitVector512NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitVectorT512_IsEmpty_Empty() => _bitVectorT512Empty.IsEmpty;

    [Benchmark]
    public bool BitVectorT512_IsEmpty_NonEmpty() => _bitVectorT512NonEmpty.IsEmpty;

    [Benchmark]
    public bool Bit1024_IsEmpty_Empty() => _bit1024Empty.IsEmpty;

    [Benchmark]
    public bool Bit1024_IsEmpty_NonEmpty() => _bit1024NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitVector1024_IsEmpty_Empty() => _bitVector1024Empty.IsEmpty;

    [Benchmark]
    public bool BitVector1024_IsEmpty_NonEmpty() => _bitVector1024NonEmpty.IsEmpty;

    [Benchmark]
    public bool BitVectorT1024_IsEmpty_Empty() => _bitVectorT1024Empty.IsEmpty;

    [Benchmark]
    public bool BitVectorT1024_IsEmpty_NonEmpty() => _bitVectorT1024NonEmpty.IsEmpty;
}

// ============================================================================
// Equality/Hashing Benchmarks
// ============================================================================

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetEqualsBenchmarks
{
    private long _int64A, _int64B;
    private ImmutableBitSet<Bit64> _bit64A, _bit64B;
    private ImmutableBitSet<Bit256> _bit256A, _bit256B;
    private ImmutableBitSet<Bit512> _bit512A, _bit512B;
    private ImmutableBitSet<Bit1024> _bit1024A, _bit1024B;
    private BitVector<Bit256> _bitVector256A, _bitVector256B;
    private BitVector<Bit512> _bitVector512A, _bitVector512B;
    private BitVector<Bit1024> _bitVector1024A, _bitVector1024B;
    private BitVectorT<Bit256> _bitVectorT256A, _bitVectorT256B;
    private BitVectorT<Bit512> _bitVectorT512A, _bitVectorT512B;
    private BitVectorT<Bit1024> _bitVectorT1024A, _bitVectorT1024B;

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

        _bitVector256A = BitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVector256B = BitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);

        _bitVector512A = BitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVector512B = BitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);

        _bitVector1024A = BitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVector1024B = BitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);

        _bitVectorT256A = BitVectorT<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVectorT256B = BitVectorT<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);

        _bitVectorT512A = BitVectorT<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVectorT512B = BitVectorT<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);

        _bitVectorT1024A = BitVectorT<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVectorT1024B = BitVectorT<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
    }

    [Benchmark]
    public bool Int64_Equals() => _int64A == _int64B;

    [Benchmark]
    public bool Bit64_Equals() => _bit64A.Equals(_bit64B);

    [Benchmark(Baseline = true)]
    public bool Bit256_Equals() => _bit256A.Equals(_bit256B);

    [Benchmark]
    public bool BitVector256_Equals() => _bitVector256A.Equals(in _bitVector256B);

    [Benchmark]
    public bool BitVectorT256_Equals() => _bitVectorT256A.Equals(in _bitVectorT256B);

    [Benchmark]
    public bool Bit512_Equals() => _bit512A.Equals(_bit512B);

    [Benchmark]
    public bool BitVector512_Equals() => _bitVector512A.Equals(in _bitVector512B);

    [Benchmark]
    public bool BitVectorT512_Equals() => _bitVectorT512A.Equals(in _bitVectorT512B);

    [Benchmark]
    public bool Bit1024_Equals() => _bit1024A.Equals(_bit1024B);

    [Benchmark]
    public bool BitVector1024_Equals() => _bitVector1024A.Equals(in _bitVector1024B);

    [Benchmark]
    public bool BitVectorT1024_Equals() => _bitVectorT1024A.Equals(in _bitVectorT1024B);
}

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetGetHashCodeBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private BitVector<Bit256> _bitVector256;
    private BitVector<Bit512> _bitVector512;
    private BitVector<Bit1024> _bitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    [GlobalSetup]
    public void Setup()
    {
        _int64 = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50) | (1L << 63);
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50).Set(63);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bit512 = ImmutableBitSet<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVector256 = BitVector<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVector512 = BitVector<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVector1024 = BitVector<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
        _bitVectorT256 = BitVectorT<Bit256>.Empty.Set(0).Set(60).Set(120).Set(180);
        _bitVectorT512 = BitVectorT<Bit512>.Empty.Set(0).Set(120).Set(240).Set(360);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty.Set(0).Set(240).Set(480).Set(720);
    }

    [Benchmark]
    public int Int64_GetHashCode() => _int64.GetHashCode();

    [Benchmark]
    public int Bit64_GetHashCode() => _bit64.GetHashCode();

    [Benchmark(Baseline = true)]
    public int Bit256_GetHashCode() => _bit256.GetHashCode();

    [Benchmark]
    public int BitVector256_GetHashCode() => _bitVector256.GetHashCode();

    [Benchmark]
    public int BitVectorT256_GetHashCode() => _bitVectorT256.GetHashCode();

    [Benchmark]
    public int Bit512_GetHashCode() => _bit512.GetHashCode();

    [Benchmark]
    public int BitVector512_GetHashCode() => _bitVector512.GetHashCode();

    [Benchmark]
    public int BitVectorT512_GetHashCode() => _bitVectorT512.GetHashCode();

    [Benchmark]
    public int Bit1024_GetHashCode() => _bit1024.GetHashCode();

    [Benchmark]
    public int BitVector1024_GetHashCode() => _bitVector1024.GetHashCode();

    [Benchmark]
    public int BitVectorT1024_GetHashCode() => _bitVectorT1024.GetHashCode();
}

// ============================================================================
// Enumeration Benchmarks
// ============================================================================

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class BitSetEnumerationBenchmarks
{
    private long _int64;
    private ImmutableBitSet<Bit64> _bit64;
    private ImmutableBitSet<Bit256> _bit256;
    private ImmutableBitSet<Bit512> _bit512;
    private ImmutableBitSet<Bit1024> _bit1024;
    private BitVector<Bit256> _bitVector256;
    private BitVector<Bit512> _bitVector512;
    private BitVector<Bit1024> _bitVector1024;
    private BitVectorT<Bit256> _bitVectorT256;
    private BitVectorT<Bit512> _bitVectorT512;
    private BitVectorT<Bit1024> _bitVectorT1024;

    [GlobalSetup]
    public void Setup()
    {
        // Set multiple bits spread across the range
        _int64 = (1L << 0) | (1L << 10) | (1L << 20) | (1L << 30) | (1L << 40) | (1L << 50) | (1L << 60);
        _bit64 = ImmutableBitSet<Bit64>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50).Set(60);
        _bit256 = ImmutableBitSet<Bit256>.Empty.Set(0).Set(30).Set(60).Set(90).Set(120).Set(150).Set(180).Set(210).Set(240);
        _bit512 = ImmutableBitSet<Bit512>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240).Set(300).Set(360).Set(420).Set(480);
        _bit1024 = ImmutableBitSet<Bit1024>.Empty.Set(0).Set(100).Set(200).Set(300).Set(400).Set(500).Set(600).Set(700).Set(800).Set(900).Set(1000);
        _bitVector256 = BitVector<Bit256>.Empty.Set(0).Set(30).Set(60).Set(90).Set(120).Set(150).Set(180).Set(210).Set(240);
        _bitVector512 = BitVector<Bit512>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240).Set(300).Set(360).Set(420).Set(480);
        _bitVector1024 = BitVector<Bit1024>.Empty.Set(0).Set(100).Set(200).Set(300).Set(400).Set(500).Set(600).Set(700).Set(800).Set(900).Set(1000);
        _bitVectorT256 = BitVectorT<Bit256>.Empty.Set(0).Set(30).Set(60).Set(90).Set(120).Set(150).Set(180).Set(210).Set(240);
        _bitVectorT512 = BitVectorT<Bit512>.Empty.Set(0).Set(60).Set(120).Set(180).Set(240).Set(300).Set(360).Set(420).Set(480);
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty.Set(0).Set(100).Set(200).Set(300).Set(400).Set(500).Set(600).Set(700).Set(800).Set(900).Set(1000);
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
        foreach (var bit in _bitVector256)
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
        foreach (var bit in _bitVector512)
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
        foreach (var bit in _bitVector1024)
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
}
