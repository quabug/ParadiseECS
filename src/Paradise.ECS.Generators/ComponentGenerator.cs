using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paradise.ECS.Generators;

/// <summary>
/// Source generator that assigns component IDs to types marked with [Component].
/// IDs are assigned based on alphabetical ordering of fully qualified type names.
/// Also generates partial struct implementations of IComponent.
/// </summary>
[Generator]
public class ComponentGenerator : IIncrementalGenerator
{
    private const string ComponentAttributeFullName = "Paradise.ECS.ComponentAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all structs with [Component] attribute
        var componentTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ComponentAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => GetComponentInfo(ctx))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!.Value);

        // Collect all components and generate the registry
        var collected = componentTypes.Collect();

        context.RegisterSourceOutput(collected, GenerateComponentCode);
    }

    private static ComponentInfo? GetComponentInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        // Verify it's a struct
        if (typeSymbol.TypeKind != TypeKind.Struct)
            return null;

        // Get fully qualified name for sorting
        var fullyQualifiedName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Remove "global::" prefix if present for cleaner output
        if (fullyQualifiedName.StartsWith("global::", StringComparison.Ordinal))
            fullyQualifiedName = fullyQualifiedName.Substring(8);

        // Check if the type is unmanaged
        var isUnmanaged = typeSymbol.IsUnmanagedType;

        // Get namespace
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        // Get type name and containing types for nested structs
        var typeName = typeSymbol.Name;
        var containingTypesList = new List<string>();
        var parent = typeSymbol.ContainingType;
        while (parent != null)
        {
            containingTypesList.Add(parent.Name);
            parent = parent.ContainingType;
        }
        containingTypesList.Reverse();
        var containingTypes = containingTypesList.ToImmutableArray();

        // Get optional GUID from attribute (constructor arg or named arg)
        string? rawGuid = null;
        foreach (var attr in context.Attributes)
        {
            // Check constructor argument first
            if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string ctorGuid)
            {
                rawGuid = ctorGuid;
                break;
            }

            // Check named argument
            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "Guid" && namedArg.Value.Value is string guidValue)
                {
                    rawGuid = guidValue;
                    break;
                }
            }
        }

        // Validate GUID format if provided
        string? validGuid = null;
        string? invalidGuid = null;
        if (rawGuid != null)
        {
            if (Guid.TryParse(rawGuid, out _))
            {
                validGuid = rawGuid;
            }
            else
            {
                invalidGuid = rawGuid;
            }
        }

        return new ComponentInfo(
            fullyQualifiedName,
            typeSymbol.Locations.FirstOrDefault() ?? Location.None,
            isUnmanaged,
            ns,
            typeName,
            containingTypes,
            validGuid,
            invalidGuid);
    }

    private static void GenerateComponentCode(
        SourceProductionContext context,
        ImmutableArray<ComponentInfo> components)
    {
        if (components.IsEmpty)
            return;

        // Sort by fully qualified name for deterministic ID assignment
        var sorted = components
            .OrderBy(static c => c.FullyQualifiedName, StringComparer.Ordinal)
            .ToList();

        // Report diagnostics for invalid components
        foreach (var component in sorted)
        {
            if (!component.IsUnmanaged)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ComponentNotUnmanaged,
                    component.Location,
                    component.FullyQualifiedName));
            }

            if (component.InvalidGuid != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidGuidFormat,
                    component.Location,
                    component.FullyQualifiedName,
                    component.InvalidGuid));
            }
        }

        // Filter to only valid (unmanaged) components for code generation
        var validComponents = sorted.Where(static c => c.IsUnmanaged).ToList();

        if (validComponents.Count == 0)
            return;

        // Check if component count exceeds maximum capacity
        const int MaxComponents = 2048; // Corresponds to Bit2048, the largest supported storage
        if (validComponents.Count > MaxComponents)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ComponentCountExceedsCapacity,
                null, // No specific location for a project-wide issue
                validComponents.Count,
                "Bit2048",
                MaxComponents));
            return; // Stop generation if over capacity
        }

        // Generate partial struct implementations for IComponent
        for (int i = 0; i < validComponents.Count; i++)
        {
            GeneratePartialStruct(context, validComponents[i], typeId: i);
        }

        // Generate global using aliases for the optimal bit storage type
        GenerateGlobalUsings(context, validComponents.Count);

        // Generate component registry for Type-based lookups
        GenerateComponentRegistry(context, validComponents);
    }

    private static string GetOptimalBitStorageType(int componentCount)
    {
        // Select the smallest bit storage type that can hold all components
        if (componentCount <= 64) return "Bit64";
        if (componentCount <= 128) return "Bit128";
        if (componentCount <= 256) return "Bit256";
        if (componentCount <= 512) return "Bit512";
        if (componentCount <= 1024) return "Bit1024";
        return "Bit2048";
    }

    private static void GenerateGlobalUsings(SourceProductionContext context, int componentCount)
    {
        var bitType = GetOptimalBitStorageType(componentCount);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine($"// Component count: {componentCount} → using {bitType}");
        sb.AppendLine();
        sb.AppendLine($"global using Archetype = global::Paradise.ECS.Archetype<global::Paradise.ECS.{bitType}>;");
        sb.AppendLine($"global using ComponentMask = global::Paradise.ECS.ImmutableBitSet<global::Paradise.ECS.{bitType}>;");

        context.AddSource("ComponentAliases.g.cs", sb.ToString());
    }

    private static void GenerateComponentRegistry(SourceProductionContext context, List<ComponentInfo> components)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Paradise.ECS;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Provides runtime Type-to-ComponentId mapping for all registered components.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class ComponentRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Paradise.ECS.ComponentId> s_typeToId =");
        sb.AppendLine("        global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(");
        sb.AppendLine("            new global::System.Collections.Generic.Dictionary<global::System.Type, global::Paradise.ECS.ComponentId>()");
        sb.AppendLine("            {");
        for (int i = 0; i < components.Count; i++)
        {
            var component = components[i];
            sb.AppendLine($"                [typeof(global::{component.FullyQualifiedName})] = new global::Paradise.ECS.ComponentId({i}),");
        }
        sb.AppendLine("            });");
        sb.AppendLine();
        sb.AppendLine("    private static readonly global::System.Collections.Frozen.FrozenDictionary<global::System.Guid, global::Paradise.ECS.ComponentId> s_guidToId =");
        sb.AppendLine("        global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(");
        sb.AppendLine("            new global::System.Collections.Generic.Dictionary<global::System.Guid, global::Paradise.ECS.ComponentId>()");
        sb.AppendLine("            {");
        for (int i = 0; i < components.Count; i++)
        {
            var component = components[i];
            if (component.Guid != null)
            {
                sb.AppendLine($"                [new global::System.Guid(\"{component.Guid}\")] = new global::Paradise.ECS.ComponentId({i}),");
            }
        }
        sb.AppendLine("            });");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the ComponentId for a given Type.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"type\">The component type.</param>");
        sb.AppendLine("    /// <returns>The ComponentId, or <see cref=\"ComponentId.Invalid\"/> if not found.</returns>");
        sb.AppendLine("    public static global::Paradise.ECS.ComponentId GetId(global::System.Type type)");
        sb.AppendLine("    {");
        sb.AppendLine("        return s_typeToId.TryGetValue(type, out var id) ? id : global::Paradise.ECS.ComponentId.Invalid;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Tries to get the ComponentId for a given Type.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"type\">The component type.</param>");
        sb.AppendLine("    /// <param name=\"id\">The ComponentId if found.</param>");
        sb.AppendLine("    /// <returns><c>true</c> if the type was found; otherwise, <c>false</c>.</returns>");
        sb.AppendLine("    public static bool TryGetId(global::System.Type type, out global::Paradise.ECS.ComponentId id)");
        sb.AppendLine("    {");
        sb.AppendLine("        return s_typeToId.TryGetValue(type, out id);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the ComponentId for a given GUID.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"guid\">The component GUID.</param>");
        sb.AppendLine("    /// <returns>The ComponentId, or <see cref=\"ComponentId.Invalid\"/> if not found.</returns>");
        sb.AppendLine("    public static global::Paradise.ECS.ComponentId GetId(global::System.Guid guid)");
        sb.AppendLine("    {");
        sb.AppendLine("        return s_guidToId.TryGetValue(guid, out var id) ? id : global::Paradise.ECS.ComponentId.Invalid;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Tries to get the ComponentId for a given GUID.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"guid\">The component GUID.</param>");
        sb.AppendLine("    /// <param name=\"id\">The ComponentId if found.</param>");
        sb.AppendLine("    /// <returns><c>true</c> if the GUID was found; otherwise, <c>false</c>.</returns>");
        sb.AppendLine("    public static bool TryGetId(global::System.Guid guid, out global::Paradise.ECS.ComponentId id)");
        sb.AppendLine("    {");
        sb.AppendLine("        return s_guidToId.TryGetValue(guid, out id);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Extension methods for Archetype to support Type-based operations.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class ArchetypeTypeExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Returns a new archetype with the specified component type added.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <typeparam name=\"TBits\">The bit storage type.</typeparam>");
        sb.AppendLine("    /// <param name=\"archetype\">The archetype to extend.</param>");
        sb.AppendLine("    /// <param name=\"type\">The component type to add.</param>");
        sb.AppendLine("    /// <returns>A new archetype containing the component.</returns>");
        sb.AppendLine("    /// <exception cref=\"global::System.ArgumentException\">Thrown if the type is not a registered component.</exception>");
        sb.AppendLine("    public static global::Paradise.ECS.Archetype<TBits> With<TBits>(this global::Paradise.ECS.Archetype<TBits> archetype, global::System.Type type)");
        sb.AppendLine("        where TBits : unmanaged, global::Paradise.ECS.IStorage");
        sb.AppendLine("    {");
        sb.AppendLine("        var id = global::Paradise.ECS.ComponentRegistry.GetId(type);");
        sb.AppendLine("        if (!id.IsValid)");
        sb.AppendLine("            throw new global::System.ArgumentException($\"Type '{type}' is not a registered component.\", nameof(type));");
        sb.AppendLine("        return archetype.With(id);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Returns a new archetype with the specified component type removed.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <typeparam name=\"TBits\">The bit storage type.</typeparam>");
        sb.AppendLine("    /// <param name=\"archetype\">The archetype to extend.</param>");
        sb.AppendLine("    /// <param name=\"type\">The component type to remove.</param>");
        sb.AppendLine("    /// <returns>A new archetype without the component.</returns>");
        sb.AppendLine("    /// <exception cref=\"global::System.ArgumentException\">Thrown if the type is not a registered component.</exception>");
        sb.AppendLine("    public static global::Paradise.ECS.Archetype<TBits> Without<TBits>(this global::Paradise.ECS.Archetype<TBits> archetype, global::System.Type type)");
        sb.AppendLine("        where TBits : unmanaged, global::Paradise.ECS.IStorage");
        sb.AppendLine("    {");
        sb.AppendLine("        var id = global::Paradise.ECS.ComponentRegistry.GetId(type);");
        sb.AppendLine("        if (!id.IsValid)");
        sb.AppendLine("            throw new global::System.ArgumentException($\"Type '{type}' is not a registered component.\", nameof(type));");
        sb.AppendLine("        return archetype.Without(id);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Checks if this archetype contains the specified component type.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <typeparam name=\"TBits\">The bit storage type.</typeparam>");
        sb.AppendLine("    /// <param name=\"archetype\">The archetype to check.</param>");
        sb.AppendLine("    /// <param name=\"type\">The component type to check.</param>");
        sb.AppendLine("    /// <returns><c>true</c> if the archetype contains the component; otherwise, <c>false</c>.</returns>");
        sb.AppendLine("    public static bool Has<TBits>(this global::Paradise.ECS.Archetype<TBits> archetype, global::System.Type type)");
        sb.AppendLine("        where TBits : unmanaged, global::Paradise.ECS.IStorage");
        sb.AppendLine("    {");
        sb.AppendLine("        var id = global::Paradise.ECS.ComponentRegistry.GetId(type);");
        sb.AppendLine("        return id.IsValid && archetype.Has(id);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("ComponentRegistry.g.cs", sb.ToString());
    }

    private static void GeneratePartialStruct(
        SourceProductionContext context,
        ComponentInfo component,
        int typeId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Open namespace if present
        if (component.Namespace != null)
        {
            sb.AppendLine($"namespace {component.Namespace};");
            sb.AppendLine();
        }

        // Open containing types if nested
        var indent = "";
        foreach (var containingType in component.ContainingTypes)
        {
            sb.AppendLine($"{indent}partial struct {containingType}");
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        // Generate the partial struct implementing IComponent
        if (component.Guid != null)
        {
            sb.AppendLine($"{indent}[global::System.Runtime.InteropServices.Guid(\"{component.Guid}\")]");
        }
        sb.AppendLine($"{indent}partial struct {component.TypeName} : global::Paradise.ECS.IComponent");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    /// <summary>The unique component type ID assigned at compile time.</summary>");
        sb.AppendLine($"{indent}    public static global::Paradise.ECS.ComponentId TypeId => new global::Paradise.ECS.ComponentId({typeId});");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>The stable GUID for this component type.</summary>");
        if (component.Guid != null)
        {
            sb.AppendLine($"{indent}    public static global::System.Guid Guid {{ get; }} = new global::System.Guid(\"{component.Guid}\");");
        }
        else
        {
            sb.AppendLine($"{indent}    public static global::System.Guid Guid => global::System.Guid.Empty;");
        }
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>The size of this component in bytes.</summary>");
        sb.AppendLine($"{indent}    public static int Size {{ get; }} = global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::{component.FullyQualifiedName}>();");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>The alignment of this component in bytes.</summary>");
        sb.AppendLine($"{indent}    public static int Alignment {{ get; }} = global::Paradise.ECS.AlignmentHelper<global::{component.FullyQualifiedName}>.Alignment;");
        sb.AppendLine($"{indent}}}");

        // Close containing types
        for (int i = component.ContainingTypes.Length - 1; i >= 0; i--)
        {
            indent = new string(' ', i * 4);
            sb.AppendLine($"{indent}}}");
        }

        // Generate a unique filename based on fully qualified name
        var filename = component.FullyQualifiedName.Replace(".", "_").Replace("+", "_") + ".g.cs";
        context.AddSource(filename, sb.ToString());
    }

    private readonly struct ComponentInfo
    {
        public string FullyQualifiedName { get; }
        public Location Location { get; }
        public bool IsUnmanaged { get; }
        public string? Namespace { get; }
        public string TypeName { get; }
        public ImmutableArray<string> ContainingTypes { get; }
        public string? Guid { get; }
        public string? InvalidGuid { get; }

        public ComponentInfo(
            string fullyQualifiedName,
            Location location,
            bool isUnmanaged,
            string? ns,
            string typeName,
            ImmutableArray<string> containingTypes,
            string? guid,
            string? invalidGuid)
        {
            FullyQualifiedName = fullyQualifiedName;
            Location = location;
            IsUnmanaged = isUnmanaged;
            Namespace = ns;
            TypeName = typeName;
            ContainingTypes = containingTypes;
            Guid = guid;
            InvalidGuid = invalidGuid;
        }
    }
}
