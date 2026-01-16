using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Concurrent.Benchmarks;

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
