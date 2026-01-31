using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Paradise.ECS.Generators;

/// <summary>
/// Source generator that processes types marked with [System] attribute.
/// Generates system execution code and DAG metadata for parallel scheduling.
/// </summary>
[Generator]
public class SystemGenerator : IIncrementalGenerator
{
    private const string SystemAttributeFullName = "Paradise.ECS.SystemAttribute";
    private const string QueryableAttributeFullName = "Paradise.ECS.QueryableAttribute";
    private const string WithAttributePrefix = "Paradise.ECS.WithAttribute<";
    private const string WithoutAttributePrefix = "Paradise.ECS.WithoutAttribute<";
    private const string WithAnyAttributePrefix = "Paradise.ECS.WithAnyAttribute<";
    private const string AfterAttributePrefix = "Paradise.ECS.AfterAttribute<";
    private const string BeforeAttributePrefix = "Paradise.ECS.BeforeAttribute<";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all structs with [System] attribute
        var systemTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                SystemAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => GetSystemInfo(ctx))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!.Value);

        // Find all queryable types for reference
        var queryableTypes = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                QueryableAttributeFullName,
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => GetQueryableInfo(ctx))
            .Where(static x => x is not null)
            .Select(static (x, _) => x!.Value)
            .Collect();

        // Count components to determine mask type
        var componentCount = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Paradise.ECS.ComponentAttribute",
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => 1)
            .Collect()
            .Select(static (components, _) => components.Length);

        // Combine systems with queryables and component count
        var combined = systemTypes.Collect()
            .Combine(queryableTypes)
            .Combine(componentCount);

        context.RegisterSourceOutput(combined, static (ctx, data) =>
            GenerateSystemCode(ctx, data.Left.Left, data.Left.Right, data.Right));
    }

    private static SystemInfo? GetSystemInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        if (typeSymbol.TypeKind != Microsoft.CodeAnalysis.TypeKind.Struct)
            return null;

        var fullyQualifiedName = GeneratorUtilities.GetFullyQualifiedName(typeSymbol);
        var ns = GeneratorUtilities.GetNamespace(typeSymbol);
        var containingTypes = GeneratorUtilities.GetContainingTypes(typeSymbol);

        // Check if partial
        var isPartial = typeSymbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<StructDeclarationSyntax>()
            .Any(s => s.Modifiers.Any(m => m.Text == "partial"));

        // Extract [System] attribute properties
        int? manualId = null;
        string? groupType = null;

        foreach (var attr in context.Attributes)
        {
            foreach (var namedArg in attr.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "Id" when namedArg.Value.Value is int id && id >= 0:
                        manualId = id;
                        break;
                    case "Group" when namedArg.Value.Value is INamedTypeSymbol groupSymbol:
                        groupType = GeneratorUtilities.GetFullyQualifiedName(groupSymbol);
                        break;
                }
            }
        }

        // Find Execute method
        var executeMethod = typeSymbol.GetMembers("Execute")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsStatic && m.ReturnsVoid);

        if (executeMethod == null)
            return null;

        // Analyze Execute parameters to find Queryable types
        var parameters = new List<ExecuteParameterInfo>();
        foreach (var param in executeMethod.Parameters)
        {
            var paramType = param.Type as INamedTypeSymbol;
            if (paramType == null)
                continue;

            // Check if parameter type has [Queryable] attribute
            var hasQueryable = paramType.GetAttributes()
                .Any(a => a.AttributeClass?.ToDisplayString() == QueryableAttributeFullName);

            if (!hasQueryable)
                continue;

            var isRef = param.RefKind == RefKind.Ref;
            var isIn = param.RefKind == RefKind.In;
            var isReadOnly = isIn || param.RefKind == RefKind.None;

            parameters.Add(new ExecuteParameterInfo(
                GeneratorUtilities.GetFullyQualifiedName(paramType),
                paramType.Name,
                param.Name,
                isRef,
                isReadOnly));
        }

        if (parameters.Count == 0)
            return null;

        // Extract [After<T>] and [Before<T>] attributes
        var afterTypes = new List<string>();
        var beforeTypes = new List<string>();
        var withoutComponents = new List<string>();
        var withAnyComponents = new List<string>();

        foreach (var attr in typeSymbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null || !attrClass.IsGenericType)
                continue;

            var metadataName = attrClass.OriginalDefinition.ToDisplayString();
            var typeArg = attrClass.TypeArguments.FirstOrDefault();
            if (typeArg is null)
                continue;

            var typeArgName = typeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (typeArgName.StartsWith("global::", StringComparison.Ordinal))
                typeArgName = typeArgName.Substring(8);

            if (metadataName.StartsWith(AfterAttributePrefix, StringComparison.Ordinal))
            {
                afterTypes.Add(typeArgName);
            }
            else if (metadataName.StartsWith(BeforeAttributePrefix, StringComparison.Ordinal))
            {
                beforeTypes.Add(typeArgName);
            }
            else if (metadataName.StartsWith(WithoutAttributePrefix, StringComparison.Ordinal))
            {
                withoutComponents.Add(typeArgName);
            }
            else if (metadataName.StartsWith(WithAnyAttributePrefix, StringComparison.Ordinal))
            {
                withAnyComponents.Add(typeArgName);
            }
        }

        return new SystemInfo(
            fullyQualifiedName,
            typeSymbol.Locations.FirstOrDefault() ?? Location.None,
            isPartial,
            ns,
            typeSymbol.Name,
            containingTypes,
            manualId,
            groupType,
            parameters.ToImmutableArray(),
            afterTypes.ToImmutableArray(),
            beforeTypes.ToImmutableArray(),
            withoutComponents.ToImmutableArray(),
            withAnyComponents.ToImmutableArray());
    }

    private static QueryableComponentInfo? GetQueryableInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var fullyQualifiedName = GeneratorUtilities.GetFullyQualifiedName(typeSymbol);

        // Collect component info from [With<T>] attributes
        var components = new List<QueryableComponentAccess>();

        foreach (var attr in typeSymbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null || !attrClass.IsGenericType)
                continue;

            var metadataName = attrClass.OriginalDefinition.ToDisplayString();
            if (!metadataName.StartsWith(WithAttributePrefix, StringComparison.Ordinal))
                continue;

            var typeArg = attrClass.TypeArguments.FirstOrDefault();
            if (typeArg is null)
                continue;

            var componentName = typeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (componentName.StartsWith("global::", StringComparison.Ordinal))
                componentName = componentName.Substring(8);

            bool isReadOnly = false;
            bool queryOnly = false;

            foreach (var namedArg in attr.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "IsReadOnly" when namedArg.Value.Value is bool ro:
                        isReadOnly = ro;
                        break;
                    case "QueryOnly" when namedArg.Value.Value is bool qo:
                        queryOnly = qo;
                        break;
                }
            }

            components.Add(new QueryableComponentAccess(componentName, isReadOnly, queryOnly));
        }

        return new QueryableComponentInfo(fullyQualifiedName, components.ToImmutableArray());
    }

    private static void GenerateSystemCode(
        SourceProductionContext context,
        ImmutableArray<SystemInfo> systems,
        ImmutableArray<QueryableComponentInfo> queryables,
        int componentCount)
    {
        if (systems.IsEmpty)
            return;

        // Build queryable lookup
        var queryableLookup = queryables.ToDictionary(q => q.FullyQualifiedName);

        // Validate systems
        var validSystems = new List<SystemInfo>();
        foreach (var system in systems)
        {
            if (!system.IsPartial)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.SystemMustBePartial,
                    system.Location,
                    system.FullyQualifiedName));
                continue;
            }

            // Validate all parameters are known queryables
            bool allValid = true;
            foreach (var param in system.Parameters)
            {
                if (!queryableLookup.ContainsKey(param.QueryableTypeName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.SystemParameterNotQueryable,
                        system.Location,
                        param.ParameterName,
                        param.QueryableTypeName,
                        system.FullyQualifiedName));
                    allValid = false;
                }
            }

            if (allValid)
                validSystems.Add(system);
        }

        if (validSystems.Count == 0)
            return;

        // Sort and assign IDs
        var sorted = validSystems.OrderBy(s => s.FullyQualifiedName, StringComparer.Ordinal).ToList();
        var manualIds = new HashSet<int>(sorted.Where(s => s.ManualId.HasValue).Select(s => s.ManualId!.Value));
        var systemsWithIds = new List<(SystemInfo Info, int Id)>();
        int nextAutoId = 0;

        foreach (var system in sorted)
        {
            if (system.ManualId.HasValue)
            {
                systemsWithIds.Add((system, system.ManualId.Value));
            }
            else
            {
                while (manualIds.Contains(nextAutoId)) nextAutoId++;
                systemsWithIds.Add((system, nextAutoId));
                nextAutoId++;
            }
        }

        // Generate each system
        foreach (var (system, id) in systemsWithIds)
        {
            GenerateSystemPartial(context, system, id, queryableLookup);
        }

        // Generate SystemRegistry
        GenerateSystemRegistry(context, systemsWithIds, queryableLookup, componentCount);
    }

    private static void GenerateSystemPartial(
        SourceProductionContext context,
        SystemInfo system,
        int systemId,
        Dictionary<string, QueryableComponentInfo> queryableLookup)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        var hasNamespace = system.Namespace != null;
        var baseIndent = hasNamespace ? "    " : "";

        if (hasNamespace)
        {
            sb.AppendLine($"namespace {system.Namespace}");
            sb.AppendLine("{");
        }

        // Open containing types
        var indent = baseIndent;
        foreach (var containingType in system.ContainingTypes)
        {
            sb.AppendLine($"{indent}partial {containingType.Keyword} {containingType.Name}");
            sb.AppendLine($"{indent}{{");
            indent += "    ";
        }

        // Generate partial struct
        sb.AppendLine($"{indent}partial struct {system.TypeName}");
        sb.AppendLine($"{indent}{{");

        // SystemId property
        sb.AppendLine($"{indent}    /// <summary>The unique system identifier.</summary>");
        sb.AppendLine($"{indent}    public static int SystemId => {systemId};");
        sb.AppendLine();

        // Name property
        sb.AppendLine($"{indent}    /// <summary>The system name.</summary>");
        sb.AppendLine($"{indent}    public static string Name => \"{system.TypeName}\";");
        sb.AppendLine();

        // Run method
        GenerateRunMethod(sb, system, queryableLookup, indent + "    ");

        sb.AppendLine($"{indent}}}");

        // Close containing types
        for (int i = system.ContainingTypes.Length - 1; i >= 0; i--)
        {
            indent = baseIndent + new string(' ', i * 4);
            sb.AppendLine($"{indent}}}");
        }

        if (hasNamespace)
        {
            sb.AppendLine("}");
        }

        var filename = $"System_{system.FullyQualifiedName.Replace(".", "_").Replace("+", "_")}.g.cs";
        context.AddSource(filename, sb.ToString());
    }

    private static void GenerateRunMethod(
        StringBuilder sb,
        SystemInfo system,
        Dictionary<string, QueryableComponentInfo> queryableLookup,
        string indent)
    {
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Executes this system on all matching entities in the world.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine($"{indent}public static void Run<TWorld, TMask, TConfig>(TWorld world)");
        sb.AppendLine($"{indent}    where TWorld : global::Paradise.ECS.IWorld<TMask, TConfig>");
        sb.AppendLine($"{indent}    where TMask : unmanaged, global::Paradise.ECS.IBitSet<TMask>");
        sb.AppendLine($"{indent}    where TConfig : global::Paradise.ECS.IConfig, new()");
        sb.AppendLine($"{indent}{{");

        // Build the combined query - use first parameter's queryable for now
        // In a full implementation, we'd combine all queryables' masks
        var firstParam = system.Parameters[0];
        sb.AppendLine($"{indent}    var chunkQuery = world.ChunkQuery(default(global::{firstParam.QueryableTypeName}));");
        sb.AppendLine();
        sb.AppendLine($"{indent}    foreach (var chunk in chunkQuery)");
        sb.AppendLine($"{indent}    {{");

        // For each parameter, get the component spans
        foreach (var param in system.Parameters)
        {
            if (!queryableLookup.TryGetValue(param.QueryableTypeName, out var queryable))
                continue;

            // We need to create the queryable Data struct for each entity
            // For now, we'll iterate and create per entity
        }

        sb.AppendLine($"{indent}        for (int i = 0; i < chunk.EntityCount; i++)");
        sb.AppendLine($"{indent}        {{");

        // Create each queryable instance
        foreach (var param in system.Parameters)
        {
            var globalName = $"global::{param.QueryableTypeName}";
            sb.AppendLine($"{indent}            var {param.ParameterName} = {globalName}.Data<TMask, TConfig>.Create(");
            sb.AppendLine($"{indent}                world.ChunkManager,");
            sb.AppendLine($"{indent}                world.EntityManager,");
            sb.AppendLine($"{indent}                chunk.Layout,");
            sb.AppendLine($"{indent}                chunk.ChunkHandle,");
            sb.AppendLine($"{indent}                i);");
        }

        // Call Execute with parameters
        sb.Append($"{indent}            Execute(");
        for (int i = 0; i < system.Parameters.Length; i++)
        {
            var param = system.Parameters[i];
            if (i > 0) sb.Append(", ");
            if (param.IsRef)
                sb.Append($"ref {param.ParameterName}");
            else if (param.IsReadOnly)
                sb.Append($"in {param.ParameterName}");
            else
                sb.Append(param.ParameterName);
        }
        sb.AppendLine(");");

        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}}}");
    }

    private static void GenerateSystemRegistry(
        SourceProductionContext context,
        List<(SystemInfo Info, int Id)> systems,
        Dictionary<string, QueryableComponentInfo> queryableLookup,
        int componentCount)
    {
        var maskType = GeneratorUtilities.GetOptimalMaskType(componentCount);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Paradise.ECS;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Registry containing all system metadata and the pre-computed execution DAG.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class SystemRegistry<TMask>");
        sb.AppendLine("    where TMask : unmanaged, global::Paradise.ECS.IBitSet<TMask>");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.SystemMetadata<TMask>> s_systems;");
        sb.AppendLine("    private static readonly global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.SystemDependency> s_dependencies;");
        sb.AppendLine("    private static readonly global::System.Collections.Immutable.ImmutableArray<global::System.Collections.Immutable.ImmutableArray<int>> s_executionWaves;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>All system metadata, indexed by SystemId.</summary>");
        sb.AppendLine("    public static global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.SystemMetadata<TMask>> Systems => s_systems;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>All dependency edges in the system DAG.</summary>");
        sb.AppendLine("    public static global::System.Collections.Immutable.ImmutableArray<global::Paradise.ECS.SystemDependency> Dependencies => s_dependencies;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Pre-computed parallel execution waves.</summary>");
        sb.AppendLine("    public static global::System.Collections.Immutable.ImmutableArray<global::System.Collections.Immutable.ImmutableArray<int>> ExecutionWaves => s_executionWaves;");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Total number of registered systems.</summary>");
        sb.AppendLine($"    public static int Count => {systems.Count};");
        sb.AppendLine();
        sb.AppendLine("    static SystemRegistry()");
        sb.AppendLine("    {");

        // Generate system metadata
        int maxId = systems.Count > 0 ? systems.Max(s => s.Id) : 0;
        sb.AppendLine($"        var systems = new global::Paradise.ECS.SystemMetadata<TMask>[{maxId + 1}];");
        sb.AppendLine();

        foreach (var (system, id) in systems)
        {
            // Compute read and write masks from parameters
            sb.AppendLine($"        // {system.TypeName} (SystemId = {id})");

            // Build read mask (all components from all parameters)
            sb.Append($"        var readMask{id} = TMask.Empty");
            foreach (var param in system.Parameters)
            {
                if (queryableLookup.TryGetValue(param.QueryableTypeName, out var queryable))
                {
                    foreach (var comp in queryable.Components)
                    {
                        if (!comp.QueryOnly)
                        {
                            sb.Append($".Set(global::{comp.ComponentFullName}.TypeId)");
                        }
                    }
                }
            }
            sb.AppendLine(";");

            // Build write mask (only from ref parameters, and only non-readonly components)
            sb.Append($"        var writeMask{id} = TMask.Empty");
            foreach (var param in system.Parameters)
            {
                if (!param.IsRef) // Only ref parameters can write
                    continue;

                if (queryableLookup.TryGetValue(param.QueryableTypeName, out var queryable))
                {
                    foreach (var comp in queryable.Components)
                    {
                        if (!comp.IsReadOnly && !comp.QueryOnly)
                        {
                            sb.Append($".Set(global::{comp.ComponentFullName}.TypeId)");
                        }
                    }
                }
            }
            sb.AppendLine(";");

            // Build query description (All mask from all parameters)
            sb.Append($"        var allMask{id} = TMask.Empty");
            foreach (var param in system.Parameters)
            {
                if (queryableLookup.TryGetValue(param.QueryableTypeName, out var queryable))
                {
                    foreach (var comp in queryable.Components)
                    {
                        sb.Append($".Set(global::{comp.ComponentFullName}.TypeId)");
                    }
                }
            }
            sb.AppendLine(";");

            // None mask from [Without<T>] on system
            sb.Append($"        var noneMask{id} = TMask.Empty");
            foreach (var comp in system.WithoutComponents)
            {
                sb.Append($".Set(global::{comp}.TypeId)");
            }
            sb.AppendLine(";");

            // Any mask from [WithAny<T>] on system
            sb.Append($"        var anyMask{id} = TMask.Empty");
            foreach (var comp in system.WithAnyComponents)
            {
                sb.Append($".Set(global::{comp}.TypeId)");
            }
            sb.AppendLine(";");

            sb.AppendLine($"        var query{id} = new global::Paradise.ECS.ImmutableQueryDescription<TMask>(allMask{id}, noneMask{id}, anyMask{id});");
            sb.AppendLine($"        systems[{id}] = new global::Paradise.ECS.SystemMetadata<TMask>(");
            sb.AppendLine($"            {id},");
            sb.AppendLine($"            \"{system.TypeName}\",");
            sb.AppendLine($"            readMask{id},");
            sb.AppendLine($"            writeMask{id},");
            sb.AppendLine($"            (global::Paradise.ECS.HashedKey<global::Paradise.ECS.ImmutableQueryDescription<TMask>>)query{id},");
            sb.AppendLine($"            -1);");
            sb.AppendLine();
        }

        sb.AppendLine("        s_systems = global::System.Collections.Immutable.ImmutableArray.Create(systems);");
        sb.AppendLine();

        // Compute dependencies
        sb.AppendLine("        // Compute dependencies from read/write masks");
        sb.AppendLine("        var deps = new global::System.Collections.Generic.List<global::Paradise.ECS.SystemDependency>();");

        // Build dependency edges
        sb.AppendLine("        for (int i = 0; i < systems.Length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            for (int j = i + 1; j < systems.Length; j++)");
        sb.AppendLine("            {");
        sb.AppendLine("                var a = systems[i];");
        sb.AppendLine("                var b = systems[j];");
        sb.AppendLine("                // Write-Read conflict");
        sb.AppendLine("                if (!a.WriteMask.And(b.ReadMask).IsEmpty)");
        sb.AppendLine("                    deps.Add(new global::Paradise.ECS.SystemDependency(i, j, global::Paradise.ECS.DependencyReason.WriteRead));");
        sb.AppendLine("                else if (!a.ReadMask.And(b.WriteMask).IsEmpty)");
        sb.AppendLine("                    deps.Add(new global::Paradise.ECS.SystemDependency(i, j, global::Paradise.ECS.DependencyReason.ReadWrite));");
        sb.AppendLine("                else if (!a.WriteMask.And(b.WriteMask).IsEmpty)");
        sb.AppendLine("                    deps.Add(new global::Paradise.ECS.SystemDependency(i, j, global::Paradise.ECS.DependencyReason.WriteWrite));");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        s_dependencies = global::System.Collections.Immutable.ImmutableArray.CreateRange(deps);");
        sb.AppendLine();

        // Compute execution waves using Kahn's algorithm
        sb.AppendLine("        // Compute execution waves via topological sort");
        sb.AppendLine("        var inDegree = new int[systems.Length];");
        sb.AppendLine("        var adjacency = new global::System.Collections.Generic.List<int>[systems.Length];");
        sb.AppendLine("        for (int i = 0; i < systems.Length; i++)");
        sb.AppendLine("            adjacency[i] = new global::System.Collections.Generic.List<int>();");
        sb.AppendLine("        foreach (var dep in deps)");
        sb.AppendLine("        {");
        sb.AppendLine("            adjacency[dep.Before].Add(dep.After);");
        sb.AppendLine("            inDegree[dep.After]++;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var waves = new global::System.Collections.Generic.List<global::System.Collections.Immutable.ImmutableArray<int>>();");
        sb.AppendLine("        var currentWave = new global::System.Collections.Generic.List<int>();");
        sb.AppendLine("        for (int i = 0; i < systems.Length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (inDegree[i] == 0)");
        sb.AppendLine("                currentWave.Add(i);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        while (currentWave.Count > 0)");
        sb.AppendLine("        {");
        sb.AppendLine("            waves.Add(global::System.Collections.Immutable.ImmutableArray.CreateRange(currentWave));");
        sb.AppendLine("            var nextWave = new global::System.Collections.Generic.List<int>();");
        sb.AppendLine("            foreach (var systemId in currentWave)");
        sb.AppendLine("            {");
        sb.AppendLine("                foreach (var dependent in adjacency[systemId])");
        sb.AppendLine("                {");
        sb.AppendLine("                    inDegree[dependent]--;");
        sb.AppendLine("                    if (inDegree[dependent] == 0)");
        sb.AppendLine("                        nextWave.Add(dependent);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            currentWave = nextWave;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        s_executionWaves = global::System.Collections.Immutable.ImmutableArray.CreateRange(waves);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("SystemRegistry.g.cs", sb.ToString());

        // Generate type alias
        GenerateSystemAliases(context, componentCount);
    }

    private static void GenerateSystemAliases(SourceProductionContext context, int componentCount)
    {
        var maskType = GeneratorUtilities.GetOptimalMaskType(componentCount);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine();
        sb.AppendLine("// Type alias for SystemRegistry using the same mask type as components");
        sb.AppendLine($"global using SystemRegistry = global::Paradise.ECS.SystemRegistry<{maskType}>;");

        context.AddSource("SystemAliases.g.cs", sb.ToString());
    }

    private readonly struct SystemInfo
    {
        public string FullyQualifiedName { get; }
        public Location Location { get; }
        public bool IsPartial { get; }
        public string? Namespace { get; }
        public string TypeName { get; }
        public ImmutableArray<ContainingTypeInfo> ContainingTypes { get; }
        public int? ManualId { get; }
        public string? GroupType { get; }
        public ImmutableArray<ExecuteParameterInfo> Parameters { get; }
        public ImmutableArray<string> AfterTypes { get; }
        public ImmutableArray<string> BeforeTypes { get; }
        public ImmutableArray<string> WithoutComponents { get; }
        public ImmutableArray<string> WithAnyComponents { get; }

        public SystemInfo(
            string fullyQualifiedName,
            Location location,
            bool isPartial,
            string? ns,
            string typeName,
            ImmutableArray<ContainingTypeInfo> containingTypes,
            int? manualId,
            string? groupType,
            ImmutableArray<ExecuteParameterInfo> parameters,
            ImmutableArray<string> afterTypes,
            ImmutableArray<string> beforeTypes,
            ImmutableArray<string> withoutComponents,
            ImmutableArray<string> withAnyComponents)
        {
            FullyQualifiedName = fullyQualifiedName;
            Location = location;
            IsPartial = isPartial;
            Namespace = ns;
            TypeName = typeName;
            ContainingTypes = containingTypes;
            ManualId = manualId;
            GroupType = groupType;
            Parameters = parameters;
            AfterTypes = afterTypes;
            BeforeTypes = beforeTypes;
            WithoutComponents = withoutComponents;
            WithAnyComponents = withAnyComponents;
        }
    }

    private readonly struct ExecuteParameterInfo
    {
        public string QueryableTypeName { get; }
        public string QueryableShortName { get; }
        public string ParameterName { get; }
        public bool IsRef { get; }
        public bool IsReadOnly { get; }

        public ExecuteParameterInfo(
            string queryableTypeName,
            string queryableShortName,
            string parameterName,
            bool isRef,
            bool isReadOnly)
        {
            QueryableTypeName = queryableTypeName;
            QueryableShortName = queryableShortName;
            ParameterName = parameterName;
            IsRef = isRef;
            IsReadOnly = isReadOnly;
        }
    }

    private readonly struct QueryableComponentInfo
    {
        public string FullyQualifiedName { get; }
        public ImmutableArray<QueryableComponentAccess> Components { get; }

        public QueryableComponentInfo(
            string fullyQualifiedName,
            ImmutableArray<QueryableComponentAccess> components)
        {
            FullyQualifiedName = fullyQualifiedName;
            Components = components;
        }
    }

    private readonly struct QueryableComponentAccess
    {
        public string ComponentFullName { get; }
        public bool IsReadOnly { get; }
        public bool QueryOnly { get; }

        public QueryableComponentAccess(string componentFullName, bool isReadOnly, bool queryOnly)
        {
            ComponentFullName = componentFullName;
            IsReadOnly = isReadOnly;
            QueryOnly = queryOnly;
        }
    }
}
