using BenchmarkDotNet.Attributes;

namespace Paradise.ECS.Benchmarks;

[Component("A1B2C3D4-E5F6-7890-ABCD-EF1234567890", Id = 0)]
public partial struct Position
{
    public float X, Y, Z;
}

[Component("B2C3D4E5-F678-90AB-CDEF-123456789012", Id = 1)]
public partial struct Velocity
{
    public float X, Y, Z;
}

[Component("C3D4E5F6-7890-ABCD-EF12-345678901234", Id = 2)]
public partial struct Health
{
    public int Current, Max;
}

[Component("D4E5F678-90AB-CDEF-1234-567890123456", Id = 3)]
public partial struct Damage
{
    public int Value;
}

[Component("E5F67890-ABCD-EF12-3456-789012345678", Id = 4)]
public partial struct Armor
{
    public int Value;
}

[Config(typeof(NativeAotConfig))]
[MemoryDiagnoser]
[ShortRunJob]
public class QueryBenchmarks
{
    private ChunkManager _chunkManager = null!;
    private ArchetypeRegistry<Bit64, ComponentRegistry> _registry = null!;
    private Query<Bit64, ComponentRegistry> _simpleQuery = null!;
    private Query<Bit64, ComponentRegistry> _complexQuery = null!;

    [Params(10, 50, 100)]
    public int ArchetypeCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _chunkManager = new ChunkManager(initialCapacity: 256);
        _registry = new ArchetypeRegistry<Bit64, ComponentRegistry>(_chunkManager);

        // Create various archetypes
        var componentIds = new[] { Position.TypeId, Velocity.TypeId, Health.TypeId, Damage.TypeId, Armor.TypeId };

        for (int i = 0; i < ArchetypeCount; i++)
        {
            var mask = ImmutableBitSet<Bit64>.Empty;

            // Add components based on bit pattern of i
            for (int j = 0; j < componentIds.Length; j++)
            {
                if ((i & (1 << j)) != 0)
                {
                    mask = mask.Set(componentIds[j]);
                }
            }

            // Ensure at least one component
            if (mask.IsEmpty)
            {
                mask = mask.Set(Position.TypeId);
            }

            _registry.GetOrCreate((HashedKey<ImmutableBitSet<Bit64>>)mask);
        }

        // Simple query: just Position
        _simpleQuery = new QueryBuilder<Bit64, ComponentRegistry>()
            .With<Position>()
            .Build(_registry);

        // Complex query: Position AND Velocity, NOT Health
        _complexQuery = new QueryBuilder<Bit64, ComponentRegistry>()
            .With<Position>()
            .With<Velocity>()
            .Without<Health>()
            .Build(_registry);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _registry?.Dispose();
        _chunkManager?.Dispose();
    }

    [Benchmark]
    public int SimpleQuery_ArchetypeCount()
    {
        return _simpleQuery.ArchetypeCount;
    }

    [Benchmark]
    public int ComplexQuery_ArchetypeCount()
    {
        return _complexQuery.ArchetypeCount;
    }
}
