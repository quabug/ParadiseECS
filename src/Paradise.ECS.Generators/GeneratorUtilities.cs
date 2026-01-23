using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Paradise.ECS.Generators;

/// <summary>
/// Shared utilities for source generators.
/// </summary>
internal static class GeneratorUtilities
{
    /// <summary>
    /// Gets the optimal mask type string based on the number of bits required.
    /// </summary>
    /// <param name="requiredBits">The number of bits required.</param>
    /// <returns>The fully qualified mask type string.</returns>
    public static string GetOptimalMaskType(int requiredBits)
    {
        if (requiredBits <= 32) return "global::Paradise.ECS.SmallBitSet<uint>";
        if (requiredBits <= 64) return "global::Paradise.ECS.SmallBitSet<ulong>";
        if (requiredBits <= 128) return "global::Paradise.ECS.ImmutableBitSet<global::Paradise.ECS.Bit128>";
        if (requiredBits <= 256) return "global::Paradise.ECS.ImmutableBitSet<global::Paradise.ECS.Bit256>";
        if (requiredBits <= 512) return "global::Paradise.ECS.ImmutableBitSet<global::Paradise.ECS.Bit512>";
        if (requiredBits <= 1024) return "global::Paradise.ECS.ImmutableBitSet<global::Paradise.ECS.Bit1024>";

        var capacity = ((requiredBits + 255) / 256) * 256;
        return $"global::Paradise.ECS.ImmutableBitSet<global::Paradise.ECS.Bit{capacity}>";
    }

    /// <summary>
    /// Gets the fully qualified name of a type symbol without the "global::" prefix.
    /// </summary>
    public static string GetFullyQualifiedName(INamedTypeSymbol symbol)
    {
        var fqn = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return fqn.StartsWith("global::", StringComparison.Ordinal) ? fqn.Substring(8) : fqn;
    }

    /// <summary>
    /// Gets the namespace of a type symbol, or null if it's in the global namespace.
    /// </summary>
    public static string? GetNamespace(INamedTypeSymbol symbol)
        => symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString();

    /// <summary>
    /// Gets the type keyword for a type symbol (class, struct, record class, record struct, interface).
    /// </summary>
    public static string GetTypeKeyword(INamedTypeSymbol type) => type.TypeKind switch
    {
        Microsoft.CodeAnalysis.TypeKind.Class => type.IsRecord ? "record class" : "class",
        Microsoft.CodeAnalysis.TypeKind.Struct => type.IsRecord ? "record struct" : "struct",
        Microsoft.CodeAnalysis.TypeKind.Interface => "interface",
        _ => "struct"
    };

    /// <summary>
    /// Gets the containing types for a nested type, ordered from outermost to innermost.
    /// </summary>
    public static ImmutableArray<ContainingTypeInfo> GetContainingTypes(INamedTypeSymbol symbol)
    {
        var list = new List<ContainingTypeInfo>();
        for (var parent = symbol.ContainingType; parent != null; parent = parent.ContainingType)
            list.Add(new ContainingTypeInfo(parent.Name, GetTypeKeyword(parent)));
        list.Reverse();
        return list.ToImmutableArray();
    }

    /// <summary>
    /// Extracts type information from a generator attribute syntax context.
    /// </summary>
    public static TypeInfo? ExtractTypeInfo(GeneratorAttributeSyntaxContext context, TypeKind kind)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol || typeSymbol.TypeKind != Microsoft.CodeAnalysis.TypeKind.Struct)
            return null;

        var fullyQualifiedName = GetFullyQualifiedName(typeSymbol);
        var ns = GetNamespace(typeSymbol);
        var containingTypes = GetContainingTypes(typeSymbol);

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

    /// <summary>
    /// Processes and validates types, returning valid types and the optimal mask type.
    /// </summary>
    public static (List<TypeInfo> Valid, string MaskType) ProcessTypes(
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
        var maskType = GetOptimalMaskType(requiredBits);

        return (valid, maskType);
    }

    /// <summary>
    /// Calculates the maximum assigned ID for a list of types, considering manual IDs.
    /// </summary>
    public static int CalculateMaxAssignedId(List<TypeInfo> types)
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
}

/// <summary>
/// Information about a containing type for nested types.
/// </summary>
internal readonly struct ContainingTypeInfo
{
    public string Name { get; }
    public string Keyword { get; }

    public ContainingTypeInfo(string name, string keyword)
    {
        Name = name;
        Keyword = keyword;
    }
}

/// <summary>
/// Represents the kind of type being processed.
/// </summary>
internal enum TypeKind { Component, Tag }

/// <summary>
/// Information about a type being processed by the generator.
/// </summary>
internal readonly struct TypeInfo
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
