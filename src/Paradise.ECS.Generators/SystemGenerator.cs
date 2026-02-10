using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paradise.ECS.Generators;

/// <summary>
/// Source generator that processes ref partial structs implementing IEntitySystem or IChunkSystem.
/// Generates SystemId, constructor, RunChunk, SystemRegistry, and Schedule.
/// </summary>
[Generator]
public class SystemGenerator : IIncrementalGenerator
{
    private const string IEntitySystemFullName = "Paradise.ECS.IEntitySystem";
    private const string IChunkSystemFullName = "Paradise.ECS.IChunkSystem";
    private const string ComponentAttributeFullName = "Paradise.ECS.ComponentAttribute";
    private const string DefaultConfigAttributeFullName = "Paradise.ECS.DefaultConfigAttribute";
    private const string SuppressGlobalUsingsAttributeFullName = "Paradise.ECS.SuppressGlobalUsingsAttribute";
    private const string QueryableAttributeFullName = "Paradise.ECS.QueryableAttribute";
    private const string IConfigFullName = "Paradise.ECS.IConfig";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var systemTypes = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is StructDeclarationSyntax { BaseList: not null },
            transform: static (ctx, _) => GetSystemInfo(ctx))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!.Value);

        var componentCount = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ComponentAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => 1)
            .Collect()
            .Select(static (components, _) => components.Length);

        var defaultConfig = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                DefaultConfigAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax or ClassDeclarationSyntax,
                transform: static (ctx, _) => ExtractDefaultConfigFQN(ctx))
            .Where(static x => x is not null)
            .Collect();

        var suppressGlobalUsings = context.CompilationProvider
            .Select(static (compilation, _) =>
                compilation.Assembly.GetAttributes()
                    .Any(a => a.AttributeClass?.ToDisplayString() == SuppressGlobalUsingsAttributeFullName));

        // Collect [Queryable] types for resolving composition fields
        var queryableTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                QueryableAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => GetQueryableLookupInfo(ctx))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!.Value)
            .Collect();

        var collected = systemTypes.Collect()
            .Combine(componentCount)
            .Combine(defaultConfig)
            .Combine(suppressGlobalUsings)
            .Combine(queryableTypes);

        context.RegisterSourceOutput(collected, static (ctx, data) =>
            GenerateSystemCode(ctx,
                data.Left.Left.Left.Left,
                data.Left.Left.Left.Right,
                data.Left.Left.Right,
                data.Left.Right,
                data.Right));
    }

    // ===================== Discovery =====================

    private static SystemInfo? GetSystemInfo(GeneratorSyntaxContext context)
    {
        if (context.Node is not StructDeclarationSyntax structSyntax)
            return null;

        var symbol = context.SemanticModel.GetDeclaredSymbol(structSyntax);
        if (symbol is not INamedTypeSymbol typeSymbol)
            return null;

        SystemKind? kind = null;
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var fqn = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fqn == "global::" + IEntitySystemFullName) kind = SystemKind.Entity;
            else if (fqn == "global::" + IChunkSystemFullName) kind = SystemKind.Chunk;
        }

        if (kind == null) return null;

        var fullyQualifiedName = GeneratorUtilities.GetFullyQualifiedName(typeSymbol);
        var isRefStruct = typeSymbol.IsRefLikeType;
        var isPartial = structSyntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
        var ns = GeneratorUtilities.GetNamespace(typeSymbol);
        var typeName = typeSymbol.Name;
        var containingTypes = GeneratorUtilities.GetContainingTypes(typeSymbol);

        // Analyze fields
        var fields = new List<SystemFieldInfo>();
        bool hasInvalidFields = false;
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IFieldSymbol field || field.IsStatic) continue;

            var fieldInfo = AnalyzeField(field);
            if (fieldInfo == null)
            {
                // Store error type info for potential queryable resolution later
                bool fieldIsRef = field.RefKind != RefKind.None;
                bool fieldIsRefReadOnly = fieldIsRef && IsRefReadOnlySyntax(field);
                string? errorTypeName = field.Type is IErrorTypeSymbol ? field.Type.Name : null;

                hasInvalidFields = true;
                fields.Add(new SystemFieldInfo(
                    field.Name, FieldKind.Invalid, fieldIsRefReadOnly,
                    field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    null, ImmutableArray<QueryableComponentAccess>.Empty,
                    ImmutableArray<string>.Empty, ImmutableArray<string>.Empty,
                    isRefField: fieldIsRef, errorTypeName: errorTypeName));
            }
            else
            {
                fields.Add(fieldInfo.Value);
            }
        }

        // Read attributes on the system struct
        var afterSystems = new List<string>();
        var beforeSystems = new List<string>();
        var withoutComponents = new List<string>();
        var withAnyComponents = new List<string>();

        foreach (var attr in typeSymbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is not { IsGenericType: true }) continue;

            var metadataName = attrClass.OriginalDefinition.ToDisplayString();
            var typeArg = attrClass.TypeArguments.FirstOrDefault();
            if (typeArg is not INamedTypeSymbol typeArgNamed) continue;

            var typeArgFQN = GeneratorUtilities.GetFullyQualifiedName(typeArgNamed);

            if (metadataName.StartsWith("Paradise.ECS.AfterAttribute<", StringComparison.Ordinal))
                afterSystems.Add(typeArgFQN);
            else if (metadataName.StartsWith("Paradise.ECS.BeforeAttribute<", StringComparison.Ordinal))
                beforeSystems.Add(typeArgFQN);
            else if (metadataName.StartsWith("Paradise.ECS.WithoutAttribute<", StringComparison.Ordinal))
                withoutComponents.Add(typeArgFQN);
            else if (metadataName.StartsWith("Paradise.ECS.WithAnyAttribute<", StringComparison.Ordinal))
                withAnyComponents.Add(typeArgFQN);
        }

        return new SystemInfo(
            fullyQualifiedName,
            typeSymbol.Locations.FirstOrDefault() ?? Location.None,
            isRefStruct, isPartial, ns, typeName, containingTypes,
            kind.Value,
            fields.ToImmutableArray(),
            afterSystems.ToImmutableArray(),
            beforeSystems.ToImmutableArray(),
            withoutComponents.ToImmutableArray(),
            withAnyComponents.ToImmutableArray(),
            hasInvalidFields);
    }

    // ===================== Queryable Lookup =====================

    private static QueryableLookupInfo? GetQueryableLookupInfo(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var fqn = GeneratorUtilities.GetFullyQualifiedName(typeSymbol);
        var containingTypes = GeneratorUtilities.GetContainingTypes(typeSymbol);
        var prefix = containingTypes.IsEmpty
            ? typeSymbol.Name
            : string.Join("", containingTypes.Select(ct => ct.Name)) + typeSymbol.Name;

        // Collect With/Without/WithAny component info
        var withComponents = new List<QueryableComponentAccess>();
        var withoutComponents = new List<string>();
        var withAnyComponents = new List<string>();

        foreach (var attr in typeSymbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is not { IsGenericType: true }) continue;

            var origName = attrClass.OriginalDefinition.Name;
            var origNs = attrClass.OriginalDefinition.ContainingNamespace?.ToDisplayString();
            if (origNs != "Paradise.ECS") continue;

            var typeArg = attrClass.TypeArguments.FirstOrDefault();
            if (typeArg is not INamedTypeSymbol compType) continue;

            var compFQN = GeneratorUtilities.GetFullyQualifiedName(compType);

            if (origName == "WithAttribute")
            {
                bool isReadOnly = false;
                bool queryOnly = false;
                foreach (var arg in attr.NamedArguments)
                {
                    if (arg.Key == "IsReadOnly" && arg.Value.Value is bool ro) isReadOnly = ro;
                    if (arg.Key == "QueryOnly" && arg.Value.Value is bool qo) queryOnly = qo;
                }
                withComponents.Add(new QueryableComponentAccess(compFQN, isReadOnly, queryOnly));
            }
            else if (origName == "WithoutAttribute")
            {
                withoutComponents.Add(compFQN);
            }
            else if (origName == "WithAnyAttribute")
            {
                withAnyComponents.Add(compFQN);
            }
        }

        return new QueryableLookupInfo(
            prefix, fqn,
            withComponents.ToImmutableArray(),
            withoutComponents.ToImmutableArray(),
            withAnyComponents.ToImmutableArray());
    }

    /// <summary>
    /// Resolves Invalid fields that are ref to generated Queryable Data/ChunkData types.
    /// These appear as error types because QueryableGenerator output isn't visible to SystemGenerator.
    /// Matches by naming convention: {QueryablePrefix}Data → CompositionData, {QueryablePrefix}Chunk → CompositionChunkData.
    /// </summary>
    private static SystemInfo ResolveQueryableFields(SystemInfo sys, Dictionary<string, QueryableLookupInfo> queryableLookup)
    {
        bool anyResolved = false;
        var resolvedFields = new List<SystemFieldInfo>();

        foreach (var field in sys.Fields)
        {
            if (field.Kind != FieldKind.Invalid || field.ErrorTypeName == null)
            {
                resolvedFields.Add(field);
                continue;
            }

            var name = field.ErrorTypeName;
            QueryableLookupInfo? matched = null;
            FieldKind resolvedKind = FieldKind.Invalid;

            // Try {prefix}Entity → CompositionData (entity system)
            if (name.EndsWith("Entity", StringComparison.Ordinal) && name.Length > 6)
            {
                var prefix = name.Substring(0, name.Length - 6);
                if (queryableLookup.TryGetValue(prefix, out var info))
                {
                    matched = info;
                    resolvedKind = FieldKind.CompositionData;
                }
            }
            // Try {prefix}Chunk → CompositionChunkData (chunk system)
            else if (name.EndsWith("Chunk", StringComparison.Ordinal) && name.Length > 5)
            {
                var prefix = name.Substring(0, name.Length - 5);
                if (queryableLookup.TryGetValue(prefix, out var info))
                {
                    matched = info;
                    resolvedKind = FieldKind.CompositionChunkData;
                }
            }

            if (matched != null)
            {
                anyResolved = true;
                resolvedFields.Add(new SystemFieldInfo(
                    field.FieldName, resolvedKind, field.IsReadOnly,
                    field.TypeFQN, matched.Value.FQN,
                    matched.Value.WithComponents,
                    matched.Value.WithoutComponents,
                    matched.Value.WithAnyComponents,
                    isRefField: true));
            }
            else
            {
                resolvedFields.Add(field);
            }
        }

        if (!anyResolved) return sys;

        bool hasInvalidFields = resolvedFields.Any(f => f.Kind == FieldKind.Invalid);
        return new SystemInfo(
            sys.FullyQualifiedName, sys.Location,
            sys.IsRefStruct, sys.IsPartial,
            sys.Namespace, sys.TypeName, sys.ContainingTypes,
            sys.Kind, resolvedFields.ToImmutableArray(),
            sys.AfterSystems, sys.BeforeSystems,
            sys.WithoutComponents, sys.WithAnyComponents,
            hasInvalidFields);
    }

    // ===================== Field Analysis =====================

    private static SystemFieldInfo? AnalyzeField(IFieldSymbol field)
    {
        var fieldType = field.Type;
        bool isRef = field.RefKind != RefKind.None;
        bool isRefReadOnly = isRef && IsRefReadOnlySyntax(field);

        // ref T or ref readonly T where T has [Component] or [Tag] attribute → InlineComponent
        if (isRef && fieldType is INamedTypeSymbol refNamedType)
        {
            if (HasComponentOrTagAttribute(refNamedType))
            {
                return new SystemFieldInfo(
                    field.Name, FieldKind.InlineComponent, isRefReadOnly,
                    fieldType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    GeneratorUtilities.GetFullyQualifiedName(refNamedType),
                    ImmutableArray<QueryableComponentAccess>.Empty,
                    ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
            }
        }

        // Span<T> / ReadOnlySpan<T> where T has [Component] attribute → InlineSpan
        if (!isRef && fieldType is INamedTypeSymbol spanType && spanType.IsGenericType)
        {
            var origDef = spanType.OriginalDefinition;
            if (origDef.Name == "Span" &&
                origDef.ContainingNamespace?.ToDisplayString() == "System" &&
                spanType.TypeArguments[0] is INamedTypeSymbol spanArg &&
                HasComponentOrTagAttribute(spanArg))
            {
                return new SystemFieldInfo(
                    field.Name, FieldKind.InlineSpan, false,
                    fieldType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    GeneratorUtilities.GetFullyQualifiedName(spanArg),
                    ImmutableArray<QueryableComponentAccess>.Empty,
                    ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
            }

            if (origDef.Name == "ReadOnlySpan" &&
                origDef.ContainingNamespace?.ToDisplayString() == "System" &&
                spanType.TypeArguments[0] is INamedTypeSymbol rosArg &&
                HasComponentOrTagAttribute(rosArg))
            {
                return new SystemFieldInfo(
                    field.Name, FieldKind.InlineSpan, true,
                    fieldType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    GeneratorUtilities.GetFullyQualifiedName(rosArg),
                    ImmutableArray<QueryableComponentAccess>.Empty,
                    ImmutableArray<string>.Empty, ImmutableArray<string>.Empty);
            }
        }

        return null;
    }

    private static bool IsRefReadOnlySyntax(IFieldSymbol field)
    {
        foreach (var syntaxRef in field.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is VariableDeclaratorSyntax
                {
                    Parent: VariableDeclarationSyntax varDecl
                })
            {
                if (varDecl.Type is RefTypeSyntax refType)
                    return refType.ReadOnlyKeyword.IsKind(SyntaxKind.ReadOnlyKeyword);
            }
        }
        return false;
    }

    private static bool HasComponentOrTagAttribute(INamedTypeSymbol type)
    {
        foreach (var attr in type.GetAttributes())
        {
            var name = attr.AttributeClass?.ToDisplayString();
            if (name != null &&
                (name.StartsWith("Paradise.ECS.ComponentAttribute", StringComparison.Ordinal) ||
                 name.StartsWith("Paradise.ECS.TagAttribute", StringComparison.Ordinal)))
                return true;
        }
        return false;
    }

    private static string? ExtractDefaultConfigFQN(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol) return null;

        var iConfig = context.SemanticModel.Compilation.GetTypeByMetadataName(IConfigFullName);
        if (iConfig == null) return null;
        if (!typeSymbol.AllInterfaces.Contains(iConfig, SymbolEqualityComparer.Default)) return null;

        return GeneratorUtilities.GetFullyQualifiedName(typeSymbol);
    }

    // ===================== Code Generation =====================

    private static void GenerateSystemCode(
        SourceProductionContext context,
        ImmutableArray<SystemInfo> systems,
        int componentCount,
        ImmutableArray<string?> defaultConfigs,
        bool suppressGlobalUsings,
        ImmutableArray<QueryableLookupInfo> queryableTypes)
    {
        if (systems.IsEmpty) return;

        // Build queryable prefix lookup
        var queryableLookup = new Dictionary<string, QueryableLookupInfo>();
        foreach (var q in queryableTypes)
            queryableLookup[q.Prefix] = q;

        // Resolve error-type fields that match queryable patterns
        var sorted = systems
            .Select(s => ResolveQueryableFields(s, queryableLookup))
            .OrderBy(s => s.FullyQualifiedName, StringComparer.Ordinal)
            .ToList();

        // Determine concrete types
        var maskType = GeneratorUtilities.GetOptimalMaskType(componentCount);
        var configType = defaultConfigs.FirstOrDefault(c => c != null) ?? "Paradise.ECS.DefaultConfig";
        var configTypeFull = $"global::{configType}";

        // Validate
        foreach (var sys in sorted)
        {
            if (!sys.IsRefStruct)
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.SystemMustBeRefStruct, sys.Location, sys.FullyQualifiedName));
            if (!sys.IsPartial)
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.SystemMustBePartial, sys.Location, sys.FullyQualifiedName));

            foreach (var field in sys.Fields)
            {
                if (field.Kind == FieldKind.Invalid)
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.SystemInvalidFieldType, sys.Location, field.FieldName, sys.FullyQualifiedName, field.TypeFQN));

                // IChunkSystem with entity-mode fields
                if (sys.Kind == SystemKind.Chunk && field.Kind is FieldKind.InlineComponent or FieldKind.CompositionData)
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ChunkSystemHasEntityFields, sys.Location, field.FieldName, sys.FullyQualifiedName));

                // IEntitySystem with chunk-mode fields
                if (sys.Kind == SystemKind.Entity && field.Kind is FieldKind.InlineSpan or FieldKind.CompositionChunkData)
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.EntitySystemHasChunkFields, sys.Location, field.FieldName, sys.FullyQualifiedName));
            }
        }

        // Filter to valid systems
        var valid = sorted.Where(s => s.IsRefStruct && s.IsPartial && !s.HasInvalidFields).ToList();
        valid = valid.Where(s =>
        {
            foreach (var f in s.Fields)
            {
                if (s.Kind == SystemKind.Chunk && f.Kind is FieldKind.InlineComponent or FieldKind.CompositionData) return false;
                if (s.Kind == SystemKind.Entity && f.Kind is FieldKind.InlineSpan or FieldKind.CompositionChunkData) return false;
            }
            return true;
        }).ToList();

        if (valid.Count == 0) return;

        // Assign IDs (alphabetical, no manual IDs for systems currently)
        var systemsWithIds = new List<(SystemInfo Info, int SystemId)>();
        for (int i = 0; i < valid.Count; i++)
            systemsWithIds.Add((valid[i], i));

        // Compute component access for each system
        var accessMap = new Dictionary<string, ComponentAccess>();
        foreach (var (info, id) in systemsWithIds)
            accessMap[info.FullyQualifiedName] = ComputeComponentAccess(info);

        // Build DAG and waves
        var fqnToId = systemsWithIds.ToDictionary(s => s.Info.FullyQualifiedName, s => s.SystemId);
        var (waves, hasCycle, cycleMembers) = BuildDagAndWaves(systemsWithIds, accessMap, fqnToId);

        if (hasCycle)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.SystemCyclicDependency,
                systemsWithIds[0].Info.Location,
                string.Join(", ", cycleMembers)));
            return;
        }

        // Generate per-system partials
        foreach (var (info, id) in systemsWithIds)
        {
            GeneratePerSystemPartial(context, info, id, maskType, configTypeFull);
        }

        // Generate SystemRegistry
        GenerateSystemRegistry(context, systemsWithIds, accessMap, waves);

        // Generate Schedule
        GenerateSchedule(context, systemsWithIds, maskType, configTypeFull, suppressGlobalUsings);
    }

    // ===================== Component Access Computation =====================

    private static ComponentAccess ComputeComponentAccess(SystemInfo sys)
    {
        var allComponents = new HashSet<string>();
        var readComponents = new HashSet<string>();
        var writeComponents = new HashSet<string>();
        var withoutComponents = new HashSet<string>(sys.WithoutComponents);
        var withAnyComponents = new HashSet<string>(sys.WithAnyComponents);

        foreach (var field in sys.Fields)
        {
            if (field.Kind == FieldKind.Invalid) continue;

            if (field.Kind is FieldKind.InlineComponent or FieldKind.InlineSpan)
            {
                if (field.ComponentFQN == null) continue;
                allComponents.Add(field.ComponentFQN);
                readComponents.Add(field.ComponentFQN);
                if (!field.IsReadOnly)
                    writeComponents.Add(field.ComponentFQN);
            }
            else if (field.Kind is FieldKind.CompositionData or FieldKind.CompositionChunkData)
            {
                foreach (var comp in field.QueryableWithComponents)
                {
                    allComponents.Add(comp.ComponentFQN);
                    readComponents.Add(comp.ComponentFQN);
                    if (!field.IsReadOnly && !comp.IsReadOnly)
                        writeComponents.Add(comp.ComponentFQN);
                }
                foreach (var c in field.QueryableWithoutComponents) withoutComponents.Add(c);
                foreach (var c in field.QueryableWithAnyComponents) withAnyComponents.Add(c);
            }
        }

        return new ComponentAccess(
            allComponents.ToImmutableArray(),
            readComponents.ToImmutableArray(),
            writeComponents.ToImmutableArray(),
            withoutComponents.ToImmutableArray(),
            withAnyComponents.ToImmutableArray());
    }

    // ===================== DAG & Wave Computation =====================

    private static (int[][] Waves, bool HasCycle, List<string> CycleMembers) BuildDagAndWaves(
        List<(SystemInfo Info, int SystemId)> systems,
        Dictionary<string, ComponentAccess> accessMap,
        Dictionary<string, int> fqnToId)
    {
        int n = systems.Count;
        var adj = new List<int>[n];
        var inDegree = new int[n];
        for (int i = 0; i < n; i++) adj[i] = new List<int>();

        // Explicit edges from [After<T>] / [Before<T>]
        foreach (var (info, id) in systems)
        {
            foreach (var afterFQN in info.AfterSystems)
            {
                if (fqnToId.TryGetValue(afterFQN, out var beforeId))
                {
                    adj[beforeId].Add(id);
                    inDegree[id]++;
                }
            }
            foreach (var beforeFQN in info.BeforeSystems)
            {
                if (fqnToId.TryGetValue(beforeFQN, out var afterId))
                {
                    adj[id].Add(afterId);
                    inDegree[afterId]++;
                }
            }
        }

        // Topological sort (Kahn's algorithm)
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
            if (inDegree[i] == 0) queue.Enqueue(i);

        var topoOrder = new List<int>();
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            topoOrder.Add(node);
            foreach (var succ in adj[node])
            {
                inDegree[succ]--;
                if (inDegree[succ] == 0) queue.Enqueue(succ);
            }
        }

        if (topoOrder.Count != n)
        {
            var cycleMembers = Enumerable.Range(0, n)
                .Where(i => !topoOrder.Contains(i))
                .Select(i => systems[i].Info.FullyQualifiedName)
                .ToList();
            return (Array.Empty<int[]>(), true, cycleMembers);
        }

        // Greedy wave assignment
        var waveOf = new int[n];
        for (int i = 0; i < n; i++) waveOf[i] = -1;

        var waveLists = new List<List<int>>();
        foreach (var node in topoOrder)
        {
            int wave = 0;
            for (int pred = 0; pred < n; pred++)
            {
                if (adj[pred].Contains(node) && waveOf[pred] >= 0)
                    wave = Math.Max(wave, waveOf[pred] + 1);
            }
            var nodeAccess = accessMap[systems[node].Info.FullyQualifiedName];

            while (true)
            {
                while (waveLists.Count <= wave) waveLists.Add(new List<int>());

                bool hasConflict = false;
                foreach (var other in waveLists[wave])
                {
                    if (HasConflict(nodeAccess, accessMap[systems[other].Info.FullyQualifiedName]))
                    {
                        hasConflict = true;
                        break;
                    }
                }

                if (!hasConflict) break;
                wave++;
            }

            while (waveLists.Count <= wave) waveLists.Add(new List<int>());
            waveLists[wave].Add(node);
            waveOf[node] = wave;
        }

        var waves = waveLists.Select(w => w.ToArray()).ToArray();
        return (waves, false, new List<string>());
    }

    private static bool HasConflict(ComponentAccess a, ComponentAccess b)
    {
        foreach (var w in a.WriteComponents)
            if (b.ReadComponents.Contains(w)) return true;
        foreach (var w in b.WriteComponents)
            if (a.ReadComponents.Contains(w)) return true;
        return false;
    }

    // ===================== Per-System Partial Generation =====================

    private static void GeneratePerSystemPartial(
        SourceProductionContext context,
        SystemInfo sys,
        int systemId,
        string maskType,
        string configType)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Paradise.ECS;");
        sb.AppendLine();

        var hasNs = sys.Namespace != null;
        var baseIndent = hasNs ? "    " : "";
        if (hasNs)
        {
            sb.AppendLine($"namespace {sys.Namespace}");
            sb.AppendLine("{");
        }

        var indent = baseIndent;
        foreach (var ct in sys.ContainingTypes)
        {
            sb.AppendLine($"{indent}partial {ct.Keyword} {ct.Name}");
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        sb.AppendLine($"{indent}partial struct {sys.TypeName}");
        sb.AppendLine($"{indent}{{");

        // SystemId
        sb.AppendLine($"{indent}    /// <summary>The unique system ID assigned at compile time.</summary>");
        sb.AppendLine($"{indent}    static int global::Paradise.ECS.ISystem.SystemId => {systemId};");
        sb.AppendLine();

        // Constructor
        GenerateConstructor(sb, sys, indent + "    ", maskType, configType);
        sb.AppendLine();

        // RunChunk
        GenerateRunChunk(sb, sys, indent + "    ", maskType, configType);

        sb.AppendLine($"{indent}}}");

        for (int i = sys.ContainingTypes.Length - 1; i >= 0; i--)
        {
            indent = baseIndent + new string(' ', i * 4);
            sb.AppendLine($"{indent}}}");
        }

        if (hasNs) sb.AppendLine("}");

        var filename = "System_" + sys.FullyQualifiedName.Replace(".", "_").Replace("+", "_") + ".g.cs";
        context.AddSource(filename, sb.ToString());
    }

    private static void GenerateConstructor(StringBuilder sb, SystemInfo sys, string indent, string maskType, string configType)
    {
        sb.AppendLine($"{indent}[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.Append($"{indent}internal {sys.TypeName}(");

        bool first = true;
        foreach (var field in sys.Fields)
        {
            if (field.Kind == FieldKind.Invalid) continue;
            if (!first) sb.Append(", ");
            first = false;

            switch (field.Kind)
            {
                case FieldKind.InlineComponent:
                    sb.Append($"ref {field.TypeFQN} {ToCamelCase(field.FieldName)}");
                    break;
                case FieldKind.InlineSpan:
                    sb.Append($"{field.TypeFQN} {ToCamelCase(field.FieldName)}");
                    break;
                case FieldKind.CompositionData:
                    sb.Append($"global::{field.ComponentFQN!.Replace("+", ".")}.Data<{maskType}, {configType}> {ToCamelCase(field.FieldName)}");
                    break;
                case FieldKind.CompositionChunkData:
                    sb.Append($"global::{field.ComponentFQN!.Replace("+", ".")}.ChunkData<{maskType}, {configType}> {ToCamelCase(field.FieldName)}");
                    break;
            }
        }

        sb.AppendLine(")");
        sb.AppendLine($"{indent}{{");

        foreach (var field in sys.Fields)
        {
            if (field.Kind == FieldKind.Invalid) continue;
            var paramName = ToCamelCase(field.FieldName);
            if (field.Kind is FieldKind.InlineComponent)
                sb.AppendLine($"{indent}    {field.FieldName} = ref {paramName};");
            else
                sb.AppendLine($"{indent}    {field.FieldName} = {paramName};");
        }

        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateRunChunk(StringBuilder sb, SystemInfo sys, string indent, string maskType, string configType)
    {
        sb.AppendLine($"{indent}/// <summary>Executes this system over a single chunk. Called by the scheduler.</summary>");
        sb.AppendLine($"{indent}internal static void RunChunk(");
        sb.AppendLine($"{indent}    global::Paradise.ECS.IWorld<{maskType}, {configType}> world,");
        sb.AppendLine($"{indent}    global::Paradise.ECS.ChunkHandle chunk,");
        sb.AppendLine($"{indent}    global::Paradise.ECS.ImmutableArchetypeLayout<{maskType}, {configType}> layout,");
        sb.AppendLine($"{indent}    int entityCount)");
        sb.AppendLine($"{indent}{{");

        if (sys.Kind == SystemKind.Entity)
            GenerateEntityModeRunChunk(sb, sys, indent + "    ", maskType, configType);
        else
            GenerateChunkModeRunChunk(sb, sys, indent + "    ", maskType, configType);

        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateEntityModeRunChunk(StringBuilder sb, SystemInfo sys, string indent, string maskType, string configType)
    {
        var inlineFields = sys.Fields.Where(f => f.Kind == FieldKind.InlineComponent).ToList();
        var compositionFields = sys.Fields.Where(f => f.Kind == FieldKind.CompositionData).ToList();

        if (inlineFields.Count > 0)
        {
            sb.AppendLine($"{indent}var bytes = world.ChunkManager.GetBytes(chunk);");
            foreach (var field in inlineFields)
            {
                var varName = ToCamelCase(field.FieldName) + "Span";
                sb.AppendLine($"{indent}var {varName} = bytes.GetSpan<{field.TypeFQN}>(layout.GetBaseOffset({field.TypeFQN}.TypeId), entityCount);");
            }
        }

        sb.AppendLine($"{indent}for (int __i = 0; __i < entityCount; __i++)");
        sb.AppendLine($"{indent}{{");

        // Create composition Data instances inside loop
        foreach (var field in compositionFields)
        {
            var varName = ToCamelCase(field.FieldName) + "Data";
            var dataType = $"global::{field.ComponentFQN!.Replace("+", ".")}.Data<{maskType}, {configType}>";
            sb.AppendLine($"{indent}    var {varName} = {dataType}.Create(world.ChunkManager, world.EntityManager, layout, chunk, __i);");
        }

        // Construct system
        sb.Append($"{indent}    var __system = new {GetGlobalFQN(sys)}(");
        bool first = true;
        foreach (var field in sys.Fields)
        {
            if (field.Kind == FieldKind.Invalid) continue;
            if (!first) sb.Append(", ");
            first = false;

            if (field.Kind == FieldKind.InlineComponent)
                sb.Append($"ref {ToCamelCase(field.FieldName)}Span[__i]");
            else if (field.Kind == FieldKind.CompositionData)
                sb.Append($"{ToCamelCase(field.FieldName)}Data");
        }
        sb.AppendLine(");");
        sb.AppendLine($"{indent}    __system.Execute();");
        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateChunkModeRunChunk(StringBuilder sb, SystemInfo sys, string indent, string maskType, string configType)
    {
        var inlineFields = sys.Fields.Where(f => f.Kind == FieldKind.InlineSpan).ToList();
        var compositionFields = sys.Fields.Where(f => f.Kind == FieldKind.CompositionChunkData).ToList();

        if (inlineFields.Count > 0)
        {
            sb.AppendLine($"{indent}var bytes = world.ChunkManager.GetBytes(chunk);");
            foreach (var field in inlineFields)
            {
                var compFQN = "global::" + field.ComponentFQN;
                var varName = ToCamelCase(field.FieldName) + "Span";
                sb.AppendLine($"{indent}var {varName} = bytes.GetSpan<{compFQN}>(layout.GetBaseOffset({compFQN}.TypeId), entityCount);");
            }
        }

        // Create composition ChunkData instances
        foreach (var field in compositionFields)
        {
            var varName = ToCamelCase(field.FieldName) + "ChunkData";
            var chunkDataType = $"global::{field.ComponentFQN!.Replace("+", ".")}.ChunkData<{maskType}, {configType}>";
            sb.AppendLine($"{indent}var {varName} = {chunkDataType}.Create(world.ChunkManager, world.EntityManager, layout, chunk, entityCount);");
        }

        // Construct system
        sb.Append($"{indent}var __system = new {GetGlobalFQN(sys)}(");
        bool first = true;
        foreach (var field in sys.Fields)
        {
            if (field.Kind == FieldKind.Invalid) continue;
            if (!first) sb.Append(", ");
            first = false;

            if (field.Kind == FieldKind.InlineSpan)
            {
                if (field.IsReadOnly)
                    sb.Append($"(global::System.ReadOnlySpan<global::{field.ComponentFQN}>){ToCamelCase(field.FieldName)}Span");
                else
                    sb.Append($"{ToCamelCase(field.FieldName)}Span");
            }
            else if (field.Kind == FieldKind.CompositionChunkData)
            {
                sb.Append($"{ToCamelCase(field.FieldName)}ChunkData");
            }
        }
        sb.AppendLine(");");
        sb.AppendLine($"{indent}__system.ExecuteChunk();");
    }

    // ===================== SystemRegistry Generation =====================

    private static void GenerateSystemRegistry(
        SourceProductionContext context,
        List<(SystemInfo Info, int SystemId)> systems,
        Dictionary<string, ComponentAccess> accessMap,
        int[][] waves)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Paradise.ECS;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Registry containing metadata and pre-computed execution waves for all system types.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <typeparam name=\"TMask\">The component mask type implementing IBitSet.</typeparam>");
        sb.AppendLine("public static class SystemRegistry<TMask>");
        sb.AppendLine("    where TMask : unmanaged, global::Paradise.ECS.IBitSet<TMask>");
        sb.AppendLine("{");
        sb.AppendLine($"    private static readonly global::Paradise.ECS.SystemMetadata<TMask>[] s_metadata;");
        sb.AppendLine($"    private static readonly int[][] s_waves;");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Gets the metadata for all registered systems, indexed by SystemId.</summary>");
        sb.AppendLine($"    public static global::System.ReadOnlySpan<global::Paradise.ECS.SystemMetadata<TMask>> Metadata => s_metadata;");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Gets the pre-computed execution waves. Systems in the same wave have no component conflicts.</summary>");
        sb.AppendLine($"    public static int[][] Waves => s_waves;");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Gets the total number of registered systems.</summary>");
        sb.AppendLine($"    public static int Count => {systems.Count};");
        sb.AppendLine();

        sb.AppendLine("    static SystemRegistry()");
        sb.AppendLine("    {");
        sb.AppendLine($"        s_metadata = new global::Paradise.ECS.SystemMetadata<TMask>[{systems.Count}];");
        sb.AppendLine();

        foreach (var (info, id) in systems)
        {
            var access = accessMap[info.FullyQualifiedName];
            sb.AppendLine($"        // {info.FullyQualifiedName} (SystemId = {id})");

            sb.Append($"        var readMask{id} = ");
            GenerateMask(sb, access.ReadComponents);
            sb.AppendLine(";");

            sb.Append($"        var writeMask{id} = ");
            GenerateMask(sb, access.WriteComponents);
            sb.AppendLine(";");

            sb.Append($"        var allMask{id} = ");
            GenerateMask(sb, access.AllComponents);
            sb.AppendLine(";");

            sb.Append($"        var noneMask{id} = ");
            GenerateMask(sb, access.WithoutComponents);
            sb.AppendLine(";");

            sb.Append($"        var anyMask{id} = ");
            GenerateMask(sb, access.WithAnyComponents);
            sb.AppendLine(";");

            sb.AppendLine($"        s_metadata[{id}] = new global::Paradise.ECS.SystemMetadata<TMask>");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            SystemId = {id},");
            sb.AppendLine($"            TypeName = \"{info.FullyQualifiedName}\",");
            sb.AppendLine($"            ReadMask = readMask{id},");
            sb.AppendLine($"            WriteMask = writeMask{id},");
            sb.AppendLine($"            QueryDescription = (global::Paradise.ECS.HashedKey<global::Paradise.ECS.ImmutableQueryDescription<TMask>>)new global::Paradise.ECS.ImmutableQueryDescription<TMask>(allMask{id}, noneMask{id}, anyMask{id}),");
            sb.AppendLine($"        }};");
            sb.AppendLine();
        }

        sb.AppendLine($"        s_waves = new int[{waves.Length}][];");
        for (int w = 0; w < waves.Length; w++)
        {
            sb.AppendLine($"        s_waves[{w}] = new int[] {{ {string.Join(", ", waves[w])} }};");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("SystemRegistry.g.cs", sb.ToString());
    }

    private static void GenerateMask(StringBuilder sb, ImmutableArray<string> components)
    {
        if (components.IsEmpty)
        {
            sb.Append("TMask.Empty");
            return;
        }
        sb.Append("TMask.Empty");
        foreach (var comp in components)
            sb.Append($".Set(global::{comp}.TypeId)");
    }

    // ===================== Schedule Generation =====================

    private static void GenerateSchedule(
        SourceProductionContext context,
        List<(SystemInfo Info, int SystemId)> systems,
        string maskType,
        string configType,
        bool suppressGlobalUsings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Paradise.ECS;");
        sb.AppendLine();
        sb.AppendLine("namespace Paradise.ECS;");
        sb.AppendLine();

        var worldType = $"global::Paradise.ECS.IWorld<{maskType}, {configType}>";
        var layoutType = $"global::Paradise.ECS.ImmutableArchetypeLayout<{maskType}, {configType}>";

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Pre-built execution schedule for systems. Supports sequential, parallel, and chunk-parallel execution.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public sealed class SystemSchedule");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {worldType} _world;");
        sb.AppendLine($"    private readonly int[][] _waves;");
        sb.AppendLine($"    private readonly bool[] _enabledSystems;");
        sb.AppendLine();

        sb.AppendLine($"    internal SystemSchedule({worldType} world, int[][] waves, bool[] enabledSystems)");
        sb.AppendLine("    {");
        sb.AppendLine("        _world = world;");
        sb.AppendLine("        _waves = waves;");
        sb.AppendLine("        _enabledSystems = enabledSystems;");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>Runs all systems sequentially, wave by wave.</summary>");
        sb.AppendLine("    public void RunSequential()");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var wave in _waves)");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var systemId in wave)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!_enabledSystems[systemId]) continue;");
        sb.AppendLine($"                var query = _world.ArchetypeRegistry.GetOrCreateQuery(global::Paradise.ECS.SystemRegistry<{maskType}>.Metadata[systemId].QueryDescription);");
        sb.AppendLine("                foreach (var chunkInfo in query.Chunks)");
        sb.AppendLine("                {");
        sb.AppendLine("                    DispatchRunChunk(systemId, chunkInfo.Handle, chunkInfo.Archetype.Layout, chunkInfo.EntityCount);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>Runs systems in parallel within each wave, sequentially between waves.</summary>");
        sb.AppendLine("    public void RunParallel()");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var wave in _waves)");
        sb.AppendLine("        {");
        sb.AppendLine("            var actions = new global::System.Collections.Generic.List<global::System.Action>();");
        sb.AppendLine("            foreach (var systemId in wave)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!_enabledSystems[systemId]) continue;");
        sb.AppendLine("                var sid = systemId;");
        sb.AppendLine("                actions.Add(() =>");
        sb.AppendLine("                {");
        sb.AppendLine($"                    var q = _world.ArchetypeRegistry.GetOrCreateQuery(global::Paradise.ECS.SystemRegistry<{maskType}>.Metadata[sid].QueryDescription);");
        sb.AppendLine("                    foreach (var ci in q.Chunks)");
        sb.AppendLine("                        DispatchRunChunk(sid, ci.Handle, ci.Archetype.Layout, ci.EntityCount);");
        sb.AppendLine("                });");
        sb.AppendLine("            }");
        sb.AppendLine("            if (actions.Count == 1) actions[0]();");
        sb.AppendLine("            else if (actions.Count > 1) global::System.Threading.Tasks.Parallel.Invoke(actions.ToArray());");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    private void DispatchRunChunk(int systemId, global::Paradise.ECS.ChunkHandle handle, {layoutType} layout, int entityCount)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (systemId)");
        sb.AppendLine("        {");
        foreach (var (info, id) in systems)
        {
            sb.AppendLine($"            case {id}: {GetGlobalFQN(info)}.RunChunk(_world, handle, layout, entityCount); break;");
        }
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        sb.AppendLine($"    /// <summary>Creates a schedule builder for the given world.</summary>");
        sb.AppendLine($"    public static SystemScheduleBuilder Create({worldType} world) => new(world);");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Builder for selecting which systems to include in a schedule.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public struct SystemScheduleBuilder");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {worldType} _world;");
        sb.AppendLine($"    private readonly bool[] _enabled;");
        sb.AppendLine();
        sb.AppendLine($"    internal SystemScheduleBuilder({worldType} world)");
        sb.AppendLine("    {");
        sb.AppendLine("        _world = world;");
        sb.AppendLine($"        _enabled = new bool[{systems.Count}];");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Adds a system to the schedule.</summary>");
        sb.AppendLine("    /// <typeparam name=\"T\">The system type implementing ISystem.</typeparam>");
        sb.AppendLine("    public SystemScheduleBuilder Add<T>() where T : global::Paradise.ECS.ISystem, allows ref struct");
        sb.AppendLine("    {");
        sb.AppendLine("        _enabled[T.SystemId] = true;");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Adds all registered systems to the schedule.</summary>");
        sb.AppendLine("    public SystemScheduleBuilder AddAll()");
        sb.AppendLine("    {");
        sb.AppendLine($"        global::System.Array.Fill(_enabled, true);");
        sb.AppendLine("        return this;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Builds the schedule with the selected systems.</summary>");
        sb.AppendLine("    public SystemSchedule Build()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new SystemSchedule(_world, global::Paradise.ECS.SystemRegistry<{maskType}>.Waves, _enabled);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("SystemSchedule.g.cs", sb.ToString());

        if (!suppressGlobalUsings)
        {
            var aliasSb = new StringBuilder();
            aliasSb.AppendLine("// <auto-generated/>");
            aliasSb.AppendLine();
            aliasSb.AppendLine($"global using SystemRegistry = global::Paradise.ECS.SystemRegistry<{maskType}>;");
            context.AddSource("SystemAliases.g.cs", aliasSb.ToString());
        }
    }

    // ===================== Helpers =====================

    private static string GetGlobalFQN(SystemInfo sys)
    {
        return "global::" + sys.FullyQualifiedName.Replace("+", ".");
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (name.StartsWith("_", StringComparison.Ordinal)) return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    // ===================== Data Structures =====================

    private enum SystemKind { Entity, Chunk }

    private enum FieldKind { InlineComponent, InlineSpan, CompositionData, CompositionChunkData, Invalid }

    private readonly struct QueryableComponentAccess
    {
        public string ComponentFQN { get; }
        public bool IsReadOnly { get; }
        public bool QueryOnly { get; }

        public QueryableComponentAccess(string componentFQN, bool isReadOnly, bool queryOnly)
        {
            ComponentFQN = componentFQN;
            IsReadOnly = isReadOnly;
            QueryOnly = queryOnly;
        }
    }

    private readonly struct QueryableLookupInfo
    {
        public string Prefix { get; }
        public string FQN { get; }
        public ImmutableArray<QueryableComponentAccess> WithComponents { get; }
        public ImmutableArray<string> WithoutComponents { get; }
        public ImmutableArray<string> WithAnyComponents { get; }

        public QueryableLookupInfo(
            string prefix, string fqn,
            ImmutableArray<QueryableComponentAccess> withComponents,
            ImmutableArray<string> withoutComponents,
            ImmutableArray<string> withAnyComponents)
        {
            Prefix = prefix;
            FQN = fqn;
            WithComponents = withComponents;
            WithoutComponents = withoutComponents;
            WithAnyComponents = withAnyComponents;
        }
    }

    private readonly struct SystemFieldInfo
    {
        public string FieldName { get; }
        public FieldKind Kind { get; }
        public bool IsReadOnly { get; }
        public string TypeFQN { get; }
        public string? ComponentFQN { get; }
        public ImmutableArray<QueryableComponentAccess> QueryableWithComponents { get; }
        public ImmutableArray<string> QueryableWithoutComponents { get; }
        public ImmutableArray<string> QueryableWithAnyComponents { get; }
        public bool IsRefField { get; }
        public string? ErrorTypeName { get; }

        public SystemFieldInfo(
            string fieldName, FieldKind kind, bool isReadOnly, string typeFQN,
            string? componentFQN,
            ImmutableArray<QueryableComponentAccess> queryableWithComponents,
            ImmutableArray<string> queryableWithoutComponents,
            ImmutableArray<string> queryableWithAnyComponents,
            bool isRefField = false,
            string? errorTypeName = null)
        {
            FieldName = fieldName;
            Kind = kind;
            IsReadOnly = isReadOnly;
            TypeFQN = typeFQN;
            ComponentFQN = componentFQN;
            QueryableWithComponents = queryableWithComponents;
            QueryableWithoutComponents = queryableWithoutComponents;
            QueryableWithAnyComponents = queryableWithAnyComponents;
            IsRefField = isRefField;
            ErrorTypeName = errorTypeName;
        }
    }

    private readonly struct SystemInfo
    {
        public string FullyQualifiedName { get; }
        public Location Location { get; }
        public bool IsRefStruct { get; }
        public bool IsPartial { get; }
        public string? Namespace { get; }
        public string TypeName { get; }
        public ImmutableArray<ContainingTypeInfo> ContainingTypes { get; }
        public SystemKind Kind { get; }
        public ImmutableArray<SystemFieldInfo> Fields { get; }
        public ImmutableArray<string> AfterSystems { get; }
        public ImmutableArray<string> BeforeSystems { get; }
        public ImmutableArray<string> WithoutComponents { get; }
        public ImmutableArray<string> WithAnyComponents { get; }
        public bool HasInvalidFields { get; }

        public SystemInfo(
            string fullyQualifiedName, Location location,
            bool isRefStruct, bool isPartial,
            string? ns, string typeName,
            ImmutableArray<ContainingTypeInfo> containingTypes,
            SystemKind kind,
            ImmutableArray<SystemFieldInfo> fields,
            ImmutableArray<string> afterSystems,
            ImmutableArray<string> beforeSystems,
            ImmutableArray<string> withoutComponents,
            ImmutableArray<string> withAnyComponents,
            bool hasInvalidFields)
        {
            FullyQualifiedName = fullyQualifiedName;
            Location = location;
            IsRefStruct = isRefStruct;
            IsPartial = isPartial;
            Namespace = ns;
            TypeName = typeName;
            ContainingTypes = containingTypes;
            Kind = kind;
            Fields = fields;
            AfterSystems = afterSystems;
            BeforeSystems = beforeSystems;
            WithoutComponents = withoutComponents;
            WithAnyComponents = withAnyComponents;
            HasInvalidFields = hasInvalidFields;
        }
    }

    private readonly struct ComponentAccess
    {
        public ImmutableArray<string> AllComponents { get; }
        public ImmutableArray<string> ReadComponents { get; }
        public ImmutableArray<string> WriteComponents { get; }
        public ImmutableArray<string> WithoutComponents { get; }
        public ImmutableArray<string> WithAnyComponents { get; }

        public ComponentAccess(
            ImmutableArray<string> allComponents,
            ImmutableArray<string> readComponents,
            ImmutableArray<string> writeComponents,
            ImmutableArray<string> withoutComponents,
            ImmutableArray<string> withAnyComponents)
        {
            AllComponents = allComponents;
            ReadComponents = readComponents;
            WriteComponents = writeComponents;
            WithoutComponents = withoutComponents;
            WithAnyComponents = withAnyComponents;
        }
    }
}
