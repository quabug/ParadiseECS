namespace Paradise.ECS;

/// <summary>
/// Interface for archetype registry operations.
/// Manages unique archetypes and provides lookup by component mask.
/// </summary>
/// <typeparam name="TMask">The component mask type implementing IBitSet.</typeparam>
/// <typeparam name="TRegistry">The component registry type that provides component type information.</typeparam>
/// <typeparam name="TConfig">The world configuration type.</typeparam>
/// <typeparam name="TArchetype">The concrete archetype type.</typeparam>
public interface IArchetypeRegistry<TMask, TRegistry, TConfig, TArchetype>
    where TMask : unmanaged, IBitSet<TMask>
    where TRegistry : IComponentRegistry
    where TConfig : IConfig, new()
    where TArchetype : class, IArchetype<TMask, TRegistry, TConfig>
{
    /// <summary>
    /// Gets or creates an archetype for the given component mask.
    /// </summary>
    /// <param name="mask">The component mask defining the archetype.</param>
    /// <returns>The archetype for this mask.</returns>
    TArchetype GetOrCreate(HashedKey<TMask> mask);

    /// <summary>
    /// Gets or creates the archetype resulting from adding a component to the source archetype.
    /// Uses cached graph edges for O(1) lookup on subsequent calls.
    /// </summary>
    /// <param name="source">The source archetype.</param>
    /// <param name="componentId">The component to add.</param>
    /// <returns>The target archetype with the component added.</returns>
    TArchetype GetOrCreateWithAdd(TArchetype source, ComponentId componentId);

    /// <summary>
    /// Gets or creates the archetype resulting from removing a component from the source archetype.
    /// Uses cached graph edges for O(1) lookup on subsequent calls.
    /// </summary>
    /// <param name="source">The source archetype.</param>
    /// <param name="componentId">The component to remove.</param>
    /// <returns>The target archetype with the component removed.</returns>
    TArchetype GetOrCreateWithRemove(TArchetype source, ComponentId componentId);

    /// <summary>
    /// Gets an archetype by its ID.
    /// </summary>
    /// <param name="archetypeId">The archetype ID.</param>
    /// <returns>The archetype, or null if not found.</returns>
    TArchetype? GetById(int archetypeId);

    /// <summary>
    /// Gets or creates a query for the given description.
    /// Queries are cached and reused for the same description.
    /// </summary>
    /// <param name="description">The query description defining matching criteria.</param>
    /// <returns>The query for this description.</returns>
    Query<TMask, TRegistry, TConfig, TArchetype> GetOrCreateQuery(HashedKey<ImmutableQueryDescription<TMask>> description);
}
