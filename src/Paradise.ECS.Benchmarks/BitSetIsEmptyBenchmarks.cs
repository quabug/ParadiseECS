using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Benchmarks;

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
