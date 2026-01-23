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
        await Assert.That(generated).Contains("public static global::Paradise.ECS.ComponentId TypeId { get; internal set; } = global::Paradise.ECS.ComponentId.Invalid");
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
        await Assert.That(generated).Contains("Memory.AlignOf<global::TestNamespace.Position>()");
    }
}

public class ComponentGeneratorRuntimeIdAssignmentTests
{
    [Test]
    public async Task Component_HasRuntimeIdAssignment()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Alpha { public int A; }
            """;

        var generated = GeneratorTestHelper.GetGeneratedSource(source, "TestNamespace_Alpha.g.cs");

        // Verify runtime ID assignment structure - uses auto-property with internal setter
        await Assert.That(generated).Contains("public static global::Paradise.ECS.ComponentId TypeId { get; internal set; } = global::Paradise.ECS.ComponentId.Invalid");
        // SetTypeId method is no longer generated - inline lambda is used in registry instead
    }

    [Test]
    public async Task ModuleInitializer_SortsComponentsByAlignment()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct SmallComponent { public byte A; }  // alignment=1

            [Component]
            public partial struct LargeComponent { public long A; }  // alignment=8
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        // Verify module initializer is generated
        await Assert.That(registry).Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]");
        await Assert.That(registry).Contains("internal static void Initialize()");

        // Verify sorting by alignment (descending)
        await Assert.That(registry).Contains("b.Alignment.CompareTo(a.Alignment)");
    }
}

public class ComponentGeneratorManualIdTests
{
    [Test]
    public async Task Component_WithManualId_IncludesIdInRegistry()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component(Id = 100)]
            public partial struct ManualIdComponent { public int A; }
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        // Verify the manual ID is included in the component tuple with inline lambda
        await Assert.That(registry).Contains("100, (global::Paradise.ECS.ComponentId id) => global::TestNamespace.ManualIdComponent.TypeId = id");
    }

    [Test]
    public async Task Component_WithoutManualId_UsesNegativeOneInRegistry()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct AutoIdComponent { public int A; }
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        // Verify auto-assign components use -1 as manual ID with inline lambda
        await Assert.That(registry).Contains("-1, (global::Paradise.ECS.ComponentId id) => global::TestNamespace.AutoIdComponent.TypeId = id");
    }

    [Test]
    public async Task ModuleInitializer_HandlesManualAndAutoIds()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component(Id = 5)]
            public partial struct ManualFive { public int A; }

            [Component]
            public partial struct AutoFirst { public int B; }
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        // Verify manual ID handling code
        await Assert.That(registry).Contains("if (comp.ManualId >= 0)");
        await Assert.That(registry).Contains("while (usedIds.Contains(nextId)) nextId++");
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
        await Assert.That(registry).Contains("public sealed class ComponentRegistry : global::Paradise.ECS.IComponentRegistry");
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

}

public class ComponentGeneratorBitStorageTests
{
    [Test]
    public async Task FewComponents_UsesSmallBitSetUint()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Component1 { public int X; }
            """;

        // Use includeTagReference: false to test non-tagged World alias
        var sources = GeneratorTestHelper.GetGeneratedSources(source, includeTagReference: false);
        var aliases = sources.FirstOrDefault(s => s.HintName == "ComponentAliases.g.cs").Source;

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("using SmallBitSet<uint>");
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
        // With 1 component (≤32), uses SmallBitSet<uint>
        await Assert.That(aliases).Contains("global using ComponentMask = global::Paradise.ECS.SmallBitSet<uint>");
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

        // Use includeTagReference: false to ensure no auto-generated EntityTags
        var sources = GeneratorTestHelper.GetGeneratedSources(source, includeTagReference: false);

        await Assert.That(sources.Length).IsEqualTo(0);
    }

    [Test]
    public async Task EmptySource_GeneratesNothing()
    {
        const string source = "";

        // Use includeTagReference: false to ensure no auto-generated EntityTags
        var sources = GeneratorTestHelper.GetGeneratedSources(source, includeTagReference: false);

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

public class ComponentGeneratorNamespaceTests
{
    [Test]
    public async Task ComponentRegistry_UsesAssemblyAttributeNamespace()
    {
        const string source = """
            using Paradise.ECS;

            [assembly: ComponentRegistryNamespace("MyGame.ECS")]

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        await Assert.That(registry).IsNotNull();
        await Assert.That(registry).Contains("namespace MyGame.ECS;");
        await Assert.That(registry).Contains("public sealed class ComponentRegistry : global::Paradise.ECS.IComponentRegistry");
    }

    [Test]
    public async Task ComponentRegistry_UsesBuildPropertyNamespace_WhenNoAttribute()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs", rootNamespace: "CustomNamespace");

        await Assert.That(registry).IsNotNull();
        await Assert.That(registry).Contains("namespace CustomNamespace;");
    }

    [Test]
    public async Task ComponentRegistry_AttributeTakesPrecedenceOverBuildProperty()
    {
        const string source = """
            using Paradise.ECS;

            [assembly: ComponentRegistryNamespace("FromAttribute")]

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        // Even with rootNamespace build property set, attribute should take precedence
        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs", rootNamespace: "FromBuildProperty");

        await Assert.That(registry).IsNotNull();
        await Assert.That(registry).Contains("namespace FromAttribute;");
        await Assert.That(registry).DoesNotContain("namespace FromBuildProperty;");
    }

    [Test]
    public async Task ComponentRegistry_DefaultsToParadiseEcs_WhenNoNamespaceSpecified()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        await Assert.That(registry).IsNotNull();
        await Assert.That(registry).Contains("namespace Paradise.ECS;");
    }
}

/// <summary>
/// Tests for SuppressGlobalUsingsAttribute functionality.
/// </summary>
public class ComponentGeneratorSuppressGlobalUsingsTests
{
    [Test]
    public async Task SuppressGlobalUsings_WhenAttributePresent_DoesNotGenerateGlobalUsings()
    {
        const string source = """
            using Paradise.ECS;

            [assembly: SuppressGlobalUsings]

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).DoesNotContain("global using ComponentMask =");
        await Assert.That(aliases).DoesNotContain("global using ComponentMaskBits =");
        await Assert.That(aliases).DoesNotContain("global using QueryBuilder =");
        await Assert.That(aliases).DoesNotContain("global using World =");
        await Assert.That(aliases).DoesNotContain("global using Query =");
        await Assert.That(aliases).DoesNotContain("global using SharedArchetypeMetadata =");
        await Assert.That(aliases).DoesNotContain("global using ArchetypeRegistry =");
    }

    [Test]
    public async Task SuppressGlobalUsings_WhenAttributePresent_IncludesSuppressedComment()
    {
        const string source = """
            using Paradise.ECS;

            [assembly: SuppressGlobalUsings]

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("Global usings suppressed by [assembly: SuppressGlobalUsings]");
    }

    [Test]
    public async Task SuppressGlobalUsings_WhenAttributePresent_StillShowsComponentCount()
    {
        const string source = """
            using Paradise.ECS;

            [assembly: SuppressGlobalUsings]

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }

            [Component]
            public partial struct Velocity { public float X; }
            """;

        // Use includeTagReference: false to test exact component count without auto-generated EntityTags
        var sources = GeneratorTestHelper.GetGeneratedSources(source, includeTagReference: false);
        var aliases = sources.FirstOrDefault(s => s.HintName == "ComponentAliases.g.cs").Source;

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("Component count: 2");
    }

    [Test]
    public async Task SuppressGlobalUsings_WhenAttributeAbsent_GeneratesGlobalUsings()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var aliases = GeneratorTestHelper.GetGeneratedSource(source, "ComponentAliases.g.cs");

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("global using ComponentMask =");
        // ComponentMaskBits is only generated for ImmutableBitSet (>64 components), not for SmallBitSet
        await Assert.That(aliases).Contains("global using QueryBuilder =");
        await Assert.That(aliases).Contains("global using World =");
        await Assert.That(aliases).Contains("global using Query =");
        await Assert.That(aliases).DoesNotContain("All global usings suppressed");
    }

    [Test]
    public async Task SuppressGlobalUsings_ComponentGenerationStillWorks()
    {
        const string source = """
            using Paradise.ECS;

            [assembly: SuppressGlobalUsings]

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var component = GeneratorTestHelper.GetGeneratedSource(source, "TestNamespace_Position.g.cs");
        var registry = GeneratorTestHelper.GetGeneratedSource(source, "ComponentRegistry.g.cs");

        // Component should still be generated
        await Assert.That(component).IsNotNull();
        await Assert.That(component).Contains("partial struct Position : global::Paradise.ECS.IComponent");

        // Registry should still be generated
        await Assert.That(registry).IsNotNull();
        await Assert.That(registry).Contains("public sealed class ComponentRegistry");
    }
}

/// <summary>
/// Tests for EntityTags auto-generation when Paradise.ECS.Tag is referenced.
/// </summary>
public class ComponentGeneratorEntityTagsAutoGenerationTests
{
    /// <summary>
    /// Verifies that EntityTags is auto-generated when Paradise.ECS.Tag is referenced.
    /// </summary>
    [Test]
    public async Task WithTagReference_AutoGeneratesEntityTags()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source, includeTagReference: true);
        var entityTags = sources.FirstOrDefault(s => s.HintName == "Paradise_ECS_EntityTags.g.cs").Source;

        await Assert.That(entityTags).IsNotNull();
        await Assert.That(entityTags).Contains("struct EntityTags : global::Paradise.ECS.IComponent");
        await Assert.That(entityTags).Contains("global::Paradise.ECS.IEntityTags");
    }

    /// <summary>
    /// Verifies that user-defined EntityTags in the correct namespace prevents auto-generation.
    /// </summary>
    [Test]
    public async Task UserDefinedEntityTags_InRootNamespace_PreventsAutoGeneration()
    {
        const string source = """
            using Paradise.ECS;

            namespace Paradise.ECS;

            [Component]
            public partial struct EntityTags
            {
                public int CustomField;
            }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source, includeTagReference: true);
        var entityTags = sources.FirstOrDefault(s => s.HintName == "Paradise_ECS_EntityTags.g.cs").Source;

        await Assert.That(entityTags).IsNotNull();
        // Should be partial struct (user-defined), not auto-generated struct
        await Assert.That(entityTags).Contains("partial struct EntityTags");
        // Should NOT be a complete struct (auto-generated would not have partial)
        await Assert.That(entityTags).DoesNotContain("private TagMask _mask"); // auto-generated EntityTags has _mask field
    }

    /// <summary>
    /// Regression test: User-defined EntityTags in a DIFFERENT namespace should NOT prevent
    /// auto-generation of {RootNamespace}.EntityTags.
    ///
    /// Bug: The generator was checking only TypeName == "EntityTags" without considering namespace,
    /// so MyGame.EntityTags would incorrectly prevent Paradise.ECS.EntityTags from being generated.
    /// </summary>
    [Test]
    public async Task UserDefinedEntityTags_InDifferentNamespace_StillAutoGeneratesRootNamespaceEntityTags()
    {
        const string source = """
            using Paradise.ECS;

            namespace MyGame;

            [Component]
            public partial struct EntityTags
            {
                public int CustomField;
            }

            [Component]
            public partial struct Position { public float X; }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source, includeTagReference: true);

        // MyGame.EntityTags should be generated (user-defined, partial)
        var myGameEntityTags = sources.FirstOrDefault(s => s.HintName == "MyGame_EntityTags.g.cs").Source;
        await Assert.That(myGameEntityTags).IsNotNull();
        await Assert.That(myGameEntityTags).Contains("partial struct EntityTags");

        // Paradise.ECS.EntityTags should ALSO be auto-generated (different namespace)
        var autoGeneratedEntityTags = sources.FirstOrDefault(s => s.HintName == "Paradise_ECS_EntityTags.g.cs").Source;
        await Assert.That(autoGeneratedEntityTags).IsNotNull();
        // Auto-generated EntityTags has IEntityTags interface and _mask field
        await Assert.That(autoGeneratedEntityTags).Contains("struct EntityTags : global::Paradise.ECS.IComponent, global::Paradise.ECS.IEntityTags");
    }

    /// <summary>
    /// Verifies the World alias points to TaggedWorld with the correct EntityTags type.
    /// </summary>
    [Test]
    public async Task WithTagReference_WorldAliasUsesTaggedWorld()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source, includeTagReference: true);
        var aliases = sources.FirstOrDefault(s => s.HintName == "ComponentAliases.g.cs").Source;

        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("global using World = global::Paradise.ECS.TaggedWorld");
        await Assert.That(aliases).Contains("global::Paradise.ECS.EntityTags");
        await Assert.That(aliases).Contains("Tags enabled (Paradise.ECS.Tag referenced)");
    }

    /// <summary>
    /// Verifies that without tag reference, EntityTags is not auto-generated and World alias
    /// points to regular World (not TaggedWorld).
    /// </summary>
    [Test]
    public async Task WithoutTagReference_NoEntityTagsGenerated()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; }
            """;

        var sources = GeneratorTestHelper.GetGeneratedSources(source, includeTagReference: false);
        var entityTags = sources.FirstOrDefault(s => s.HintName == "Paradise_ECS_EntityTags.g.cs").Source;
        var aliases = sources.FirstOrDefault(s => s.HintName == "ComponentAliases.g.cs").Source;

        // No auto-generated EntityTags
        await Assert.That(entityTags).IsNull();

        // World alias points to regular World, not TaggedWorld
        await Assert.That(aliases).IsNotNull();
        await Assert.That(aliases).Contains("global using World = global::Paradise.ECS.World<");
        await Assert.That(aliases).DoesNotContain("TaggedWorld");
    }
}

/// <summary>
/// Tests for ComponentGenerator duplicate ID detection (PECS015).
/// </summary>
public class ComponentGeneratorDuplicateManualIdTests
{
    /// <summary>
    /// Verifies that the PECS015 diagnostic descriptor is properly defined.
    /// </summary>
    [Test]
    public async Task DuplicateComponentIdDiagnostic_HasCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.DuplicateComponentId;

        await Assert.That(descriptor.Id).IsEqualTo("PECS015");
        await Assert.That(descriptor.Title.ToString(System.Globalization.CultureInfo.InvariantCulture)).IsEqualTo("Duplicate component ID");
        await Assert.That(descriptor.DefaultSeverity).IsEqualTo(Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        await Assert.That(descriptor.IsEnabledByDefault).IsTrue();
    }

    /// <summary>
    /// Verifies the diagnostic message format contains expected placeholders.
    /// </summary>
    [Test]
    public async Task DuplicateComponentIdDiagnostic_MessageFormat_ContainsPlaceholders()
    {
        var descriptor = DiagnosticDescriptors.DuplicateComponentId;
        var format = descriptor.MessageFormat.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Message format should include ID and type names placeholders
        await Assert.That(format).Contains("{0}"); // ID placeholder
        await Assert.That(format).Contains("{1}"); // Type names placeholder
    }
}
