using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Paradise.ECS;

/// <summary>
/// Defines the contract for a component registry that provides runtime Type-to-ComponentId mapping.
/// Implemented by the source-generated ComponentRegistry class.
/// </summary>
public interface IComponentRegistry
{
    /// <summary>
    /// Gets the array of all registered component type information, indexed by ComponentId.Value.
    /// </summary>
    ImmutableArray<ComponentTypeInfo> TypeInfos { get; }

    /// <summary>
    /// Gets the frozen dictionary mapping component types to their IDs.
    /// </summary>
    FrozenDictionary<Type, ComponentId> TypeToId { get; }

    /// <summary>
    /// Gets the frozen dictionary mapping component GUIDs to their IDs.
    /// </summary>
    FrozenDictionary<Guid, ComponentId> GuidToId { get; }
}
