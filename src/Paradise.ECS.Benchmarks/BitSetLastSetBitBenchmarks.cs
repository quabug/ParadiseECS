using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Concurrent.Benchmarks;

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
