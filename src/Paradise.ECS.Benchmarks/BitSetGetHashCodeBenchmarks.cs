using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Concurrent.Benchmarks;

[Config(typeof(NativeAotConfig))]
public class BitSetGetHashCodeBenchmarks
{
    private long _int64;
    private SmallBitSet<ulong> _bit64;
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
        _bit64 = SmallBitSet<ulong>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50).Set(63);
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
