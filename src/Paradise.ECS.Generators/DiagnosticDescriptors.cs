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

    /// <summary>
    /// PECS011: Queryable must be a ref struct.
    /// </summary>
    public static readonly DiagnosticDescriptor QueryableMustBeRefStruct = new(
        id: "PECS011",
        title: "Queryable must be ref struct",
        messageFormat: "Type '{0}' marked with [Queryable] must be a ref struct",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Queryable types must be ref structs for safe iteration over entity data.");

    /// <summary>
    /// PECS012: Queryable must be partial.
    /// </summary>
    public static readonly DiagnosticDescriptor QueryableMustBePartial = new(
        id: "PECS012",
        title: "Queryable must be partial",
        messageFormat: "Type '{0}' marked with [Queryable] must be partial",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Queryable types must be partial so the generator can implement IQueryable interface members.");

    /// <summary>
    /// PECS013: Duplicate component type in queryable attributes.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateComponentInQueryable = new(
        id: "PECS013",
        title: "Duplicate component in queryable",
        messageFormat: "Component '{0}' appears multiple times in queryable '{1}' attributes ({2})",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each component type should only appear once across With, Without, and Any attributes in a queryable.");

    /// <summary>
    /// PECS014: Duplicate manual Queryable ID.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateQueryableId = new(
        id: "PECS014",
        title: "Duplicate queryable ID",
        messageFormat: "Queryable ID {0} is used by multiple types: {1}",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each manual Queryable ID must be unique. Multiple queryables with the same ID will cause incorrect query behavior.");

    /// <summary>
    /// PECS015: Duplicate manual Component ID.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateComponentId = new(
        id: "PECS015",
        title: "Duplicate component ID",
        messageFormat: "Component ID {0} is used by multiple types: {1}",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each manual Component ID must be unique. Multiple components with the same ID will cause data corruption.");

    /// <summary>
    /// PECS016: Component is empty (zero size).
    /// </summary>
    public static readonly DiagnosticDescriptor ComponentIsEmpty = new(
        id: "PECS016",
        title: "Component is empty",
        messageFormat: "Component '{0}' has no instance fields (zero size). Consider using [Tag] instead for marker types.",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Empty components (zero size) should typically be tags. Use [Tag] for marker types that don't store data.");

    // ===== Tag-related diagnostics =====

    /// <summary>
    /// PECS020: Tag must be an unmanaged struct.
    /// </summary>
    public static readonly DiagnosticDescriptor TagNotUnmanaged = new(
        id: "PECS020",
        title: "Tag must be unmanaged",
        messageFormat: "Type '{0}' marked with [Tag] must be an unmanaged struct",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Tag types must be unmanaged structs.");

    /// <summary>
    /// PECS021: Tag must be empty (no instance fields).
    /// </summary>
    public static readonly DiagnosticDescriptor TagHasFields = new(
        id: "PECS021",
        title: "Tag must be empty",
        messageFormat: "Type '{0}' marked with [Tag] must not have instance fields. Tags are marker types only.",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Tag types must be empty marker structs with no instance fields. Use [Component] for data-carrying types.");

    /// <summary>
    /// PECS022: Invalid tag GUID format.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidTagGuidFormat = new(
        id: "PECS022",
        title: "Invalid tag GUID format",
        messageFormat: "Tag '{0}' has invalid GUID format '{1}'",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The GUID provided to [Tag] attribute must be a valid GUID format (e.g., '12345678-1234-1234-1234-123456789012').");

    /// <summary>
    /// PECS023: Tag nested in generic type.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedTagContainingType = new(
        id: "PECS023",
        title: "Tag nested in generic type",
        messageFormat: "Tag '{0}' is nested inside '{1}' which is {2}. Tags cannot be nested inside generic types.",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Tag types cannot be nested inside generic types because the source generator cannot infer type parameters.");

    /// <summary>
    /// PECS024: Tag ID exceeds maximum limit.
    /// </summary>
    public static readonly DiagnosticDescriptor TagIdExceedsLimit = new(
        id: "PECS024",
        title: "Tag ID exceeds maximum",
        messageFormat: "Tag '{0}' has ID {1} which exceeds the maximum allowed tag ID of {2}",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Tag IDs must not exceed the maximum limit.");

    /// <summary>
    /// PECS025: Too many tags.
    /// </summary>
    public static readonly DiagnosticDescriptor TooManyTags = new(
        id: "PECS025",
        title: "Too many tags",
        messageFormat: "Project has {0} tags which would result in IDs exceeding the maximum allowed tag ID of {1}",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The total number of tags must not exceed the maximum tag ID limit.");

    /// <summary>
    /// PECS026: Duplicate manual Tag ID.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateTagId = new(
        id: "PECS026",
        title: "Duplicate tag ID",
        messageFormat: "Tag ID {0} is used by multiple types: {1}",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each manual Tag ID must be unique. Multiple tags with the same ID will cause incorrect behavior.");

    // ===== System-related diagnostics =====

    /// <summary>
    /// PECS030: System must be partial.
    /// </summary>
    public static readonly DiagnosticDescriptor SystemMustBePartial = new(
        id: "PECS030",
        title: "System must be partial",
        messageFormat: "Type '{0}' marked with [System] must be partial",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "System types must be partial so the generator can implement the Run method.");

    /// <summary>
    /// PECS031: System Execute parameter is not a Queryable type.
    /// </summary>
    public static readonly DiagnosticDescriptor SystemParameterNotQueryable = new(
        id: "PECS031",
        title: "System parameter must be Queryable",
        messageFormat: "Parameter '{0}' of type '{1}' in system '{2}' must be a type marked with [Queryable]",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All Execute method parameters in a system must be Queryable types (marked with [Queryable] attribute).");

    /// <summary>
    /// PECS032: Duplicate manual System ID.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateSystemId = new(
        id: "PECS032",
        title: "Duplicate system ID",
        messageFormat: "System ID {0} is used by multiple types: {1}",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each manual System ID must be unique.");

    /// <summary>
    /// PECS033: System must have an Execute method.
    /// </summary>
    public static readonly DiagnosticDescriptor SystemMissingExecute = new(
        id: "PECS033",
        title: "System missing Execute method",
        messageFormat: "Type '{0}' marked with [System] must have a static void Execute method",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "System types must define a static void Execute method with Queryable parameters.");

    /// <summary>
    /// PECS034: Cycle detected in system dependencies.
    /// </summary>
    public static readonly DiagnosticDescriptor SystemDependencyCycle = new(
        id: "PECS034",
        title: "Cycle in system dependencies",
        messageFormat: "Cycle detected in system dependencies involving: {0}",
        category: "Paradise.ECS",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "System dependencies must form a directed acyclic graph (DAG). Cycles prevent determining execution order.");
}
