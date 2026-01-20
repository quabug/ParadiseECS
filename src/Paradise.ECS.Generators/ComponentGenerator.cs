using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paradise.ECS.Generators;

/// <summary>
/// Source generator that assigns component IDs to types marked with [Component]
/// and tag IDs to types marked with [Tag].
/// Also generates EntityTags component for tag storage.
/// </summary>
[Generator]
public class ComponentGenerator : IIncrementalGenerator
{
    private const string ComponentAttributeFullName = "Paradise.ECS.ComponentAttribute";
    private const string TagAttributeFullName = "Paradise.ECS.TagAttribute";
    private const string RegistryNamespaceAttributeFullName = "Paradise.ECS.ComponentRegistryNamespaceAttribute";
    private const string DefaultConfigAttributeFullName = "Paradise.ECS.DefaultConfigAttribute";
    private const string SuppressGlobalUsingsAttributeFullName = "Paradise.ECS.SuppressGlobalUsingsAttribute";
    private const string EdgeKeyFullName = "Paradise.ECS.EdgeKey";
    private const string IConfigFullName = "Paradise.ECS.IConfig";

    // Default fallback value if EdgeKey is not found (11 bits = 2047)
    private const int DefaultMaxComponentTypeId = (1 << 11) - 1;
    private const int DefaultMaxTagId = (1 << 11) - 1;

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

        // Find all structs with [Tag] attribute
        var tagTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                TagAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => GetTagInfo(ctx))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!.Value);

        // Get root namespace from assembly attribute first, then build properties, then default
        var rootNamespaceFromAttribute = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                var attr = compilation.Assembly.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == RegistryNamespaceAttributeFullName);
                if (attr?.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string ns)
                    return ns;
                return null;
            });

        var rootNamespaceFromBuildProperty = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var ns);
                return ns;
            });

        // Combine both sources: prefer attribute, fallback to build property, then default
        var rootNamespace = rootNamespaceFromAttribute
            .Combine(rootNamespaceFromBuildProperty)
            .Select(static (pair, _) => pair.Left ?? pair.Right ?? "Paradise.ECS");

        // Get maxComponentTypeId from EdgeKey class
        var maxComponentTypeId = context.CompilationProvider
            .Select((compilation, _) =>
            {
                var edgeKeyType = compilation.GetTypeByMetadataName(EdgeKeyFullName);
                if (edgeKeyType != null)
                {
                    var field = edgeKeyType.GetMembers("MaxComponentTypeId")
                        .OfType<IFieldSymbol>()
                        .FirstOrDefault();
                    if (field is { HasConstantValue: true, ConstantValue: int value })
                        return value;
                }
                return DefaultMaxComponentTypeId;
            });

        // Find all types with [DefaultConfig] attribute
        var defaultConfigTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                DefaultConfigAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax or ClassDeclarationSyntax,
                transform: static (ctx, _) => GetDefaultConfigInfo(ctx))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!.Value)
            .Collect();

        // Check for [assembly: SuppressGlobalUsings] attribute
        var suppressGlobalUsings = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                return compilation.Assembly.GetAttributes()
                    .Any(a => a.AttributeClass?.ToDisplayString() == SuppressGlobalUsingsAttributeFullName);
            });

        // Collect all components and tags
        var collectedComponents = componentTypes.Collect();
        var collectedTags = tagTypes.Collect();

        // Combine everything
        var combined = collectedComponents
            .Combine(collectedTags)
            .Combine(rootNamespace)
            .Combine(maxComponentTypeId)
            .Combine(defaultConfigTypes)
            .Combine(suppressGlobalUsings);

        context.RegisterSourceOutput(combined, static (ctx, data) =>
            GenerateCode(ctx,
                data.Left.Left.Left.Left.Left,  // components
                data.Left.Left.Left.Left.Right, // tags
                data.Left.Left.Left.Right,      // rootNamespace
                data.Left.Left.Right,           // maxComponentTypeId
                data.Left.Right,                // defaultConfigs
                data.Right));                   // suppressGlobalUsings
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

        // Check if this is EntityTags (special handling - will be populated by tag generator)
        var isEntityTags = typeName == "EntityTags";

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
            manualId,
            isEntityTags);
    }

    private static TagInfo? GetTagInfo(GeneratorAttributeSyntaxContext context)
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

        // Get type name and containing types for nested tags
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

        // Get optional GUID and Id from attribute
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

        // Check if struct has any instance fields - tags must be empty
        var hasInstanceFields = typeSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Any(f => !f.IsStatic);

        return new TagInfo(
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
            hasInstanceFields,
            manualId);
    }

    private static DefaultConfigInfo? GetDefaultConfigInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var fullyQualifiedName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fullyQualifiedName.StartsWith("global::", StringComparison.Ordinal))
            fullyQualifiedName = fullyQualifiedName.Substring(8);

        // Verify the type implements IConfig using SymbolEqualityComparer (more robust than string comparison)
        var iConfigInterface = context.SemanticModel.Compilation.GetTypeByMetadataName(IConfigFullName);
        var implementsIConfig = iConfigInterface is not null &&
            typeSymbol.AllInterfaces.Contains(iConfigInterface, SymbolEqualityComparer.Default);

        return new DefaultConfigInfo(
            fullyQualifiedName,
            typeSymbol.Locations.FirstOrDefault() ?? Location.None,
            implementsIConfig);
    }

    private static void GenerateCode(
        SourceProductionContext context,
        ImmutableArray<ComponentInfo> components,
        ImmutableArray<TagInfo> tags,
        string rootNamespace,
        int maxComponentTypeId,
        ImmutableArray<DefaultConfigInfo> defaultConfigs,
        bool suppressGlobalUsings)
    {
        // Validate DefaultConfig types (diagnostics only - fallback applied later if needed)
        var configType = ValidateDefaultConfig(context, defaultConfigs);

        // Process tags first to determine TagMask type
        var (validTags, tagMaskType, tagMaskBitsType) = ProcessTags(context, tags, rootNamespace);

        // Check if EntityTags component exists (user-defined with [Component])
        var entityTagsComponent = components.FirstOrDefault(c => c.IsEntityTags);
        var hasEntityTags = entityTagsComponent.FullyQualifiedName != null;

        // Process components
        var validComponents = ProcessComponents(context, components, maxComponentTypeId, hasEntityTags, tagMaskType, rootNamespace);

        // Generate tag code
        if (validTags.Count > 0)
        {
            // Generate tag partial structs
            foreach (var tag in validTags)
            {
                GenerateTagPartialStruct(context, tag);
            }

            // Generate tag registry
            GenerateTagRegistry(context, validTags, rootNamespace);

            // Generate tag aliases
            GenerateTagAliases(context, validTags.Count, tagMaskType, tagMaskBitsType);
        }

        // Generate component code
        if (validComponents.Count > 0)
        {
            // Check if component count exceeds built-in capacity (1024)
            const int MaxBuiltInComponents = 1024;
            string? customStorageType = null;
            if (validComponents.Count > MaxBuiltInComponents)
            {
                var capacity = ((validComponents.Count + 255) / 256) * 256;
                var ulongCount = capacity / 64;
                customStorageType = $"Bit{capacity}";

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ComponentCountExceedsBuiltIn,
                    null,
                    validComponents.Count,
                    customStorageType));

                GenerateCustomStorageType(context, customStorageType, ulongCount);
            }

            // Generate partial struct implementations for IComponent
            foreach (var component in validComponents)
            {
                GenerateComponentPartialStruct(context, component, tagMaskType);
            }

            // Generate global using aliases
            GenerateGlobalUsings(context, validComponents.Count, rootNamespace, configType, suppressGlobalUsings);

            // Generate component registry
            GenerateComponentRegistry(context, validComponents, rootNamespace, tagMaskType);
        }
    }

    private static (List<TagInfo> ValidTags, string TagMaskType, string? TagMaskBitsType) ProcessTags(
        SourceProductionContext context,
        ImmutableArray<TagInfo> tags,
        string _)
    {
        if (tags.IsEmpty)
            return (new List<TagInfo>(), "global::Paradise.ECS.ImmutableBitSet32", null);

        // Sort by fully qualified name for deterministic ID assignment
        var sorted = tags.OrderBy(static t => t.FullyQualifiedName, StringComparer.Ordinal).ToList();

        // Report diagnostics for invalid tags
        foreach (var tag in sorted)
        {
            if (!tag.IsUnmanaged)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TagNotUnmanaged,
                    tag.Location,
                    tag.FullyQualifiedName));
            }

            if (tag.HasInstanceFields)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TagHasFields,
                    tag.Location,
                    tag.FullyQualifiedName));
            }

            if (tag.InvalidGuid != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidTagGuidFormat,
                    tag.Location,
                    tag.FullyQualifiedName,
                    tag.InvalidGuid));
            }

            if (tag.InvalidContainingType != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.UnsupportedTagContainingType,
                    tag.Location,
                    tag.FullyQualifiedName,
                    tag.InvalidContainingType,
                    tag.InvalidContainingTypeReason));
            }

            if (tag.ManualId.HasValue && tag.ManualId.Value > DefaultMaxTagId)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TagIdExceedsLimit,
                    tag.Location,
                    tag.FullyQualifiedName,
                    tag.ManualId.Value,
                    DefaultMaxTagId));
            }
        }

        // Detect duplicate manual IDs
        var manualIdGroups = sorted
            .Where(t => t.ManualId.HasValue)
            .GroupBy(t => t.ManualId!.Value)
            .Where(g => g.Count() > 1)
            .ToList();

        var duplicateManualIds = new HashSet<int>();
        foreach (var group in manualIdGroups)
        {
            duplicateManualIds.Add(group.Key);
            var typeNames = string.Join(", ", group.Select(t => t.FullyQualifiedName));
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DuplicateTagId,
                group.First().Location,
                group.Key,
                typeNames));
        }

        // Filter to only valid tags
        var validTags = sorted.Where(t =>
            t.IsUnmanaged &&
            !t.HasInstanceFields &&
            t.HasValidNesting &&
            (!t.ManualId.HasValue || t.ManualId.Value <= DefaultMaxTagId) &&
            (!t.ManualId.HasValue || !duplicateManualIds.Contains(t.ManualId.Value))).ToList();

        // Determine TagMask type based on tag count
        string tagMaskType;
        string? tagMaskBitsType = null;
        if (validTags.Count <= 32)
        {
            tagMaskType = "global::Paradise.ECS.ImmutableBitSet32";
        }
        else
        {
            string bitType;
            if (validTags.Count <= 64) bitType = "Bit64";
            else if (validTags.Count <= 128) bitType = "Bit128";
            else if (validTags.Count <= 256) bitType = "Bit256";
            else if (validTags.Count <= 512) bitType = "Bit512";
            else if (validTags.Count <= 1024) bitType = "Bit1024";
            else
            {
                var capacity = ((validTags.Count + 255) / 256) * 256;
                bitType = $"Bit{capacity}";
            }

            tagMaskBitsType = bitType;
            tagMaskType = $"global::Paradise.ECS.ImmutableBitSet<global::Paradise.ECS.{bitType}>";
        }

        return (validTags, tagMaskType, tagMaskBitsType);
    }

    private static List<ComponentInfo> ProcessComponents(
        SourceProductionContext context,
        ImmutableArray<ComponentInfo> components,
        int maxComponentTypeId,
        bool _1,
        string _2,
        string _3)
    {
        if (components.IsEmpty)
            return new List<ComponentInfo>();

        // Sort by fully qualified name for deterministic ID assignment
        var sorted = components.OrderBy(static c => c.FullyQualifiedName, StringComparer.Ordinal).ToList();

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

            // Warn about empty components - but NOT EntityTags (it will have generated content)
            if (component.IsEmpty && !component.IsEntityTags)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ComponentIsEmpty,
                    component.Location,
                    component.FullyQualifiedName));
            }

            if (component.ManualId.HasValue && component.ManualId.Value > maxComponentTypeId)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ComponentIdExceedsLimit,
                    component.Location,
                    component.FullyQualifiedName,
                    component.ManualId.Value,
                    maxComponentTypeId));
            }
        }

        // Detect duplicate manual IDs
        var manualIdGroups = sorted
            .Where(c => c.ManualId.HasValue)
            .GroupBy(c => c.ManualId!.Value)
            .Where(g => g.Count() > 1)
            .ToList();

        var duplicateManualIds = new HashSet<int>();
        foreach (var group in manualIdGroups)
        {
            duplicateManualIds.Add(group.Key);
            var typeNames = string.Join(", ", group.Select(c => c.FullyQualifiedName));
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DuplicateComponentId,
                group.First().Location,
                group.Key,
                typeNames));
        }

        // Filter to only valid components
        var validComponents = sorted.Where(c =>
            c.IsUnmanaged &&
            c.HasValidNesting &&
            (!c.ManualId.HasValue || c.ManualId.Value <= maxComponentTypeId) &&
            (!c.ManualId.HasValue || !duplicateManualIds.Contains(c.ManualId.Value))).ToList();

        // Calculate max ID
        var manualIds = new HashSet<int>(validComponents
            .Where(c => c.ManualId.HasValue)
            .Select(c => c.ManualId!.Value));
        var autoAssignCount = validComponents.Count - manualIds.Count;

        int maxAssignedId = manualIds.Count > 0 ? manualIds.Max() : -1;
        int nextAutoId = 0;
        for (int i = 0; i < autoAssignCount; i++)
        {
            while (manualIds.Contains(nextAutoId)) nextAutoId++;
            if (nextAutoId > maxAssignedId) maxAssignedId = nextAutoId;
            nextAutoId++;
        }

        if (maxAssignedId > maxComponentTypeId)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TooManyComponents,
                null,
                validComponents.Count,
                maxComponentTypeId));
            return new List<ComponentInfo>();
        }

        return validComponents;
    }

    private static void GenerateTagPartialStruct(SourceProductionContext context, TagInfo tag)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CA1708 // Identifiers should differ by more than case");
        sb.AppendLine();

        if (tag.Namespace != null)
        {
            sb.AppendLine($"namespace {tag.Namespace};");
            sb.AppendLine();
        }

        var indent = "";
        foreach (var containingType in tag.ContainingTypes)
        {
            sb.AppendLine($"{indent}partial {containingType.Keyword} {containingType.Name}");
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        if (tag.Guid != null)
        {
            sb.AppendLine($"{indent}[global::System.Runtime.InteropServices.Guid(\"{tag.Guid}\")]");
        }
        sb.AppendLine($"{indent}partial struct {tag.TypeName} : global::Paradise.ECS.ITag");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    /// <summary>The unique tag type ID assigned at module initialization.</summary>");
        sb.AppendLine($"{indent}    public static global::Paradise.ECS.TagId TagId {{ get; internal set; }} = global::Paradise.ECS.TagId.Invalid;");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>The stable GUID for this tag type.</summary>");
        if (tag.Guid != null)
        {
            sb.AppendLine($"{indent}    public static global::System.Guid Guid {{ get; }} = new global::System.Guid(\"{tag.Guid}\");");
        }
        else
        {
            sb.AppendLine($"{indent}    public static global::System.Guid Guid => global::System.Guid.Empty;");
        }
        sb.AppendLine($"{indent}}}");

        for (int i = tag.ContainingTypes.Length - 1; i >= 0; i--)
        {
            indent = new string(' ', i * 4);
            sb.AppendLine($"{indent}}}");
        }

        var filename = "Tag_" + tag.FullyQualifiedName.Replace(".", "_").Replace("+", "_") + ".g.cs";
        context.AddSource(filename, sb.ToString());
    }

    private static void GenerateTagRegistry(SourceProductionContext context, List<TagInfo> tags, string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Provides runtime Type-to-TagId mapping for all registered tags.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class TagRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    private static global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Paradise.ECS.TagId>? s_typeToId;");
        sb.AppendLine("    private static global::System.Collections.Frozen.FrozenDictionary<global::System.Guid, global::Paradise.ECS.TagId>? s_guidToId;");
        sb.AppendLine();
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        var tags = new (global::System.Type Type, global::System.Guid Guid, int ManualId, global::System.Action<global::Paradise.ECS.TagId> SetId)[]");
        sb.AppendLine("        {");
        foreach (var tag in tags)
        {
            var guid = tag.Guid != null
                ? $"new global::System.Guid(\"{tag.Guid}\")"
                : "global::System.Guid.Empty";
            var manualId = tag.ManualId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-1";
            sb.AppendLine($"            (typeof(global::{tag.FullyQualifiedName}), {guid}, {manualId}, (global::Paradise.ECS.TagId id) => global::{tag.FullyQualifiedName}.TagId = id),");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        var typeToId = new global::System.Collections.Generic.Dictionary<global::System.Type, global::Paradise.ECS.TagId>(tags.Length);");
        sb.AppendLine("        var guidToId = new global::System.Collections.Generic.Dictionary<global::System.Guid, global::Paradise.ECS.TagId>();");
        sb.AppendLine("        var usedIds = new global::System.Collections.Generic.HashSet<int>();");
        sb.AppendLine();
        sb.AppendLine("        foreach (var tag in tags)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (tag.ManualId >= 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                var id = new global::Paradise.ECS.TagId(tag.ManualId);");
        sb.AppendLine("                tag.SetId(id);");
        sb.AppendLine("                typeToId[tag.Type] = id;");
        sb.AppendLine("                if (tag.Guid != global::System.Guid.Empty) guidToId[tag.Guid] = id;");
        sb.AppendLine("                usedIds.Add(tag.ManualId);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var autoTags = global::System.Linq.Enumerable.ToList(global::System.Linq.Enumerable.Where(tags, t => t.ManualId < 0));");
        sb.AppendLine("        int nextId = 0;");
        sb.AppendLine("        foreach (var tag in autoTags)");
        sb.AppendLine("        {");
        sb.AppendLine("            while (usedIds.Contains(nextId)) nextId++;");
        sb.AppendLine("            var id = new global::Paradise.ECS.TagId(nextId);");
        sb.AppendLine("            tag.SetId(id);");
        sb.AppendLine("            typeToId[tag.Type] = id;");
        sb.AppendLine("            if (tag.Guid != global::System.Guid.Empty) guidToId[tag.Guid] = id;");
        sb.AppendLine("            usedIds.Add(nextId);");
        sb.AppendLine("            nextId++;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        s_typeToId = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(typeToId);");
        sb.AppendLine("        s_guidToId = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(guidToId);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public static global::Paradise.ECS.TagId GetId(global::System.Type type) =>");
        sb.AppendLine("        s_typeToId!.TryGetValue(type, out var id) ? id : global::Paradise.ECS.TagId.Invalid;");
        sb.AppendLine();
        sb.AppendLine("    public static bool TryGetId(global::System.Type type, out global::Paradise.ECS.TagId id) =>");
        sb.AppendLine("        s_typeToId!.TryGetValue(type, out id);");
        sb.AppendLine();
        sb.AppendLine("    public static global::Paradise.ECS.TagId GetId(global::System.Guid guid) =>");
        sb.AppendLine("        s_guidToId!.TryGetValue(guid, out var id) ? id : global::Paradise.ECS.TagId.Invalid;");
        sb.AppendLine();
        sb.AppendLine("    public static bool TryGetId(global::System.Guid guid, out global::Paradise.ECS.TagId id) =>");
        sb.AppendLine("        s_guidToId!.TryGetValue(guid, out id);");
        sb.AppendLine("}");

        context.AddSource("TagRegistry.g.cs", sb.ToString());
    }

    private static void GenerateTagAliases(SourceProductionContext context, int tagCount, string tagMaskType, string? tagMaskBitsType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine($"// Tag count: {tagCount}");
        sb.AppendLine();

        if (tagMaskBitsType != null)
        {
            sb.AppendLine($"global using TagMaskBits = global::Paradise.ECS.{tagMaskBitsType};");
        }
        sb.AppendLine($"global using TagMask = {tagMaskType};");

        context.AddSource("TagAliases.g.cs", sb.ToString());
    }

    private static void GenerateComponentPartialStruct(SourceProductionContext context, ComponentInfo component, string tagMaskType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CA1708 // Identifiers should differ by more than case");
        sb.AppendLine();

        if (component.Namespace != null)
        {
            sb.AppendLine($"namespace {component.Namespace};");
            sb.AppendLine();
        }

        var indent = "";
        foreach (var containingType in component.ContainingTypes)
        {
            sb.AppendLine($"{indent}partial {containingType.Keyword} {containingType.Name}");
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        if (component.Guid != null)
        {
            sb.AppendLine($"{indent}[global::System.Runtime.InteropServices.Guid(\"{component.Guid}\")]");
        }

        // EntityTags gets special treatment - implement IEntityTags and add Mask property
        if (component.IsEntityTags)
        {
            sb.AppendLine($"{indent}partial struct {component.TypeName} : global::Paradise.ECS.IComponent, global::Paradise.ECS.IEntityTags<{tagMaskType}>");
        }
        else
        {
            sb.AppendLine($"{indent}partial struct {component.TypeName} : global::Paradise.ECS.IComponent");
        }
        sb.AppendLine($"{indent}{{");

        sb.AppendLine($"{indent}    /// <summary>The unique component type ID assigned at module initialization.</summary>");
        sb.AppendLine($"{indent}    public static global::Paradise.ECS.ComponentId TypeId {{ get; internal set; }} = global::Paradise.ECS.ComponentId.Invalid;");
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

        // EntityTags: add Mask property and use its size
        if (component.IsEntityTags)
        {
            sb.AppendLine($"{indent}    private {tagMaskType} _mask;");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>The tag bitmask for this entity.</summary>");
            sb.AppendLine($"{indent}    public {tagMaskType} Mask {{ get => _mask; set => _mask = value; }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>The size of this component in bytes.</summary>");
            sb.AppendLine($"{indent}    public static int Size {{ get; }} = global::System.Runtime.CompilerServices.Unsafe.SizeOf<{tagMaskType}>();");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>The alignment of this component in bytes.</summary>");
            sb.AppendLine($"{indent}    public static int Alignment {{ get; }} = global::Paradise.ECS.Memory.AlignOf<{tagMaskType}>();");
        }
        else if (component.IsEmpty)
        {
            sb.AppendLine($"{indent}    /// <summary>The size of this component in bytes.</summary>");
            sb.AppendLine($"{indent}    public static int Size => 0;");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>The alignment of this component in bytes.</summary>");
            sb.AppendLine($"{indent}    public static int Alignment => 0;");
        }
        else
        {
            sb.AppendLine($"{indent}    /// <summary>The size of this component in bytes.</summary>");
            sb.AppendLine($"{indent}    public static int Size {{ get; }} = global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::{component.FullyQualifiedName}>();");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>The alignment of this component in bytes.</summary>");
            sb.AppendLine($"{indent}    public static int Alignment {{ get; }} = global::Paradise.ECS.Memory.AlignOf<global::{component.FullyQualifiedName}>();");
        }

        sb.AppendLine($"{indent}}}");

        for (int i = component.ContainingTypes.Length - 1; i >= 0; i--)
        {
            indent = new string(' ', i * 4);
            sb.AppendLine($"{indent}}}");
        }

        var filename = component.FullyQualifiedName.Replace(".", "_").Replace("+", "_") + ".g.cs";
        context.AddSource(filename, sb.ToString());
    }

    private static string GetOptimalBitStorageType(int componentCount)
    {
        if (componentCount <= 64) return "Bit64";
        if (componentCount <= 128) return "Bit128";
        if (componentCount <= 256) return "Bit256";
        if (componentCount <= 512) return "Bit512";
        if (componentCount <= 1024) return "Bit1024";

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
        sb.AppendLine($"[global::System.Runtime.CompilerServices.InlineArray({ulongCount})]");
        sb.AppendLine($"public struct {typeName} : global::Paradise.ECS.IStorage");
        sb.AppendLine("{");
        sb.AppendLine("    private ulong _element0;");
        sb.AppendLine("}");

        context.AddSource($"{typeName}.g.cs", sb.ToString());
    }

    private static void GenerateGlobalUsings(
        SourceProductionContext context,
        int componentCount,
        string rootNamespace,
        string configType,
        bool suppressGlobalUsings)
    {
        var bitType = GetOptimalBitStorageType(componentCount);
        var bitTypeFullyQualified = $"global::Paradise.ECS.{bitType}";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine($"// Component count: {componentCount} → using {bitType}");
        if (suppressGlobalUsings)
        {
            sb.AppendLine("// Global usings suppressed by [assembly: SuppressGlobalUsings]");
        }
        sb.AppendLine();

        if (!suppressGlobalUsings)
        {
            sb.AppendLine($"global using ComponentMaskBits = {bitTypeFullyQualified};");
            sb.AppendLine($"global using ComponentMask = global::Paradise.ECS.ImmutableBitSet<{bitTypeFullyQualified}>;");
            sb.AppendLine($"global using QueryBuilder = global::Paradise.ECS.QueryBuilder<{bitTypeFullyQualified}>;");

            if (componentCount > 0)
            {
                var registryType = $"{rootNamespace}.ComponentRegistry";
                sb.AppendLine($"global using SharedArchetypeMetadata = global::Paradise.ECS.SharedArchetypeMetadata<{bitTypeFullyQualified}, global::{registryType}, global::{configType}>;");
                sb.AppendLine($"global using ArchetypeRegistry = global::Paradise.ECS.ArchetypeRegistry<{bitTypeFullyQualified}, global::{registryType}, global::{configType}>;");
                sb.AppendLine($"global using World = global::Paradise.ECS.World<{bitTypeFullyQualified}, global::{registryType}, global::{configType}>;");
                sb.AppendLine($"global using Query = global::Paradise.ECS.Query<{bitTypeFullyQualified}, global::{registryType}, global::{configType}, global::Paradise.ECS.Archetype<{bitTypeFullyQualified}, global::{registryType}, global::{configType}>>;");
            }
        }

        context.AddSource("ComponentAliases.g.cs", sb.ToString());

        if (componentCount > 0)
        {
            var extSb = new StringBuilder();
            extSb.AppendLine("// <auto-generated/>");
            extSb.AppendLine();
            extSb.AppendLine($"namespace {rootNamespace};");
            extSb.AppendLine();
            extSb.AppendLine("public static class DefaultChunkManager");
            extSb.AppendLine("{");
            extSb.AppendLine($"    public static global::Paradise.ECS.ChunkManager Create()");
            extSb.AppendLine($"        => global::Paradise.ECS.ChunkManager.Create<global::{configType}>();");
            extSb.AppendLine();
            extSb.AppendLine($"    public static global::Paradise.ECS.ChunkManager Create(global::{configType} config)");
            extSb.AppendLine($"        => global::Paradise.ECS.ChunkManager.Create(config);");
            extSb.AppendLine("}");

            context.AddSource("DefaultChunkManager.g.cs", extSb.ToString());
        }
    }

    private static void GenerateComponentRegistry(SourceProductionContext context, List<ComponentInfo> components, string rootNamespace, string _)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace};");
        sb.AppendLine();
        sb.AppendLine("public sealed class ComponentRegistry : global::Paradise.ECS.IComponentRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    private static global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Paradise.ECS.ComponentId>? s_typeToId;");
        sb.AppendLine("    private static global::System.Collections.Frozen.FrozenDictionary<global::System.Guid, global::Paradise.ECS.ComponentId>? s_guidToId;");
        sb.AppendLine("    private static global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ComponentTypeInfo> s_typeInfos;");
        sb.AppendLine();
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        var components = new (global::System.Type Type, global::System.Guid Guid, int Size, int Alignment, int ManualId, global::System.Action<global::Paradise.ECS.ComponentId> SetId)[]");
        sb.AppendLine("        {");
        foreach (var component in components)
        {
            var guid = component.Guid != null
                ? $"new global::System.Guid(\"{component.Guid}\")"
                : "global::System.Guid.Empty";
            var manualId = component.ManualId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-1";
            sb.AppendLine($"            (typeof(global::{component.FullyQualifiedName}), {guid}, global::{component.FullyQualifiedName}.Size, global::{component.FullyQualifiedName}.Alignment, {manualId}, (global::Paradise.ECS.ComponentId id) => global::{component.FullyQualifiedName}.TypeId = id),");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        var typeToId = new global::System.Collections.Generic.Dictionary<global::System.Type, global::Paradise.ECS.ComponentId>(components.Length);");
        sb.AppendLine("        var guidToId = new global::System.Collections.Generic.Dictionary<global::System.Guid, global::Paradise.ECS.ComponentId>();");
        sb.AppendLine("        var usedIds = new global::System.Collections.Generic.HashSet<int>();");
        sb.AppendLine("        int maxId = -1;");
        sb.AppendLine();
        sb.AppendLine("        foreach (var comp in components)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (comp.ManualId >= 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                var id = new global::Paradise.ECS.ComponentId(comp.ManualId);");
        sb.AppendLine("                comp.SetId(id);");
        sb.AppendLine("                typeToId[comp.Type] = id;");
        sb.AppendLine("                if (comp.Guid != global::System.Guid.Empty) guidToId[comp.Guid] = id;");
        sb.AppendLine("                usedIds.Add(comp.ManualId);");
        sb.AppendLine("                if (comp.ManualId > maxId) maxId = comp.ManualId;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var autoComponents = global::System.Linq.Enumerable.ToList(global::System.Linq.Enumerable.Where(components, c => c.ManualId < 0));");
        sb.AppendLine("        autoComponents.Sort((a, b) =>");
        sb.AppendLine("        {");
        sb.AppendLine("            int cmp = b.Alignment.CompareTo(a.Alignment);");
        sb.AppendLine("            return cmp != 0 ? cmp : global::System.StringComparer.Ordinal.Compare(a.Type.FullName, b.Type.FullName);");
        sb.AppendLine("        });");
        sb.AppendLine();
        sb.AppendLine("        int nextId = 0;");
        sb.AppendLine("        foreach (var comp in autoComponents)");
        sb.AppendLine("        {");
        sb.AppendLine("            while (usedIds.Contains(nextId)) nextId++;");
        sb.AppendLine("            var id = new global::Paradise.ECS.ComponentId(nextId);");
        sb.AppendLine("            comp.SetId(id);");
        sb.AppendLine("            typeToId[comp.Type] = id;");
        sb.AppendLine("            if (comp.Guid != global::System.Guid.Empty) guidToId[comp.Guid] = id;");
        sb.AppendLine("            usedIds.Add(nextId);");
        sb.AppendLine("            if (nextId > maxId) maxId = nextId;");
        sb.AppendLine("            nextId++;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var typeInfosBuilder = global::System.Collections.Immutable.ImmutableArray.CreateBuilder<global::Paradise.ECS.ComponentTypeInfo>(maxId + 1);");
        sb.AppendLine("        for (int i = 0; i <= maxId; i++) typeInfosBuilder.Add(default);");
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
        sb.AppendLine("    public static global::Paradise.ECS.ComponentId GetId(global::System.Type type) =>");
        sb.AppendLine("        s_typeToId!.TryGetValue(type, out var id) ? id : global::Paradise.ECS.ComponentId.Invalid;");
        sb.AppendLine();
        sb.AppendLine("    public static bool TryGetId(global::System.Type type, out global::Paradise.ECS.ComponentId id) =>");
        sb.AppendLine("        s_typeToId!.TryGetValue(type, out id);");
        sb.AppendLine();
        sb.AppendLine("    public static global::Paradise.ECS.ComponentId GetId(global::System.Guid guid) =>");
        sb.AppendLine("        s_guidToId!.TryGetValue(guid, out var id) ? id : global::Paradise.ECS.ComponentId.Invalid;");
        sb.AppendLine();
        sb.AppendLine("    public static bool TryGetId(global::System.Guid guid, out global::Paradise.ECS.ComponentId id) =>");
        sb.AppendLine("        s_guidToId!.TryGetValue(guid, out id);");
        sb.AppendLine();
        sb.AppendLine("    public static global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ComponentTypeInfo> TypeInfos => s_typeInfos;");
        sb.AppendLine("}");

        context.AddSource("ComponentRegistry.g.cs", sb.ToString());
    }

    private static string ValidateDefaultConfig(
        SourceProductionContext context,
        ImmutableArray<DefaultConfigInfo> defaultConfigs)
    {
        const string FallbackConfig = "Paradise.ECS.DefaultConfig";

        var validConfigs = new List<DefaultConfigInfo>();

        foreach (var info in defaultConfigs)
        {
            if (!info.ImplementsIConfig)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DefaultConfigInvalidType,
                    info.Location,
                    info.FullyQualifiedName));
                continue;
            }

            validConfigs.Add(info);
        }

        if (validConfigs.Count == 0)
            return FallbackConfig;

        if (validConfigs.Count > 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MultipleDefaultConfigs,
                validConfigs[0].Location,
                string.Join(", ", validConfigs.Select(t => t.FullyQualifiedName))));
        }

        return validConfigs[0].FullyQualifiedName;
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
        public bool IsEntityTags { get; }

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
            int? manualId,
            bool isEntityTags)
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
            IsEntityTags = isEntityTags;
        }
    }

    private readonly struct TagInfo
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
        public bool HasInstanceFields { get; }
        public int? ManualId { get; }

        public bool HasValidNesting => InvalidContainingType == null;

        public TagInfo(
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
            bool hasInstanceFields,
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
            HasInstanceFields = hasInstanceFields;
            ManualId = manualId;
        }
    }

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

    private readonly struct DefaultConfigInfo
    {
        public string FullyQualifiedName { get; }
        public Location Location { get; }
        public bool ImplementsIConfig { get; }

        public DefaultConfigInfo(string fullyQualifiedName, Location location, bool implementsIConfig)
        {
            FullyQualifiedName = fullyQualifiedName;
            Location = location;
            ImplementsIConfig = implementsIConfig;
        }
    }
}
