using System.Collections.Frozen;
using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Concurrent.Benchmarks;

/// <summary>
/// Benchmarks comparing HashedKey cached hash vs direct GetHashCode calls.
/// Tests dictionary lookup performance and hash computation overhead.
/// </summary>
[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
public class HashedKeyBenchmarks
{
    // A simple struct that overrides GetHashCode (cheap hash)
    public readonly struct SimpleKey : IEquatable<SimpleKey>
    {
        public readonly int Id;
        public SimpleKey(int id) => Id = id;
        public override int GetHashCode() => Id;
        public bool Equals(SimpleKey other) => Id == other.Id;
        public override bool Equals(object? obj) => obj is SimpleKey other && Equals(other);
    }

    // A struct with expensive GetHashCode (simulates complex key)
    public readonly struct ExpensiveKey : IEquatable<ExpensiveKey>
    {
        public readonly int A, B, C, D;
        public ExpensiveKey(int a, int b, int c, int d) => (A, B, C, D) = (a, b, c, d);
        public override int GetHashCode() => HashCode.Combine(A, B, C, D);
        public bool Equals(ExpensiveKey other) => A == other.A && B == other.B && C == other.C && D == other.D;
        public override bool Equals(object? obj) => obj is ExpensiveKey other && Equals(other);
    }

    // A struct using ImmutableBitSet as key (real ECS use case)
    public readonly record struct BitSetKey
    {
        public readonly ImmutableBitSet<Bit256> Bits;
        public BitSetKey(ImmutableBitSet<Bit256> bits) => Bits = bits;
        public override int GetHashCode() => Bits.GetHashCode();
        public bool Equals(BitSetKey other) => Bits.Equals(other.Bits);
    }

    private const int N = 1000;

    private SimpleKey[] _simpleKeys = null!;
    private ExpensiveKey[] _expensiveKeys = null!;
    private BitSetKey[] _bitSetKeys = null!;

    private HashedKey<SimpleKey>[] _hashedSimpleKeys = null!;
    private HashedKey<ExpensiveKey>[] _hashedExpensiveKeys = null!;
    private HashedKey<BitSetKey>[] _hashedBitSetKeys = null!;

    private Dictionary<SimpleKey, int> _simpleDictRaw = null!;
    private Dictionary<HashedKey<SimpleKey>, int> _simpleDictHashed = null!;
    private Dictionary<ExpensiveKey, int> _expensiveDictRaw = null!;
    private Dictionary<HashedKey<ExpensiveKey>, int> _expensiveDictHashed = null!;
    private Dictionary<BitSetKey, int> _bitSetDictRaw = null!;
    private Dictionary<HashedKey<BitSetKey>, int> _bitSetDictHashed = null!;

    private FrozenDictionary<SimpleKey, int> _simpleDictFrozen = null!;
    private FrozenDictionary<ExpensiveKey, int> _expensiveDictFrozen = null!;
    private FrozenDictionary<BitSetKey, int> _bitSetDictFrozen = null!;

    private FrozenDictionary<HashedKey<SimpleKey>, int> _simpleDictHashedFrozen = null!;
    private FrozenDictionary<HashedKey<ExpensiveKey>, int> _expensiveDictHashedFrozen = null!;
    private FrozenDictionary<HashedKey<BitSetKey>, int> _bitSetDictHashedFrozen = null!;

    [GlobalSetup]
    public void Setup()
    {
        _simpleKeys = new SimpleKey[N];
        _expensiveKeys = new ExpensiveKey[N];
        _bitSetKeys = new BitSetKey[N];
        _hashedSimpleKeys = new HashedKey<SimpleKey>[N];
        _hashedExpensiveKeys = new HashedKey<ExpensiveKey>[N];
        _hashedBitSetKeys = new HashedKey<BitSetKey>[N];

        _simpleDictRaw = new Dictionary<SimpleKey, int>(N);
        _simpleDictHashed = new Dictionary<HashedKey<SimpleKey>, int>(N);
        _expensiveDictRaw = new Dictionary<ExpensiveKey, int>(N);
        _expensiveDictHashed = new Dictionary<HashedKey<ExpensiveKey>, int>(N);
        _bitSetDictRaw = new Dictionary<BitSetKey, int>(N);
        _bitSetDictHashed = new Dictionary<HashedKey<BitSetKey>, int>(N);

        for (int i = 0; i < N; i++)
        {
            _simpleKeys[i] = new SimpleKey(i);
            _expensiveKeys[i] = new ExpensiveKey(i, i * 2, i * 3, i * 4);
            _bitSetKeys[i] = new BitSetKey(
                ImmutableBitSet<Bit256>.Empty.Set(i % 256).Set((i + 50) % 256).Set((i + 100) % 256)
            );

            _hashedSimpleKeys[i] = new HashedKey<SimpleKey>(_simpleKeys[i]);
            _hashedExpensiveKeys[i] = new HashedKey<ExpensiveKey>(_expensiveKeys[i]);
            _hashedBitSetKeys[i] = new HashedKey<BitSetKey>(_bitSetKeys[i]);

            _simpleDictRaw[_simpleKeys[i]] = i;
            _simpleDictHashed[_hashedSimpleKeys[i]] = i;
            _expensiveDictRaw[_expensiveKeys[i]] = i;
            _expensiveDictHashed[_hashedExpensiveKeys[i]] = i;
            _bitSetDictRaw[_bitSetKeys[i]] = i;
            _bitSetDictHashed[_hashedBitSetKeys[i]] = i;
        }

        _simpleDictFrozen = _simpleDictRaw.ToFrozenDictionary();
        _expensiveDictFrozen = _expensiveDictRaw.ToFrozenDictionary();
        _bitSetDictFrozen = _bitSetDictRaw.ToFrozenDictionary();

        _simpleDictHashedFrozen = _simpleDictHashed.ToFrozenDictionary();
        _expensiveDictHashedFrozen = _expensiveDictHashed.ToFrozenDictionary();
        _bitSetDictHashedFrozen = _bitSetDictHashed.ToFrozenDictionary();
    }

    // ============================================================================
    // Single GetHashCode call comparison
    // ============================================================================

    [Benchmark]
    public int SimpleKey_GetHashCode() => _simpleKeys[500].GetHashCode();

    [Benchmark]
    public int SimpleKey_HashedKey_GetHashCode() => _hashedSimpleKeys[500].GetHashCode();

    [Benchmark]
    public int ExpensiveKey_GetHashCode() => _expensiveKeys[500].GetHashCode();

    [Benchmark]
    public int ExpensiveKey_HashedKey_GetHashCode() => _hashedExpensiveKeys[500].GetHashCode();

    [Benchmark]
    public int BitSetKey_GetHashCode() => _bitSetKeys[500].GetHashCode();

    [Benchmark]
    public int BitSetKey_HashedKey_GetHashCode() => _hashedBitSetKeys[500].GetHashCode();

    // ============================================================================
    // Dictionary lookup comparison (TryGetValue computes hash internally)
    // ============================================================================

    [Benchmark]
    public int SimpleKey_DictLookup()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_simpleDictRaw.TryGetValue(_simpleKeys[i], out var val))
                sum += val;
        }
        return sum;
    }

    [Benchmark]
    public int SimpleKey_HashedKey_DictLookup()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_simpleDictHashed.TryGetValue(_hashedSimpleKeys[i], out var val))
                sum += val;
        }
        return sum;
    }

    [Benchmark]
    public int ExpensiveKey_DictLookup()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_expensiveDictRaw.TryGetValue(_expensiveKeys[i], out var val))
                sum += val;
        }
        return sum;
    }

    [Benchmark]
    public int ExpensiveKey_HashedKey_DictLookup()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_expensiveDictHashed.TryGetValue(_hashedExpensiveKeys[i], out var val))
                sum += val;
        }
        return sum;
    }

    [Benchmark(Baseline = true)]
    public int BitSetKey_DictLookup()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_bitSetDictRaw.TryGetValue(_bitSetKeys[i], out var val))
                sum += val;
        }
        return sum;
    }

    [Benchmark]
    public int BitSetKey_HashedKey_DictLookup()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_bitSetDictHashed.TryGetValue(_hashedBitSetKeys[i], out var val))
                sum += val;
        }
        return sum;
    }

    // ============================================================================
    // FrozenDictionary lookup comparison (optimized for read-heavy scenarios)
    // ============================================================================

    [Benchmark]
    public int SimpleKey_FrozenDictLookup()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_simpleDictFrozen.TryGetValue(_simpleKeys[i], out var val))
                sum += val;
        }
        return sum;
    }

    [Benchmark]
    public int ExpensiveKey_FrozenDictLookup()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_expensiveDictFrozen.TryGetValue(_expensiveKeys[i], out var val))
                sum += val;
        }
        return sum;
    }

    [Benchmark]
    public int BitSetKey_FrozenDictLookup()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_bitSetDictFrozen.TryGetValue(_bitSetKeys[i], out var val))
                sum += val;
        }
        return sum;
    }

    // ============================================================================
    // FrozenDictionary with HashedKey (combining both optimizations)
    // ============================================================================

    [Benchmark]
    public int SimpleKey_HashedKey_FrozenDictLookup()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_simpleDictHashedFrozen.TryGetValue(_hashedSimpleKeys[i], out var val))
                sum += val;
        }
        return sum;
    }

    [Benchmark]
    public int ExpensiveKey_HashedKey_FrozenDictLookup()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_expensiveDictHashedFrozen.TryGetValue(_hashedExpensiveKeys[i], out var val))
                sum += val;
        }
        return sum;
    }

    [Benchmark]
    public int BitSetKey_HashedKey_FrozenDictLookup()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_bitSetDictHashedFrozen.TryGetValue(_hashedBitSetKeys[i], out var val))
                sum += val;
        }
        return sum;
    }
}
