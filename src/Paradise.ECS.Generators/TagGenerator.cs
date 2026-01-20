using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paradise.ECS.Generators;

/// <summary>
/// Source generator that assigns tag IDs to types marked with [Tag].
/// IDs are assigned based on alphabetical ordering of fully qualified type names.
/// Also generates partial struct implementations of ITag.
/// </summary>
[Generator]
public class TagGenerator : IIncrementalGenerator
{
    private const string TagAttributeFullName = "Paradise.ECS.TagAttribute";
    private const string RegistryNamespaceAttributeFullName = "Paradise.ECS.ComponentRegistryNamespaceAttribute";

    // Default max tag ID (same as component for now, but separate namespace)
    private const int DefaultMaxTagId = (1 << 11) - 1;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
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

        // Collect all tags and combine with root namespace
        var collected = tagTypes.Collect();
        var collectedWithConfig = collected.Combine(rootNamespace);

        context.RegisterSourceOutput(collectedWithConfig, static (ctx, data) =>
            GenerateTagCode(ctx, data.Left, data.Right));
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

    private static void GenerateTagCode(
        SourceProductionContext context,
        ImmutableArray<TagInfo> tags,
        string rootNamespace)
    {
        if (tags.IsEmpty)
        {
            // No tags = nothing to generate
            return;
        }

        // Sort by fully qualified name for deterministic ID assignment
        var sorted = tags
            .OrderBy(static t => t.FullyQualifiedName, StringComparer.Ordinal)
            .ToList();

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

            // Validate manual ID doesn't exceed limit
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

        // Report diagnostics for duplicate manual IDs
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

        // Filter to only valid tags for code generation
        var validTags = sorted.Where(t =>
            t.IsUnmanaged &&
            !t.HasInstanceFields &&
            t.HasValidNesting &&
            (!t.ManualId.HasValue || t.ManualId.Value <= DefaultMaxTagId) &&
            (!t.ManualId.HasValue || !duplicateManualIds.Contains(t.ManualId.Value))).ToList();

        if (validTags.Count == 0)
            return;

        // Calculate the maximum tag ID that will be assigned
        var manualIds = new HashSet<int>(validTags
            .Where(t => t.ManualId.HasValue)
            .Select(t => t.ManualId!.Value));
        var autoAssignCount = validTags.Count - manualIds.Count;

        int maxAssignedId = manualIds.Count > 0 ? manualIds.Max() : -1;
        int nextAutoId = 0;
        for (int i = 0; i < autoAssignCount; i++)
        {
            while (manualIds.Contains(nextAutoId)) nextAutoId++;
            if (nextAutoId > maxAssignedId) maxAssignedId = nextAutoId;
            nextAutoId++;
        }

        if (maxAssignedId > DefaultMaxTagId)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TooManyTags,
                null,
                validTags.Count,
                DefaultMaxTagId));
            return;
        }

        // Generate partial struct implementations for ITag
        foreach (var tag in validTags)
        {
            GeneratePartialStruct(context, tag);
        }

        // Generate tag registry
        GenerateTagRegistry(context, validTags, rootNamespace);

        // Generate global using for TagMask
        GenerateTagAliases(context, validTags.Count);
    }

    private static void GenerateTagAliases(SourceProductionContext context, int tagCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");

        // Select the smallest bitset type that can hold all tags
        // Available types: ImmutableBitSet32, ImmutableBitSet<Bit64/128/256/512/1024/custom>
        if (tagCount <= 32)
        {
            sb.AppendLine($"// Tag count: {tagCount} → using ImmutableBitSet32");
            sb.AppendLine();
            sb.AppendLine("global using TagMask = global::Paradise.ECS.ImmutableBitSet32;");
        }
        else
        {
            string bitType;
            if (tagCount <= 64) bitType = "Bit64";
            else if (tagCount <= 128) bitType = "Bit128";
            else if (tagCount <= 256) bitType = "Bit256";
            else if (tagCount <= 512) bitType = "Bit512";
            else if (tagCount <= 1024) bitType = "Bit1024";
            else
            {
                // For >1024, calculate custom type name aligned to 256 bits
                var capacity = ((tagCount + 255) / 256) * 256;
                bitType = $"Bit{capacity}";
            }

            sb.AppendLine($"// Tag count: {tagCount} → using ImmutableBitSet<{bitType}>");
            sb.AppendLine();
            sb.AppendLine($"global using TagMaskBits = global::Paradise.ECS.{bitType};");
            sb.AppendLine($"global using TagMask = global::Paradise.ECS.ImmutableBitSet<global::Paradise.ECS.{bitType}>;");
        }

        context.AddSource("TagAliases.g.cs", sb.ToString());
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
        sb.AppendLine("/// Tag IDs are assigned at module initialization, sorted alphabetically by name.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class TagRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    private static global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Paradise.ECS.TagId>? s_typeToId;");
        sb.AppendLine("    private static global::System.Collections.Frozen.FrozenDictionary<global::System.Guid, global::Paradise.ECS.TagId>? s_guidToId;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Initializes tag IDs. Manual IDs are assigned first, then remaining tags");
        sb.AppendLine("    /// are auto-assigned sorted alphabetically by name.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Collect all tag metadata with optional manual ID");
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
        sb.AppendLine("        // Build lookup dictionaries and assign IDs");
        sb.AppendLine("        var typeToId = new global::System.Collections.Generic.Dictionary<global::System.Type, global::Paradise.ECS.TagId>(tags.Length);");
        sb.AppendLine("        var guidToId = new global::System.Collections.Generic.Dictionary<global::System.Guid, global::Paradise.ECS.TagId>();");
        sb.AppendLine("        var usedIds = new global::System.Collections.Generic.HashSet<int>();");
        sb.AppendLine();
        sb.AppendLine("        // First pass: assign manual IDs");
        sb.AppendLine("        foreach (var tag in tags)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (tag.ManualId >= 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                var id = new global::Paradise.ECS.TagId(tag.ManualId);");
        sb.AppendLine("                tag.SetId(id);");
        sb.AppendLine("                typeToId[tag.Type] = id;");
        sb.AppendLine("                if (tag.Guid != global::System.Guid.Empty)");
        sb.AppendLine("                    guidToId[tag.Guid] = id;");
        sb.AppendLine("                usedIds.Add(tag.ManualId);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // Get auto-assign tags (already sorted by name from generator)");
        sb.AppendLine("        var autoTags = global::System.Linq.Enumerable.ToList(");
        sb.AppendLine("            global::System.Linq.Enumerable.Where(tags, t => t.ManualId < 0));");
        sb.AppendLine();
        sb.AppendLine("        // Second pass: auto-assign IDs to remaining tags");
        sb.AppendLine("        int nextId = 0;");
        sb.AppendLine("        foreach (var tag in autoTags)");
        sb.AppendLine("        {");
        sb.AppendLine("            while (usedIds.Contains(nextId)) nextId++;");
        sb.AppendLine("            var id = new global::Paradise.ECS.TagId(nextId);");
        sb.AppendLine("            tag.SetId(id);");
        sb.AppendLine("            typeToId[tag.Type] = id;");
        sb.AppendLine("            if (tag.Guid != global::System.Guid.Empty)");
        sb.AppendLine("                guidToId[tag.Guid] = id;");
        sb.AppendLine("            usedIds.Add(nextId);");
        sb.AppendLine("            nextId++;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        s_typeToId = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(typeToId);");
        sb.AppendLine("        s_guidToId = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(guidToId);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the TagId for a given Type.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static global::Paradise.ECS.TagId GetId(global::System.Type type)");
        sb.AppendLine("    {");
        sb.AppendLine("        return s_typeToId!.TryGetValue(type, out var id) ? id : global::Paradise.ECS.TagId.Invalid;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Tries to get the TagId for a given Type.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool TryGetId(global::System.Type type, out global::Paradise.ECS.TagId id)");
        sb.AppendLine("    {");
        sb.AppendLine("        return s_typeToId!.TryGetValue(type, out id);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the TagId for a given GUID.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static global::Paradise.ECS.TagId GetId(global::System.Guid guid)");
        sb.AppendLine("    {");
        sb.AppendLine("        return s_guidToId!.TryGetValue(guid, out var id) ? id : global::Paradise.ECS.TagId.Invalid;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Tries to get the TagId for a given GUID.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool TryGetId(global::System.Guid guid, out global::Paradise.ECS.TagId id)");
        sb.AppendLine("    {");
        sb.AppendLine("        return s_guidToId!.TryGetValue(guid, out id);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("TagRegistry.g.cs", sb.ToString());
    }

    private static void GeneratePartialStruct(SourceProductionContext context, TagInfo tag)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable CA1708 // Identifiers should differ by more than case");
        sb.AppendLine();

        // Open namespace if present
        if (tag.Namespace != null)
        {
            sb.AppendLine($"namespace {tag.Namespace};");
            sb.AppendLine();
        }

        // Open containing types if nested
        var indent = "";
        foreach (var containingType in tag.ContainingTypes)
        {
            sb.AppendLine($"{indent}partial {containingType.Keyword} {containingType.Name}");
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        // Generate the partial struct implementing ITag
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

        // Close containing types
        for (int i = tag.ContainingTypes.Length - 1; i >= 0; i--)
        {
            indent = new string(' ', i * 4);
            sb.AppendLine($"{indent}}}");
        }

        // Generate a unique filename based on fully qualified name
        var filename = "Tag_" + tag.FullyQualifiedName.Replace(".", "_").Replace("+", "_") + ".g.cs";
        context.AddSource(filename, sb.ToString());
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
}
