using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.NativeAot;

namespace Paradise.ECS.Concurrent.Benchmarks;

/// <summary>
/// Configuration for running benchmarks with NativeAOT toolchain.
/// Usage: dotnet run -c Release -- --filter "*" --job NativeAot
/// Or use [Config(typeof(NativeAotConfig))] attribute on benchmark classes.
/// </summary>
public class NativeAotConfig : ManualConfig
{
    public NativeAotConfig()
    {
        // Create NativeAOT toolchain for .NET 10
        var toolchain = NativeAotToolchain.CreateBuilder()
            .UseNuGet()
            .TargetFrameworkMoniker("net10.0")
            .ToToolchain();

        var job = Job.ShortRun
            .WithToolchain(toolchain)
            .WithIterationCount(10)
            .WithWarmupCount(3)
            .WithEvaluateOverhead(true)
            .WithId("NativeAOT");

        AddJob(job);
        AddColumnProvider(DefaultColumnProviders.Instance);
    }
}

/// <summary>
/// Configuration comparing both runtime and NativeAOT toolchains.
/// </summary>
public class MultiToolchainConfig : ManualConfig
{
    public MultiToolchainConfig()
    {
        // Default runtime (JIT)
        AddJob(Job.ShortRun.WithId("JIT"));

        // Create NativeAOT toolchain for .NET 10
        var aotToolchain = NativeAotToolchain.CreateBuilder()
            .UseNuGet()
            .TargetFrameworkMoniker("net10.0")
            .ToToolchain();

        // NativeAOT
        AddJob(Job.ShortRun
            .WithToolchain(aotToolchain)
            .WithId("NativeAOT"));

        AddColumnProvider(DefaultColumnProviders.Instance);
    }
}
