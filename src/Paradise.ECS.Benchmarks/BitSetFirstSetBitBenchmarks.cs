using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Concurrent.Benchmarks;

[Config(typeof(NativeAotConfig))]
public class BitSetFirstSetBitBenchmarks
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
        // First bit is somewhere in the middle to test search
        _int64 = (1L << 25) | (1L << 40) | (1L << 50);
        _bit64 = SmallBitSet<ulong>.Empty.Set(25).Set(40).Set(50);
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
