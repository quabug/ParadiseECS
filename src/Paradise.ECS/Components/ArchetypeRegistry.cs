using System.Collections.Concurrent;

namespace Paradise.ECS;

/// <summary>
/// Manages unique archetypes and provides lookup by component mask.
/// Thread-safe for concurrent archetype creation and lookup.
/// </summary>
/// <typeparam name="TBits">The bit storage type for component masks.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
public sealed class ArchetypeRegistry<TBits, TRegistry> : IDisposable
    where TBits : unmanaged, IStorage
    where TRegistry : IComponentRegistry
{
    private readonly ConcurrentDictionary<ImmutableBitSet<TBits>, int> _maskToArchetypeId = new();
    private readonly List<ArchetypeStore<TBits, TRegistry>> _archetypes = [];
    private readonly List<ImmutableArchetypeLayout<TBits, TRegistry>> _layouts = [];
    private readonly Lock _createLock = new();
    private readonly ChunkManager _chunkManager;
    private int _disposed;

    /// <summary>
    /// Gets the number of registered archetypes.
    /// </summary>
    public int Count => _archetypes.Count;

    /// <summary>
    /// Creates a new archetype registry.
    /// </summary>
    /// <param name="chunkManager">The chunk manager for memory allocation.</param>
    public ArchetypeRegistry(ChunkManager chunkManager)
    {
        ArgumentNullException.ThrowIfNull(chunkManager);
        _chunkManager = chunkManager;
    }

    /// <summary>
    /// Gets or creates an archetype for the given component mask.
    /// </summary>
    /// <param name="mask">The component mask defining the archetype.</param>
    /// <returns>The archetype store for this mask.</returns>
    public ArchetypeStore<TBits, TRegistry> GetOrCreate(ImmutableBitSet<TBits> mask)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        // Fast path: already exists
        if (_maskToArchetypeId.TryGetValue(mask, out int existingId))
        {
            using var _ = _createLock.EnterScope();
            return _archetypes[existingId];
        }

        // Slow path: create new archetype
        using var createScope = _createLock.EnterScope();

        // Double-check after acquiring lock
        if (_maskToArchetypeId.TryGetValue(mask, out existingId))
        {
            return _archetypes[existingId];
        }

        var layout = new ImmutableArchetypeLayout<TBits, TRegistry>(mask);
        int newId = _archetypes.Count;
        var store = new ArchetypeStore<TBits, TRegistry>(newId, layout, _chunkManager);

        _layouts.Add(layout);
        _archetypes.Add(store);
        _maskToArchetypeId[mask] = newId;

        return store;
    }

    /// <summary>
    /// Gets an archetype by its ID.
    /// </summary>
    /// <param name="archetypeId">The archetype ID.</param>
    /// <returns>The archetype store, or null if not found.</returns>
    public ArchetypeStore<TBits, TRegistry>? GetById(int archetypeId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        if (archetypeId < 0 || archetypeId >= _archetypes.Count)
            return null;
        return _archetypes[archetypeId];
    }

    /// <summary>
    /// Tries to get an archetype by its component mask.
    /// </summary>
    /// <param name="mask">The component mask.</param>
    /// <param name="store">The archetype store if found.</param>
    /// <returns>True if found.</returns>
    public bool TryGet(ImmutableBitSet<TBits> mask, out ArchetypeStore<TBits, TRegistry>? store)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        if (_maskToArchetypeId.TryGetValue(mask, out int id))
        {
            store = _archetypes[id];
            return true;
        }

        store = null;
        return false;
    }

    /// <summary>
    /// Iterates all archetypes matching the given filter.
    /// </summary>
    /// <typeparam name="T">The collection type to store matching archetypes.</typeparam>
    /// <param name="all">Components that must all be present.</param>
    /// <param name="none">Components that must not be present.</param>
    /// <param name="any">At least one of these components must be present. Pass empty for no constraint.</param>
    /// <param name="output">The collection to receive matching archetype stores.</param>
    /// <returns>The number of matching archetypes added to the output collection.</returns>
    public int GetMatching<T>(
        ImmutableBitSet<TBits> all,
        ImmutableBitSet<TBits> none,
        ImmutableBitSet<TBits> any,
        T output
    ) where T : IList<ArchetypeStore<TBits, TRegistry>>
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);

        int count = 0;
        bool hasAnyConstraint = !any.IsEmpty;
        using var _ = _createLock.EnterScope();
        foreach (var store in _archetypes)
        {
            // Get the mask from the store's component IDs
            var layout = store.Layout;
            var mask = layout.ComponentMask;

            // Check if archetype matches the filter
            if (mask.ContainsAll(all) &&
                mask.ContainsNone(none) &&
                (!hasAnyConstraint || mask.ContainsAny(any)))
            {
                output.Add(store);
                count++;
            }
        }
        return count;
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        foreach (var layout in _layouts)
        {
            layout.Dispose();
        }

        _layouts.Clear();
        _archetypes.Clear();
        _maskToArchetypeId.Clear();
    }
}
