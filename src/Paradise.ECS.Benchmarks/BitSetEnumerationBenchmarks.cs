using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Concurrent.Benchmarks;

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
