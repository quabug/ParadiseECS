using Microsoft.CodeAnalysis;

namespace Paradise.ECS.Generators.Test;

/// <summary>
/// Tests for the DisposableRefStructAnalyzer.
/// </summary>
public class DisposableRefStructAnalyzerTests
{
    private const string DisposableRefStructDefinition = """
        public readonly ref struct DisposableResource
        {
            public void Dispose() { }
        }

        public static class Factory
        {
            public static DisposableResource Create() => default;
        }
        """;

    [Test]
    public async Task LocalDeclaration_WithoutUsing_ReportsDiagnostic()
    {
        var source = $$"""
            {{DisposableRefStructDefinition}}

            public class Test
            {
                public void Method()
                {
                    var resource = Factory.Create();
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostics[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("DisposableResource");
    }

    [Test]
    public async Task LocalDeclaration_WithUsingDeclaration_NoDiagnostic()
    {
        var source = $$"""
            {{DisposableRefStructDefinition}}

            public class Test
            {
                public void Method()
                {
                    using var resource = Factory.Create();
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task LocalDeclaration_WithUsingStatement_NoDiagnostic()
    {
        var source = $$"""
            {{DisposableRefStructDefinition}}

            public class Test
            {
                public void Method()
                {
                    using (var resource = Factory.Create())
                    {
                    }
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task LocalDeclaration_WithExplicitDispose_NoDiagnostic()
    {
        var source = $$"""
            {{DisposableRefStructDefinition}}

            public class Test
            {
                public void Method()
                {
                    var resource = Factory.Create();
                    resource.Dispose();
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task DiscardedReturnValue_ReportsDiagnostic()
    {
        var source = $$"""
            {{DisposableRefStructDefinition}}

            public class Test
            {
                public void Method()
                {
                    Factory.Create();
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task ExplicitDiscard_ReportsDiagnostic()
    {
        var source = $$"""
            {{DisposableRefStructDefinition}}

            public class Test
            {
                public void Method()
                {
                    _ = Factory.Create();
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task NonDisposableRefStruct_NoDiagnostic()
    {
        var source = """
            public readonly ref struct NonDisposable
            {
                public int Value { get; }
            }

            public static class Factory
            {
                public static NonDisposable Create() => default;
            }

            public class Test
            {
                public void Method()
                {
                    var resource = Factory.Create();
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task RegularClass_NoDiagnostic()
    {
        var source = """
            public class RegularClass : System.IDisposable
            {
                public void Dispose() { }
            }

            public static class Factory
            {
                public static RegularClass Create() => new();
            }

            public class Test
            {
                public void Method()
                {
                    var resource = Factory.Create();
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        // The analyzer only targets ref structs, not regular classes
        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task MultipleUndisposedVariables_ReportsMultipleDiagnostics()
    {
        var source = $$"""
            {{DisposableRefStructDefinition}}

            public class Test
            {
                public void Method()
                {
                    var resource1 = Factory.Create();
                    var resource2 = Factory.Create();
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        await Assert.That(diagnostics.Length).IsEqualTo(2);
    }

    [Test]
    public async Task MixedDisposedAndUndisposed_ReportsOnlyUndisposed()
    {
        var source = $$"""
            {{DisposableRefStructDefinition}}

            public class Test
            {
                public void Method()
                {
                    using var disposed = Factory.Create();
                    var undisposed = Factory.Create();
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task MemberAccessOnInvocation_ReportsDiagnostic()
    {
        var source = """
            public readonly ref struct ComponentRef
            {
                public int Value { get; set; }
                public void Dispose() { }
            }

            public static class Factory
            {
                public static ComponentRef GetComponent() => default;
            }

            public class Test
            {
                public void Method()
                {
                    Factory.GetComponent().Value = 42;
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task MemberAccessOnInvocation_WithUsing_NoDiagnostic()
    {
        var source = """
            public readonly ref struct ComponentRef
            {
                public int Value { get; set; }
                public void Dispose() { }
            }

            public static class Factory
            {
                public static ComponentRef GetComponent() => default;
            }

            public class Test
            {
                public void Method()
                {
                    using var component = Factory.GetComponent();
                    component.Value = 42;
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task DisposeCallOnInvocation_NoDiagnostic()
    {
        var source = $$"""
            {{DisposableRefStructDefinition}}

            public class Test
            {
                public void Method()
                {
                    Factory.Create().Dispose();
                }
            }
            """;

        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync<DisposableRefStructAnalyzer>(source, "PECS008");

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}
