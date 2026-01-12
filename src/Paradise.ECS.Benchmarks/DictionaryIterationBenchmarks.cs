using System.Collections.Frozen;
using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Benchmarks;

/// <summary>
/// Benchmarks comparing iteration performance between Dictionary, FrozenDictionary, and List.
/// Tests keys and values iteration with 1000 long values.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class DictionaryIterationBenchmarks
{
    private const int N = 1000;

    private Dictionary<int, long> _dictionary = null!;
    private FrozenDictionary<int, long> _frozenDictionary = null!;
    private List<KeyValuePair<int, long>> _list = null!;
    private KeyValuePair<int, long>[] _array = null!;

    // Separate keys/values arrays for List-based iteration
    private int[] _keys = null!;
    private long[] _values = null!;

    [GlobalSetup]
    public void Setup()
    {
        _dictionary = new Dictionary<int, long>(N);
        _list = new List<KeyValuePair<int, long>>(N);
        _array = new KeyValuePair<int, long>[N];
        _keys = new int[N];
        _values = new long[N];

        for (int i = 0; i < N; i++)
        {
            var key = i;
            var value = (long)i * 12345L;
            _dictionary[key] = value;
            _list.Add(new KeyValuePair<int, long>(key, value));
            _array[i] = new KeyValuePair<int, long>(key, value);
            _keys[i] = key;
            _values[i] = value;
        }

        _frozenDictionary = _dictionary.ToFrozenDictionary();
    }

    // ============================================================================
    // Iterate KeyValuePairs (foreach on entire collection)
    // ============================================================================

    [Benchmark(Baseline = true)]
    public long Dictionary_IterateKeyValuePairs()
    {
        long sum = 0;
        foreach (var kvp in _dictionary)
        {
            sum += kvp.Key + kvp.Value;
        }
        return sum;
    }

    [Benchmark]
    public long FrozenDictionary_IterateKeyValuePairs()
    {
        long sum = 0;
        foreach (var kvp in _frozenDictionary)
        {
            sum += kvp.Key + kvp.Value;
        }
        return sum;
    }

    [Benchmark]
    public long List_IterateKeyValuePairs()
    {
        long sum = 0;
        foreach (var kvp in _list)
        {
            sum += kvp.Key + kvp.Value;
        }
        return sum;
    }

    [Benchmark]
    public long Array_IterateKeyValuePairs()
    {
        long sum = 0;
        foreach (var kvp in _array)
        {
            sum += kvp.Key + kvp.Value;
        }
        return sum;
    }

    // ============================================================================
    // Iterate Keys only
    // ============================================================================

    [Benchmark]
    public long Dictionary_IterateKeys()
    {
        long sum = 0;
        foreach (var key in _dictionary.Keys)
        {
            sum += key;
        }
        return sum;
    }

    [Benchmark]
    public long FrozenDictionary_IterateKeys()
    {
        long sum = 0;
        foreach (var key in _frozenDictionary.Keys)
        {
            sum += key;
        }
        return sum;
    }

    [Benchmark]
    public long List_IterateKeys()
    {
        long sum = 0;
        foreach (var kvp in _list)
        {
            sum += kvp.Key;
        }
        return sum;
    }

    [Benchmark]
    public long Array_IterateKeys()
    {
        long sum = 0;
        foreach (var key in _keys)
        {
            sum += key;
        }
        return sum;
    }

    // ============================================================================
    // Iterate Values only
    // ============================================================================

    [Benchmark]
    public long Dictionary_IterateValues()
    {
        long sum = 0;
        foreach (var value in _dictionary.Values)
        {
            sum += value;
        }
        return sum;
    }

    [Benchmark]
    public long FrozenDictionary_IterateValues()
    {
        long sum = 0;
        foreach (var value in _frozenDictionary.Values)
        {
            sum += value;
        }
        return sum;
    }

    [Benchmark]
    public long List_IterateValues()
    {
        long sum = 0;
        foreach (var kvp in _list)
        {
            sum += kvp.Value;
        }
        return sum;
    }

    [Benchmark]
    public long Array_IterateValues()
    {
        long sum = 0;
        foreach (var value in _values)
        {
            sum += value;
        }
        return sum;
    }

    // ============================================================================
    // Span-based iteration (where applicable)
    // ============================================================================

    [Benchmark]
    public long ListAsSpan_IterateKeyValuePairs()
    {
        long sum = 0;
        var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_list);
        foreach (var kvp in span)
        {
            sum += kvp.Key + kvp.Value;
        }
        return sum;
    }

    [Benchmark]
    public long ArrayAsSpan_IterateKeyValuePairs()
    {
        long sum = 0;
        ReadOnlySpan<KeyValuePair<int, long>> span = _array;
        foreach (var kvp in span)
        {
            sum += kvp.Key + kvp.Value;
        }
        return sum;
    }

    [Benchmark]
    public long KeysAsSpan_Iterate()
    {
        long sum = 0;
        ReadOnlySpan<int> span = _keys;
        foreach (var key in span)
        {
            sum += key;
        }
        return sum;
    }

    [Benchmark]
    public long ValuesAsSpan_Iterate()
    {
        long sum = 0;
        ReadOnlySpan<long> span = _values;
        foreach (var value in span)
        {
            sum += value;
        }
        return sum;
    }
}
