using System.Collections.Immutable;

namespace Paradise.ECS.Concurrent;

/// <summary>
/// Defines the contract for a component registry that provides runtime Type-to-ComponentId mapping.
/// Implemented by the source-generated ComponentRegistry class.
/// </summary>
public interface IComponentRegistry
{
    /// <summary>
    /// Gets the ComponentId for a given Type.
    /// </summary>
    /// <param name="type">The component type.</param>
    /// <returns>The ComponentId, or ComponentId.Invalid if type is not a registered component.</returns>
    static abstract ComponentId GetId(Type type);

    /// <summary>
    /// Tries to get the ComponentId for a given Type.
    /// </summary>
    /// <param name="type">The component type.</param>
    /// <param name="id">When this method returns, contains the ComponentId if found.</param>
    /// <returns>True if the type is a registered component; otherwise, false.</returns>
    static abstract bool TryGetId(Type type, out ComponentId id);

    /// <summary>
    /// Gets the ComponentId for a given GUID.
    /// </summary>
    /// <param name="guid">The component GUID.</param>
    /// <returns>The ComponentId, or ComponentId.Invalid if GUID is not registered.</returns>
    static abstract ComponentId GetId(Guid guid);

    /// <summary>
    /// Tries to get the ComponentId for a given GUID.
    /// </summary>
    /// <param name="guid">The component GUID.</param>
    /// <param name="id">When this method returns, contains the ComponentId if found.</param>
    /// <returns>True if the GUID is registered; otherwise, false.</returns>
    static abstract bool TryGetId(Guid guid, out ComponentId id);

    /// <summary>
    /// Gets the array of all registered component type information, indexed by ComponentId.Value.
    /// </summary>
    static abstract ImmutableArray<ComponentTypeInfo> TypeInfos { get; }
}
