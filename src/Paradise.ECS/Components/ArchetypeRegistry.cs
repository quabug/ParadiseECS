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
    private readonly ConcurrentDictionary<HashedKey<ImmutableBitSet<TBits>>, int> _maskToArchetypeId = new();
    private readonly List<Archetype<TBits, TRegistry>> _archetypes = [];
    private readonly List<ImmutableArchetypeLayout<TBits, TRegistry>> _layouts = [];
    private readonly List<Query<TBits, TRegistry>> _queries = [];
    private readonly Lock _createLock = new();
    private readonly ChunkManager _chunkManager;

    // Graph edges for O(1) structural changes.
    private readonly ConcurrentDictionary<EdgeKey, int> _edges = new();

    private int _activeOperations;
    private int _disposed;

    /// <summary>
    /// Gets the number of registered archetypes.
    /// </summary>
    public int Count => _archetypes.Count;

    private OperationGuard BeginOperation() => new(ref _activeOperations);

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
    /// Registers a query with this registry. The query's cache will be populated with
    /// existing matching archetypes and updated when new archetypes are created.
    /// </summary>
    /// <param name="query">The query to register.</param>
    internal void RegisterQuery(Query<TBits, TRegistry> query)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = BeginOperation();

        using var lockScope = _createLock.EnterScope();

        // Populate with existing matching archetypes
        var description = query.Description;

        foreach (var archetype in _archetypes)
        {
            if (description.Matches(archetype.Layout.ComponentMask))
            {
                query.AddMatchingArchetype(archetype);
            }
        }

        _queries.Add(query);
    }

    /// <summary>
    /// Gets or creates an archetype for the given component mask.
    /// </summary>
    /// <param name="mask">The component mask defining the archetype.</param>
    /// <returns>The archetype store for this mask.</returns>
    public Archetype<TBits, TRegistry> GetOrCreate(HashedKey<ImmutableBitSet<TBits>> mask)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = BeginOperation();

        // Fast path: already exists
        if (_maskToArchetypeId.TryGetValue(mask, out int existingId))
        {
            using var lockScope = _createLock.EnterScope();
            return _archetypes[existingId];
        }

        // Slow path: create new archetype
        using var createScope = _createLock.EnterScope();

        // Double-check after acquiring lock
        if (_maskToArchetypeId.TryGetValue(mask, out existingId))
        {
            return _archetypes[existingId];
        }

        int newId = _archetypes.Count;
        if (newId > EcsLimits.MaxArchetypeId)
            throw new InvalidOperationException($"Archetype count exceeded maximum of {EcsLimits.MaxArchetypeId}.");

        var layout = new ImmutableArchetypeLayout<TBits, TRegistry>(mask);
        var store = new Archetype<TBits, TRegistry>(newId, layout, _chunkManager);

        _layouts.Add(layout);
        _archetypes.Add(store);
        _maskToArchetypeId[mask] = newId;

        // Notify all registered queries about the new archetype
        NotifyQueries(store);

        return store;
    }

    /// <summary>
    /// Notifies all registered queries about a newly created archetype.
    /// </summary>
    /// <param name="archetype">The newly created archetype.</param>
    private void NotifyQueries(Archetype<TBits, TRegistry> archetype)
    {
        var mask = archetype.Layout.ComponentMask;

        foreach (var query in _queries)
        {
            if (query.Description.Matches(mask))
            {
                query.AddMatchingArchetype(archetype);
            }
        }
    }

    /// <summary>
    /// Gets an archetype by its ID.
    /// </summary>
    /// <param name="archetypeId">The archetype ID.</param>
    /// <returns>The archetype store, or null if not found.</returns>
    public Archetype<TBits, TRegistry>? GetById(int archetypeId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = BeginOperation();

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
    public bool TryGet(HashedKey<ImmutableBitSet<TBits>> mask, out Archetype<TBits, TRegistry>? store)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = BeginOperation();

        if (_maskToArchetypeId.TryGetValue(mask, out int id))
        {
            store = _archetypes[id];
            return true;
        }

        store = null;
        return false;
    }

    /// <summary>
    /// Gets or creates the archetype resulting from adding a component to the source archetype.
    /// Uses cached graph edges for O(1) lookup on subsequent calls.
    /// </summary>
    /// <param name="source">The source archetype.</param>
    /// <param name="componentId">The component to add.</param>
    /// <returns>The target archetype with the component added.</returns>
    public Archetype<TBits, TRegistry> GetOrCreateWithAdd(
        Archetype<TBits, TRegistry> source,
        ComponentId componentId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = BeginOperation();
        ArgumentNullException.ThrowIfNull(source);

        var addKey = EdgeKey.ForAdd(source.Id, componentId.Value);

        // Fast path: edge already exists
        if (_edges.TryGetValue(addKey, out int targetId))
        {
            using var lockScope = _createLock.EnterScope();
            return _archetypes[targetId];
        }

        // Slow path: compute mask and get/create archetype
        var newMask = (HashedKey<ImmutableBitSet<TBits>>)source.Layout.ComponentMask.Set(componentId);
        var target = GetOrCreate(newMask);

        // Cache bidirectional edges
        var removeKey = EdgeKey.ForRemove(target.Id, componentId.Value);
        _edges[addKey] = target.Id;
        _edges[removeKey] = source.Id;

        return target;
    }

    /// <summary>
    /// Gets or creates the archetype resulting from removing a component from the source archetype.
    /// Uses cached graph edges for O(1) lookup on subsequent calls.
    /// </summary>
    /// <param name="source">The source archetype.</param>
    /// <param name="componentId">The component to remove.</param>
    /// <returns>The target archetype with the component removed.</returns>
    public Archetype<TBits, TRegistry> GetOrCreateWithRemove(
        Archetype<TBits, TRegistry> source,
        ComponentId componentId)
    {
        ThrowHelper.ThrowIfDisposed(_disposed != 0, this);
        using var _ = BeginOperation();
        ArgumentNullException.ThrowIfNull(source);

        var removeKey = EdgeKey.ForRemove(source.Id, componentId.Value);

        // Fast path: edge already exists
        if (_edges.TryGetValue(removeKey, out int targetId))
        {
            using var lockScope = _createLock.EnterScope();
            return _archetypes[targetId];
        }

        // Slow path: compute mask and get/create archetype
        var newMask = (HashedKey<ImmutableBitSet<TBits>>)source.Layout.ComponentMask.Clear(componentId);
        var target = GetOrCreate(newMask);

        // Cache bidirectional edges
        var addKey = EdgeKey.ForAdd(target.Id, componentId.Value);
        _edges[removeKey] = target.Id;
        _edges[addKey] = source.Id;

        return target;
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            return;

        // Wait for all in-flight operations to complete
        var sw = new SpinWait();
        while (Volatile.Read(ref _activeOperations) > 0)
            sw.SpinOnce();

        foreach (var layout in _layouts)
        {
            layout.Dispose();
        }

        _layouts.Clear();
        _archetypes.Clear();
        _maskToArchetypeId.Clear();
        _edges.Clear();
        _queries.Clear();
    }
}
