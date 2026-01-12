using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Benchmarks;

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
