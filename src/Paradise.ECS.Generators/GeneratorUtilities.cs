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
        TypeKind.Class => type.IsRecord ? "record class" : "class",
        TypeKind.Struct => type.IsRecord ? "record struct" : "struct",
        TypeKind.Interface => "interface",
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
