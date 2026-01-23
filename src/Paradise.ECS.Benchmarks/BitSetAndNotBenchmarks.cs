using System.Collections;
using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Concurrent.Benchmarks;

[Config(typeof(NativeAotConfig))]
public class BitSetAndNotBenchmarks
{
    private long _int64A, _int64B;
    private SmallBitSet<ulong> _bit64A, _bit64B;
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

        _bit64A = SmallBitSet<ulong>.Empty.Set(0).Set(10).Set(20).Set(30).Set(40).Set(50);
        _bit64B = SmallBitSet<ulong>.Empty.Set(10).Set(20).Set(40).Set(63);

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
    public SmallBitSet<ulong> Bit64_AndNot() => _bit64A.AndNot(_bit64B);

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
