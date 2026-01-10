using Microsoft.CodeAnalysis;

namespace Paradise.ECS.Generators;

/// <summary>
/// Diagnostic descriptors for component-related compile-time errors.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <summary>
    /// PECS001: Component must be an unmanaged struct.
    /// </summary>
    public static readonly DiagnosticDescriptor ComponentNotUnmanaged = new(
        id: "PECS001",
        title: "Component must be unmanaged",
        messageFormat: "Type '{0}' marked with [Component] must be an unmanaged struct",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Component types must be unmanaged structs to be stored efficiently in ECS chunks.");

    /// <summary>
    /// PECS002: Component count exceeds capacity.
    /// </summary>
    public static readonly DiagnosticDescriptor ComponentCountExceedsCapacity = new(
        id: "PECS002",
        title: "Component count exceeds capacity",
        messageFormat: "Project has {0} components but uses {1} which supports only {2} components",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The number of component types exceeds the capacity of the specified bit storage type.");

    /// <summary>
    /// PECS003: Component must be a struct.
    /// </summary>
    public static readonly DiagnosticDescriptor ComponentMustBeStruct = new(
        id: "PECS003",
        title: "Component must be a struct",
        messageFormat: "Type '{0}' marked with [Component] must be a struct, not a class or record class",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Component types must be value types (structs) for efficient storage.");

    /// <summary>
    /// PECS004: Invalid GUID format.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGuidFormat = new(
        id: "PECS004",
        title: "Invalid GUID format",
        messageFormat: "Component '{0}' has invalid GUID format '{1}'",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The GUID provided to [Component] attribute must be a valid GUID format (e.g., '12345678-1234-1234-1234-123456789012').");
}
