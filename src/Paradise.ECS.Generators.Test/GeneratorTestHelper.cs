using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Paradise.ECS.Generators.Test;

/// <summary>
/// Helper class for testing source generators.
/// </summary>
public static class GeneratorTestHelper
{
    /// <summary>
    /// Creates a compilation with the given source code and runs the ComponentGenerator.
    /// </summary>
    public static GeneratorDriverRunResult RunGenerator(string source, bool includeEcsReferences = true)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        // Add System.Runtime reference
        var runtimeAssembly = System.Reflection.Assembly.Load("System.Runtime");
        references.Add(MetadataReference.CreateFromFile(runtimeAssembly.Location));

        // Add netstandard reference if available
        var netstandardPath = Path.Combine(
            Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "netstandard.dll");
        if (File.Exists(netstandardPath))
        {
            references.Add(MetadataReference.CreateFromFile(netstandardPath));
        }

        // Add Paradise.ECS reference if requested
        if (includeEcsReferences)
        {
            references.Add(MetadataReference.CreateFromFile(typeof(Paradise.ECS.ComponentAttribute).Assembly.Location));
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ComponentGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        return driver.GetRunResult();
    }

    /// <summary>
    /// Runs the generator and returns the generated source texts.
    /// </summary>
    public static ImmutableArray<(string HintName, string Source)> GetGeneratedSources(string source)
    {
        var result = RunGenerator(source);
        return [.. result.GeneratedTrees.Select(t => (
            Path.GetFileName(t.FilePath),
            t.GetText().ToString()
        ))];
    }

    /// <summary>
    /// Runs the generator and returns diagnostics.
    /// </summary>
    public static ImmutableArray<Diagnostic> GetDiagnostics(string source)
    {
        var result = RunGenerator(source);
        return result.Diagnostics;
    }

    /// <summary>
    /// Gets the generated source for a specific hint name.
    /// </summary>
    public static string? GetGeneratedSource(string source, string hintName)
    {
        var sources = GetGeneratedSources(source);
        return sources.FirstOrDefault(s => s.HintName == hintName).Source;
    }
}
