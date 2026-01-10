using Microsoft.CodeAnalysis;

namespace Paradise.ECS.Generators.Test;

public class ComponentGeneratorBasicTests
{
    [Test]
    public async Task SimpleComponent_GeneratesPartialStruct()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position
            {
                public float X;
                public float Y;
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        await Assert.That(sources.Length).IsGreaterThan(0);

        var generated = sources.FirstOrDefault(s => s.HintName == "TestNamespace_Position.g.cs").Source;
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("partial struct Position : global::Paradise.ECS.IComponent");
        await Assert.That(generated).Contains("public static global::Paradise.ECS.ComponentId TypeId =>");
        await Assert.That(generated).Contains("new global::Paradise.ECS.ComponentId(0)");
    }

    [Test]
    public async Task SimpleComponent_GeneratesGuidProperty()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position
            {
                public float X;
            }
            """;

        var generated = GeneratorTestHelper.GetGeneratedSource(source, "TestNamespace_Position.g.cs");

        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("public static global::System.Guid Guid => global::System.Guid.Empty;");
    }

    [Test]
    public async Task SimpleComponent_GeneratesSizeProperty()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position
            {
                public float X;
            }
            """;

        var generated = GeneratorTestHelper.GetGeneratedSource(source, "TestNamespace_Position.g.cs");

        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("public static int Size { get; }");
        await Assert.That(generated).Contains("Unsafe.SizeOf<global::TestNamespace.Position>()");
    }

    [Test]
    public async Task SimpleComponent_GeneratesAlignmentProperty()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position
            {
                public float X;
            }
            """;

        var generated = GeneratorTestHelper.GetGeneratedSource(source, "TestNamespace_Position.g.cs");

        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("public static int Alignment { get; }");
        await Assert.That(generated).Contains("AlignmentHelper<global::TestNamespace.Position>.Alignment");
    }
}

public class ComponentGeneratorAlphabeticalOrderingTests
{
    [Test]
    public async Task MultipleComponents_AssignsIdsAlphabetically()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Zebra { public int A; }

            [Component]
            public partial struct Alpha { public int A; }

            [Component]
            public partial struct Beta { public int A; }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        // Alpha should get ID 0 (comes first alphabetically)
        var alphaSource = sources.FirstOrDefault(s => s.HintName == "TestNamespace_Alpha.g.cs").Source;
        await Assert.That(alphaSource).Contains("new global::Paradise.ECS.ComponentId(0)");

        // Beta should get ID 1
        var betaSource = sources.FirstOrDefault(s => s.HintName == "TestNamespace_Beta.g.cs").Source;
        await Assert.That(betaSource).Contains("new global::Paradise.ECS.ComponentId(1)");

        // Zebra should get ID 2
        var zebraSource = sources.FirstOrDefault(s => s.HintName == "TestNamespace_Zebra.g.cs").Source;
        await Assert.That(zebraSource).Contains("new global::Paradise.ECS.ComponentId(2)");
    }

    [Test]
    public async Task CrossNamespaceComponents_SortsByFullyQualifiedName()
    {
        const string source = """
            using Paradise.ECS;

            namespace A.Components
            {
                [Component]
                public partial struct ZComponent { public int X; }
            }

            namespace B.Components
            {
                [Component]
                public partial struct AComponent { public int X; }
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        // A.Components.ZComponent should come before B.Components.AComponent
        var aZComponent = sources.FirstOrDefault(s => s.HintName == "A_Components_ZComponent.g.cs").Source;
        var bAComponent = sources.FirstOrDefault(s => s.HintName == "B_Components_AComponent.g.cs").Source;

        await Assert.That(aZComponent).Contains("new global::Paradise.ECS.ComponentId(0)");
        await Assert.That(bAComponent).Contains("new global::Paradise.ECS.ComponentId(1)");
    }
}

public class ComponentGeneratorGuidTests
{
    [Test]
    public async Task ComponentWithGuid_GeneratesGuidProperty()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component("12345678-1234-1234-1234-123456789012")]
            public partial struct Position
            {
                public float X;
            }
            """;

        var generated = GeneratorTestHelper.GetGeneratedSource(source, "TestNamespace_Position.g.cs");

        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("public static global::System.Guid Guid { get; } = new global::System.Guid(\"12345678-1234-1234-1234-123456789012\")");
    }

    [Test]
    public async Task ComponentWithGuid_AddsGuidAttribute()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component("12345678-1234-1234-1234-123456789012")]
            public partial struct Position
            {
                public float X;
            }
            """;

        var generated = GeneratorTestHelper.GetGeneratedSource(source, "TestNamespace_Position.g.cs");

        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("[global::System.Runtime.InteropServices.Guid(\"12345678-1234-1234-1234-123456789012\")]");
    }

    [Test]
    public async Task ComponentWithGuid_IncludedInRegistry()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component("12345678-1234-1234-1234-123456789012")]
            public partial struct Position
            {
                public float X;
            }
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        await Assert.That(registry).IsNotNull();
        await Assert.That(registry).Contains("new global::System.Guid(\"12345678-1234-1234-1234-123456789012\")");
    }

    [Test]
    public async Task ComponentWithoutGuid_NotIncludedInGuidRegistry()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position
            {
                public float X;
            }
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        await Assert.That(registry).IsNotNull();
        // Should have empty guidToId dictionary
        await Assert.That(registry).Contains("s_guidToId");
        // Should have the Position in typeToId but not in guidToId entries
        await Assert.That(registry).Contains("typeof(global::TestNamespace.Position)");
    }
}

public class ComponentGeneratorNestedTypeTests
{
    [Test]
    public async Task NestedComponent_GeneratesCorrectly()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            public partial struct Outer
            {
                [Component]
                public partial struct Inner
                {
                    public int Value;
                }
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        // Nested types use + in FQN, which gets replaced with _ in filename
        var generated = sources.FirstOrDefault(s => s.HintName == "TestNamespace_Outer_Inner.g.cs").Source;
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("partial struct Outer");
        await Assert.That(generated).Contains("partial struct Inner : global::Paradise.ECS.IComponent");
    }

    [Test]
    public async Task DeeplyNestedComponent_GeneratesCorrectly()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            public partial struct Level1
            {
                public partial struct Level2
                {
                    [Component]
                    public partial struct Level3
                    {
                        public int Value;
                    }
                }
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        // Nested types use + in FQN, which gets replaced with _ in filename
        var generated = sources.FirstOrDefault(s => s.HintName == "TestNamespace_Level1_Level2_Level3.g.cs").Source;
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("partial struct Level1");
        await Assert.That(generated).Contains("partial struct Level2");
        await Assert.That(generated).Contains("partial struct Level3 : global::Paradise.ECS.IComponent");
    }
}

public class ComponentGeneratorGlobalNamespaceTests
{
    [Test]
    public async Task GlobalNamespaceComponent_GeneratesCorrectly()
    {
        const string source = """
            using Paradise.ECS;

            [Component]
            public partial struct GlobalComponent
            {
                public int Value;
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        var generated = sources.FirstOrDefault(s => s.HintName == "GlobalComponent.g.cs").Source;
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("partial struct GlobalComponent : global::Paradise.ECS.IComponent");
        // Should not have namespace declaration
        await Assert.That(generated).DoesNotContain("namespace");
    }
}

public class ComponentGeneratorDiagnosticTests
{
    [Test]
    public async Task NonUnmanagedComponent_ReportsPECS001()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct BadComponent
            {
                public string Name; // string is not unmanaged
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        var pecs001 = diagnostics.FirstOrDefault(d => d.Id == "PECS001");
        await Assert.That(pecs001).IsNotNull();
        await Assert.That(pecs001!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(pecs001.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("TestNamespace.BadComponent");
        await Assert.That(pecs001.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("must be an unmanaged struct");
    }

    [Test]
    public async Task NonUnmanagedComponent_DoesNotGenerateCode()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct BadComponent
            {
                public string Name; // string is not unmanaged
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        // Should not generate the partial struct file
        var generated = sources.FirstOrDefault(s => s.HintName == "TestNamespace_BadComponent.g.cs").Source;
        await Assert.That(generated).IsNull();
    }

    [Test]
    public async Task MixedValidAndInvalidComponents_OnlyGeneratesValid()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct ValidComponent
            {
                public int Value;
            }

            [Component]
            public partial struct InvalidComponent
            {
                public string Name; // Invalid
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);
        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        // Valid component should be generated
        var validGenerated = sources.FirstOrDefault(s => s.HintName == "TestNamespace_ValidComponent.g.cs").Source;
        await Assert.That(validGenerated).IsNotNull();

        // Invalid component should not be generated
        var invalidGenerated = sources.FirstOrDefault(s => s.HintName == "TestNamespace_InvalidComponent.g.cs").Source;
        await Assert.That(invalidGenerated).IsNull();

        // Should have error diagnostic for invalid
        var pecs001 = diagnostics.FirstOrDefault(d => d.Id == "PECS001");
        await Assert.That(pecs001).IsNotNull();
    }

    [Test]
    public async Task InvalidGuidFormat_ReportsPECS004()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component("not-a-valid-guid")]
            public partial struct BadGuidComponent
            {
                public int Value;
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        var pecs004 = diagnostics.FirstOrDefault(d => d.Id == "PECS004");
        await Assert.That(pecs004).IsNotNull();
        await Assert.That(pecs004!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(pecs004.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("TestNamespace.BadGuidComponent");
        await Assert.That(pecs004.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("not-a-valid-guid");
    }

    [Test]
    public async Task InvalidGuidFormat_StillGeneratesComponentWithoutGuid()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component("invalid")]
            public partial struct ComponentWithBadGuid
            {
                public int Value;
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        // Component should still be generated (just without the GUID)
        var generated = sources.FirstOrDefault(s => s.HintName == "TestNamespace_ComponentWithBadGuid.g.cs").Source;
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("partial struct ComponentWithBadGuid : global::Paradise.ECS.IComponent");
        // Should use Guid.Empty since the provided GUID was invalid
        await Assert.That(generated).Contains("public static global::System.Guid Guid => global::System.Guid.Empty;");
    }

    [Test]
    public async Task ValidGuidFormat_NoErrorReported()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component("12345678-1234-1234-1234-123456789012")]
            public partial struct ValidGuidComponent
            {
                public int Value;
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        var pecs004 = diagnostics.FirstOrDefault(d => d.Id == "PECS004");
        await Assert.That(pecs004).IsNull();
    }

    [Test]
    public async Task ComponentNestedInClass_GeneratesCorrectPartialClass()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            public partial class OuterClass
            {
                [Component]
                public partial struct NestedInClass
                {
                    public int Value;
                }
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        var generated = sources.FirstOrDefault(s => s.HintName.Contains("NestedInClass", StringComparison.Ordinal)).Source;
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("partial class OuterClass");
        await Assert.That(generated).Contains("partial struct NestedInClass : global::Paradise.ECS.IComponent");
    }

    [Test]
    public async Task ComponentNestedInInterface_GeneratesCorrectPartialInterface()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            public partial interface IOuterInterface
            {
                [Component]
                public partial struct NestedInInterface
                {
                    public int Value;
                }
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        var generated = sources.FirstOrDefault(s => s.HintName.Contains("NestedInInterface", StringComparison.Ordinal)).Source;
        await Assert.That(generated).IsNotNull();
        await Assert.That(generated).Contains("partial interface IOuterInterface");
        await Assert.That(generated).Contains("partial struct NestedInInterface : global::Paradise.ECS.IComponent");
    }

    [Test]
    public async Task ComponentNestedInGenericStruct_ReportsPECS005()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            public partial struct GenericOuter<T>
            {
                [Component]
                public partial struct NestedInGeneric
                {
                    public int Value;
                }
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        var pecs005 = diagnostics.FirstOrDefault(d => d.Id == "PECS005");
        await Assert.That(pecs005).IsNotNull();
        await Assert.That(pecs005!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(pecs005.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("a generic type");
    }

    [Test]
    public async Task ComponentNestedInGenericClass_ReportsPECS005()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            public partial class GenericOuter<T>
            {
                [Component]
                public partial struct NestedInGeneric
                {
                    public int Value;
                }
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        var pecs005 = diagnostics.FirstOrDefault(d => d.Id == "PECS005");
        await Assert.That(pecs005).IsNotNull();
        await Assert.That(pecs005!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(pecs005.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("a generic type");
    }

    [Test]
    public async Task ComponentNestedInNonGenericStruct_NoError()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            public partial struct OuterStruct
            {
                [Component]
                public partial struct ValidNested
                {
                    public int Value;
                }
            }
            """;

        var diagnostics = GeneratorTestHelper.GetDiagnostics(source);

        var pecs005 = diagnostics.FirstOrDefault(d => d.Id == "PECS005");
        await Assert.That(pecs005).IsNull();
    }

    [Test]
    public async Task ComponentNestedInGenericType_DoesNotGenerateCode()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            public partial class GenericOuter<T>
            {
                [Component]
                public partial struct NestedInGeneric
                {
                    public int Value;
                }
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        var generated = sources.FirstOrDefault(s => s.HintName.Contains("NestedInGeneric", StringComparison.Ordinal));
        await Assert.That(generated.Source).IsNull();
    }
}

public class ComponentGeneratorRegistryTests
{
    [Test]
    public async Task ComponentRegistry_GeneratesTypeToIdMapping()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [Component]
            public partial struct Velocity { public float X; }
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        await Assert.That(registry).IsNotNull();
        await Assert.That(registry).Contains("public static class ComponentRegistry");
        await Assert.That(registry).Contains("typeof(global::TestNamespace.Position)");
        await Assert.That(registry).Contains("typeof(global::TestNamespace.Velocity)");
    }

    [Test]
    public async Task ComponentRegistry_GeneratesGetIdMethod()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        await Assert.That(registry).IsNotNull();
        await Assert.That(registry).Contains("public static global::Paradise.ECS.ComponentId GetId(global::System.Type type)");
        await Assert.That(registry).Contains("public static bool TryGetId(global::System.Type type, out global::Paradise.ECS.ComponentId id)");
    }

    [Test]
    public async Task ComponentRegistry_GeneratesGuidLookupMethods()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component("12345678-1234-1234-1234-123456789012")]
            public partial struct Position { public float X; }
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        await Assert.That(registry).IsNotNull();
        await Assert.That(registry).Contains("public static global::Paradise.ECS.ComponentId GetId(global::System.Guid guid)");
        await Assert.That(registry).Contains("public static bool TryGetId(global::System.Guid guid, out global::Paradise.ECS.ComponentId id)");
    }

    [Test]
    public async Task ComponentRegistry_GeneratesArchetypeExtensions()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        await Assert.That(registry).IsNotNull();
        await Assert.That(registry).Contains("public static class ArchetypeTypeExtensions");
        await Assert.That(registry).Contains("public static global::Paradise.ECS.Archetype<TBits> With<TBits>");
        await Assert.That(registry).Contains("public static global::Paradise.ECS.Archetype<TBits> Without<TBits>");
        await Assert.That(registry).Contains("public static bool Has<TBits>");
    }
}

public class ComponentGeneratorBitStorageTests
{
    [Test]
    public async Task FewComponents_UsesBit64()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Component1 { public int X; }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("using Bit64");
        await Assert.That(aliases).Contains("Component count: 1");
    }

    [Test]
    public async Task ComponentAliases_GeneratesGlobalUsings()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("global using Archetype = global::Paradise.ECS.Archetype<global::Paradise.ECS.Bit64>");
        await Assert.That(aliases).Contains("global using ComponentMask = global::Paradise.ECS.ImmutableBitSet<global::Paradise.ECS.Bit64>");
    }
}

public class ComponentGeneratorNoComponentsTests
{
    [Test]
    public async Task NoComponents_GeneratesNothing()
    {
        const string source = """
            namespace TestNamespace;

            public struct NotAComponent
            {
                public int Value;
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        await Assert.That(sources.Length).IsEqualTo(0);
    }

    [Test]
    public async Task EmptySource_GeneratesNothing()
    {
        const string source = "";

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        await Assert.That(sources.Length).IsEqualTo(0);
    }
}

public class ComponentGeneratorAutoGeneratedHeaderTests
{
    [Test]
    public async Task GeneratedFiles_HaveAutoGeneratedComment()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source);

        foreach (var (_, sourceText) in sources)
        {
            await Assert.That(sourceText).Contains("// <auto-generated/>");
        }
    }

    [Test]
    public async Task GeneratedFiles_HaveNullableEnabled()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var componentSource = GeneratorTestHelper.GetGeneratedSource(source, "TestNamespace_Position.g.cs");
        var registrySource = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        await Assert.That(componentSource).Contains("#nullable enable");
        await Assert.That(registrySource).Contains("#nullable enable");
    }
}
