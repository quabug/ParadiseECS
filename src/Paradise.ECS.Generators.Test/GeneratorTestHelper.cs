using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

#pragma warning disable RS1035 // Do not use banned APIs

namespace Paradise.ECS.Generators.Test;

/// <summary>
/// Helper class for testing source generators.
/// </summary>
public static class GeneratorTestHelper
{
    /// <summary>
    /// Creates a compilation with the given source code and runs the ComponentGenerator.
    /// </summary>
    public static GeneratorDriverRunResult RunGenerator(string source, bool includeEcsReferences = true, string? rootNamespace = null, bool includeTagReference = true)
    {
        return RunGenerators(source, [new ComponentGenerator()], includeEcsReferences, rootNamespace, includeTagReference);
    }

    /// <summary>
    /// Creates a compilation with the given source code and runs the QueryableGenerator.
    /// </summary>
    public static GeneratorDriverRunResult RunQueryableGenerator(string source, bool includeEcsReferences = true, string? rootNamespace = null, bool includeTagReference = true)
    {
        return RunGenerators(source, [new ComponentGenerator(), new QueryableGenerator()], includeEcsReferences, rootNamespace, includeTagReference);
    }

    /// <summary>
    /// Creates a compilation with the given source code and runs specified generators.
    /// </summary>
    private static GeneratorDriverRunResult RunGenerators(string source, IIncrementalGenerator[] generators, bool includeEcsReferences = true, string? rootNamespace = null, bool includeTagReference = true)
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
            // Add Paradise.ECS.Tag reference if requested (enables tag generation)
            if (includeTagReference)
            {
                references.Add(MetadataReference.CreateFromFile(typeof(Paradise.ECS.TagAttribute).Assembly.Location));
            }
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Create analyzer config options provider if root namespace is specified
        var optionsProvider = rootNamespace != null
            ? new TestAnalyzerConfigOptionsProvider(rootNamespace)
            : null;

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: generators.Select(g => g.AsSourceGenerator()).ToArray(),
            optionsProvider: optionsProvider);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        return driver.GetRunResult();
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestGlobalOptions _globalOptions;

        public TestAnalyzerConfigOptionsProvider(string rootNamespace)
        {
            _globalOptions = new TestGlobalOptions(rootNamespace);
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestAnalyzerConfigOptions.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => TestAnalyzerConfigOptions.Empty;

        private sealed class TestGlobalOptions : AnalyzerConfigOptions
        {
            private readonly string _rootNamespace;

            public TestGlobalOptions(string rootNamespace)
            {
                _rootNamespace = rootNamespace;
            }

            public override bool TryGetValue(string key, out string value)
            {
                if (key == "build_property.RootNamespace")
                {
                    value = _rootNamespace;
                    return true;
                }
                value = null!;
                return false;
            }
        }

        private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            public static readonly TestAnalyzerConfigOptions Empty = new();

            public override bool TryGetValue(string key, out string value)
            {
                value = null!;
                return false;
            }
        }
    }

    /// <summary>
    /// Runs the generator and returns the generated source texts.
    /// </summary>
    public static ImmutableArray<(string HintName, string Source)> GetGeneratedSources(string source, bool includeTagReference = true)
    {
        var result = RunGenerator(source, includeTagReference: includeTagReference);
        return [.. result.GeneratedTrees.Select(t => (
            Path.GetFileName(t.FilePath),
            t.GetText().ToString()
        ))];
    }

    /// <summary>
    /// Runs the queryable generator and returns the generated source texts.
    /// </summary>
    public static ImmutableArray<(string HintName, string Source)> GetQueryableGeneratedSources(string source)
    {
        var result = RunQueryableGenerator(source);
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
    /// Runs the queryable generator and returns diagnostics.
    /// </summary>
    public static ImmutableArray<Diagnostic> GetQueryableDiagnostics(string source)
    {
        var result = RunQueryableGenerator(source);
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

    /// <summary>
    /// Gets the generated source for a specific hint name with a custom root namespace.
    /// </summary>
    public static string? GetGeneratedSource(string source, string hintName, string rootNamespace)
    {
        var result = RunGenerator(source, rootNamespace: rootNamespace);
        var sources = result.GeneratedTrees.Select(t => (
            HintName: Path.GetFileName(t.FilePath),
            Source: t.GetText().ToString()
        ));
        return sources.FirstOrDefault(s => s.HintName == hintName).Source;
    }

    /// <summary>
    /// Creates a compilation with the given source code and runs the specified analyzer.
    /// </summary>
    public static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync<TAnalyzer>(string source, bool includeEcsReferences = true)
        where TAnalyzer : DiagnosticAnalyzer, new()
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
            references.Add(MetadataReference.CreateFromFile(typeof(Paradise.ECS.TagAttribute).Assembly.Location));
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new TAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers([analyzer]);

        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        return diagnostics;
    }

    /// <summary>
    /// Gets analyzer diagnostics filtered by ID.
    /// </summary>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync<TAnalyzer>(string source, string diagnosticId)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var allDiagnostics = await RunAnalyzerAsync<TAnalyzer>(source);
        return [.. allDiagnostics.Where(d => d.Id == diagnosticId)];
    }
}
