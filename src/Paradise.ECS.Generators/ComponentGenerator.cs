using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Paradise.ECS.Generators;

/// <summary>
/// Source generator that assigns component IDs to types marked with [Component]
/// and tag IDs to types marked with [Tag].
/// </summary>
[Generator]
public class ComponentGenerator : IIncrementalGenerator
{
    private const string ComponentAttributeFullName = "Paradise.ECS.ComponentAttribute";
    private const string TagAttributeFullName = "Paradise.ECS.TagAttribute";
    private const string RegistryNamespaceAttributeFullName = "Paradise.ECS.ComponentRegistryNamespaceAttribute";
    private const string DefaultConfigAttributeFullName = "Paradise.ECS.DefaultConfigAttribute";
    private const string SuppressGlobalUsingsAttributeFullName = "Paradise.ECS.SuppressGlobalUsingsAttribute";
    private const string IConfigFullName = "Paradise.ECS.IConfig";
    private const string TagAssemblyName = "Paradise.ECS.Tag";
    private const string EdgeKeyFullName = "Paradise.ECS.EdgeKey";

    private const int DefaultMaxComponentTypeId = (1 << 11) - 1;
    private const int DefaultMaxTagId = (1 << 11) - 1;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var componentTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ComponentAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => ExtractTypeInfo(ctx, TypeKind.Component))
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x!.Value);

        var tagTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                TagAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => ExtractTypeInfo(ctx, TypeKind.Tag))
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x!.Value);

        var defaultConfigTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                DefaultConfigAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax or ClassDeclarationSyntax,
                transform: static (ctx, _) => ExtractDefaultConfigInfo(ctx))
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x!.Value)
            .Collect();

        var config = context.CompilationProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (pair, _) => ExtractConfig(pair.Left, pair.Right));

        var combined = componentTypes.Collect()
            .Combine(tagTypes.Collect())
            .Combine(defaultConfigTypes)
            .Combine(config);

        context.RegisterSourceOutput(combined, static (ctx, data) =>
            GenerateCode(ctx, data.Left.Left.Left, data.Left.Left.Right, data.Left.Right, data.Right));
    }

    private static GeneratorConfig ExtractConfig(Compilation compilation, AnalyzerConfigOptionsProvider options)
    {
        // Root namespace: attribute > build property > default
        var nsAttr = compilation.Assembly.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == RegistryNamespaceAttributeFullName);
        var rootNamespace = nsAttr?.ConstructorArguments.FirstOrDefault().Value as string;
        if (rootNamespace == null)
        {
            options.GlobalOptions.TryGetValue("build_property.RootNamespace", out rootNamespace);
            rootNamespace ??= "Paradise.ECS";
        }

        // Max component type ID from EdgeKey
        var edgeKeyType = compilation.GetTypeByMetadataName(EdgeKeyFullName);
        var maxComponentTypeId = edgeKeyType?.GetMembers("MaxComponentTypeId")
            .OfType<IFieldSymbol>()
            .FirstOrDefault() is { HasConstantValue: true, ConstantValue: int v } ? v : DefaultMaxComponentTypeId;

        // Check assembly attributes
        var suppressGlobalUsings = compilation.Assembly.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == SuppressGlobalUsingsAttributeFullName);

        // Check if project references Paradise.ECS.Tag assembly
        var hasTagAssemblyReference = compilation.ReferencedAssemblyNames
            .Any(a => a.Name == TagAssemblyName);

        return new GeneratorConfig(rootNamespace, maxComponentTypeId, suppressGlobalUsings, hasTagAssemblyReference);
    }

    private static DefaultConfigInfo? ExtractDefaultConfigInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var fullyQualifiedName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fullyQualifiedName.StartsWith("global::", StringComparison.Ordinal))
            fullyQualifiedName = fullyQualifiedName.Substring(8);

        var iConfigInterface = context.SemanticModel.Compilation.GetTypeByMetadataName(IConfigFullName);
        var implementsIConfig = iConfigInterface != null &&
            typeSymbol.AllInterfaces.Contains(iConfigInterface, SymbolEqualityComparer.Default);

        return new DefaultConfigInfo(
            fullyQualifiedName,
            typeSymbol.Locations.FirstOrDefault() ?? Location.None,
            implementsIConfig);
    }

    private static TypeInfo? ExtractTypeInfo(GeneratorAttributeSyntaxContext context, TypeKind kind)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol || typeSymbol.TypeKind != Microsoft.CodeAnalysis.TypeKind.Struct)
            return null;

        var fullyQualifiedName = GeneratorUtilities.GetFullyQualifiedName(typeSymbol);
        var ns = GeneratorUtilities.GetNamespace(typeSymbol);
        var containingTypes = GeneratorUtilities.GetContainingTypes(typeSymbol);

        // Check for invalid generic containing types
        string? invalidContainingType = null;
        for (var parent = typeSymbol.ContainingType; parent != null; parent = parent.ContainingType)
        {
            if (parent.IsGenericType)
            {
                invalidContainingType = parent.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                break;
            }
        }

        // Extract GUID and manual ID from attributes
        string? validGuid = null, invalidGuid = null;
        int? manualId = null;
        foreach (var attr in context.Attributes)
        {
            if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string ctorGuid)
            {
                if (Guid.TryParse(ctorGuid, out _))
                    validGuid = ctorGuid;
                else
                    invalidGuid = ctorGuid;
            }

            foreach (var arg in attr.NamedArguments)
            {
                if (arg.Key == "Guid" && arg.Value.Value is string g)
                {
                    if (Guid.TryParse(g, out _))
                    {
                        validGuid = g;
                        invalidGuid = null;
                    }
                    else
                    {
                        validGuid = null;
                        invalidGuid = g;
                    }
                }
                else if (arg.Key == "Id" && arg.Value.Value is int id && id >= 0)
                    manualId = id;
            }
        }

        var hasInstanceFields = typeSymbol.GetMembers().OfType<IFieldSymbol>().Any(f => !f.IsStatic);

        return new TypeInfo(
            kind,
            fullyQualifiedName,
            typeSymbol.Locations.FirstOrDefault() ?? Location.None,
            typeSymbol.IsUnmanagedType,
            ns,
            typeSymbol.Name,
            containingTypes,
            validGuid,
            invalidGuid,
            invalidContainingType,
            hasInstanceFields,
            manualId);
    }

    private static void GenerateCode(
        SourceProductionContext context,
        ImmutableArray<TypeInfo> components,
        ImmutableArray<TypeInfo> tags,
        ImmutableArray<DefaultConfigInfo> defaultConfigs,
        GeneratorConfig config)
    {
        // Validate and determine config type
        var configType = ValidateDefaultConfig(context, defaultConfigs);

        // Process and validate tags
        var (validTags, tagMaskType) = ProcessTypes(
            context, tags, DefaultMaxTagId,
            DiagnosticDescriptors.TagNotUnmanaged,
            DiagnosticDescriptors.InvalidTagGuidFormat,
            DiagnosticDescriptors.UnsupportedTagContainingType,
            DiagnosticDescriptors.TagIdExceedsLimit,
            DiagnosticDescriptors.DuplicateTagId,
            t => t.HasInstanceFields ? DiagnosticDescriptors.TagHasFields : null);

        var tagsEffectivelyEnabled = config.HasTagAssemblyReference;

        // Auto-generate EntityTags when tags are enabled
        // Check for user-defined EntityTags in the root namespace (fully qualified name match)
        var expectedEntityTagsFqn = $"{config.RootNamespace}.EntityTags";
        var userDefinedEntityTags = components.Any(c => c.FullyQualifiedName == expectedEntityTagsFqn);

        // Process and validate components
        var (validComponents, _) = ProcessTypes(
            context, components, config.MaxComponentTypeId,
            DiagnosticDescriptors.ComponentNotUnmanaged,
            DiagnosticDescriptors.InvalidGuidFormat,
            DiagnosticDescriptors.UnsupportedContainingType,
            DiagnosticDescriptors.ComponentIdExceedsLimit,
            DiagnosticDescriptors.DuplicateComponentId,
            c => c is { HasInstanceFields: false, TypeName: not "EntityTags" } ? DiagnosticDescriptors.ComponentIsEmpty : null);

        // Add auto-generated EntityTags if tags are enabled and user didn't define one
        var autoGenerateEntityTags = tagsEffectivelyEnabled && !userDefinedEntityTags;
        if (autoGenerateEntityTags)
        {
            var entityTagsInfo = new TypeInfo(
                TypeKind.Component,
                $"{config.RootNamespace}.EntityTags",
                Location.None,
                IsUnmanaged: true,
                config.RootNamespace,
                "EntityTags",
                ImmutableArray<ContainingTypeInfo>.Empty,
                Guid: null,
                InvalidGuid: null,
                InvalidContainingType: null,
                HasInstanceFields: true, // The mask field
                ManualId: null);
            validComponents.Add(entityTagsInfo);
            validComponents.Sort((a, b) => StringComparer.Ordinal.Compare(a.FullyQualifiedName, b.FullyQualifiedName));
        }

        // Check total component count
        if (validComponents.Count > 0)
        {
            var maxId = CalculateMaxAssignedId(validComponents);
            if (maxId > config.MaxComponentTypeId)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TooManyComponents, null, validComponents.Count, config.MaxComponentTypeId));
                return;
            }
        }

        // Generate tag code
        if (validTags.Count > 0)
        {
            foreach (var tag in validTags)
                GeneratePartialStruct(context, tag, tagMaskType, "Tag_");
            GenerateTagRegistry(context, validTags, config.RootNamespace);
            GenerateTagAliases(context, validTags.Count, tagMaskType);
        }

        // Generate component code
        if (validComponents.Count > 0)
        {
            const int MaxBuiltInComponents = 1024;
            if (validComponents.Count > MaxBuiltInComponents)
            {
                var capacity = ((validComponents.Count + 255) / 256) * 256;
                var customType = $"Bit{capacity}";
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ComponentCountExceedsBuiltIn, null, validComponents.Count, customType));
                GenerateCustomStorageType(context, customType, capacity / 64);
            }

            foreach (var component in validComponents)
            {
                // Auto-generated EntityTags needs a complete struct, not partial
                // Check by fully qualified name to avoid matching user-defined EntityTags in other namespaces
                var isAutoGenerated = autoGenerateEntityTags && component.FullyQualifiedName == expectedEntityTagsFqn;
                GeneratePartialStruct(context, component, tagMaskType, "", isAutoGenerated, expectedEntityTagsFqn);
            }
            GenerateGlobalUsings(context, validComponents.Count, config, configType, tagsEffectivelyEnabled, tagMaskType);
            GenerateComponentRegistry(context, validComponents, config.RootNamespace);
        }
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

    private static (List<TypeInfo> Valid, string MaskType) ProcessTypes(
        SourceProductionContext context,
        ImmutableArray<TypeInfo> types,
        int maxId,
        DiagnosticDescriptor notUnmanaged,
        DiagnosticDescriptor invalidGuid,
        DiagnosticDescriptor invalidContaining,
        DiagnosticDescriptor idExceedsLimit,
        DiagnosticDescriptor duplicateId,
        Func<TypeInfo, DiagnosticDescriptor?> additionalCheck)
    {
        if (types.IsEmpty)
            return (new List<TypeInfo>(), "global::Paradise.ECS.SmallBitSet<uint>");

        var sorted = types.OrderBy(t => t.FullyQualifiedName, StringComparer.Ordinal).ToList();
        var duplicateManualIds = new HashSet<int>();

        // Report diagnostics
        foreach (var t in sorted)
        {
            if (!t.IsUnmanaged)
                context.ReportDiagnostic(Diagnostic.Create(notUnmanaged, t.Location, t.FullyQualifiedName));
            if (t.InvalidGuid != null)
                context.ReportDiagnostic(Diagnostic.Create(invalidGuid, t.Location, t.FullyQualifiedName, t.InvalidGuid));
            if (t.InvalidContainingType != null)
                context.ReportDiagnostic(Diagnostic.Create(invalidContaining, t.Location, t.FullyQualifiedName, t.InvalidContainingType, "a generic type"));
            if (t.ManualId > maxId)
                context.ReportDiagnostic(Diagnostic.Create(idExceedsLimit, t.Location, t.FullyQualifiedName, t.ManualId, maxId));
            if (additionalCheck(t) is { } desc)
                context.ReportDiagnostic(Diagnostic.Create(desc, t.Location, t.FullyQualifiedName));
        }

        // Check duplicate manual IDs
        foreach (var group in sorted.Where(t => t.ManualId.HasValue).GroupBy(t => t.ManualId!.Value).Where(g => g.Count() > 1))
        {
            duplicateManualIds.Add(group.Key);
            context.ReportDiagnostic(Diagnostic.Create(duplicateId, group.First().Location, group.Key,
                string.Join(", ", group.Select(t => t.FullyQualifiedName))));
        }

        // Filter valid types
        var valid = sorted.Where(t =>
            t.IsUnmanaged &&
            t.InvalidContainingType == null &&
            (!t.ManualId.HasValue || (t.ManualId.Value <= maxId && !duplicateManualIds.Contains(t.ManualId.Value))) &&
            (t.Kind != TypeKind.Tag || !t.HasInstanceFields)).ToList();

        // Calculate mask type
        var maxAssignedId = CalculateMaxAssignedId(valid);
        var requiredBits = maxAssignedId + 1;
        var maskType = GeneratorUtilities.GetOptimalMaskType(requiredBits);

        return (valid, maskType);
    }

    private static int CalculateMaxAssignedId(List<TypeInfo> types)
    {
        var manualIds = new HashSet<int>(types.Where(t => t.ManualId.HasValue).Select(t => t.ManualId!.Value));
        var autoCount = types.Count - manualIds.Count;
        var maxId = manualIds.Count > 0 ? manualIds.Max() : -1;

        for (int i = 0, nextId = 0; i < autoCount; i++, nextId++)
        {
            while (manualIds.Contains(nextId)) nextId++;
            if (nextId > maxId) maxId = nextId;
        }
        return maxId;
    }

    private static void GeneratePartialStruct(SourceProductionContext context, TypeInfo info, string tagMaskType, string filePrefix, bool isAutoGenerated = false, string? entityTagsFqn = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CA1708 // Identifiers should differ by more than case");
        sb.AppendLine();

        if (info.Namespace != null)
        {
            sb.AppendLine($"namespace {info.Namespace};");
            sb.AppendLine();
        }

        var indent = "";
        foreach (var ct in info.ContainingTypes)
        {
            sb.AppendLine($"{indent}partial {ct.Keyword} {ct.Name}");
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        if (info.Guid != null)
            sb.AppendLine($"{indent}[global::System.Runtime.InteropServices.Guid(\"{info.Guid}\")]");

        var structKeyword = isAutoGenerated ? "struct" : "partial struct";

        if (info.Kind == TypeKind.Tag)
        {
            sb.AppendLine($"{indent}{structKeyword} {info.TypeName} : global::Paradise.ECS.ITag");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    /// <summary>The unique tag type ID assigned at module initialization.</summary>");
            sb.AppendLine($"{indent}    public static global::Paradise.ECS.TagId TagId {{ get; internal set; }} = global::Paradise.ECS.TagId.Invalid;");
        }
        else
        {
            // Only treat as EntityTags if the fully qualified name matches the expected EntityTags FQN
            var isEntityTags = entityTagsFqn != null && info.FullyQualifiedName == entityTagsFqn;
            var interfaces = isEntityTags
                ? $"global::Paradise.ECS.IComponent, global::Paradise.ECS.IEntityTags<{tagMaskType}>"
                : "global::Paradise.ECS.IComponent";

            sb.AppendLine($"{indent}{structKeyword} {info.TypeName} : {interfaces}");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    /// <summary>The unique component type ID assigned at module initialization.</summary>");
            sb.AppendLine($"{indent}    public static global::Paradise.ECS.ComponentId TypeId {{ get; internal set; }} = global::Paradise.ECS.ComponentId.Invalid;");

            if (isEntityTags)
            {
                sb.AppendLine();
                sb.AppendLine($"{indent}    private {tagMaskType} _mask;");
                sb.AppendLine($"{indent}    /// <summary>The tag bitmask for this entity.</summary>");
                sb.AppendLine($"{indent}    public {tagMaskType} Mask {{ get => _mask; set => _mask = value; }}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>The stable GUID for this component type.</summary>");
        sb.AppendLine(info.Guid != null
            ? $"{indent}    public static global::System.Guid Guid {{ get; }} = new global::System.Guid(\"{info.Guid}\");"
            : $"{indent}    public static global::System.Guid Guid => global::System.Guid.Empty;");

        if (info.Kind == TypeKind.Component)
        {
            sb.AppendLine();
            // Only treat as EntityTags if the fully qualified name matches the expected EntityTags FQN
            var isEntityTags = entityTagsFqn != null && info.FullyQualifiedName == entityTagsFqn;
            var sizeType = isEntityTags ? tagMaskType : $"global::{info.FullyQualifiedName}";

            if (!info.HasInstanceFields && !isEntityTags)
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
                sb.AppendLine($"{indent}    public static int Size {{ get; }} = global::System.Runtime.CompilerServices.Unsafe.SizeOf<{sizeType}>();");
                sb.AppendLine();
                sb.AppendLine($"{indent}    /// <summary>The alignment of this component in bytes.</summary>");
                sb.AppendLine($"{indent}    public static int Alignment {{ get; }} = global::Paradise.ECS.Memory.AlignOf<{sizeType}>();");
            }
        }

        sb.AppendLine($"{indent}}}");

        for (int i = info.ContainingTypes.Length - 1; i >= 0; i--)
            sb.AppendLine($"{new string(' ', i * 4)}}}");

        var filename = $"{filePrefix}{info.FullyQualifiedName.Replace(".", "_").Replace("+", "_")}.g.cs";
        context.AddSource(filename, sb.ToString());
    }

    private static void GenerateTagRegistry(SourceProductionContext context, List<TypeInfo> tags, string rootNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>Provides runtime Type-to-TagId mapping for all registered tags.</summary>");
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
            var guid = tag.Guid != null ? $"new global::System.Guid(\"{tag.Guid}\")" : "global::System.Guid.Empty";
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

    private static void GenerateTagAliases(SourceProductionContext context, int tagCount, string tagMaskType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine($"// Tag count: {tagCount}");
        sb.AppendLine();
        sb.AppendLine($"global using TagMask = {tagMaskType};");

        context.AddSource("TagAliases.g.cs", sb.ToString());
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
        GeneratorConfig config,
        string configType,
        bool enableTags,
        string tagMaskType)
    {
        var maskTypeFull = GeneratorUtilities.GetOptimalMaskType(componentCount);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine($"// Component count: {componentCount}");
        if (enableTags)
            sb.AppendLine("// Tags enabled (Paradise.ECS.Tag referenced) → World alias points to TaggedWorld");
        if (config.SuppressGlobalUsings)
            sb.AppendLine("// Global usings suppressed by [assembly: SuppressGlobalUsings]");
        sb.AppendLine();

        if (!config.SuppressGlobalUsings)
        {
            sb.AppendLine($"global using ComponentMask = {maskTypeFull};");
            sb.AppendLine($"global using QueryBuilder = global::Paradise.ECS.QueryBuilder<{maskTypeFull}>;");

            var registry = $"global::{config.RootNamespace}.ComponentRegistry";
            var configTypeFull = $"global::{configType}";
            sb.AppendLine($"global using SharedArchetypeMetadata = global::Paradise.ECS.SharedArchetypeMetadata<{maskTypeFull}, {registry}, {configTypeFull}>;");
            sb.AppendLine($"global using ArchetypeRegistry = global::Paradise.ECS.ArchetypeRegistry<{maskTypeFull}, {registry}, {configTypeFull}>;");

            if (enableTags)
            {
                var entityTags = $"global::{config.RootNamespace}.EntityTags";
                sb.AppendLine($"global using World = global::Paradise.ECS.TaggedWorld<{maskTypeFull}, {registry}, {configTypeFull}, {entityTags}, {tagMaskType}>;");
            }
            else
            {
                sb.AppendLine($"global using World = global::Paradise.ECS.World<{maskTypeFull}, {registry}, {configTypeFull}>;");
            }

            sb.AppendLine($"global using Query = global::Paradise.ECS.Query<{maskTypeFull}, {registry}, {configTypeFull}, global::Paradise.ECS.Archetype<{maskTypeFull}, {registry}, {configTypeFull}>>;");
        }

        context.AddSource("ComponentAliases.g.cs", sb.ToString());

        // Generate DefaultChunkManager helper
        var extSb = new StringBuilder();
        extSb.AppendLine("// <auto-generated/>");
        extSb.AppendLine();
        extSb.AppendLine($"namespace {config.RootNamespace};");
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

    private static void GenerateComponentRegistry(SourceProductionContext context, List<TypeInfo> components, string rootNamespace)
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

        foreach (var c in components)
        {
            var guid = c.Guid != null ? $"new global::System.Guid(\"{c.Guid}\")" : "global::System.Guid.Empty";
            var manualId = c.ManualId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-1";
            sb.AppendLine($"            (typeof(global::{c.FullyQualifiedName}), {guid}, global::{c.FullyQualifiedName}.Size, global::{c.FullyQualifiedName}.Alignment, {manualId}, (global::Paradise.ECS.ComponentId id) => global::{c.FullyQualifiedName}.TypeId = id),");
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

    private enum TypeKind { Component, Tag }

    private readonly struct TypeInfo
    {
        public TypeKind Kind { get; }
        public string FullyQualifiedName { get; }
        public Location Location { get; }
        public bool IsUnmanaged { get; }
        public string? Namespace { get; }
        public string TypeName { get; }
        public ImmutableArray<ContainingTypeInfo> ContainingTypes { get; }
        public string? Guid { get; }
        public string? InvalidGuid { get; }
        public string? InvalidContainingType { get; }
        public bool HasInstanceFields { get; }
        public int? ManualId { get; }

        public TypeInfo(
            TypeKind Kind,
            string FullyQualifiedName,
            Location Location,
            bool IsUnmanaged,
            string? Namespace,
            string TypeName,
            ImmutableArray<ContainingTypeInfo> ContainingTypes,
            string? Guid,
            string? InvalidGuid,
            string? InvalidContainingType,
            bool HasInstanceFields,
            int? ManualId)
        {
            this.Kind = Kind;
            this.FullyQualifiedName = FullyQualifiedName;
            this.Location = Location;
            this.IsUnmanaged = IsUnmanaged;
            this.Namespace = Namespace;
            this.TypeName = TypeName;
            this.ContainingTypes = ContainingTypes;
            this.Guid = Guid;
            this.InvalidGuid = InvalidGuid;
            this.InvalidContainingType = InvalidContainingType;
            this.HasInstanceFields = HasInstanceFields;
            this.ManualId = ManualId;
        }
    }

    private readonly struct GeneratorConfig
    {
        public string RootNamespace { get; }
        public int MaxComponentTypeId { get; }
        public bool SuppressGlobalUsings { get; }
        public bool HasTagAssemblyReference { get; }

        public GeneratorConfig(
            string rootNamespace,
            int maxComponentTypeId,
            bool suppressGlobalUsings,
            bool hasTagAssemblyReference)
        {
            RootNamespace = rootNamespace;
            MaxComponentTypeId = maxComponentTypeId;
            SuppressGlobalUsings = suppressGlobalUsings;
            HasTagAssemblyReference = hasTagAssemblyReference;
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
