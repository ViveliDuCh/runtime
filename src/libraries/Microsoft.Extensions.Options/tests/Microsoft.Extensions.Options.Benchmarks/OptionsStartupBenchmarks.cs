// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Options.Benchmarks;

[MemoryDiagnoser]
public class OptionsStartupBenchmarks
{
    [Benchmark(Baseline = true)]
    public void Baseline_NoGuard() => RunStartup(ComparisonApproach.Baseline);

    [Benchmark]
    public void OptionA1_SimpleGuard() => RunStartup(ComparisonApproach.OptionA1);

    [Benchmark]
    public void OptionA2_LifecycleGuard() => RunStartup(ComparisonApproach.OptionA2);

    [Benchmark]
    public void OptionB_DualRegistration() => RunStartup(ComparisonApproach.OptionB);

    [Benchmark]
    public void OptionC_FactoryAwareness() => RunStartup(ComparisonApproach.OptionC);

    [Benchmark]
    public void OptionD_StartupOrdering() => RunStartup(ComparisonApproach.OptionD);

    private static void RunStartup(ComparisonApproach approach)
    {
        using ServiceProvider provider = BenchmarkScenarioFactory.CreateStartupProvider(approach);
        provider.GetRequiredService<IAsyncStartupValidator>().ValidateAsync().GetAwaiter().GetResult();
    }
}
