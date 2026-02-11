namespace Paradise.ECS;

/// <summary>
/// Strategy interface for computing execution waves from system metadata.
/// The DAG scheduler resolves dependency edges and component access conflicts
/// to produce parallelizable wave groups.
/// </summary>
public interface IDagScheduler
{
    /// <summary>
    /// Computes execution waves from system metadata.
    /// Each wave contains system indices (into the input span) that can run in parallel.
    /// </summary>
    /// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
    /// <param name="systems">The metadata for all systems to schedule.</param>
    /// <returns>An array of waves, where each wave is an array of local indices into <paramref name="systems"/>.</returns>
    int[][] ComputeWaves<TMask>(ReadOnlySpan<SystemMetadata<TMask>> systems)
        where TMask : unmanaged, IBitSet<TMask>;
}

/// <summary>
/// Default DAG scheduler using Kahn's topological sort and greedy wave assignment.
/// Resolves explicit dependency edges (<c>[After]</c>/<c>[Before]</c>) and
/// separates systems with component read/write conflicts into different waves.
/// </summary>
public sealed class DefaultDagScheduler : IDagScheduler
{
    /// <inheritdoc/>
    public int[][] ComputeWaves<TMask>(ReadOnlySpan<SystemMetadata<TMask>> systems)
        where TMask : unmanaged, IBitSet<TMask>
    {
        int n = systems.Length;
        if (n == 0) return [];

        // Map global SystemId → local index (0..N-1)
        var globalToLocal = new Dictionary<int, int>(n);
        for (int i = 0; i < n; i++)
            globalToLocal[systems[i].SystemId] = i;

        // Build adjacency list from AfterSystemIds (skip deps not in the set)
        var adj = new List<int>[n];
        var inDegree = new int[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();

        for (int i = 0; i < n; i++)
        {
            var afterIds = systems[i].AfterSystemIds;
            if (afterIds.IsDefault) continue;
            foreach (var globalId in afterIds)
            {
                if (globalToLocal.TryGetValue(globalId, out var localPred))
                {
                    adj[localPred].Add(i);
                    inDegree[i]++;
                }
            }
        }

        // Topological sort (Kahn's algorithm)
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
            if (inDegree[i] == 0) queue.Enqueue(i);

        var topoOrder = new List<int>(n);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            topoOrder.Add(node);
            foreach (var succ in adj[node])
            {
                inDegree[succ]--;
                if (inDegree[succ] == 0) queue.Enqueue(succ);
            }
        }

        if (topoOrder.Count != n)
            throw new InvalidOperationException("Cyclic dependency detected among systems.");

        // Greedy wave assignment: earliest wave respecting deps, then bump on conflicts
        var waveOf = new int[n];
        for (int i = 0; i < n; i++) waveOf[i] = -1;

        var waveLists = new List<List<int>>();
        foreach (var node in topoOrder)
        {
            int wave = 0;
            foreach (var pred in Enumerable.Range(0, n).Where(p => adj[p].Contains(node)))
            {
                if (waveOf[pred] >= 0)
                    wave = Math.Max(wave, waveOf[pred] + 1);
            }

            while (true)
            {
                while (waveLists.Count <= wave) waveLists.Add(new List<int>());

                bool hasConflict = false;
                foreach (var other in waveLists[wave])
                {
                    if (HasConflict(systems[node], systems[other]))
                    {
                        hasConflict = true;
                        break;
                    }
                }

                if (!hasConflict) break;
                wave++;
            }

            while (waveLists.Count <= wave) waveLists.Add(new List<int>());
            waveLists[wave].Add(node);
            waveOf[node] = wave;
        }

        var waves = new int[waveLists.Count][];
        for (int w = 0; w < waveLists.Count; w++)
            waves[w] = waveLists[w].ToArray();
        return waves;
    }

    private static bool HasConflict<TMask>(SystemMetadata<TMask> a, SystemMetadata<TMask> b)
        where TMask : unmanaged, IBitSet<TMask>
    {
        if (a.WriteMask.ContainsAny(b.ReadMask)) return true;
        if (b.WriteMask.ContainsAny(a.ReadMask)) return true;
        return false;
    }
}
