namespace Paradise.ECS.Generators.Test;

/// <summary>
/// Tests for queryable generator structure.
/// Verifies that Data and ChunkData structs implement IQueryData/IQueryChunkData interfaces,
/// and that static Query/ChunkQuery methods are generated.
/// </summary>
public class QueryableGeneratorStructureTests
{
    /// <summary>
    /// Verifies that nested queryables generate proper Data and ChunkData types.
    /// </summary>
    [Test]
    public async Task NestedQueryables_GenerateDataAndChunkDataTypes()
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

        // Both should have nested Data and ChunkData types implementing interfaces
        await Assert.That(containerAGenerated!).Contains("public readonly ref struct Data<TMask, TConfig>");
        await Assert.That(containerAGenerated).Contains(": global::Paradise.ECS.IQueryData<Data<TMask, TConfig>, TMask, TConfig>");
        await Assert.That(containerAGenerated).Contains("public readonly ref struct ChunkData<TMask, TConfig>");
        await Assert.That(containerAGenerated).Contains(": global::Paradise.ECS.IQueryChunkData<ChunkData<TMask, TConfig>, TMask, TConfig>");

        await Assert.That(containerBGenerated!).Contains("public readonly ref struct Data<TMask, TConfig>");
        await Assert.That(containerBGenerated).Contains(": global::Paradise.ECS.IQueryData<Data<TMask, TConfig>, TMask, TConfig>");
        await Assert.That(containerBGenerated).Contains("public readonly ref struct ChunkData<TMask, TConfig>");
        await Assert.That(containerBGenerated).Contains(": global::Paradise.ECS.IQueryChunkData<ChunkData<TMask, TConfig>, TMask, TConfig>");

        // Verify static Query and ChunkQuery methods
        await Assert.That(containerAGenerated).Contains("public static global::Paradise.ECS.QueryResult<Data<TMask, TConfig>, TMask, TConfig> Query<TMask, TConfig>");
        await Assert.That(containerAGenerated).Contains("public static global::Paradise.ECS.ChunkQueryResult<ChunkData<TMask, TConfig>, TMask, TConfig> ChunkQuery<TMask, TConfig>");
    }

    /// <summary>
    /// Verifies that non-nested queryables generate proper Data and ChunkData types.
    /// </summary>
    [Test]
    public async Task NonNestedQueryable_GeneratesDataAndChunkDataTypes()
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

        // Should have nested Data type implementing IQueryData
        await Assert.That(generated!).Contains("public readonly ref struct Data<TMask, TConfig>");
        await Assert.That(generated).Contains(": global::Paradise.ECS.IQueryData<Data<TMask, TConfig>, TMask, TConfig>");
        // Should not have prefixed type names
        await Assert.That(generated).DoesNotContain("SimpleQueryData");
        await Assert.That(generated).DoesNotContain("SimpleQueryChunkData");
    }

    /// <summary>
    /// Verifies deeply nested queryables generate proper types.
    /// </summary>
    [Test]
    public async Task DeeplyNestedQueryable_GeneratesDataAndChunkDataTypes()
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

        // Should have nested types inside DeepQuery
        await Assert.That(generated!).Contains("public readonly ref struct Data<TMask, TConfig>");
        await Assert.That(generated).Contains(": global::Paradise.ECS.IQueryData<Data<TMask, TConfig>, TMask, TConfig>");
        await Assert.That(generated).DoesNotContain("Level1Level2DeepQueryData");
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

/// <summary>
/// Tests for SuppressGlobalUsingsAttribute functionality in QueryableGenerator.
/// </summary>
public class QueryableGeneratorSuppressGlobalUsingsTests
{
    [Test]
    public async Task SuppressGlobalUsings_WhenAttributePresent_DoesNotGenerateQueryableRegistryAlias()
    {
        const string source = """
            using Paradise.ECS;

            [assembly: SuppressGlobalUsings]

            namespace TestNamespace;

            [Component("44444444-4444-4444-4444-444444444444")]
            public partial struct SuppressTestComp { public int Value; }

            [Queryable]
            [With<SuppressTestComp>]
            public readonly ref partial struct SuppressTestQuery;
            """;

        var sources = GeneratorTestHelper.GetQueryableGeneratedSources(source);
        var aliases = sources.FirstOrDefault(s => s.HintName == "QueryableAliases.g.cs").Source;

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).DoesNotContain("global using QueryableRegistry =");
    }

    [Test]
    public async Task SuppressGlobalUsings_WhenAttributePresent_IncludesSuppressedComment()
    {
        const string source = """
            using Paradise.ECS;

            [assembly: SuppressGlobalUsings]

            namespace TestNamespace;

            [Component("55555555-5555-5555-5555-555555555555")]
            public partial struct SuppressCommentComp { public int Value; }

            [Queryable]
            [With<SuppressCommentComp>]
            public readonly ref partial struct SuppressCommentQuery;
            """;

        var sources = GeneratorTestHelper.GetQueryableGeneratedSources(source);
        var aliases = sources.FirstOrDefault(s => s.HintName == "QueryableAliases.g.cs").Source;

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("All global usings suppressed by [assembly: SuppressGlobalUsings]");
    }

    [Test]
    public async Task SuppressGlobalUsings_WhenAttributeAbsent_GeneratesQueryableRegistryAlias()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component("66666666-6666-6666-6666-666666666666")]
            public partial struct NoSuppressComp { public int Value; }

            [Queryable]
            [With<NoSuppressComp>]
            public readonly ref partial struct NoSuppressQuery;
            """;

        var sources = GeneratorTestHelper.GetQueryableGeneratedSources(source);
        var aliases = sources.FirstOrDefault(s => s.HintName == "QueryableAliases.g.cs").Source;

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("global using QueryableRegistry =");
        await Assert.That(aliases).DoesNotContain("All global usings suppressed");
    }

    [Test]
    public async Task SuppressGlobalUsings_QueryableRegistryGenerationStillWorks()
    {
        const string source = """
            using Paradise.ECS;

            [assembly: SuppressGlobalUsings]

            namespace TestNamespace;

            [Component("77777777-7777-7777-7777-777777777777")]
            public partial struct SuppressRegistryComp { public int Value; }

            [Queryable]
            [With<SuppressRegistryComp>]
            public readonly ref partial struct SuppressRegistryQuery;
            """;

        var sources = GeneratorTestHelper.GetQueryableGeneratedSources(source);
        var registry = sources.FirstOrDefault(s => s.HintName == "QueryableRegistry.g.cs").Source;
        var partialStruct = sources.FirstOrDefault(s =>
            s.HintName.Contains("SuppressRegistryQuery", StringComparison.Ordinal)).Source;

        // Registry should still be generated
        await Assert.That(registry).IsNotNull();
        await Assert.That(registry).Contains("public static class QueryableRegistry<TMask>");

        // Partial struct should still be generated
        await Assert.That(partialStruct).IsNotNull();
        await Assert.That(partialStruct).Contains("public static int QueryableId =>");
    }

    [Test]
    public async Task SuppressGlobalUsings_ShowsFullyQualifiedTypeReference()
    {
        const string source = """
            using Paradise.ECS;

            [assembly: SuppressGlobalUsings]

            namespace TestNamespace;

            [Component("88888888-8888-8888-8888-888888888888")]
            public partial struct SuppressRefComp { public int Value; }

            [Queryable]
            [With<SuppressRefComp>]
            public readonly ref partial struct SuppressRefQuery;
            """;

        var sources = GeneratorTestHelper.GetQueryableGeneratedSources(source);
        var aliases = sources.FirstOrDefault(s => s.HintName == "QueryableAliases.g.cs").Source;

        await Assert.That(aliases).IsNotNull();
        // Should show how to reference QueryableRegistry when suppressed
        await Assert.That(aliases).Contains("global::Paradise.ECS.QueryableRegistry<");
    }
}
