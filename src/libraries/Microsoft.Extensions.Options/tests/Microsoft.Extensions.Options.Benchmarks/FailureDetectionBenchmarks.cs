// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Options.Benchmarks;

[MemoryDiagnoser]
public class FailureDetectionBenchmarks
{
    private ServiceProvider _simpleGuardProvider = null!;
    private ServiceProvider _lifecycleGuardProvider = null!;
    private ServiceProvider _dualRegistrationProvider = null!;
    private ServiceProvider _factoryAwareProvider = null!;

    private IOptionsFactory<TestOptions> _simpleGuardFactory = null!;
    private IOptionsFactory<TestOptions> _lifecycleGuardFactory = null!;
    private IOptionsFactory<TestOptions> _dualRegistrationFactory = null!;
    private IOptionsFactory<TestOptions> _factoryAwareFactory = null!;

    [GlobalSetup]
    public void Setup()
    {
        _simpleGuardProvider = BenchmarkScenarioFactory.CreateNoHostProvider(ComparisonApproach.OptionA1);
        _lifecycleGuardProvider = BenchmarkScenarioFactory.CreateNoHostProvider(ComparisonApproach.OptionA2);
        _dualRegistrationProvider = BenchmarkScenarioFactory.CreateNoHostProvider(ComparisonApproach.OptionB);
        _factoryAwareProvider = BenchmarkScenarioFactory.CreateNoHostProvider(ComparisonApproach.OptionC);

        _simpleGuardFactory = _simpleGuardProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
        _lifecycleGuardFactory = _lifecycleGuardProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
        _dualRegistrationFactory = _dualRegistrationProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
        _factoryAwareFactory = _factoryAwareProvider.GetRequiredService<IOptionsFactory<TestOptions>>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _factoryAwareProvider.Dispose();
        _dualRegistrationProvider.Dispose();
        _lifecycleGuardProvider.Dispose();
        _simpleGuardProvider.Dispose();
    }

    [Benchmark]
    public bool OptionA1_NoHost_ThrowTiming() => ThrowsInvalidOperation(_simpleGuardFactory);

    [Benchmark]
    public bool OptionA2_NoHost_ThrowTiming() => ThrowsInvalidOperation(_lifecycleGuardFactory);

    [Benchmark]
    public TestOptions OptionB_NoHost_PartialValidation() => _dualRegistrationFactory.Create(BenchmarkScenarioFactory.SingleName);

    [Benchmark]
    public bool OptionC_NoHost_ThrowTiming() => ThrowsInvalidOperation(_factoryAwareFactory);

    private static bool ThrowsInvalidOperation(IOptionsFactory<TestOptions> factory)
    {
        try
        {
            factory.Create(BenchmarkScenarioFactory.SingleName);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
}
