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

        // Get type name and containing types for nested components
        var typeName = typeSymbol.Name;
        var containingTypesList = new List<ContainingTypeInfo>();
        string? invalidContainingType = null;
        string? invalidContainingTypeReason = null;
        var parent = typeSymbol.ContainingType;
        while (parent != null)
        {
            // Get the keyword for this type kind
            var keyword = parent.TypeKind switch
            {
                TypeKind.Class => parent.IsRecord ? "record class" : "class",
                TypeKind.Struct => parent.IsRecord ? "record struct" : "struct",
                TypeKind.Interface => "interface",
                _ => "struct" // Fallback, shouldn't happen
            };
            containingTypesList.Add(new ContainingTypeInfo(parent.Name, keyword));

            // Check for unsupported containing type (only generic types are rejected)
            if (invalidContainingType == null && parent.IsGenericType)
            {
                invalidContainingType = parent.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                invalidContainingTypeReason = "a generic type";
            }

            parent = parent.ContainingType;
        }
        containingTypesList.Reverse();
        var containingTypes = containingTypesList.ToImmutableArray();

        // Get optional GUID and Id from attribute (constructor arg or named arg)
        string? rawGuid = null;
        int? manualId = null;
        foreach (var attr in context.Attributes)
        {
            // Check constructor argument first for GUID
            if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string ctorGuid)
            {
                rawGuid = ctorGuid;
            }

            // Check named arguments
            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "Guid" && namedArg.Value.Value is string guidValue)
                {
                    rawGuid = guidValue;
                }
                else if (namedArg.Key == "Id" && namedArg.Value.Value is int idValue && idValue >= 0)
                {
                    manualId = idValue;
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

        // Check if struct has any instance fields
        var hasInstanceFields = typeSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(f => !f.IsStatic);
        var isEmpty = !hasInstanceFields;

        return new ComponentInfo(
            fullyQualifiedName,
            typeSymbol.Locations.FirstOrDefault() ?? Location.None,
            isUnmanaged,
            ns,
            typeName,
            containingTypes,
            validGuid,
            invalidGuid,
            invalidContainingType,
            invalidContainingTypeReason,
            isEmpty,
            manualId);
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

            if (component.InvalidContainingType != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedContainingType,
                    component.Location,
                    component.FullyQualifiedName,
                    component.InvalidContainingType,
                    component.InvalidContainingTypeReason));
            }
        }

        // Filter to only valid components for code generation
        var validComponents = sorted.Where(static c => c.IsUnmanaged && c.HasValidNesting).ToList();

        if (validComponents.Count == 0)
            return;

        // Check if component count exceeds built-in capacity (1024)
        // If so, generate a custom storage type and warn
        const int MaxBuiltInComponents = 1024;
        string? customStorageType = null;
        if (validComponents.Count > MaxBuiltInComponents)
        {
            // Calculate capacity aligned to 256 bits (4 ulongs)
            var capacity = ((validComponents.Count + 255) / 256) * 256;
            var ulongCount = capacity / 64;
            customStorageType = $"Bit{capacity}";

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ComponentCountExceedsBuiltIn,
                null, // No specific location for a project-wide issue
                validComponents.Count,
                customStorageType));

            // Generate custom storage type
            GenerateCustomStorageType(context, customStorageType, ulongCount);
        }

        // Generate partial struct implementations for IComponent (without TypeId - assigned at runtime)
        foreach (var component in validComponents)
        {
            GeneratePartialStruct(context, component);
        }

        // Generate global using aliases for the optimal bit storage type
        GenerateGlobalUsings(context, validComponents.Count);

        // Generate component registry with module initializer for runtime ID assignment
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

        // For >1024, calculate custom type name aligned to 256 bits (4 ulongs)
        var capacity = ((componentCount + 255) / 256) * 256;
        return $"Bit{capacity}";
    }

    private static void GenerateCustomStorageType(SourceProductionContext context, string typeName, int ulongCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine();
        sb.AppendLine("namespace Paradise.ECS;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Custom bit storage type generated to support {ulongCount * 64} component types.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[global::System.Runtime.CompilerServices.InlineArray({ulongCount})]");
        sb.AppendLine($"public struct {typeName} : global::Paradise.ECS.IStorage");
        sb.AppendLine("{");
        sb.AppendLine("    private ulong _element0;");
        sb.AppendLine("}");

        context.AddSource($"{typeName}.g.cs", sb.ToString());
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
        sb.AppendLine("/// Component IDs are assigned at module initialization, sorted by alignment (descending) then by name.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class ComponentRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    private static global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Paradise.ECS.ComponentId>? s_typeToId;");
        sb.AppendLine("    private static global::System.Collections.Frozen.FrozenDictionary<global::System.Guid, global::Paradise.ECS.ComponentId>? s_guidToId;");
        sb.AppendLine("    private static global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ComponentTypeInfo> s_typeInfos;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Initializes component IDs. Manual IDs are assigned first, then remaining components");
        sb.AppendLine("    /// are auto-assigned sorted by alignment (descending) then by name.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Collect all component metadata with optional manual ID");
        sb.AppendLine("        var components = new (global::System.Type Type, global::System.Guid Guid, int Size, int Alignment, int ManualId, global::System.Action<global::Paradise.ECS.ComponentId> SetId)[]");
        sb.AppendLine("        {");
        foreach (var component in components)
        {
            var guid = component.Guid != null
                ? $"new global::System.Guid(\"{component.Guid}\")"
                : "global::System.Guid.Empty";
            var manualId = component.ManualId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-1";
            sb.AppendLine($"            (typeof(global::{component.FullyQualifiedName}), {guid}, global::{component.FullyQualifiedName}.Size, global::{component.FullyQualifiedName}.Alignment, {manualId}, global::{component.FullyQualifiedName}.SetTypeId),");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        // Build lookup dictionaries and assign IDs");
        sb.AppendLine("        var typeToId = new global::System.Collections.Generic.Dictionary<global::System.Type, global::Paradise.ECS.ComponentId>(components.Length);");
        sb.AppendLine("        var guidToId = new global::System.Collections.Generic.Dictionary<global::System.Guid, global::Paradise.ECS.ComponentId>();");
        sb.AppendLine("        var usedIds = new global::System.Collections.Generic.HashSet<int>();");
        sb.AppendLine();
        sb.AppendLine("        // First pass: assign manual IDs");
        sb.AppendLine("        foreach (var comp in components)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (comp.ManualId >= 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                var id = new global::Paradise.ECS.ComponentId(comp.ManualId);");
        sb.AppendLine("                comp.SetId(id);");
        sb.AppendLine("                typeToId[comp.Type] = id;");
        sb.AppendLine("                if (comp.Guid != global::System.Guid.Empty)");
        sb.AppendLine("                    guidToId[comp.Guid] = id;");
        sb.AppendLine("                usedIds.Add(comp.ManualId);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Get auto-assign components and sort by alignment descending, then by type name");
        sb.AppendLine("        var autoComponents = global::System.Linq.Enumerable.ToList(");
        sb.AppendLine("            global::System.Linq.Enumerable.Where(components, c => c.ManualId < 0));");
        sb.AppendLine("        autoComponents.Sort((a, b) =>");
        sb.AppendLine("        {");
        sb.AppendLine("            int cmp = b.Alignment.CompareTo(a.Alignment);");
        sb.AppendLine("            return cmp != 0 ? cmp : global::System.StringComparer.Ordinal.Compare(a.Type.FullName, b.Type.FullName);");
        sb.AppendLine("        });");
        sb.AppendLine();
        sb.AppendLine("        // Second pass: auto-assign IDs to remaining components");
        sb.AppendLine("        int nextId = 0;");
        sb.AppendLine("        foreach (var comp in autoComponents)");
        sb.AppendLine("        {");
        sb.AppendLine("            while (usedIds.Contains(nextId)) nextId++;");
        sb.AppendLine("            var id = new global::Paradise.ECS.ComponentId(nextId);");
        sb.AppendLine("            comp.SetId(id);");
        sb.AppendLine("            typeToId[comp.Type] = id;");
        sb.AppendLine("            if (comp.Guid != global::System.Guid.Empty)");
        sb.AppendLine("                guidToId[comp.Guid] = id;");
        sb.AppendLine("            usedIds.Add(nextId);");
        sb.AppendLine("            nextId++;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Build TypeInfos array sorted by ID");
        sb.AppendLine("        int maxId = global::System.Linq.Enumerable.Max(usedIds);");
        sb.AppendLine("        var typeInfosBuilder = global::System.Collections.Immutable.ImmutableArray.CreateBuilder<global::Paradise.ECS.ComponentTypeInfo>(maxId + 1);");
        sb.AppendLine("        for (int i = 0; i <= maxId; i++)");
        sb.AppendLine("            typeInfosBuilder.Add(default);");
        sb.AppendLine("        foreach (var comp in components)");
        sb.AppendLine("        {");
        sb.AppendLine("            var id = typeToId[comp.Type];");
        sb.AppendLine("            typeInfosBuilder[id.Value] = new global::Paradise.ECS.ComponentTypeInfo(id, comp.Size, comp.Alignment);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        s_typeToId = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(typeToId);");
        sb.AppendLine("        s_guidToId = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(guidToId);");
        sb.AppendLine("        s_typeInfos = typeInfosBuilder.MoveToImmutable();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the ComponentId for a given Type.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"type\">The component type.</param>");
        sb.AppendLine("    /// <returns>The ComponentId, or <see cref=\"ComponentId.Invalid\"/> if not found.</returns>");
        sb.AppendLine("    public static global::Paradise.ECS.ComponentId GetId(global::System.Type type)");
        sb.AppendLine("    {");
        sb.AppendLine("        return s_typeToId!.TryGetValue(type, out var id) ? id : global::Paradise.ECS.ComponentId.Invalid;");
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
        sb.AppendLine("        return s_typeToId!.TryGetValue(type, out id);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the ComponentId for a given GUID.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"guid\">The component GUID.</param>");
        sb.AppendLine("    /// <returns>The ComponentId, or <see cref=\"ComponentId.Invalid\"/> if not found.</returns>");
        sb.AppendLine("    public static global::Paradise.ECS.ComponentId GetId(global::System.Guid guid)");
        sb.AppendLine("    {");
        sb.AppendLine("        return s_guidToId!.TryGetValue(guid, out var id) ? id : global::Paradise.ECS.ComponentId.Invalid;");
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
        sb.AppendLine("        return s_guidToId!.TryGetValue(guid, out id);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the immutable array of component type information for all registered components.");
        sb.AppendLine("    /// Indexed by ComponentId.Value for O(1) lookup. Sorted by alignment (descending) then by name.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ComponentTypeInfo> TypeInfos => s_typeInfos;");
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
        ComponentInfo component)
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
            sb.AppendLine($"{indent}partial {containingType.Keyword} {containingType.Name}");
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
        sb.AppendLine($"{indent}    /// <summary>The unique component type ID assigned at module initialization (sorted by alignment).</summary>");
        sb.AppendLine($"{indent}    public static global::Paradise.ECS.ComponentId TypeId {{ get; internal set; }} = global::Paradise.ECS.ComponentId.Invalid;");
        sb.AppendLine();
        sb.AppendLine($"{indent}    internal static void SetTypeId(global::Paradise.ECS.ComponentId id) => TypeId = id;");
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
        if (component.IsEmpty)
        {
            sb.AppendLine($"{indent}    public static int Size => 0;");
        }
        else
        {
            sb.AppendLine($"{indent}    public static int Size {{ get; }} = global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::{component.FullyQualifiedName}>();");
        }
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>The alignment of this component in bytes.</summary>");
        if (component.IsEmpty)
        {
            sb.AppendLine($"{indent}    public static int Alignment => 0;");
        }
        else
        {
            sb.AppendLine($"{indent}    public static int Alignment {{ get; }} = global::Paradise.ECS.AlignmentHelper<global::{component.FullyQualifiedName}>.Alignment;");
        }
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
        public ImmutableArray<ContainingTypeInfo> ContainingTypes { get; }
        public string? Guid { get; }
        public string? InvalidGuid { get; }
        public string? InvalidContainingType { get; }
        public string? InvalidContainingTypeReason { get; }
        public bool IsEmpty { get; }
        public int? ManualId { get; }

        public bool HasValidNesting => InvalidContainingType == null;

        public ComponentInfo(
            string fullyQualifiedName,
            Location location,
            bool isUnmanaged,
            string? ns,
            string typeName,
            ImmutableArray<ContainingTypeInfo> containingTypes,
            string? guid,
            string? invalidGuid,
            string? invalidContainingType,
            string? invalidContainingTypeReason,
            bool isEmpty,
            int? manualId)
        {
            FullyQualifiedName = fullyQualifiedName;
            Location = location;
            IsUnmanaged = isUnmanaged;
            Namespace = ns;
            TypeName = typeName;
            ContainingTypes = containingTypes;
            Guid = guid;
            InvalidGuid = invalidGuid;
            InvalidContainingType = invalidContainingType;
            InvalidContainingTypeReason = invalidContainingTypeReason;
            IsEmpty = isEmpty;
            ManualId = manualId;
        }
    }

    /// <summary>
    /// Information about a containing type for nested components.
    /// </summary>
    private readonly struct ContainingTypeInfo
    {
        public string Name { get; }
        public string Keyword { get; }

        public ContainingTypeInfo(string name, string keyword)
        {
            Name = name;
            Keyword = keyword;
        }
    }
}
