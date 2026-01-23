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
    private const string SuppressGlobalUsingsAttributeFullName = "Paradise.ECS.SuppressGlobalUsingsAttribute";

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

        // Check for [assembly: SuppressGlobalUsings] attribute
        var suppressGlobalUsings = context.CompilationProvider
            .Select(static (compilation, _) =>
            {
                return compilation.Assembly.GetAttributes()
                    .Any(a => a.AttributeClass?.ToDisplayString() == SuppressGlobalUsingsAttributeFullName);
            });

        // Collect all queryables with component count and suppress flag
        var collected = queryableTypes.Collect().Combine(componentCount).Combine(suppressGlobalUsings);

        context.RegisterSourceOutput(collected, static (ctx, data) =>
            GenerateQueryableCode(ctx, data.Left.Left, data.Left.Right, data.Right));
    }

    private static QueryableInfo? GetQueryableInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        // Verify it's a struct
        if (typeSymbol.TypeKind != TypeKind.Struct)
            return null;

        var fullyQualifiedName = GeneratorUtilities.GetFullyQualifiedName(typeSymbol);
        var isRefStruct = typeSymbol.IsRefLikeType;

        // Check if it's partial
        var isPartial = typeSymbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<StructDeclarationSyntax>()
            .Any(s => s.Modifiers.Any(m => m.Text == "partial"));

        var ns = GeneratorUtilities.GetNamespace(typeSymbol);
        var typeName = typeSymbol.Name;
        var containingTypes = GeneratorUtilities.GetContainingTypes(typeSymbol);

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
        var withComponentsAccess = new List<ComponentInfo>();
        var withoutComponents = new List<string>();
        var anyComponents = new List<string>();
        var optionalComponents = new List<ComponentInfo>();

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

                // Get simple type name (without namespace)
                var componentTypeName = typeArg.Name;

                string? attrType = null;

                // Match by checking if it ends with the expected attribute name pattern
                if (metadataName.StartsWith("Paradise.ECS.WithAttribute<", StringComparison.Ordinal))
                {
                    withComponents.Add(componentFullName);
                    attrType = "With";

                    // Extract Name, IsReadOnly, QueryOnly from named arguments
                    string? customName = null;
                    bool isReadOnly = false;
                    bool queryOnly = false;

                    foreach (var namedArg in attr.NamedArguments)
                    {
                        switch (namedArg.Key)
                        {
                            case "Name" when namedArg.Value.Value is string name:
                                customName = name;
                                break;
                            case "IsReadOnly" when namedArg.Value.Value is bool ro:
                                isReadOnly = ro;
                                break;
                            case "QueryOnly" when namedArg.Value.Value is bool qo:
                                queryOnly = qo;
                                break;
                        }
                    }

                    withComponentsAccess.Add(new ComponentInfo(
                        componentFullName, componentTypeName, customName, isReadOnly, queryOnly));
                }
                else if (metadataName.StartsWith("Paradise.ECS.WithoutAttribute<", StringComparison.Ordinal))
                {
                    withoutComponents.Add(componentFullName);
                    attrType = "Without";
                }
                else if (metadataName.StartsWith("Paradise.ECS.WithAnyAttribute<", StringComparison.Ordinal))
                {
                    anyComponents.Add(componentFullName);
                    attrType = "Any";
                }
                else if (metadataName.StartsWith("Paradise.ECS.OptionalAttribute<", StringComparison.Ordinal))
                {
                    attrType = "Optional";

                    // Extract Name, IsReadOnly from named arguments
                    string? customName = null;
                    bool isReadOnly = false;

                    foreach (var namedArg in attr.NamedArguments)
                    {
                        switch (namedArg.Key)
                        {
                            case "Name" when namedArg.Value.Value is string name:
                                customName = name;
                                break;
                            case "IsReadOnly" when namedArg.Value.Value is bool ro:
                                isReadOnly = ro;
                                break;
                        }
                    }

                    optionalComponents.Add(new ComponentInfo(
                        componentFullName, componentTypeName, customName, isReadOnly));
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
            withComponentsAccess.ToImmutableArray(),
            withoutComponents.ToImmutableArray(),
            anyComponents.ToImmutableArray(),
            optionalComponents.ToImmutableArray(),
            duplicates);
    }

    private static void GenerateQueryableCode(
        SourceProductionContext context,
        ImmutableArray<QueryableInfo> queryables,
        int componentCount,
        bool suppressGlobalUsings)
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

        // Detect duplicate manual IDs
        var manualIdGroups = validQueryables
            .Where(q => q.ManualId.HasValue)
            .GroupBy(q => q.ManualId!.Value)
            .Where(g => g.Count() > 1)
            .ToList();

        // Report diagnostics for duplicate manual IDs
        var duplicateManualIds = new HashSet<int>();
        foreach (var group in manualIdGroups)
        {
            duplicateManualIds.Add(group.Key);
            var typeNames = string.Join(", ", group.Select(q => q.FullyQualifiedName));
            // Report on the first occurrence's location
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DuplicateQueryableId,
                group.First().Location,
                group.Key,
                typeNames));
        }

        // Filter out queryables with duplicate manual IDs
        validQueryables = validQueryables
            .Where(q => !q.ManualId.HasValue || !duplicateManualIds.Contains(q.ManualId.Value))
            .ToList();
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
        GenerateQueryableRegistry(context, queryableWithIds, componentCount, suppressGlobalUsings);
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
        sb.AppendLine("using Paradise.ECS;");  // Required for extension methods like GetRef<T>
        sb.AppendLine();

        // Use block-scoped namespace for consistency (required since QueryBuilder and Query are in same file)
        var hasNamespace = queryable.Namespace != null;
        var baseIndent = hasNamespace ? "    " : "";

        if (hasNamespace)
        {
            sb.AppendLine($"namespace {queryable.Namespace}");
            sb.AppendLine("{");
        }

        // Open containing types if nested
        var indent = baseIndent;
        foreach (var containingType in queryable.ContainingTypes)
        {
            sb.AppendLine($"{indent}partial {containingType.Keyword} {containingType.Name}");
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        // Generate the partial ref struct with QueryableId and Query property
        sb.AppendLine($"{indent}partial struct {queryable.TypeName}");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    /// <summary>The unique queryable type ID.</summary>");
        sb.AppendLine($"{indent}    public static int QueryableId => {typeId};");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>Gets the query builder for this queryable type.</summary>");
        sb.AppendLine($"{indent}    public static {queryable.HelperStructPrefix}QueryBuilder Query => default;");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>Gets the chunk query builder for batch component access.</summary>");
        sb.AppendLine($"{indent}    public static {queryable.HelperStructPrefix}ChunkQueryBuilder ChunkQuery => default;");
        sb.AppendLine();

        // Generate nested Data<TMask, TRegistry, TConfig> struct
        GenerateNestedDataStruct(sb, queryable, indent + "    ");

        // Generate nested ChunkData<TMask, TRegistry, TConfig> struct
        GenerateNestedChunkDataStruct(sb, queryable, indent + "    ");

        sb.AppendLine($"{indent}}}");

        // Close containing types
        for (int i = queryable.ContainingTypes.Length - 1; i >= 0; i--)
        {
            indent = baseIndent + new string(' ', i * 4);
            sb.AppendLine($"{indent}}}");
        }

        // Generate QueryBuilder and Query structs at namespace level (inside the same namespace block)
        GenerateQueryBuilderStruct(sb, queryable, baseIndent);
        GenerateTypedQueryStruct(sb, queryable, baseIndent);

        // Generate ChunkQueryBuilder and ChunkQuery structs
        GenerateChunkQueryBuilderStruct(sb, queryable, baseIndent);
        GenerateTypedChunkQueryStruct(sb, queryable, baseIndent);

        // Close namespace
        if (hasNamespace)
        {
            sb.AppendLine("}");
        }

        // Generate filename
        var filename = "Queryable_" + queryable.FullyQualifiedName.Replace(".", "_").Replace("+", "_") + ".g.cs";
        context.AddSource(filename, sb.ToString());
    }

    private static void GenerateNestedDataStruct(StringBuilder sb, QueryableInfo queryable, string indent)
    {
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Iteration data providing component access. Returned by query enumeration.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <typeparam name=\"TMask\">The component mask type implementing IBitSet.</typeparam>");
        sb.AppendLine($"{indent}/// <typeparam name=\"TRegistry\">The component registry type.</typeparam>");
        sb.AppendLine($"{indent}/// <typeparam name=\"TConfig\">The world configuration type.</typeparam>");
        sb.AppendLine($"{indent}public readonly ref struct Data<TMask, TRegistry, TConfig>");
        sb.AppendLine($"{indent}    where TMask : unmanaged, global::Paradise.ECS.IBitSet<TMask>");
        sb.AppendLine($"{indent}    where TRegistry : global::Paradise.ECS.IComponentRegistry");
        sb.AppendLine($"{indent}    where TConfig : global::Paradise.ECS.IConfig, new()");
        sb.AppendLine($"{indent}{{");

        // Generate private fields
        sb.AppendLine($"{indent}    private readonly global::Paradise.ECS.ChunkManager _chunkManager;");
        sb.AppendLine($"{indent}    private readonly global::Paradise.ECS.ImmutableArchetypeLayout<TMask, TRegistry, TConfig> _layout;");
        sb.AppendLine($"{indent}    private readonly global::Paradise.ECS.ChunkHandle _chunk;");
        sb.AppendLine($"{indent}    private readonly int _indexInChunk;");
        sb.AppendLine();

        // Generate internal constructor
        sb.AppendLine($"{indent}    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}    internal Data(");
        sb.AppendLine($"{indent}        global::Paradise.ECS.ChunkManager chunkManager,");
        sb.AppendLine($"{indent}        global::Paradise.ECS.ImmutableArchetypeLayout<TMask, TRegistry, TConfig> layout,");
        sb.AppendLine($"{indent}        global::Paradise.ECS.ChunkHandle chunk,");
        sb.AppendLine($"{indent}        int indexInChunk)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        _chunkManager = chunkManager;");
        sb.AppendLine($"{indent}        _layout = layout;");
        sb.AppendLine($"{indent}        _chunk = chunk;");
        sb.AppendLine($"{indent}        _indexInChunk = indexInChunk;");
        sb.AppendLine($"{indent}    }}");

        // Generate component properties for With<T> components (unless QueryOnly)
        foreach (var comp in queryable.WithComponentsAccess)
        {
            if (comp.QueryOnly)
                continue;

            sb.AppendLine();
            var refType = comp.IsReadOnly ? "ref readonly" : "ref";
            sb.AppendLine($"{indent}    /// <summary>Gets a {(comp.IsReadOnly ? "read-only " : "")}reference to the {comp.ComponentTypeName} component.</summary>");
            sb.AppendLine($"{indent}    public {refType} global::{comp.ComponentFullName} {comp.PropertyName}");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"{indent}        get");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            int offset = _layout.GetEntityComponentOffset<global::{comp.ComponentFullName}>(_indexInChunk);");
            sb.AppendLine($"{indent}            return ref _chunkManager.GetBytes(_chunk).GetRef<global::{comp.ComponentFullName}>(offset);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
        }

        // Generate HasXxx property and GetXxx() method for Optional<T> components
        foreach (var opt in queryable.OptionalComponents)
        {
            sb.AppendLine();
            // HasXxx property
            sb.AppendLine($"{indent}    /// <summary>Gets whether the {opt.ComponentTypeName} component is present.</summary>");
            sb.AppendLine($"{indent}    public bool Has{opt.PropertyName}");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"{indent}        get => _layout.HasComponent<global::{opt.ComponentFullName}>();");
            sb.AppendLine($"{indent}    }}");

            sb.AppendLine();
            // GetXxx() method
            var refType = opt.IsReadOnly ? "ref readonly" : "ref";
            sb.AppendLine($"{indent}    /// <summary>Gets a {(opt.IsReadOnly ? "read-only " : "")}reference to the {opt.ComponentTypeName} component.</summary>");
            sb.AppendLine($"{indent}    /// <exception cref=\"global::System.InvalidOperationException\">Thrown when the component is not present. Check Has{opt.PropertyName} first.</exception>");
            sb.AppendLine($"{indent}    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"{indent}    public {refType} global::{opt.ComponentFullName} Get{opt.PropertyName}()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        int offset = _layout.GetEntityComponentOffset<global::{opt.ComponentFullName}>(_indexInChunk);");
            sb.AppendLine($"{indent}        if (offset < 0)");
            sb.AppendLine($"{indent}            throw new global::System.InvalidOperationException(\"Optional component {opt.ComponentTypeName} is not present. Check Has{opt.PropertyName} before calling Get{opt.PropertyName}().\");");
            sb.AppendLine($"{indent}        return ref _chunkManager.GetBytes(_chunk).GetRef<global::{opt.ComponentFullName}>(offset);");
            sb.AppendLine($"{indent}    }}");
        }

        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateQueryBuilderStruct(StringBuilder sb, QueryableInfo queryable, string indent)
    {
        var prefix = queryable.HelperStructPrefix;
        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Query builder for {queryable.TypeName}. Builds a typed query that returns {queryable.TypeName}.Data instances.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}public readonly struct {prefix}QueryBuilder");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    /// <summary>Builds a typed query for the specified world.</summary>");
        sb.AppendLine($"{indent}    /// <typeparam name=\"TMask\">The component mask type implementing IBitSet.</typeparam>");
        sb.AppendLine($"{indent}    /// <typeparam name=\"TRegistry\">The component registry type.</typeparam>");
        sb.AppendLine($"{indent}    /// <typeparam name=\"TConfig\">The world configuration type.</typeparam>");
        sb.AppendLine($"{indent}    /// <param name=\"world\">The world to query.</param>");
        sb.AppendLine($"{indent}    /// <returns>A typed query that iterates over {queryable.TypeName}.Data instances.</returns>");
        sb.AppendLine($"{indent}    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}    public {prefix}Query<TMask, TRegistry, TConfig> Build<TMask, TRegistry, TConfig>(");
        sb.AppendLine($"{indent}        global::Paradise.ECS.World<TMask, TRegistry, TConfig> world)");
        sb.AppendLine($"{indent}        where TMask : unmanaged, global::Paradise.ECS.IBitSet<TMask>");
        sb.AppendLine($"{indent}        where TRegistry : global::Paradise.ECS.IComponentRegistry");
        sb.AppendLine($"{indent}        where TConfig : global::Paradise.ECS.IConfig, new()");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        var hashedDescription = global::Paradise.ECS.QueryableRegistry<TMask>.Descriptions[{queryable.TypeName}.QueryableId];");
        sb.AppendLine($"{indent}        var query = world.Registry.GetOrCreateQuery(hashedDescription);");
        sb.AppendLine($"{indent}        return new {prefix}Query<TMask, TRegistry, TConfig>(world, query);");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateTypedQueryStruct(StringBuilder sb, QueryableInfo queryable, string indent)
    {
        var prefix = queryable.HelperStructPrefix;
        sb.AppendLine();
        var dataType = $"{queryable.TypeName}.Data<TMask, TRegistry, TConfig>";

        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Typed query that returns {queryable.TypeName}.Data instances during iteration.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <typeparam name=\"TMask\">The component mask type implementing IBitSet.</typeparam>");
        sb.AppendLine($"{indent}/// <typeparam name=\"TRegistry\">The component registry type.</typeparam>");
        sb.AppendLine($"{indent}/// <typeparam name=\"TConfig\">The world configuration type.</typeparam>");
        sb.AppendLine($"{indent}public readonly struct {prefix}Query<TMask, TRegistry, TConfig>");
        sb.AppendLine($"{indent}    where TMask : unmanaged, global::Paradise.ECS.IBitSet<TMask>");
        sb.AppendLine($"{indent}    where TRegistry : global::Paradise.ECS.IComponentRegistry");
        sb.AppendLine($"{indent}    where TConfig : global::Paradise.ECS.IConfig, new()");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    private readonly global::Paradise.ECS.ChunkManager _chunkManager;");
        sb.AppendLine($"{indent}    private readonly global::Paradise.ECS.Query<TMask, TRegistry, TConfig, global::Paradise.ECS.Archetype<TMask, TRegistry, TConfig>> _query;");
        sb.AppendLine();
        sb.AppendLine($"{indent}    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}    internal {prefix}Query(");
        sb.AppendLine($"{indent}        global::Paradise.ECS.World<TMask, TRegistry, TConfig> world,");
        sb.AppendLine($"{indent}        global::Paradise.ECS.Query<TMask, TRegistry, TConfig, global::Paradise.ECS.Archetype<TMask, TRegistry, TConfig>> query)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        _chunkManager = world.ChunkManager;");
        sb.AppendLine($"{indent}        _query = query;");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>Gets the total number of entities matching this query.</summary>");
        sb.AppendLine($"{indent}    public int EntityCount");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}        get => _query.EntityCount;");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>Gets whether this query has any matching entities.</summary>");
        sb.AppendLine($"{indent}    public bool IsEmpty");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}        get => _query.IsEmpty;");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>Returns an enumerator that iterates through all entities in the matching archetypes.</summary>");
        sb.AppendLine($"{indent}    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}    public Enumerator GetEnumerator() => new Enumerator(_chunkManager, _query);");
        sb.AppendLine();

        // Generate nested Enumerator struct
        sb.AppendLine($"{indent}    /// <summary>Enumerator for iterating over {queryable.TypeName}.Data instances.</summary>");
        sb.AppendLine($"{indent}    public ref struct Enumerator");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        private readonly global::Paradise.ECS.ChunkManager _chunkManager;");
        sb.AppendLine($"{indent}        private global::Paradise.ECS.Query<TMask, TRegistry, TConfig, global::Paradise.ECS.Archetype<TMask, TRegistry, TConfig>>.ChunkEnumerator _chunkEnumerator;");
        sb.AppendLine($"{indent}        private global::Paradise.ECS.ImmutableArchetypeLayout<TMask, TRegistry, TConfig> _currentLayout;");
        sb.AppendLine($"{indent}        private global::Paradise.ECS.ChunkHandle _currentChunk;");
        sb.AppendLine($"{indent}        private int _indexInChunk;");
        sb.AppendLine($"{indent}        private int _entitiesInChunk;");
        sb.AppendLine();
        sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}        internal Enumerator(");
        sb.AppendLine($"{indent}            global::Paradise.ECS.ChunkManager chunkManager,");
        sb.AppendLine($"{indent}            global::Paradise.ECS.Query<TMask, TRegistry, TConfig, global::Paradise.ECS.Archetype<TMask, TRegistry, TConfig>> query)");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            _chunkManager = chunkManager;");
        sb.AppendLine($"{indent}            _chunkEnumerator = query.Chunks.GetEnumerator();");
        sb.AppendLine($"{indent}            _currentLayout = default;");
        sb.AppendLine($"{indent}            _currentChunk = default;");
        sb.AppendLine($"{indent}            _indexInChunk = -1;");
        sb.AppendLine($"{indent}            _entitiesInChunk = 0;");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}        /// <summary>Gets the current {queryable.TypeName}.Data.</summary>");
        sb.AppendLine($"{indent}        public {dataType} Current");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}            get => new {dataType}(_chunkManager, _currentLayout, _currentChunk, _indexInChunk);");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}        /// <summary>Advances to the next entity.</summary>");
        sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}        public bool MoveNext()");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            _indexInChunk++;");
        sb.AppendLine($"{indent}            while (_indexInChunk >= _entitiesInChunk)");
        sb.AppendLine($"{indent}            {{");
        sb.AppendLine($"{indent}                if (!_chunkEnumerator.MoveNext()) return false;");
        sb.AppendLine($"{indent}                var info = _chunkEnumerator.Current;");
        sb.AppendLine($"{indent}                _currentLayout = info.Archetype.Layout;");
        sb.AppendLine($"{indent}                _currentChunk = info.Handle;");
        sb.AppendLine($"{indent}                _entitiesInChunk = info.EntityCount;");
        sb.AppendLine($"{indent}                _indexInChunk = 0;");
        sb.AppendLine($"{indent}            }}");
        sb.AppendLine($"{indent}            return true;");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateNestedChunkDataStruct(StringBuilder sb, QueryableInfo queryable, string indent)
    {
        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Chunk data providing span-based component access for batch processing.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <typeparam name=\"TMask\">The component mask type implementing IBitSet.</typeparam>");
        sb.AppendLine($"{indent}/// <typeparam name=\"TRegistry\">The component registry type.</typeparam>");
        sb.AppendLine($"{indent}/// <typeparam name=\"TConfig\">The world configuration type.</typeparam>");
        sb.AppendLine($"{indent}public readonly ref struct ChunkData<TMask, TRegistry, TConfig>");
        sb.AppendLine($"{indent}    where TMask : unmanaged, global::Paradise.ECS.IBitSet<TMask>");
        sb.AppendLine($"{indent}    where TRegistry : global::Paradise.ECS.IComponentRegistry");
        sb.AppendLine($"{indent}    where TConfig : global::Paradise.ECS.IConfig, new()");
        sb.AppendLine($"{indent}{{");

        // Generate private fields
        sb.AppendLine($"{indent}    private readonly global::Paradise.ECS.ChunkManager _chunkManager;");
        sb.AppendLine($"{indent}    private readonly global::Paradise.ECS.ImmutableArchetypeLayout<TMask, TRegistry, TConfig> _layout;");
        sb.AppendLine($"{indent}    private readonly global::Paradise.ECS.ChunkHandle _chunk;");
        sb.AppendLine($"{indent}    private readonly int _entityCount;");
        sb.AppendLine();

        // Generate internal constructor
        sb.AppendLine($"{indent}    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}    internal ChunkData(");
        sb.AppendLine($"{indent}        global::Paradise.ECS.ChunkManager chunkManager,");
        sb.AppendLine($"{indent}        global::Paradise.ECS.ImmutableArchetypeLayout<TMask, TRegistry, TConfig> layout,");
        sb.AppendLine($"{indent}        global::Paradise.ECS.ChunkHandle chunk,");
        sb.AppendLine($"{indent}        int entityCount)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        _chunkManager = chunkManager;");
        sb.AppendLine($"{indent}        _layout = layout;");
        sb.AppendLine($"{indent}        _chunk = chunk;");
        sb.AppendLine($"{indent}        _entityCount = entityCount;");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();

        // EntityCount property
        sb.AppendLine($"{indent}    /// <summary>Gets the number of entities in this chunk.</summary>");
        sb.AppendLine($"{indent}    public int EntityCount");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}        get => _entityCount;");
        sb.AppendLine($"{indent}    }}");

        // Generate span properties for With<T> components (unless QueryOnly)
        foreach (var comp in queryable.WithComponentsAccess)
        {
            if (comp.QueryOnly)
                continue;

            sb.AppendLine();
            // Pluralize property name for span (simple pluralization)
            var spanPropertyName = comp.PropertyName + "s";
            var spanType = comp.IsReadOnly ? "ReadOnlySpan" : "Span";
            sb.AppendLine($"{indent}    /// <summary>Gets a {(comp.IsReadOnly ? "read-only " : "")}span over all {comp.ComponentTypeName} components in this chunk.</summary>");
            sb.AppendLine($"{indent}    public global::System.{spanType}<global::{comp.ComponentFullName}> {spanPropertyName}");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"{indent}        get");
            sb.AppendLine($"{indent}        {{");
            sb.AppendLine($"{indent}            int baseOffset = _layout.GetBaseOffset<global::{comp.ComponentFullName}>();");
            sb.AppendLine($"{indent}            return _chunkManager.GetBytes(_chunk).GetSpan<global::{comp.ComponentFullName}>(baseOffset, _entityCount);");
            sb.AppendLine($"{indent}        }}");
            sb.AppendLine($"{indent}    }}");
        }

        // Generate Has property and GetXxxSpan() method for Optional<T> components
        foreach (var opt in queryable.OptionalComponents)
        {
            sb.AppendLine();
            // HasXxx property
            sb.AppendLine($"{indent}    /// <summary>Gets whether this chunk's archetype has the {opt.ComponentTypeName} component.</summary>");
            sb.AppendLine($"{indent}    public bool Has{opt.PropertyName}");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"{indent}        get => _layout.HasComponent<global::{opt.ComponentFullName}>();");
            sb.AppendLine($"{indent}    }}");

            sb.AppendLine();
            // GetXxxSpan() method - pluralize name
            var spanMethodName = "Get" + opt.PropertyName + "s";
            var optSpanType = opt.IsReadOnly ? "ReadOnlySpan" : "Span";
            sb.AppendLine($"{indent}    /// <summary>Gets a {(opt.IsReadOnly ? "read-only " : "")}span over all {opt.ComponentTypeName} components in this chunk.</summary>");
            sb.AppendLine($"{indent}    /// <exception cref=\"global::System.InvalidOperationException\">Thrown when the component is not present. Check Has{opt.PropertyName} first.</exception>");
            sb.AppendLine($"{indent}    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"{indent}    public global::System.{optSpanType}<global::{opt.ComponentFullName}> {spanMethodName}()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        int baseOffset = _layout.GetBaseOffset<global::{opt.ComponentFullName}>();");
            sb.AppendLine($"{indent}        if (baseOffset < 0)");
            sb.AppendLine($"{indent}            throw new global::System.InvalidOperationException(\"Optional component {opt.ComponentTypeName} is not present. Check Has{opt.PropertyName} before calling {spanMethodName}().\");");
            sb.AppendLine($"{indent}        return _chunkManager.GetBytes(_chunk).GetSpan<global::{opt.ComponentFullName}>(baseOffset, _entityCount);");
            sb.AppendLine($"{indent}    }}");
        }

        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateChunkQueryBuilderStruct(StringBuilder sb, QueryableInfo queryable, string indent)
    {
        var prefix = queryable.HelperStructPrefix;
        sb.AppendLine();
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Chunk query builder for {queryable.TypeName}. Builds a typed chunk query for batch component access.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}public readonly struct {prefix}ChunkQueryBuilder");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    /// <summary>Builds a typed chunk query for the specified world.</summary>");
        sb.AppendLine($"{indent}    /// <typeparam name=\"TMask\">The component mask type implementing IBitSet.</typeparam>");
        sb.AppendLine($"{indent}    /// <typeparam name=\"TRegistry\">The component registry type.</typeparam>");
        sb.AppendLine($"{indent}    /// <typeparam name=\"TConfig\">The world configuration type.</typeparam>");
        sb.AppendLine($"{indent}    /// <param name=\"world\">The world to query.</param>");
        sb.AppendLine($"{indent}    /// <returns>A typed chunk query that iterates over {queryable.TypeName}.ChunkData instances.</returns>");
        sb.AppendLine($"{indent}    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}    public {prefix}ChunkQuery<TMask, TRegistry, TConfig> Build<TMask, TRegistry, TConfig>(");
        sb.AppendLine($"{indent}        global::Paradise.ECS.World<TMask, TRegistry, TConfig> world)");
        sb.AppendLine($"{indent}        where TMask : unmanaged, global::Paradise.ECS.IBitSet<TMask>");
        sb.AppendLine($"{indent}        where TRegistry : global::Paradise.ECS.IComponentRegistry");
        sb.AppendLine($"{indent}        where TConfig : global::Paradise.ECS.IConfig, new()");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        var hashedDescription = global::Paradise.ECS.QueryableRegistry<TMask>.Descriptions[{queryable.TypeName}.QueryableId];");
        sb.AppendLine($"{indent}        var query = world.Registry.GetOrCreateQuery(hashedDescription);");
        sb.AppendLine($"{indent}        return new {prefix}ChunkQuery<TMask, TRegistry, TConfig>(world, query);");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateTypedChunkQueryStruct(StringBuilder sb, QueryableInfo queryable, string indent)
    {
        var prefix = queryable.HelperStructPrefix;
        sb.AppendLine();
        var chunkDataType = $"{queryable.TypeName}.ChunkData<TMask, TRegistry, TConfig>";

        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Typed chunk query that returns {queryable.TypeName}.ChunkData instances for batch processing.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}/// <typeparam name=\"TMask\">The component mask type implementing IBitSet.</typeparam>");
        sb.AppendLine($"{indent}/// <typeparam name=\"TRegistry\">The component registry type.</typeparam>");
        sb.AppendLine($"{indent}/// <typeparam name=\"TConfig\">The world configuration type.</typeparam>");
        sb.AppendLine($"{indent}public readonly struct {prefix}ChunkQuery<TMask, TRegistry, TConfig>");
        sb.AppendLine($"{indent}    where TMask : unmanaged, global::Paradise.ECS.IBitSet<TMask>");
        sb.AppendLine($"{indent}    where TRegistry : global::Paradise.ECS.IComponentRegistry");
        sb.AppendLine($"{indent}    where TConfig : global::Paradise.ECS.IConfig, new()");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    private readonly global::Paradise.ECS.ChunkManager _chunkManager;");
        sb.AppendLine($"{indent}    private readonly global::Paradise.ECS.Query<TMask, TRegistry, TConfig, global::Paradise.ECS.Archetype<TMask, TRegistry, TConfig>> _query;");
        sb.AppendLine();
        sb.AppendLine($"{indent}    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}    internal {prefix}ChunkQuery(");
        sb.AppendLine($"{indent}        global::Paradise.ECS.World<TMask, TRegistry, TConfig> world,");
        sb.AppendLine($"{indent}        global::Paradise.ECS.Query<TMask, TRegistry, TConfig, global::Paradise.ECS.Archetype<TMask, TRegistry, TConfig>> query)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        _chunkManager = world.ChunkManager;");
        sb.AppendLine($"{indent}        _query = query;");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>Gets the total number of entities matching this query.</summary>");
        sb.AppendLine($"{indent}    public int EntityCount");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}        get => _query.EntityCount;");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>Gets whether this query has any matching entities.</summary>");
        sb.AppendLine($"{indent}    public bool IsEmpty");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}        get => _query.IsEmpty;");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>Returns an enumerator that iterates through all chunks in the matching archetypes.</summary>");
        sb.AppendLine($"{indent}    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}    public Enumerator GetEnumerator() => new Enumerator(_chunkManager, _query);");
        sb.AppendLine();

        // Generate nested Enumerator struct
        sb.AppendLine($"{indent}    /// <summary>Enumerator for iterating over {queryable.TypeName}.ChunkData instances.</summary>");
        sb.AppendLine($"{indent}    public ref struct Enumerator");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        private readonly global::Paradise.ECS.ChunkManager _chunkManager;");
        sb.AppendLine($"{indent}        private global::Paradise.ECS.Query<TMask, TRegistry, TConfig, global::Paradise.ECS.Archetype<TMask, TRegistry, TConfig>>.ChunkEnumerator _chunkEnumerator;");
        sb.AppendLine();
        sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}        internal Enumerator(");
        sb.AppendLine($"{indent}            global::Paradise.ECS.ChunkManager chunkManager,");
        sb.AppendLine($"{indent}            global::Paradise.ECS.Query<TMask, TRegistry, TConfig, global::Paradise.ECS.Archetype<TMask, TRegistry, TConfig>> query)");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            _chunkManager = chunkManager;");
        sb.AppendLine($"{indent}            _chunkEnumerator = query.Chunks.GetEnumerator();");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}        /// <summary>Gets the current {queryable.TypeName}.ChunkData.</summary>");
        sb.AppendLine($"{indent}        public {chunkDataType} Current");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}            get");
        sb.AppendLine($"{indent}            {{");
        sb.AppendLine($"{indent}                var info = _chunkEnumerator.Current;");
        sb.AppendLine($"{indent}                return new {chunkDataType}(_chunkManager, info.Archetype.Layout, info.Handle, info.EntityCount);");
        sb.AppendLine($"{indent}            }}");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}        /// <summary>Advances to the next chunk.</summary>");
        sb.AppendLine($"{indent}        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}        public bool MoveNext() => _chunkEnumerator.MoveNext();");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateQueryableRegistry(
        SourceProductionContext context,
        List<(QueryableInfo Info, int TypeId)> queryables,
        int componentCount,
        bool suppressGlobalUsings)
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
        sb.AppendLine("/// <typeparam name=\"TMask\">The component mask type implementing IBitSet.</typeparam>");
        sb.AppendLine("public static class QueryableRegistry<TMask>");
        sb.AppendLine("    where TMask : unmanaged, global::Paradise.ECS.IBitSet<TMask>");
        sb.AppendLine("{");
        sb.AppendLine($"    private static readonly global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.HashedKey<global::Paradise.ECS.ImmutableQueryDescription<TMask>>> s_descriptions;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the query descriptions for all queryable types, indexed by QueryableId.");
        sb.AppendLine("    /// Descriptions are pre-wrapped in HashedKey for efficient lookup without re-computing hash.");
        sb.AppendLine("    /// Access All, None, Any masks via Description[id].Value.All/None/Any.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.HashedKey<global::Paradise.ECS.ImmutableQueryDescription<TMask>>> Descriptions => s_descriptions;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the total number of registered queryable types.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static int Count => {queryables.Count};");
        sb.AppendLine();
        sb.AppendLine("    static QueryableRegistry()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var descriptions = new global::Paradise.ECS.HashedKey<global::Paradise.ECS.ImmutableQueryDescription<TMask>>[{maxId + 1}];");
        sb.AppendLine();

        // Generate mask initialization for each queryable
        foreach (var (info, typeId) in queryables)
        {
            sb.AppendLine($"        // {info.FullyQualifiedName} (QueryableId = {typeId})");
            sb.Append($"        var allMask{typeId} = ");
            GenerateMask(sb, info.WithComponents);
            sb.AppendLine(";");
            sb.Append($"        var noneMask{typeId} = ");
            GenerateMask(sb, info.WithoutComponents);
            sb.AppendLine(";");
            sb.Append($"        var anyMask{typeId} = ");
            GenerateMask(sb, info.AnyComponents);
            sb.AppendLine(";");
            sb.AppendLine($"        descriptions[{typeId}] = (global::Paradise.ECS.HashedKey<global::Paradise.ECS.ImmutableQueryDescription<TMask>>)new global::Paradise.ECS.ImmutableQueryDescription<TMask>(allMask{typeId}, noneMask{typeId}, anyMask{typeId});");
            sb.AppendLine();
        }

        sb.AppendLine("        s_descriptions = global::System.Collections.Immutable.ImmutableArray.Create(descriptions);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("QueryableRegistry.g.cs", sb.ToString());

        // Generate type alias and module initializer in separate files
        GenerateQueryableAliases(context, componentCount, suppressGlobalUsings);
        GenerateModuleInitializer(context, queryables);
    }

    private static void GenerateQueryableAliases(SourceProductionContext context, int componentCount, bool suppressGlobalUsings)
    {
        var maskTypeFullyQualified = GeneratorUtilities.GetOptimalMaskType(componentCount);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine();

        if (suppressGlobalUsings)
        {
            sb.AppendLine("// All global usings suppressed by [assembly: SuppressGlobalUsings]");
            sb.AppendLine($"// To use QueryableRegistry, reference: global::Paradise.ECS.QueryableRegistry<{maskTypeFullyQualified}>");
        }
        else
        {
            sb.AppendLine("// Type alias for QueryableRegistry using the same mask type as components");
            sb.AppendLine($"global using QueryableRegistry = global::Paradise.ECS.QueryableRegistry<{maskTypeFullyQualified}>;");
        }

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
            sb.Append("TMask.Empty");
            return;
        }

        // Generate mask by ORing component TypeIds
        sb.Append("TMask.Empty");
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
        public ImmutableArray<ComponentInfo> WithComponentsAccess { get; }
        public ImmutableArray<string> WithoutComponents { get; }
        public ImmutableArray<string> AnyComponents { get; }
        public ImmutableArray<ComponentInfo> OptionalComponents { get; }
        public ImmutableArray<(string Component, List<string> Attributes)> DuplicateComponents { get; }

        public bool HasDuplicates => !DuplicateComponents.IsEmpty;

        /// <summary>
        /// Gets the unique helper struct name prefix that includes containing type names.
        /// For nested types like A.B.Player, returns "ABPlayer".
        /// For non-nested types like Player, returns "Player".
        /// </summary>
        public string HelperStructPrefix
        {
            get
            {
                if (ContainingTypes.IsEmpty)
                    return TypeName;

                var sb = new StringBuilder();
                foreach (var containingType in ContainingTypes)
                {
                    sb.Append(containingType.Name);
                }
                sb.Append(TypeName);
                return sb.ToString();
            }
        }

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
            ImmutableArray<ComponentInfo> withComponentsAccess,
            ImmutableArray<string> withoutComponents,
            ImmutableArray<string> anyComponents,
            ImmutableArray<ComponentInfo> optionalComponents,
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
            WithComponentsAccess = withComponentsAccess;
            WithoutComponents = withoutComponents;
            AnyComponents = anyComponents;
            OptionalComponents = optionalComponents;
            DuplicateComponents = duplicateComponents;
        }
    }

    /// <summary>
    /// Information about a component access from With&lt;T&gt; or Optional&lt;T&gt; attribute.
    /// </summary>
    private readonly struct ComponentInfo
    {
        /// <summary>Fully qualified component type name.</summary>
        public string ComponentFullName { get; }

        /// <summary>Simple type name (without namespace).</summary>
        public string ComponentTypeName { get; }

        /// <summary>Property name (Name ?? ComponentTypeName).</summary>
        public string PropertyName { get; }

        /// <summary>If true, generates ref readonly property/method.</summary>
        public bool IsReadOnly { get; }

        /// <summary>If true, component used only for filtering, no property generated. Only applicable to With components.</summary>
        public bool QueryOnly { get; }

        public ComponentInfo(
            string componentFullName,
            string componentTypeName,
            string? customName,
            bool isReadOnly,
            bool queryOnly = false)
        {
            ComponentFullName = componentFullName;
            ComponentTypeName = componentTypeName;
            PropertyName = customName ?? componentTypeName;
            IsReadOnly = isReadOnly;
            QueryOnly = queryOnly;
        }
    }
}
