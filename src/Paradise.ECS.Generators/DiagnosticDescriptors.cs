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
    /// PECS002: Component count exceeds built-in capacity.
    /// </summary>
    public static readonly DiagnosticDescriptor ComponentCountExceedsBuiltIn = new(
        id: "PECS002",
        title: "Component count exceeds built-in capacity",
        messageFormat: "Project has {0} components which exceeds built-in Bit1024 capacity. Generating custom {1} storage type.",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The number of component types exceeds the largest built-in bit storage type (Bit1024). A custom storage type will be generated.");

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

    /// <summary>
    /// PECS005: Component nested in generic type.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedContainingType = new(
        id: "PECS005",
        title: "Component nested in generic type",
        messageFormat: "Component '{0}' is nested inside '{1}' which is {2}. Components cannot be nested inside generic types.",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Component types cannot be nested inside generic types because the source generator cannot infer type parameters.");

    /// <summary>
    /// PECS006: Component type ID exceeds maximum limit.
    /// </summary>
    public static readonly DiagnosticDescriptor ComponentIdExceedsLimit = new(
        id: "PECS006",
        title: "Component type ID exceeds maximum",
        messageFormat: "Component '{0}' has ID {1} which exceeds the maximum allowed component type ID of {2}",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Component type IDs must not exceed the maximum limit imposed by the archetype graph edge key packing (11 bits = 2047).");

    /// <summary>
    /// PECS007: Too many components - exceeds maximum component type ID.
    /// </summary>
    public static readonly DiagnosticDescriptor TooManyComponents = new(
        id: "PECS007",
        title: "Too many components",
        messageFormat: "Project has {0} components which would result in IDs exceeding the maximum allowed component type ID of {1}",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The total number of components (including manual ID assignments) must not exceed the maximum component type ID limit imposed by the archetype graph edge key packing.");

    /// <summary>
    /// PECS008: Disposable ref struct must be disposed.
    /// </summary>
    public static readonly DiagnosticDescriptor DisposableRefStructNotDisposed = new(
        id: "PECS008",
        title: "Disposable ref struct must be disposed",
        messageFormat: "Disposable ref struct '{0}' must be disposed before going out of scope. Use a 'using' statement or call Dispose() explicitly.",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Ref structs with a Dispose method manage resources that must be released. Failing to dispose them can lead to resource leaks such as unreleased chunk borrows.");

    /// <summary>
    /// PECS009: Multiple types marked as DefaultConfig.
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleDefaultConfigs = new(
        id: "PECS009",
        title: "Multiple DefaultConfig attributes",
        messageFormat: "Multiple types marked with [DefaultConfig]: {0}. Only one type is allowed.",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only one type can be marked with [DefaultConfig] per assembly.");

    /// <summary>
    /// PECS010: Type marked with DefaultConfig doesn't implement IConfig.
    /// </summary>
    public static readonly DiagnosticDescriptor DefaultConfigInvalidType = new(
        id: "PECS010",
        title: "DefaultConfig type must implement IConfig",
        messageFormat: "Type '{0}' is marked with [DefaultConfig] but does not implement IConfig",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types marked with [DefaultConfig] must implement the IConfig interface.");
}
