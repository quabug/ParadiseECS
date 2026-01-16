using System.Collections;
using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Concurrent.Benchmarks;

[Config(typeof(NativeAotConfig))]
public class BitSetSetBenchmarks
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
        _int64 = 0L;
        _bit64 = ImmutableBitSet<Bit64>.Empty;
        _bit256 = ImmutableBitSet<Bit256>.Empty;
        _bit512 = ImmutableBitSet<Bit512>.Empty;
        _bit1024 = ImmutableBitSet<Bit1024>.Empty;
        _immutableBitVector256 = ImmutableBitVector<Bit256>.Empty;
        _immutableBitVector512 = ImmutableBitVector<Bit512>.Empty;
        _immutableBitVector1024 = ImmutableBitVector<Bit1024>.Empty;
        _bitVectorT256 = BitVectorT<Bit256>.Empty;
        _bitVectorT512 = BitVectorT<Bit512>.Empty;
        _bitVectorT1024 = BitVectorT<Bit1024>.Empty;

        _mutableBitSet256 = BitSet<Bit256>.Empty;
        _mutableBitSet512 = BitSet<Bit512>.Empty;
        _mutableBitSet1024 = BitSet<Bit1024>.Empty;
        _mutableBitVector256 = BitVector<Bit256>.Empty;
        _mutableBitVector512 = BitVector<Bit512>.Empty;
        _mutableBitVector1024 = BitVector<Bit1024>.Empty;

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
    public ImmutableBitVector<Bit256> BitVector256_Set() => _immutableBitVector256.Set(128);

    [Benchmark]
    public void BitVectorT256_Set() => _bitVectorT256.Set(128);

    [Benchmark]
    public void MutableBitSet256_Set() => _mutableBitSet256.Set(128);

    [Benchmark]
    public void MutableBitVector256_Set() => _mutableBitVector256.Set(128);

    [Benchmark]
    public void BitArray256_Set() => _bitArray256.Set(128, true);

    [Benchmark]
    public ImmutableBitSet<Bit512> Bit512_Set() => _bit512.Set(256);

    [Benchmark]
    public ImmutableBitVector<Bit512> BitVector512_Set() => _immutableBitVector512.Set(256);

    [Benchmark]
    public void BitVectorT512_Set() => _bitVectorT512.Set(256);

    [Benchmark]
    public void MutableBitSet512_Set() => _mutableBitSet512.Set(256);

    [Benchmark]
    public void MutableBitVector512_Set() => _mutableBitVector512.Set(256);

    [Benchmark]
    public void BitArray512_Set() => _bitArray512.Set(256, true);

    [Benchmark]
    public ImmutableBitSet<Bit1024> Bit1024_Set() => _bit1024.Set(512);

    [Benchmark]
    public ImmutableBitVector<Bit1024> BitVector1024_Set() => _immutableBitVector1024.Set(512);

    [Benchmark]
    public void BitVectorT1024_Set() => _bitVectorT1024.Set(512);

    [Benchmark]
    public void MutableBitSet1024_Set() => _mutableBitSet1024.Set(512);

    [Benchmark]
    public void MutableBitVector1024_Set() => _mutableBitVector1024.Set(512);

    [Benchmark]
    public void BitArray1024_Set() => _bitArray1024.Set(512, true);
}
