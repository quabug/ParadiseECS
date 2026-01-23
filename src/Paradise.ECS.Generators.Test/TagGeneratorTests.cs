namespace Paradise.ECS.Generators.Test;

/// <summary>
/// Tests for TagGenerator functionality.
/// </summary>
public class TagGeneratorBasicTests
{
    [Test]
    public async Task SimpleTag_GeneratesPartialStruct()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Tag]
            public partial struct EnemyTag;
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        await Assert.That(sources.Length).IsGreaterThan(0);

        // Tag files are prefixed with "Tag_"
        var generated = sources.FirstOrDefault(s => s.HintName == "Tag_TestNamespace_EnemyTag.g.cs").Source;
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("partial struct EnemyTag : global::Paradise.ECS.ITag");
        await Assert.That(generated).Contains("public static global::Paradise.ECS.TagId TagId");
    }

    [Test]
    public async Task Tag_WithManualId_AssignsIdAtRuntime()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Tag(Id = 5)]
            public partial struct CustomIdTag;
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);
        // Check TagRegistry for manual ID assignment
        var tagRegistry = sources.FirstOrDefault(s => s.HintName == "TagRegistry.g.cs").Source;

        await Assert.That(tagRegistry).IsNotNull();
        // Manual ID 5 should appear in the registry initialization
        await Assert.That(tagRegistry).Contains("ManualId, global::System.Action<global::Paradise.ECS.TagId>");
    }

    [Test]
    public async Task MultipleTags_GenerateTagRegistry()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Tag]
            public partial struct EnemyTag;

            [Tag]
            public partial struct PlayerTag;
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);
        var tagRegistry = sources.FirstOrDefault(s => s.HintName == "TagRegistry.g.cs").Source;

        await Assert.That(tagRegistry).IsNotNull();
        await Assert.That(tagRegistry).Contains("static class TagRegistry");
        await Assert.That(tagRegistry).Contains("EnemyTag");
        await Assert.That(tagRegistry).Contains("PlayerTag");
    }
}

/// <summary>
/// Tests for TagMask sizing based on maximum TagId.
/// </summary>
public class TagGeneratorMaskSizingTests
{
    /// <summary>
    /// Regression test: TagMask should be sized based on max TagId, not tag count.
    /// A single tag with Id=100 should use ImmutableBitSet128, not ImmutableBitSet32.
    /// </summary>
    [Test]
    public async Task Tag_WithHighManualId_UsesAppropriatelySizedMask()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Tag(Id = 100)]
            public partial struct HighIdTag;
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);
        var tagAliases = sources.FirstOrDefault(s => s.HintName == "TagAliases.g.cs").Source;

        await Assert.That(tagAliases).IsNotNull();
        // With Id=100, we need at least 101 bits, so ImmutableBitSet128 is required
        await Assert.That(tagAliases).Contains("ImmutableBitSet<global::Paradise.ECS.Bit128>");
    }

    /// <summary>
    /// Tag with Id=33 requires 34 bits, so SmallBitSet&lt;ulong&gt; is needed.
    /// </summary>
    [Test]
    public async Task Tag_WithId33_UsesSmallBitSetUlong()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Tag(Id = 33)]
            public partial struct Tag33;
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);
        var tagAliases = sources.FirstOrDefault(s => s.HintName == "TagAliases.g.cs").Source;

        await Assert.That(tagAliases).IsNotNull();
        // With Id=33, we need at least 34 bits, so SmallBitSet<ulong> is required
        await Assert.That(tagAliases).Contains("SmallBitSet<ulong>");
    }

    /// <summary>
    /// Tag with Id=31 (max for 32-bit) should use SmallBitSet&lt;uint&gt;.
    /// </summary>
    [Test]
    public async Task Tag_WithId31_UsesSmallBitSetUint()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Tag(Id = 31)]
            public partial struct Tag31;
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);
        var tagAliases = sources.FirstOrDefault(s => s.HintName == "TagAliases.g.cs").Source;

        await Assert.That(tagAliases).IsNotNull();
        // With Id=31, we need 32 bits, SmallBitSet<uint> is sufficient
        await Assert.That(tagAliases).Contains("SmallBitSet<uint>");
    }

    /// <summary>
    /// Multiple tags with one having high Id should size mask appropriately.
    /// </summary>
    [Test]
    public async Task MixedTags_WithOneHighId_UsesMaskForHighestId()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Tag]
            public partial struct AutoTag1;

            [Tag]
            public partial struct AutoTag2;

            [Tag(Id = 200)]
            public partial struct HighIdTag;
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);
        var tagAliases = sources.FirstOrDefault(s => s.HintName == "TagAliases.g.cs").Source;

        await Assert.That(tagAliases).IsNotNull();
        // With Id=200, we need at least 201 bits, so Bit256 is required
        await Assert.That(tagAliases).Contains("ImmutableBitSet<global::Paradise.ECS.Bit256>");
    }
}
