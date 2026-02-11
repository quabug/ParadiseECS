namespace Paradise.ECS.Generators.Test;

/// <summary>
/// Tests for SystemGenerator access mask generation with optional queryable components.
/// </summary>
public sealed class SystemGeneratorOptionalComponentTests
{
    [Test]
    public async Task OptionalComponent_IncludedInReadMask()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Health { public float Current; }

            [Component]
            public partial struct Position { public float X; public float Y; }

            [Queryable]
            [With<Health>]
            [Optional<Position>(IsReadOnly = true)]
            public readonly ref partial struct Damageable;

            public ref partial struct DamageSystem : IEntitySystem
            {
                public ref DamageableEntity Damageable;
                public void Execute() { }
            }
            """;

        var result = GeneratorTestHelper.RunSystemGenerator(source);
        var registrySource = result.GeneratedTrees
            .Select(t => (HintName: System.IO.Path.GetFileName(t.FilePath), Source: t.GetText().ToString()))
            .FirstOrDefault(s => s.HintName == "SystemRegistry.g.cs").Source;

        await Assert.That(registrySource).IsNotNull();
        // Health is a required [With] component — always in readMask
        await Assert.That(registrySource!).Contains("Health.TypeId");
        // Position is [Optional] — must also appear in readMask for conflict detection
        await Assert.That(registrySource).Contains("readMask0 = TMask.Empty.Set(global::TestNamespace.Health.TypeId).Set(global::TestNamespace.Position.TypeId)");
        // Position is IsReadOnly=true, so it must NOT appear in writeMask
        await Assert.That(registrySource).DoesNotContain("writeMask0 = TMask.Empty.Set(global::TestNamespace.Health.TypeId).Set(global::TestNamespace.Position.TypeId)");
    }

    [Test]
    public async Task OptionalComponent_WritableIncludedInWriteMask()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Health { public float Current; }

            [Component]
            public partial struct Velocity { public float X; public float Y; }

            [Queryable]
            [With<Health>]
            [Optional<Velocity>]
            public readonly ref partial struct Player;

            public ref partial struct PlayerSystem : IEntitySystem
            {
                public ref PlayerEntity Player;
                public void Execute() { }
            }
            """;

        var result = GeneratorTestHelper.RunSystemGenerator(source);
        var registrySource = result.GeneratedTrees
            .Select(t => (HintName: System.IO.Path.GetFileName(t.FilePath), Source: t.GetText().ToString()))
            .FirstOrDefault(s => s.HintName == "SystemRegistry.g.cs").Source;

        await Assert.That(registrySource).IsNotNull();
        // Velocity is [Optional] without IsReadOnly — must appear in both readMask and writeMask
        await Assert.That(registrySource!).Contains("readMask0 = TMask.Empty.Set(global::TestNamespace.Health.TypeId).Set(global::TestNamespace.Velocity.TypeId)");
        await Assert.That(registrySource).Contains("writeMask0 = TMask.Empty.Set(global::TestNamespace.Health.TypeId).Set(global::TestNamespace.Velocity.TypeId)");
    }

    [Test]
    public async Task QueryOnlyComponent_ExcludedFromReadWriteMasks()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; public float Y; }

            [Component]
            public partial struct PlayerTag { }

            [Queryable]
            [With<Position>]
            [With<PlayerTag>(QueryOnly = true)]
            public readonly ref partial struct PlayerPos;

            public ref partial struct PlayerPosSystem : IEntitySystem
            {
                public ref PlayerPosEntity PlayerPos;
                public void Execute() { }
            }
            """;

        var result = GeneratorTestHelper.RunSystemGenerator(source);
        var registrySource = result.GeneratedTrees
            .Select(t => (HintName: System.IO.Path.GetFileName(t.FilePath), Source: t.GetText().ToString()))
            .FirstOrDefault(s => s.HintName == "SystemRegistry.g.cs").Source;

        await Assert.That(registrySource).IsNotNull();
        // Position is [With] (not QueryOnly) — must be in readMask and writeMask
        await Assert.That(registrySource!).Contains("readMask0 = TMask.Empty.Set(global::TestNamespace.Position.TypeId)");
        await Assert.That(registrySource).Contains("writeMask0 = TMask.Empty.Set(global::TestNamespace.Position.TypeId)");
        // PlayerTag is QueryOnly — must be in allMask (for query matching) but NOT in readMask/writeMask
        await Assert.That(registrySource).Contains("allMask0 = TMask.Empty.Set(global::TestNamespace.Position.TypeId).Set(global::TestNamespace.PlayerTag.TypeId)");
        // readMask should only have Position, not PlayerTag
        await Assert.That(registrySource).DoesNotContain("readMask0 = TMask.Empty.Set(global::TestNamespace.PlayerTag.TypeId)");
    }
}

/// <summary>
/// Tests for SystemGenerator constructor and code generation.
/// </summary>
public sealed class SystemGeneratorConstructorTests
{
    [Test]
    public async Task UnderscorePrefixedFields_GeneratesCorrectParameterNames()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Position { public float X; public float Y; }

            [Component]
            public partial struct Velocity { public float X; public float Y; }

            public ref partial struct UnderscoreSystem : IEntitySystem
            {
                public ref Position _Position;
                public ref readonly Velocity _Velocity;
                public void Execute() { }
            }
            """;

        var generated = GeneratorTestHelper.GetSystemGeneratedSource(
            source, "System_TestNamespace_UnderscoreSystem.g.cs");

        await Assert.That(generated).IsNotNull();
        // Parameter names should strip underscore: "position", "velocity"
        await Assert.That(generated!).Contains("ref global::TestNamespace.Position position");
        await Assert.That(generated).Contains("ref global::TestNamespace.Velocity velocity");
        // Body should assign field from stripped parameter: "_Position = ref position"
        await Assert.That(generated).Contains("_Position = ref position;");
        await Assert.That(generated).Contains("_Velocity = ref velocity;");
        // Must NOT contain self-assignment like "_position = ref _position"
        await Assert.That(generated).DoesNotContain("_position");
    }

    [Test]
    public async Task PascalCaseFields_GeneratesCorrectParameterNames()
    {
        const string source = """
            using Paradise.ECS;

            namespace TestNamespace;

            [Component]
            public partial struct Health { public float Current; }

            public ref partial struct HealthSystem : IEntitySystem
            {
                public ref Health Health;
                public void Execute() { }
            }
            """;

        var generated = GeneratorTestHelper.GetSystemGeneratedSource(
            source, "System_TestNamespace_HealthSystem.g.cs");

        await Assert.That(generated).IsNotNull();
        // Standard PascalCase field → camelCase parameter
        await Assert.That(generated!).Contains("ref global::TestNamespace.Health health");
        await Assert.That(generated).Contains("Health = ref health;");
    }

    [Test]
    public async Task ChunkSystem_UnderscorePrefixedSpanFields_GeneratesCorrectParameterNames()
    {
        const string source = """
            using Paradise.ECS;
            using System;

            namespace TestNamespace;

            [Component]
            public partial struct Velocity { public float X; public float Y; }

            public ref partial struct UnderscoreChunkSystem : IChunkSystem
            {
                public Span<Velocity> _Velocities;
                public void ExecuteChunk() { }
            }
            """;

        var generated = GeneratorTestHelper.GetSystemGeneratedSource(
            source, "System_TestNamespace_UnderscoreChunkSystem.g.cs");

        await Assert.That(generated).IsNotNull();
        // Span parameter should strip underscore: "velocities"
        await Assert.That(generated!).Contains("velocities");
        // Must NOT contain self-referencing "_velocities"
        await Assert.That(generated).DoesNotContain("_velocities");
    }
}
