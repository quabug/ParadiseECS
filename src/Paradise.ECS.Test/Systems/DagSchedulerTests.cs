using System.Collections.Immutable;

namespace Paradise.ECS.Test;

using Mask = SmallBitSet<uint>;

/// <summary>
/// Unit tests for <see cref="DefaultDagScheduler"/> covering DAG resolution,
/// conflict detection, cycle detection, and subset scheduling.
/// </summary>
public sealed class DagSchedulerTests
{
    private readonly DefaultDagScheduler _scheduler = new();

    private static SystemMetadata<Mask> Meta(
        int systemId,
        Mask readMask = default,
        Mask writeMask = default,
        ImmutableArray<int> afterSystemIds = default)
    {
        return new SystemMetadata<Mask>
        {
            SystemId = systemId,
            TypeName = $"System{systemId}",
            ReadMask = readMask,
            WriteMask = writeMask,
            AfterSystemIds = afterSystemIds.IsDefault ? ImmutableArray<int>.Empty : afterSystemIds,
        };
    }

    private static Mask Bit(int index) => Mask.Empty.Set(index);

    /// <summary>Returns sorted local indices for the given wave.</summary>
    private static int[] Sorted(int[] wave) => wave.OrderBy(x => x).ToArray();

    /// <summary>Returns the wave index for each local system index.</summary>
    private static int[] WaveMap(int[][] waves, int systemCount)
    {
        var map = new int[systemCount];
        for (int w = 0; w < waves.Length; w++)
            foreach (var idx in waves[w])
                map[idx] = w;
        return map;
    }

    // ---- Empty / Single ----

    [Test]
    public async Task ComputeWaves_EmptyInput_ReturnsEmptyArray()
    {
        var waves = _scheduler.ComputeWaves(ReadOnlySpan<SystemMetadata<Mask>>.Empty);
        await Assert.That(waves.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ComputeWaves_SingleSystem_ReturnsSingleWave()
    {
        SystemMetadata<Mask>[] systems = [Meta(0)];
        var waves = _scheduler.ComputeWaves(systems);

        await Assert.That(waves.Length).IsEqualTo(1);
        await Assert.That(waves[0].Length).IsEqualTo(1);
        await Assert.That(waves[0][0]).IsEqualTo(0);
    }

    // ---- Independent Systems ----

    [Test]
    public async Task ComputeWaves_IndependentNonConflicting_SingleWave()
    {
        // System A reads bit 0, System B reads bit 1 — no conflict, no deps
        SystemMetadata<Mask>[] systems =
        [
            Meta(0, readMask: Bit(0)),
            Meta(1, readMask: Bit(1)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        await Assert.That(waves.Length).IsEqualTo(1);
        await Assert.That(waves[0].Length).IsEqualTo(2);
    }

    [Test]
    public async Task ComputeWaves_ReadOnlyOverlap_SameWave()
    {
        // Both read bit 0, neither writes — no conflict
        SystemMetadata<Mask>[] systems =
        [
            Meta(0, readMask: Bit(0)),
            Meta(1, readMask: Bit(0)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        await Assert.That(waves.Length).IsEqualTo(1);
        await Assert.That(waves[0].Length).IsEqualTo(2);
    }

    // ---- Conflict Detection ----

    [Test]
    public async Task ComputeWaves_WriteReadConflict_SeparateWaves()
    {
        // A writes bit 0, B reads bit 0 — conflict
        SystemMetadata<Mask>[] systems =
        [
            Meta(0, readMask: Bit(0), writeMask: Bit(0)),
            Meta(1, readMask: Bit(0)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        await Assert.That(waves.Length).IsEqualTo(2);
    }

    [Test]
    public async Task ComputeWaves_WriteWriteConflict_SeparateWaves()
    {
        // Both write bit 0 — conflict
        SystemMetadata<Mask>[] systems =
        [
            Meta(0, readMask: Bit(0), writeMask: Bit(0)),
            Meta(1, readMask: Bit(0), writeMask: Bit(0)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        await Assert.That(waves.Length).IsEqualTo(2);
    }

    [Test]
    public async Task ComputeWaves_ThreeSystemsTwoConflict_CorrectWaves()
    {
        // A writes bit 0, B reads bit 0 (conflict with A), C reads bit 1 (no conflict with either)
        SystemMetadata<Mask>[] systems =
        [
            Meta(0, readMask: Bit(0), writeMask: Bit(0)),
            Meta(1, readMask: Bit(0)),
            Meta(2, readMask: Bit(1)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        var waveOf = WaveMap(waves, 3);

        // A and B must be in different waves
        await Assert.That(waveOf[0]).IsNotEqualTo(waveOf[1]);
        // A and C can be in the same wave (no conflict)
        await Assert.That(waveOf[0]).IsEqualTo(waveOf[2]);
    }

    // ---- Explicit Dependencies ----

    [Test]
    public async Task ComputeWaves_AfterDependency_EnforcesOrder()
    {
        // B runs after A (even though no conflict)
        SystemMetadata<Mask>[] systems =
        [
            Meta(0),
            Meta(1, afterSystemIds: ImmutableArray.Create(0)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        await Assert.That(waves.Length).IsEqualTo(2);
        await Assert.That(waves[0][0]).IsEqualTo(0);
        await Assert.That(waves[1][0]).IsEqualTo(1);
    }

    [Test]
    public async Task ComputeWaves_ChainDependency_ThreeWaves()
    {
        // A → B → C
        SystemMetadata<Mask>[] systems =
        [
            Meta(10),
            Meta(20, afterSystemIds: ImmutableArray.Create(10)),
            Meta(30, afterSystemIds: ImmutableArray.Create(20)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        await Assert.That(waves.Length).IsEqualTo(3);
        await Assert.That(waves[0][0]).IsEqualTo(0);
        await Assert.That(waves[1][0]).IsEqualTo(1);
        await Assert.That(waves[2][0]).IsEqualTo(2);
    }

    [Test]
    public async Task ComputeWaves_DiamondDependency_CorrectWaves()
    {
        // A → B, A → C, B → D, C → D
        SystemMetadata<Mask>[] systems =
        [
            Meta(0),
            Meta(1, afterSystemIds: ImmutableArray.Create(0)),
            Meta(2, afterSystemIds: ImmutableArray.Create(0)),
            Meta(3, afterSystemIds: ImmutableArray.Create(1, 2)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        var waveOf = WaveMap(waves, 4);

        // A must be first
        await Assert.That(waveOf[0]).IsEqualTo(0);
        // B and C both depend on A, can be in same wave
        await Assert.That(waveOf[1]).IsEqualTo(waveOf[2]);
        await Assert.That(waveOf[1]).IsGreaterThan(waveOf[0]);
        // D depends on both B and C
        await Assert.That(waveOf[3]).IsGreaterThan(waveOf[1]);
    }

    [Test]
    public async Task ComputeWaves_DependencyPlusConflict_BothRespected()
    {
        // B depends on A (explicit). C conflicts with B (write/read on bit 0). No dep between A and C.
        SystemMetadata<Mask>[] systems =
        [
            Meta(0),
            Meta(1, readMask: Bit(0), writeMask: Bit(0), afterSystemIds: ImmutableArray.Create(0)),
            Meta(2, readMask: Bit(0)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        var waveOf = WaveMap(waves, 3);

        // A before B (explicit dep)
        await Assert.That(waveOf[1]).IsGreaterThan(waveOf[0]);
        // B and C in different waves (conflict)
        await Assert.That(waveOf[1]).IsNotEqualTo(waveOf[2]);
    }

    // ---- Cycle Detection ----

    [Test]
    public async Task ComputeWaves_CyclicDependency_ThrowsInvalidOperation()
    {
        // A → B → A
        SystemMetadata<Mask>[] systems =
        [
            Meta(0, afterSystemIds: ImmutableArray.Create(1)),
            Meta(1, afterSystemIds: ImmutableArray.Create(0)),
        ];

        await Assert.That(() => _scheduler.ComputeWaves<Mask>(systems))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task ComputeWaves_SelfCycle_ThrowsInvalidOperation()
    {
        // A depends on itself
        SystemMetadata<Mask>[] systems =
        [
            Meta(0, afterSystemIds: ImmutableArray.Create(0)),
        ];

        await Assert.That(() => _scheduler.ComputeWaves<Mask>(systems))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task ComputeWaves_ThreeNodeCycle_ThrowsInvalidOperation()
    {
        // A → B → C → A
        SystemMetadata<Mask>[] systems =
        [
            Meta(0, afterSystemIds: ImmutableArray.Create(2)),
            Meta(1, afterSystemIds: ImmutableArray.Create(0)),
            Meta(2, afterSystemIds: ImmutableArray.Create(1)),
        ];

        await Assert.That(() => _scheduler.ComputeWaves<Mask>(systems))
            .ThrowsExactly<InvalidOperationException>();
    }

    // ---- Subset Scheduling ----

    [Test]
    public async Task ComputeWaves_DepsOutsideSubset_AreSkipped()
    {
        // B depends on A (systemId=99), but A is not in the set → dep is ignored
        SystemMetadata<Mask>[] systems =
        [
            Meta(1, afterSystemIds: ImmutableArray.Create(99)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        await Assert.That(waves.Length).IsEqualTo(1);
        await Assert.That(waves[0][0]).IsEqualTo(0);
    }

    [Test]
    public async Task ComputeWaves_SubsetSkipsMissing_NoDeps()
    {
        // Original: A(0) → B(1) → C(2). Only add A and C.
        // C depends on B(1) which is missing, so dep is skipped.
        // A and C have no conflict → same wave.
        SystemMetadata<Mask>[] systems =
        [
            Meta(0),
            Meta(2, afterSystemIds: ImmutableArray.Create(1)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        await Assert.That(waves.Length).IsEqualTo(1);
        await Assert.That(waves[0].Length).IsEqualTo(2);
    }

    // ---- Non-Sequential SystemIds ----

    [Test]
    public async Task ComputeWaves_NonSequentialGlobalIds_MapsCorrectly()
    {
        // Global IDs are 100, 200, 300 — must map to local indices 0, 1, 2
        SystemMetadata<Mask>[] systems =
        [
            Meta(100),
            Meta(200, afterSystemIds: ImmutableArray.Create(100)),
            Meta(300, afterSystemIds: ImmutableArray.Create(200)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        await Assert.That(waves.Length).IsEqualTo(3);
        await Assert.That(waves[0][0]).IsEqualTo(0);
        await Assert.That(waves[1][0]).IsEqualTo(1);
        await Assert.That(waves[2][0]).IsEqualTo(2);
    }

    // ---- Default AfterSystemIds ----

    [Test]
    public async Task ComputeWaves_DefaultAfterSystemIds_HandledGracefully()
    {
        // AfterSystemIds left as default (IsDefault = true)
        SystemMetadata<Mask>[] systems =
        [
            new SystemMetadata<Mask> { SystemId = 0, TypeName = "A" },
            new SystemMetadata<Mask> { SystemId = 1, TypeName = "B" },
        ];
        var waves = _scheduler.ComputeWaves(systems);

        await Assert.That(waves.Length).IsEqualTo(1);
        await Assert.That(waves[0].Length).IsEqualTo(2);
    }

    // ---- Wave Completeness ----

    [Test]
    public async Task ComputeWaves_AllIndicesPresent()
    {
        // 5 systems with various deps and conflicts — verify all indices appear exactly once
        SystemMetadata<Mask>[] systems =
        [
            Meta(0, readMask: Bit(0), writeMask: Bit(0)),
            Meta(1, readMask: Bit(0)),
            Meta(2, readMask: Bit(1), writeMask: Bit(1)),
            Meta(3, readMask: Bit(1)),
            Meta(4, afterSystemIds: ImmutableArray.Create(0)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        var allIndices = Sorted(waves.SelectMany(w => w).ToArray());
        await Assert.That(allIndices.Length).IsEqualTo(5);
        for (int i = 0; i < 5; i++)
            await Assert.That(allIndices[i]).IsEqualTo(i);
    }

    // ---- Multiple Cascading Conflicts ----

    [Test]
    public async Task ComputeWaves_CascadingConflicts_MultipleWaves()
    {
        // 3 systems all write to bit 0 — must be in 3 separate waves
        SystemMetadata<Mask>[] systems =
        [
            Meta(0, readMask: Bit(0), writeMask: Bit(0)),
            Meta(1, readMask: Bit(0), writeMask: Bit(0)),
            Meta(2, readMask: Bit(0), writeMask: Bit(0)),
        ];
        var waves = _scheduler.ComputeWaves(systems);

        await Assert.That(waves.Length).IsEqualTo(3);
    }
}
