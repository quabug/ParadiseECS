using Microsoft.CodeAnalysis;

namespace Paradise.ECS.Generators.Test;

public class DefaultConfigAttributeTests
{
    [Test]
    public async Task DefaultConfig_OnConfig_GeneratesWorldAlias()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [DefaultConfig]
            public struct MyConfig : IConfig
            {
                public static int ChunkSize => 16 * 1024;
                public static int MaxMetaBlocks => 1024;
                public static int EntityIdByteSize => sizeof(int);
                public int DefaultChunkCapacity => 256;
                public int DefaultEntityCapacity => 1024;
                public IAllocator ChunkAllocator => NativeMemoryAllocator.Shared;
                public IAllocator MetadataAllocator => NativeMemoryAllocator.Shared;
                public IAllocator LayoutAllocator => NativeMemoryAllocator.Shared;
            }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("global using World = global::Paradise.ECS.World<");
        await Assert.That(aliases).Contains("global::TestNamespace.MyConfig>");
    }

    [Test]
    public async Task DefaultConfig_OnConfig_GeneratesAllAliases()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [DefaultConfig]
            public struct MyConfig : IConfig
            {
                public static int ChunkSize => 16 * 1024;
                public static int MaxMetaBlocks => 1024;
                public static int EntityIdByteSize => sizeof(int);
                public int DefaultChunkCapacity => 256;
                public int DefaultEntityCapacity => 1024;
                public IAllocator ChunkAllocator => NativeMemoryAllocator.Shared;
                public IAllocator MetadataAllocator => NativeMemoryAllocator.Shared;
                public IAllocator LayoutAllocator => NativeMemoryAllocator.Shared;
            }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("global using ComponentMask = global::Paradise.ECS.ImmutableBitSet<");
        await Assert.That(aliases).Contains("global using ChunkManager = global::Paradise.ECS.ChunkManager<global::TestNamespace.MyConfig>");
        await Assert.That(aliases).Contains("global using SharedArchetypeMetadata = global::Paradise.ECS.SharedArchetypeMetadata<");
        await Assert.That(aliases).Contains("global using ArchetypeRegistry = global::Paradise.ECS.ArchetypeRegistry<");
        await Assert.That(aliases).Contains("global using World = global::Paradise.ECS.World<");
    }

    [Test]
    public async Task DefaultConfig_OnConfig_UsesGeneratedComponentRegistry()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [DefaultConfig]
            public struct MyConfig : IConfig
            {
                public static int ChunkSize => 16 * 1024;
                public static int MaxMetaBlocks => 1024;
                public static int EntityIdByteSize => sizeof(int);
                public int DefaultChunkCapacity => 256;
                public int DefaultEntityCapacity => 1024;
                public IAllocator ChunkAllocator => NativeMemoryAllocator.Shared;
                public IAllocator MetadataAllocator => NativeMemoryAllocator.Shared;
                public IAllocator LayoutAllocator => NativeMemoryAllocator.Shared;
            }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        // Should use auto-generated ComponentRegistry from Paradise.ECS namespace (default)
        await Assert.That(aliases).Contains("global::Paradise.ECS.ComponentRegistry");
    }

    [Test]
    public async Task DefaultConfig_OnConfig_UsesAutoDeterminedBitType()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [DefaultConfig]
            public struct MyConfig : IConfig
            {
                public static int ChunkSize => 16 * 1024;
                public static int MaxMetaBlocks => 1024;
                public static int EntityIdByteSize => sizeof(int);
                public int DefaultChunkCapacity => 256;
                public int DefaultEntityCapacity => 1024;
                public IAllocator ChunkAllocator => NativeMemoryAllocator.Shared;
                public IAllocator MetadataAllocator => NativeMemoryAllocator.Shared;
                public IAllocator LayoutAllocator => NativeMemoryAllocator.Shared;
            }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        // 1 component -> Bit64
        await Assert.That(aliases).Contains("global::Paradise.ECS.Bit64");
    }

    [Test]
    public async Task NoDefaultConfig_UsesBuiltInDefaultConfigFallback()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("global using ComponentMask");
        // Falls back to Paradise.ECS.DefaultConfig when no [DefaultConfig] attribute is found
        await Assert.That(aliases).Contains("global::Paradise.ECS.DefaultConfig");
        await Assert.That(aliases).Contains("global using World");
    }

    [Test]
    public async Task MultipleDefaultConfigs_ReportsPECS009()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [DefaultConfig]
            public struct Config1 : IConfig
            {
                public static int ChunkSize => 16 * 1024;
                public static int MaxMetaBlocks => 1024;
                public static int EntityIdByteSize => sizeof(int);
                public int DefaultChunkCapacity => 256;
                public int DefaultEntityCapacity => 1024;
                public IAllocator ChunkAllocator => NativeMemoryAllocator.Shared;
                public IAllocator MetadataAllocator => NativeMemoryAllocator.Shared;
                public IAllocator LayoutAllocator => NativeMemoryAllocator.Shared;
            }

            [DefaultConfig]
            public struct Config2 : IConfig
            {
                public static int ChunkSize => 16 * 1024;
                public static int MaxMetaBlocks => 1024;
                public static int EntityIdByteSize => sizeof(int);
                public int DefaultChunkCapacity => 256;
                public int DefaultEntityCapacity => 1024;
                public IAllocator ChunkAllocator => NativeMemoryAllocator.Shared;
                public IAllocator MetadataAllocator => NativeMemoryAllocator.Shared;
                public IAllocator LayoutAllocator => NativeMemoryAllocator.Shared;
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        var pecs009 = diagnostics.FirstOrDefault(d => d.Id == "PECS009");
        await Assert.That(pecs009).IsNotNull();
        await Assert.That(pecs009!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(pecs009.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("Config1");
        await Assert.That(pecs009.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("Config2");
    }

    [Test]
    public async Task DefaultConfig_OnInvalidType_ReportsPECS010()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [DefaultConfig]
            public struct NotAnIConfig
            {
                public int Value;
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        var pecs010 = diagnostics.FirstOrDefault(d => d.Id == "PECS010");
        await Assert.That(pecs010).IsNotNull();
        await Assert.That(pecs010!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(pecs010.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("TestNamespace.NotAnIConfig");
    }

    [Test]
    public async Task DefaultConfig_UsesCustomRootNamespace_ForRegistry()
    {
        const string source = """
            using Paradise.ECS;

            [assembly: ComponentRegistryNamespace("MyGame.ECS")]

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [DefaultConfig]
            public struct MyConfig : IConfig
            {
                public static int ChunkSize => 16 * 1024;
                public static int MaxMetaBlocks => 1024;
                public static int EntityIdByteSize => sizeof(int);
                public int DefaultChunkCapacity => 256;
                public int DefaultEntityCapacity => 1024;
                public IAllocator ChunkAllocator => NativeMemoryAllocator.Shared;
                public IAllocator MetadataAllocator => NativeMemoryAllocator.Shared;
                public IAllocator LayoutAllocator => NativeMemoryAllocator.Shared;
            }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("global::MyGame.ECS.ComponentRegistry");
    }
}
