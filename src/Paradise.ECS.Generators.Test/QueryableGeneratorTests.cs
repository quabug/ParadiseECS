namespace Paradise.ECS.Generators.Test;

/// <summary>
/// Tests for nested queryable name collision fix.
/// Verifies that nested queryables with the same simple name in different containing types
/// generate unique builder/query struct names to avoid compile-time collisions.
/// </summary>
public class QueryableGeneratorNestedNameCollisionTests
{
    /// <summary>
    /// Verifies that nested queryables with same simple name get unique builder names
    /// by incorporating containing type names into the generated struct names.
    /// </summary>
    [Test]
    public async Task NestedQueryables_WithSameName_GenerateUniqueBuilderNames()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component("11111111-1111-1111-1111-111111111111")]
            public partial struct TestComp { public int Value; }

            public partial class ContainerA
            {
                [Queryable]
                [With<TestComp>]
                public readonly ref partial struct Player;
            }

            public partial class ContainerB
            {
                [Queryable]
                [With<TestComp>]
                public readonly ref partial struct Player;
            }
            """;

        var sources = GeneratorTestHelper.GetQueryableGeneratedSources(source);

        // Find the generated files for both queryables
        var containerAGenerated = sources.FirstOrDefault(s =>
            s.HintName.Contains("ContainerA", StringComparison.Ordinal) &&
            s.HintName.Contains("Player", StringComparison.Ordinal)).Source;
        var containerBGenerated = sources.FirstOrDefault(s =>
            s.HintName.Contains("ContainerB", StringComparison.Ordinal) &&
            s.HintName.Contains("Player", StringComparison.Ordinal)).Source;

        await Assert.That(containerAGenerated).IsNotNull();
        await Assert.That(containerBGenerated).IsNotNull();

        // Verify that generated builder names include the containing type name
        // ContainerA.Player should generate ContainerAPlayerQueryBuilder
        await Assert.That(containerAGenerated!).Contains("ContainerAPlayerQueryBuilder");
        await Assert.That(containerAGenerated).Contains("ContainerAPlayerQuery<");
        await Assert.That(containerAGenerated).Contains("ContainerAPlayerChunkQueryBuilder");
        await Assert.That(containerAGenerated).Contains("ContainerAPlayerChunkQuery<");

        // ContainerB.Player should generate ContainerBPlayerQueryBuilder
        await Assert.That(containerBGenerated!).Contains("ContainerBPlayerQueryBuilder");
        await Assert.That(containerBGenerated).Contains("ContainerBPlayerQuery<");
        await Assert.That(containerBGenerated).Contains("ContainerBPlayerChunkQueryBuilder");
        await Assert.That(containerBGenerated).Contains("ContainerBPlayerChunkQuery<");

        // Ensure they don't just use "PlayerQueryBuilder" without prefix
        // Check that the names are actually unique between the two files
        await Assert.That(containerAGenerated).DoesNotContain("struct PlayerQueryBuilder");
        await Assert.That(containerBGenerated).DoesNotContain("struct PlayerQueryBuilder");
    }

    /// <summary>
    /// Verifies that non-nested queryables still use simple names (no prefix needed).
    /// </summary>
    [Test]
    public async Task NonNestedQueryable_UsesSimpleName()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component("22222222-2222-2222-2222-222222222222")]
            public partial struct SimpleComp { public int Value; }

            [Queryable]
            [With<SimpleComp>]
            public readonly ref partial struct SimpleQuery;
            """;

        var sources = GeneratorTestHelper.GetQueryableGeneratedSources(source);
        var generated = sources.FirstOrDefault(s =>
            s.HintName.Contains("SimpleQuery", StringComparison.Ordinal)).Source;

        await Assert.That(generated).IsNotNull();

        // Non-nested queryable should use simple name
        await Assert.That(generated!).Contains("struct SimpleQueryQueryBuilder");
        await Assert.That(generated).Contains("struct SimpleQueryQuery<");
    }

    /// <summary>
    /// Verifies deeply nested queryables include all containing type names.
    /// </summary>
    [Test]
    public async Task DeeplyNestedQueryable_IncludesAllContainingTypeNames()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component("33333333-3333-3333-3333-333333333333")]
            public partial struct DeepComp { public int Value; }

            public partial class Level1
            {
                public partial class Level2
                {
                    [Queryable]
                    [With<DeepComp>]
                    public readonly ref partial struct DeepQuery;
                }
            }
            """;

        var sources = GeneratorTestHelper.GetQueryableGeneratedSources(source);
        var generated = sources.FirstOrDefault(s =>
            s.HintName.Contains("DeepQuery", StringComparison.Ordinal)).Source;

        await Assert.That(generated).IsNotNull();

        // Should include both Level1 and Level2 in the name
        await Assert.That(generated!).Contains("Level1Level2DeepQueryQueryBuilder");
        await Assert.That(generated).Contains("Level1Level2DeepQueryQuery<");
    }
}

/// <summary>
/// Tests for QueryableGenerator duplicate ID detection (PECS014).
/// Note: The Roslyn test infrastructure cannot properly test generators that depend on
/// other generators' output (With&lt;T&gt; requires T to implement IComponent).
/// The duplicate ID detection was verified to work via manual compilation test:
/// Adding duplicate IDs to TestComponents.cs produced the expected PECS014 error.
/// </summary>
public class QueryableGeneratorDuplicateManualIdTests
{
    /// <summary>
    /// Verifies that the PECS014 diagnostic descriptor is properly defined.
    /// The actual duplicate detection is tested via compile-time verification.
    /// </summary>
    [Test]
    public async Task DuplicateQueryableIdDiagnostic_HasCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.DuplicateQueryableId;

        await Assert.That(descriptor.Id).IsEqualTo("PECS014");
        await Assert.That(descriptor.Title.ToString(System.Globalization.CultureInfo.InvariantCulture)).IsEqualTo("Duplicate queryable ID");
        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        await Assert.That(descriptor.IsEnabledByDefault).IsTrue();
    }

    /// <summary>
    /// Verifies the diagnostic message format contains expected placeholders.
    /// </summary>
    [Test]
    public async Task DuplicateQueryableIdDiagnostic_MessageFormat_ContainsPlaceholders()
    {
        var descriptor = DiagnosticDescriptors.DuplicateQueryableId;
        var format = descriptor.MessageFormat.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Message format should include ID and type names placeholders
        await Assert.That(format).Contains("{0}"); // ID placeholder
        await Assert.That(format).Contains("{1}"); // Type names placeholder
    }
}
