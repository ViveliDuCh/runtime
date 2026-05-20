// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Options.Benchmarks;

[MemoryDiagnoser]
public class GuardOverheadBenchmarks
{
    private ServiceProvider _noGuardProvider = null!;
    private ServiceProvider _simpleGuardProvider = null!;
    private ServiceProvider _lifecycleGuardProvider = null!;
    private ServiceProvider _dualRegistrationProvider = null!;
    private ServiceProvider _factoryAwareProvider = null!;
    private ServiceProvider _startupOrderingProvider = null!;

    private IOptionsFactory<TestOptions> _noGuardFactory = null!;
    private IOptionsFactory<TestOptions> _simpleGuardFactory = null!;
    private IOptionsFactory<TestOptions> _lifecycleGuardFactory = null!;
    private IOptionsFactory<TestOptions> _dualRegistrationFactory = null!;
    private IOptionsFactory<TestOptions> _factoryAwareFactory = null!;
    private IOptionsFactory<TestOptions> _startupOrderingFactory = null!;

    [GlobalSetup]
    public void Setup()
    {
        _noGuardProvider = BenchmarkScenarioFactory.CreateCreateProvider(ComparisonApproach.Baseline);
        _simpleGuardProvider = BenchmarkScenarioFactory.CreateCreateProvider(ComparisonApproach.OptionA1);
        _lifecycleGuardProvider = BenchmarkScenarioFactory.CreateCreateProvider(ComparisonApproach.OptionA2);
        _dualRegistrationProvider = BenchmarkScenarioFactory.CreateCreateProvider(ComparisonApproach.OptionB);
        _factoryAwareProvider = BenchmarkScenarioFactory.CreateCreateProvider(ComparisonApproach.OptionC);
        _startupOrderingProvider = BenchmarkScenarioFactory.CreateCreateProvider(ComparisonApproach.OptionD);

        _noGuardFactory = _noGuardProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
        _simpleGuardFactory = _simpleGuardProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
        _lifecycleGuardFactory = _lifecycleGuardProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
        _dualRegistrationFactory = _dualRegistrationProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
        _factoryAwareFactory = _factoryAwareProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
        _startupOrderingFactory = _startupOrderingProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _startupOrderingProvider.Dispose();
        _factoryAwareProvider.Dispose();
        _dualRegistrationProvider.Dispose();
        _lifecycleGuardProvider.Dispose();
        _simpleGuardProvider.Dispose();
        _noGuardProvider.Dispose();
    }

    [Benchmark(Baseline = true)]
    public TestOptions NoGuard_CreateOptions() => _noGuardFactory.Create(BenchmarkScenarioFactory.SingleName);

    [Benchmark]
    public TestOptions OptionA1_SimpleGuard_CreateOptions() => _simpleGuardFactory.Create(BenchmarkScenarioFactory.SingleName);

    [Benchmark]
    public TestOptions OptionA2_LifecycleGuard_CreateOptions() => _lifecycleGuardFactory.Create(BenchmarkScenarioFactory.SingleName);

    [Benchmark]
    public TestOptions OptionB_DualRegistration_CreateOptions() => _dualRegistrationFactory.Create(BenchmarkScenarioFactory.SingleName);

    [Benchmark]
    public TestOptions OptionC_FactoryAwareness_CreateOptions() => _factoryAwareFactory.Create(BenchmarkScenarioFactory.SingleName);

    [Benchmark]
    public TestOptions OptionD_StartupOrdering_CreateOptions() => _startupOrderingFactory.Create(BenchmarkScenarioFactory.SingleName);
}
