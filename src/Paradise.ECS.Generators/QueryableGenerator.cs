using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paradise.ECS.Generators;

/// <summary>
/// Source generator that processes types marked with [Queryable] attribute.
/// Generates QueryableRegistry with static arrays for query descriptions and component masks.
/// </summary>
[Generator]
public class QueryableGenerator : IIncrementalGenerator
{
    private const string QueryableAttributeFullName = "Paradise.ECS.QueryableAttribute";
    private const string ComponentAttributeFullName = "Paradise.ECS.ComponentAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all ref structs with [Queryable] attribute
        var queryableTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                QueryableAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => GetQueryableInfo(ctx))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!.Value);

        // Count components to determine bit type
        var componentCount = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ComponentAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => 1)
            .Collect()
            .Select(static (components, _) => components.Length);

        // Collect all queryables with component count
        var collected = queryableTypes.Collect().Combine(componentCount);

        context.RegisterSourceOutput(collected, static (ctx, data) =>
            GenerateQueryableCode(ctx, data.Left, data.Right));
    }

    private static QueryableInfo? GetQueryableInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        // Verify it's a struct
        if (typeSymbol.TypeKind != TypeKind.Struct)
            return null;

        // Get fully qualified name for sorting
        var fullyQualifiedName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fullyQualifiedName.StartsWith("global::", StringComparison.Ordinal))
            fullyQualifiedName = fullyQualifiedName.Substring(8);

        // Check if it's a ref struct
        var isRefStruct = typeSymbol.IsRefLikeType;

        // Check if it's partial
        var isPartial = typeSymbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<StructDeclarationSyntax>()
            .Any(s => s.Modifiers.Any(m => m.Text == "partial"));

        // Get namespace
        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        // Get type name and containing types for nested queryables
        var typeName = typeSymbol.Name;
        var containingTypesList = new List<ContainingTypeInfo>();
        var parent = typeSymbol.ContainingType;
        while (parent != null)
        {
            var keyword = parent.TypeKind switch
            {
                TypeKind.Class => parent.IsRecord ? "record class" : "class",
                TypeKind.Struct => parent.IsRecord ? "record struct" : "struct",
                TypeKind.Interface => "interface",
                _ => "struct"
            };
            containingTypesList.Add(new ContainingTypeInfo(parent.Name, keyword));
            parent = parent.ContainingType;
        }
        containingTypesList.Reverse();
        var containingTypes = containingTypesList.ToImmutableArray();

        // Get optional manual Id from [Queryable(Id = X)]
        int? manualId = null;
        foreach (var attr in context.Attributes)
        {
            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "Id" && namedArg.Value.Value is int idValue && idValue >= 0)
                {
                    manualId = idValue;
                }
            }
        }

        // Collect component constraints from attributes
        // Track component -> list of attribute types for duplicate detection
        var componentUsages = new Dictionary<string, List<string>>();
        var withComponents = new List<string>();
        var withoutComponents = new List<string>();
        var anyComponents = new List<string>();

        foreach (var attr in typeSymbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null) continue;

            // Check if it's a generic attribute
            if (attrClass.IsGenericType && attrClass.OriginalDefinition is { } originalDef)
            {
                var metadataName = originalDef.ToDisplayString();
                var typeArg = attrClass.TypeArguments.FirstOrDefault();
                if (typeArg is null) continue;

                var componentFullName = typeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (componentFullName.StartsWith("global::", StringComparison.Ordinal))
                    componentFullName = componentFullName.Substring(8);

                string? attrType = null;

                // Match by checking if it ends with the expected attribute name pattern
                if (metadataName.StartsWith("Paradise.ECS.WithAttribute<", StringComparison.Ordinal))
                {
                    withComponents.Add(componentFullName);
                    attrType = "With";
                }
                else if (metadataName.StartsWith("Paradise.ECS.WithoutAttribute<", StringComparison.Ordinal))
                {
                    withoutComponents.Add(componentFullName);
                    attrType = "Without";
                }
                else if (metadataName.StartsWith("Paradise.ECS.AnyAttribute<", StringComparison.Ordinal))
                {
                    anyComponents.Add(componentFullName);
                    attrType = "Any";
                }

                // Track usage for duplicate detection
                if (attrType != null)
                {
                    if (!componentUsages.TryGetValue(componentFullName, out var usages))
                    {
                        usages = new List<string>();
                        componentUsages[componentFullName] = usages;
                    }
                    usages.Add(attrType);
                }
            }
        }

        // Find duplicates
        var duplicates = componentUsages
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => (Component: kvp.Key, Attributes: kvp.Value))
            .ToImmutableArray();

        return new QueryableInfo(
            fullyQualifiedName,
            typeSymbol.Locations.FirstOrDefault() ?? Location.None,
            isRefStruct,
            isPartial,
            ns,
            typeName,
            containingTypes,
            manualId,
            withComponents.ToImmutableArray(),
            withoutComponents.ToImmutableArray(),
            anyComponents.ToImmutableArray(),
            duplicates);
    }

    private static void GenerateQueryableCode(
        SourceProductionContext context,
        ImmutableArray<QueryableInfo> queryables,
        int componentCount)
    {
        if (queryables.IsEmpty)
            return;

        // Sort by fully qualified name for deterministic ID assignment
        var sorted = queryables
            .OrderBy(static q => q.FullyQualifiedName, StringComparer.Ordinal)
            .ToList();

        // Report diagnostics for invalid queryables
        foreach (var queryable in sorted)
        {
            if (!queryable.IsRefStruct)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.QueryableMustBeRefStruct,
                    queryable.Location,
                    queryable.FullyQualifiedName));
            }

            if (!queryable.IsPartial)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.QueryableMustBePartial,
                    queryable.Location,
                    queryable.FullyQualifiedName));
            }

            // Report duplicate component diagnostics
            foreach (var (component, attrs) in queryable.DuplicateComponents)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateComponentInQueryable,
                    queryable.Location,
                    component,
                    queryable.FullyQualifiedName,
                    string.Join(", ", attrs)));
            }
        }

        // Filter to valid queryables (must be ref struct, partial, and no duplicates)
        var validQueryables = sorted.Where(q => q.IsRefStruct && q.IsPartial && !q.HasDuplicates).ToList();
        if (validQueryables.Count == 0)
            return;

        // Assign IDs (manual first, then auto-assign)
        var manualIds = new HashSet<int>(validQueryables
            .Where(q => q.ManualId.HasValue)
            .Select(q => q.ManualId!.Value));

        var queryableWithIds = new List<(QueryableInfo Info, int TypeId)>();
        int nextAutoId = 0;

        foreach (var queryable in validQueryables)
        {
            if (queryable.ManualId.HasValue)
            {
                queryableWithIds.Add((queryable, queryable.ManualId.Value));
            }
            else
            {
                while (manualIds.Contains(nextAutoId)) nextAutoId++;
                queryableWithIds.Add((queryable, nextAutoId));
                nextAutoId++;
            }
        }

        // Generate partial struct implementations with TypeId
        foreach (var (info, typeId) in queryableWithIds)
        {
            GeneratePartialStruct(context, info, typeId);
        }

        // Generate QueryableRegistry
        GenerateQueryableRegistry(context, queryableWithIds, componentCount);
    }

    private static string GetOptimalBitStorageType(int componentCount)
    {
        if (componentCount <= 64) return "Bit64";
        if (componentCount <= 128) return "Bit128";
        if (componentCount <= 256) return "Bit256";
        if (componentCount <= 512) return "Bit512";
        if (componentCount <= 1024) return "Bit1024";

        // For >1024, calculate custom type name aligned to 256 bits (4 ulongs)
        var capacity = ((componentCount + 255) / 256) * 256;
        return $"Bit{capacity}";
    }

    private static void GeneratePartialStruct(
        SourceProductionContext context,
        QueryableInfo queryable,
        int typeId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Open namespace if present
        if (queryable.Namespace != null)
        {
            sb.AppendLine($"namespace {queryable.Namespace};");
            sb.AppendLine();
        }

        // Open containing types if nested
        var indent = "";
        foreach (var containingType in queryable.ContainingTypes)
        {
            sb.AppendLine($"{indent}partial {containingType.Keyword} {containingType.Name}");
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        // Generate the partial ref struct with QueryableId
        sb.AppendLine($"{indent}partial struct {queryable.TypeName}");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    /// <summary>The unique queryable type ID.</summary>");
        sb.AppendLine($"{indent}    public static int QueryableId => {typeId};");
        sb.AppendLine($"{indent}}}");

        // Close containing types
        for (int i = queryable.ContainingTypes.Length - 1; i >= 0; i--)
        {
            indent = new string(' ', i * 4);
            sb.AppendLine($"{indent}}}");
        }

        // Generate filename
        var filename = "Queryable_" + queryable.FullyQualifiedName.Replace(".", "_").Replace("+", "_") + ".g.cs";
        context.AddSource(filename, sb.ToString());
    }

    private static void GenerateQueryableRegistry(
        SourceProductionContext context,
        List<(QueryableInfo Info, int TypeId)> queryables,
        int componentCount)
    {
        // Find max ID
        int maxId = queryables.Max(q => q.TypeId);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Paradise.ECS;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Registry containing query descriptions and component masks for all queryable types.");
        sb.AppendLine("/// Indexed by queryable QueryableId for O(1) lookup.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"TBits\">The bit storage type for component masks.</typeparam>");
        sb.AppendLine("public static class QueryableRegistry<TBits>");
        sb.AppendLine("    where TBits : unmanaged, global::Paradise.ECS.IStorage");
        sb.AppendLine("{");
        sb.AppendLine($"    private static readonly global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ImmutableQueryDescription<TBits>> s_descriptions;");
        sb.AppendLine($"    private static readonly global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ImmutableBitSet<TBits>> s_allMasks;");
        sb.AppendLine($"    private static readonly global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ImmutableBitSet<TBits>> s_noneMasks;");
        sb.AppendLine($"    private static readonly global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ImmutableBitSet<TBits>> s_anyMasks;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the query descriptions for all queryable types, indexed by QueryableId.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ImmutableQueryDescription<TBits>> Descriptions => s_descriptions;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the All (required) component masks for all queryable types, indexed by QueryableId.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ImmutableBitSet<TBits>> AllMasks => s_allMasks;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the None (excluded) component masks for all queryable types, indexed by QueryableId.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ImmutableBitSet<TBits>> NoneMasks => s_noneMasks;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the Any (optional) component masks for all queryable types, indexed by QueryableId.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.ImmutableBitSet<TBits>> AnyMasks => s_anyMasks;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the total number of registered queryable types.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static int Count => {queryables.Count};");
        sb.AppendLine();
        sb.AppendLine("    static QueryableRegistry()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var descriptions = new global::Paradise.ECS.ImmutableQueryDescription<TBits>[{maxId + 1}];");
        sb.AppendLine($"        var allMasks = new global::Paradise.ECS.ImmutableBitSet<TBits>[{maxId + 1}];");
        sb.AppendLine($"        var noneMasks = new global::Paradise.ECS.ImmutableBitSet<TBits>[{maxId + 1}];");
        sb.AppendLine($"        var anyMasks = new global::Paradise.ECS.ImmutableBitSet<TBits>[{maxId + 1}];");
        sb.AppendLine();

        // Generate mask initialization for each queryable
        foreach (var (info, typeId) in queryables)
        {
            sb.AppendLine($"        // {info.FullyQualifiedName} (QueryableId = {typeId})");
            sb.Append($"        allMasks[{typeId}] = ");
            GenerateMask(sb, info.WithComponents);
            sb.AppendLine(";");
            sb.Append($"        noneMasks[{typeId}] = ");
            GenerateMask(sb, info.WithoutComponents);
            sb.AppendLine(";");
            sb.Append($"        anyMasks[{typeId}] = ");
            GenerateMask(sb, info.AnyComponents);
            sb.AppendLine(";");
            sb.AppendLine($"        descriptions[{typeId}] = new global::Paradise.ECS.ImmutableQueryDescription<TBits>(allMasks[{typeId}], noneMasks[{typeId}], anyMasks[{typeId}]);");
            sb.AppendLine();
        }

        sb.AppendLine("        s_descriptions = global::System.Collections.Immutable.ImmutableArray.Create(descriptions);");
        sb.AppendLine("        s_allMasks = global::System.Collections.Immutable.ImmutableArray.Create(allMasks);");
        sb.AppendLine("        s_noneMasks = global::System.Collections.Immutable.ImmutableArray.Create(noneMasks);");
        sb.AppendLine("        s_anyMasks = global::System.Collections.Immutable.ImmutableArray.Create(anyMasks);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("QueryableRegistry.g.cs", sb.ToString());

        // Generate type alias and module initializer in separate files
        GenerateQueryableAliases(context, componentCount);
        GenerateModuleInitializer(context, queryables);
    }

    private static void GenerateQueryableAliases(SourceProductionContext context, int componentCount)
    {
        var bitType = GetOptimalBitStorageType(componentCount);
        var bitTypeFullyQualified = $"global::Paradise.ECS.{bitType}";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine();
        sb.AppendLine("// Type alias for QueryableRegistry using the same bit type as components");
        sb.AppendLine($"global using QueryableRegistry = global::Paradise.ECS.QueryableRegistry<{bitTypeFullyQualified}>;");

        context.AddSource("QueryableAliases.g.cs", sb.ToString());
    }

    private static void GenerateModuleInitializer(
        SourceProductionContext context,
        List<(QueryableInfo Info, int TypeId)> queryables)
    {
        // Get namespace from first queryable for module initializer placement
        var firstQueryable = queryables.FirstOrDefault();
        var ns = firstQueryable.Info.Namespace ?? "Paradise.ECS";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Module initializer that ensures QueryableRegistry is initialized when the assembly loads.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static class QueryableRegistryInitializer");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Access Count to trigger static constructor");
        sb.AppendLine("        _ = QueryableRegistry.Count;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("QueryableRegistryInitializer.g.cs", sb.ToString());
    }

    private static void GenerateMask(StringBuilder sb, ImmutableArray<string> components)
    {
        if (components.IsEmpty)
        {
            sb.Append("global::Paradise.ECS.ImmutableBitSet<TBits>.Empty");
            return;
        }

        // Generate mask by ORing component TypeIds
        sb.Append("global::Paradise.ECS.ImmutableBitSet<TBits>.Empty");
        foreach (var component in components)
        {
            sb.Append($".Set(global::{component}.TypeId)");
        }
    }

    private readonly struct QueryableInfo
    {
        public string FullyQualifiedName { get; }
        public Location Location { get; }
        public bool IsRefStruct { get; }
        public bool IsPartial { get; }
        public string? Namespace { get; }
        public string TypeName { get; }
        public ImmutableArray<ContainingTypeInfo> ContainingTypes { get; }
        public int? ManualId { get; }
        public ImmutableArray<string> WithComponents { get; }
        public ImmutableArray<string> WithoutComponents { get; }
        public ImmutableArray<string> AnyComponents { get; }
        public ImmutableArray<(string Component, List<string> Attributes)> DuplicateComponents { get; }

        public bool HasDuplicates => !DuplicateComponents.IsEmpty;

        public QueryableInfo(
            string fullyQualifiedName,
            Location location,
            bool isRefStruct,
            bool isPartial,
            string? ns,
            string typeName,
            ImmutableArray<ContainingTypeInfo> containingTypes,
            int? manualId,
            ImmutableArray<string> withComponents,
            ImmutableArray<string> withoutComponents,
            ImmutableArray<string> anyComponents,
            ImmutableArray<(string Component, List<string> Attributes)> duplicateComponents)
        {
            FullyQualifiedName = fullyQualifiedName;
            Location = location;
            IsRefStruct = isRefStruct;
            IsPartial = isPartial;
            Namespace = ns;
            TypeName = typeName;
            ContainingTypes = containingTypes;
            ManualId = manualId;
            WithComponents = withComponents;
            WithoutComponents = withoutComponents;
            AnyComponents = anyComponents;
            DuplicateComponents = duplicateComponents;
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
