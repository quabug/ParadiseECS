using System.Runtime.CompilerServices;

namespace Paradise.ECS;

/// <summary>
/// Extension methods for QueryBuilder to support TaggedWorld.
/// </summary>
public static class QueryBuilderExtensions
{
    /// <summary>
    /// Builds a query from this description using the specified tagged world.
    /// </summary>
    /// <typeparam name="TMask">The component mask type.</typeparam>
    /// <typeparam name="TConfig">The world configuration type.</typeparam>
    /// <typeparam name="TEntityTags">The entity tags component type.</typeparam>
    /// <typeparam name="TTagMask">The tag mask type.</typeparam>
    /// <param name="builder">The query builder.</param>
    /// <param name="world">The tagged world to query.</param>
    /// <returns>A cached query that matches archetypes based on this description.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Query<TMask, TConfig, Archetype<TMask, TConfig>> Build<TMask, TConfig, TEntityTags, TTagMask>(
        this QueryBuilder<TMask> builder,
        TaggedWorld<TMask, TConfig, TEntityTags, TTagMask> world)
        where TMask : unmanaged, IBitSet<TMask>
        where TConfig : IConfig, new()
        where TEntityTags : unmanaged, IComponent, IEntityTags<TTagMask>
        where TTagMask : unmanaged, IBitSet<TTagMask>
        => world.ArchetypeRegistry.GetOrCreateQuery((HashedKey<ImmutableQueryDescription<TMask>>)builder.Description);
}
