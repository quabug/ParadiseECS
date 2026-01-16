using System.Collections;
using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Concurrent.Benchmarks;

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
