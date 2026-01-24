using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Paradise.ECS.Generators;

/// <summary>
/// Source generator that assigns component IDs to types marked with [Component].
/// </summary>
[Generator]
public class ComponentGenerator : IIncrementalGenerator
{
    private const string ComponentAttributeFullName = "Paradise.ECS.ComponentAttribute";
    private const string RegistryNamespaceAttributeFullName = "Paradise.ECS.ComponentRegistryNamespaceAttribute";
    private const string DefaultConfigAttributeFullName = "Paradise.ECS.DefaultConfigAttribute";
    private const string SuppressGlobalUsingsAttributeFullName = "Paradise.ECS.SuppressGlobalUsingsAttribute";
    private const string IConfigFullName = "Paradise.ECS.IConfig";
    private const string TagAssemblyName = "Paradise.ECS.Tag";
    private const string EdgeKeyFullName = "Paradise.ECS.EdgeKey";
    private const string TagAttributeFullName = "Paradise.ECS.TagAttribute";

    private const int DefaultMaxComponentTypeId = (1 << 11) - 1;
    private const int DefaultMaxTagId = (1 << 11) - 1;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var componentTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ComponentAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => GeneratorUtilities.ExtractTypeInfo(ctx, TypeKind.Component))
            .Where(static x => x.HasValue)
            .Select(static (x, _) => x!.Value);

        // Collect tag types only for counting (to determine TagMask type)
        var tagTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                TagAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => GeneratorUtilities.ExtractTypeInfo(ctx, TypeKind.Tag))
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

    private static void GenerateCode(
        SourceProductionContext context,
        ImmutableArray<TypeInfo> components,
        ImmutableArray<TypeInfo> tags,
        ImmutableArray<DefaultConfigInfo> defaultConfigs,
        GeneratorConfig config)
    {
        // Validate and determine config type
        var configType = ValidateDefaultConfig(context, defaultConfigs);

        // Calculate tag mask type from tags (for EntityTags component)
        // We need to calculate the same mask type as TagGenerator for consistency
        // Filter valid tags (unmanaged, no invalid containing types, valid manual IDs)
        var tagMaskType = CalculateTagMaskType(tags);
        var validTagCount = tags.Count(t =>
            t.IsUnmanaged &&
            t.InvalidContainingType == null &&
            (!t.ManualId.HasValue || t.ManualId.Value <= DefaultMaxTagId) &&
            !t.HasInstanceFields);

        var tagsEffectivelyEnabled = config.HasTagAssemblyReference && validTagCount > 0;

        // Auto-generate EntityTags when tags are enabled
        // Check for user-defined EntityTags in the root namespace (fully qualified name match)
        var expectedEntityTagsFqn = $"{config.RootNamespace}.EntityTags";
        var userDefinedEntityTags = components.Any(c => c.FullyQualifiedName == expectedEntityTagsFqn);

        // Process and validate components
        var (validComponents, _) = GeneratorUtilities.ProcessTypes(
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
            var maxId = GeneratorUtilities.CalculateMaxAssignedId(validComponents);
            if (maxId > config.MaxComponentTypeId)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TooManyComponents, null, validComponents.Count, config.MaxComponentTypeId));
                return;
            }
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

            // Generate partial structs for user-defined components only
            // Auto-generated EntityTags struct is generated by TagGenerator
            foreach (var component in validComponents)
            {
                var isAutoGeneratedEntityTags = autoGenerateEntityTags && component.FullyQualifiedName == expectedEntityTagsFqn;
                if (!isAutoGeneratedEntityTags)
                {
                    // For user-defined EntityTags, we need to force computed Size/Alignment
                    // because TagGenerator will add the _mask field
                    var isUserDefinedEntityTags = userDefinedEntityTags && component.FullyQualifiedName == expectedEntityTagsFqn;
                    GeneratePartialStruct(context, component, forceComputedSize: isUserDefinedEntityTags);
                }
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

    private static void GeneratePartialStruct(SourceProductionContext context, TypeInfo info, bool forceComputedSize = false)
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

        sb.AppendLine($"{indent}partial struct {info.TypeName} : global::Paradise.ECS.IComponent");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    /// <summary>The unique component type ID assigned at module initialization.</summary>");
        sb.AppendLine($"{indent}    public static global::Paradise.ECS.ComponentId TypeId {{ get; internal set; }} = global::Paradise.ECS.ComponentId.Invalid;");

        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>The stable GUID for this component type.</summary>");
        sb.AppendLine(info.Guid != null
            ? $"{indent}    public static global::System.Guid Guid {{ get; }} = new global::System.Guid(\"{info.Guid}\");"
            : $"{indent}    public static global::System.Guid Guid => global::System.Guid.Empty;");

        sb.AppendLine();
        // Use computed size/alignment if component has instance fields OR if forced (e.g., for user-defined EntityTags)
        if (!info.HasInstanceFields && !forceComputedSize)
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
            sb.AppendLine($"{indent}    public static int Size {{ get; }} = global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::{info.FullyQualifiedName}>();");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>The alignment of this component in bytes.</summary>");
            sb.AppendLine($"{indent}    public static int Alignment {{ get; }} = global::Paradise.ECS.Memory.AlignOf<global::{info.FullyQualifiedName}>();");
        }

        sb.AppendLine($"{indent}}}");

        for (int i = info.ContainingTypes.Length - 1; i >= 0; i--)
            sb.AppendLine($"{new string(' ', i * 4)}}}");

        var filename = $"{info.FullyQualifiedName.Replace(".", "_").Replace("+", "_")}.g.cs";
        context.AddSource(filename, sb.ToString());
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

            var configTypeFull = $"global::{configType}";
            sb.AppendLine($"global using SharedArchetypeMetadata = global::Paradise.ECS.SharedArchetypeMetadata<{maskTypeFull}, {configTypeFull}>;");
            sb.AppendLine($"global using ArchetypeRegistry = global::Paradise.ECS.ArchetypeRegistry<{maskTypeFull}, {configTypeFull}>;");

            if (enableTags)
            {
                var entityTags = $"global::{config.RootNamespace}.EntityTags";
                sb.AppendLine($"global using World = global::Paradise.ECS.TaggedWorld<{maskTypeFull}, {configTypeFull}, {entityTags}, {tagMaskType}>;");
            }
            else
            {
                sb.AppendLine($"global using World = global::Paradise.ECS.World<{maskTypeFull}, {configTypeFull}>;");
            }

            sb.AppendLine($"global using Query = global::Paradise.ECS.Query<{maskTypeFull}, {configTypeFull}, global::Paradise.ECS.Archetype<{maskTypeFull}, {configTypeFull}>>;");
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
        sb.AppendLine("public sealed class ComponentRegistry");
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
        sb.AppendLine("    // Static accessors");
        sb.AppendLine("    public static global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Paradise.ECS.ComponentId> TypeToIdStatic => s_typeToId!;");
        sb.AppendLine("    public static global::System.Collections.Frozen.FrozenDictionary<global::System.Guid, global::Paradise.ECS.ComponentId> GuidToIdStatic => s_guidToId!;");
        sb.AppendLine("    public static global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ComponentTypeInfo> TypeInfosStatic => s_typeInfos;");
        sb.AppendLine();
        sb.AppendLine("    // Instance members");
        sb.AppendLine("    public global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Paradise.ECS.ComponentId> TypeToId => TypeToIdStatic;");
        sb.AppendLine("    public global::System.Collections.Frozen.FrozenDictionary<global::System.Guid, global::Paradise.ECS.ComponentId> GuidToId => GuidToIdStatic;");
        sb.AppendLine("    public global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ComponentTypeInfo> TypeInfos => TypeInfosStatic;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Shared singleton instance of the component registry.</summary>");
        sb.AppendLine("    public static ComponentRegistry Shared { get; } = new();");
        sb.AppendLine("}");

        context.AddSource("ComponentRegistry.g.cs", sb.ToString());
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

    /// <summary>
    /// Calculates the tag mask type based on valid tags, matching TagGenerator's calculation.
    /// </summary>
    private static string CalculateTagMaskType(ImmutableArray<TypeInfo> tags)
    {
        if (tags.IsEmpty)
            return "global::Paradise.ECS.SmallBitSet<uint>";

        // Filter valid tags (same criteria as TagGenerator uses via ProcessTypes)
        var validTags = tags.Where(t =>
            t.IsUnmanaged &&
            t.InvalidContainingType == null &&
            (!t.ManualId.HasValue || t.ManualId.Value <= DefaultMaxTagId) &&
            !t.HasInstanceFields).ToList();

        if (validTags.Count == 0)
            return "global::Paradise.ECS.SmallBitSet<uint>";

        // Calculate max assigned ID accounting for manual IDs
        var maxAssignedId = GeneratorUtilities.CalculateMaxAssignedId(validTags);
        var requiredBits = maxAssignedId + 1;
        return GeneratorUtilities.GetOptimalMaskType(requiredBits);
    }
}
