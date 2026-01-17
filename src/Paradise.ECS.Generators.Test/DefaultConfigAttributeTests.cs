using Microsoft.CodeAnalysis;

namespace Paradise.ECS.Generators.Test;

public class DefaultConfigAttributeTests
{
    [Test]
    public async Task WorldDefault_OnConfig_GeneratesWorldAlias()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [WorldDefault]
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
    public async Task WorldDefault_OnConfig_UsesGeneratedComponentRegistry()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [WorldDefault]
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
    public async Task NoWorldDefault_NoWorldAlias()
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
        await Assert.That(aliases).DoesNotContain("global using World");
    }

    [Test]
    public async Task MultipleWorldDefaults_ForConfig_ReportsPECS009()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [WorldDefault]
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

            [WorldDefault]
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
        await Assert.That(pecs009.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("IConfig");
    }

    [Test]
    public async Task WorldDefault_OnInvalidType_ReportsPECS010()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [WorldDefault]
            public struct NotAnInterface
            {
                public int Value;
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        var pecs010 = diagnostics.FirstOrDefault(d => d.Id == "PECS010");
        await Assert.That(pecs010).IsNotNull();
        await Assert.That(pecs010!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(pecs010.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("TestNamespace.NotAnInterface");
    }

    [Test]
    public async Task WorldDefault_OnStorage_OverridesBitType()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [WorldDefault]
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

            [WorldDefault]
            public struct MyBit256 : IStorage
            {
                private ulong _e0, _e1, _e2, _e3;
            }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        // Should use the custom storage type instead of auto-determined Bit64
        await Assert.That(aliases).Contains("global::TestNamespace.MyBit256");
        await Assert.That(aliases).Contains("global using ComponentMask = global::Paradise.ECS.ImmutableBitSet<global::TestNamespace.MyBit256>");
        await Assert.That(aliases).Contains("global using World = global::Paradise.ECS.World<global::TestNamespace.MyBit256");
    }

    [Test]
    public async Task WorldDefault_OnStorage_WithoutConfig_NoWorldAlias()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [WorldDefault]
            public struct MyBit256 : IStorage
            {
                private ulong _e0, _e1, _e2, _e3;
            }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        // Should use the custom storage for ComponentMask
        await Assert.That(aliases).Contains("global using ComponentMask = global::Paradise.ECS.ImmutableBitSet<global::TestNamespace.MyBit256>");
        // But no World alias without Config
        await Assert.That(aliases).DoesNotContain("global using World");
    }

    [Test]
    public async Task WorldDefault_UsesCustomRootNamespace_ForRegistry()
    {
        const string source = """
            using Paradise.ECS;

            [assembly: ComponentRegistryNamespace("MyGame.ECS")]

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [WorldDefault]
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

    [Test]
    public async Task WorldDefault_AllThreeTypes_GeneratesCorrectAlias()
    {
        const string source = """
            using Paradise.ECS;
            using System;
            using System.Collections.Immutable;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [WorldDefault]
            public struct MyBit128 : IStorage
            {
                private ulong _e0, _e1;
            }

            [WorldDefault]
            public sealed class MyRegistry : IComponentRegistry
            {
                public static ComponentId GetId(Type type) => ComponentId.Invalid;
                public static bool TryGetId(Type type, out ComponentId id) { id = default; return false; }
                public static ComponentId GetId(Guid guid) => ComponentId.Invalid;
                public static bool TryGetId(Guid guid, out ComponentId id) { id = default; return false; }
                public static ImmutableArray<ComponentTypeInfo> TypeInfos => ImmutableArray<ComponentTypeInfo>.Empty;
            }

            [WorldDefault]
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
        await Assert.That(aliases).Contains("global using World = global::Paradise.ECS.World<global::TestNamespace.MyBit128, global::TestNamespace.MyRegistry, global::TestNamespace.MyConfig>");
    }

    [Test]
    public async Task MultipleWorldDefaults_ForStorage_ReportsPECS009()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [WorldDefault]
            public struct MyBit128A : IStorage
            {
                private ulong _e0, _e1;
            }

            [WorldDefault]
            public struct MyBit128B : IStorage
            {
                private ulong _e0, _e1;
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        var pecs009 = diagnostics.FirstOrDefault(d => d.Id == "PECS009");
        await Assert.That(pecs009).IsNotNull();
        await Assert.That(pecs009!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(pecs009.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("IStorage");
    }
}
