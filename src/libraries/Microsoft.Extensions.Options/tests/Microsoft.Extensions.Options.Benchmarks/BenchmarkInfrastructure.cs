// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Options.Benchmarks;

internal enum ComparisonApproach
{
    Baseline,
    OptionA1,
    OptionA2,
    OptionB,
    OptionC,
    OptionD,
}

internal enum AsyncValidatorBehavior
{
    Succeed,
    Fail,
}

internal enum StartupLifecyclePhase
{
    NoHostAccess,
    StartupValidatorRegistered,
    StartupValidationRunning,
    StartupValidationCompleted,
}

internal static class BenchmarkMessages
{
    public const string MissingStartupValidation = "Async validation requires startup validation before options are created.";
}

public sealed class TestOptions
{
    public int Value { get; set; }
}

internal sealed class AsyncValidationState
{
    public bool StartupValidatorRegistered { get; set; }

    public StartupLifecyclePhase Phase { get; set; }
}

internal sealed class BenchmarkAsyncStartupValidatorOptions
{
    public Dictionary<(Type OptionsType, string Name), Func<CancellationToken, Task>> Validators { get; } = new();
}

internal static class BenchmarkScenarioFactory
{
    public const string SingleName = "benchmark";

    private static readonly string[] s_startupNames = new[] { "alpha", "beta", "gamma", "delta" };

    public static ServiceProvider CreateStartupProvider(ComparisonApproach approach)
        => CreateProvider(approach, s_startupNames, StartupLifecyclePhase.StartupValidatorRegistered, AsyncValidatorBehavior.Succeed, registerStartupValidation: true);

    public static ServiceProvider CreateCreateProvider(ComparisonApproach approach)
        => CreateProvider(approach, new[] { SingleName }, StartupLifecyclePhase.StartupValidationCompleted, AsyncValidatorBehavior.Succeed, registerStartupValidation: false);

    public static ServiceProvider CreateNoHostProvider(ComparisonApproach approach)
        => CreateProvider(approach, new[] { SingleName }, StartupLifecyclePhase.NoHostAccess, AsyncValidatorBehavior.Fail, registerStartupValidation: false);

    private static ServiceProvider CreateProvider(ComparisonApproach approach, string[] names, StartupLifecyclePhase initialPhase, AsyncValidatorBehavior asyncBehavior, bool registerStartupValidation)
    {
        ServiceCollection services = new();
        services.AddOptions();
        services.AddSingleton(new AsyncValidationState
        {
            StartupValidatorRegistered = initialPhase is not StartupLifecyclePhase.NoHostAccess,
            Phase = initialPhase,
        });

        services.AddSingleton<IAsyncValidateOptions<TestOptions>>(asyncBehavior switch
        {
            AsyncValidatorBehavior.Fail => new FailingAsyncValidator<TestOptions>("async failure"),
            _ => new SucceedingAsyncValidator<TestOptions>(),
        });

        switch (approach)
        {
            case ComparisonApproach.OptionA1:
                services.AddSingleton<IValidateOptions<TestOptions>, SimpleGuardValidateOptions<TestOptions>>();
                break;
            case ComparisonApproach.OptionA2:
                services.AddSingleton<IValidateOptions<TestOptions>, LifecycleGuardValidateOptions<TestOptions>>();
                break;
            case ComparisonApproach.OptionB:
                services.AddSingleton<IValidateOptions<TestOptions>, DualRegistrationValidateOptions<TestOptions>>();
                break;
            case ComparisonApproach.OptionC:
                services.AddTransient<IOptionsFactory<TestOptions>, FactoryAwareOptionsFactory<TestOptions>>();
                break;
        }

        foreach (string name in names)
        {
            OptionsBuilder<TestOptions> builder = services.AddOptions<TestOptions>(name)
                .Configure(static options => options.Value = 42)
                .Validate(static options => options.Value > 0, "Value must be positive.");

            if (registerStartupValidation)
            {
                builder.ValidateOnStartAsyncBenchmark();
            }
        }

        if (registerStartupValidation && approach == ComparisonApproach.OptionD)
        {
            services.AddTransient<IAsyncStartupValidator, SequentialBenchmarkStartupValidator>();
        }

        return services.BuildServiceProvider();
    }
}

internal static class BenchmarkOptionsBuilderExtensions
{
    public static OptionsBuilder<TOptions> ValidateOnStartAsyncBenchmark<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions>(this OptionsBuilder<TOptions> optionsBuilder)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        optionsBuilder.Services.TryAddTransient<IAsyncStartupValidator, ParallelBenchmarkStartupValidator>();
        optionsBuilder.Services.AddOptions<BenchmarkAsyncStartupValidatorOptions>()
            .Configure<IOptionsMonitor<TOptions>, IEnumerable<IAsyncValidateOptions<TOptions>>>((validatorOptions, optionsMonitor, validators) =>
            {
                validatorOptions.Validators[(typeof(TOptions), optionsBuilder.Name)] = async cancellationToken =>
                {
                    TOptions optionsValue = optionsMonitor.Get(optionsBuilder.Name);
                    List<Task<ValidateOptionsResult>> tasks = new();

                    foreach (IAsyncValidateOptions<TOptions> validator in validators)
                    {
                        tasks.Add(validator.ValidateAsync(optionsBuilder.Name, optionsValue, cancellationToken).AsTask());
                    }

                    ValidateOptionsResult[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
                    List<string>? failures = null;
                    foreach (ValidateOptionsResult result in results)
                    {
                        if (result is not null && result.Failed)
                        {
                            failures ??= new();
                            failures.AddRange(result.Failures);
                        }
                    }

                    if (failures is not null)
                    {
                        throw new OptionsValidationException(optionsBuilder.Name, typeof(TOptions), failures);
                    }
                };
            });

        return optionsBuilder;
    }
}

internal class ParallelBenchmarkStartupValidator : IAsyncStartupValidator
{
    private readonly BenchmarkAsyncStartupValidatorOptions _validatorOptions;
    private readonly AsyncValidationState _state;

    public ParallelBenchmarkStartupValidator(IOptions<BenchmarkAsyncStartupValidatorOptions> validatorOptions, AsyncValidationState state)
    {
        _validatorOptions = validatorOptions.Value;
        _state = state;
    }

    public virtual async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        _state.Phase = StartupLifecyclePhase.StartupValidationRunning;
        try
        {
            List<Task> validatorTasks = new();
            foreach (Func<CancellationToken, Task> validator in _validatorOptions.Validators.Values)
            {
                validatorTasks.Add(validator(cancellationToken));
            }

            await AwaitValidatorsAsync(validatorTasks).ConfigureAwait(false);
        }
        finally
        {
            _state.Phase = StartupLifecyclePhase.StartupValidationCompleted;
        }
    }

    protected static async Task AwaitValidatorsAsync(IEnumerable<Task> validatorTasks)
    {
        List<Exception>? exceptions = null;
        foreach (Task task in validatorTasks)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OptionsValidationException ex)
            {
                exceptions ??= new();
                exceptions.Add(ex);
            }
        }

        if (exceptions is null)
        {
            return;
        }

        if (exceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        throw new AggregateException(exceptions);
    }
}

internal sealed class SequentialBenchmarkStartupValidator : ParallelBenchmarkStartupValidator
{
    private readonly BenchmarkAsyncStartupValidatorOptions _validatorOptions;
    private readonly AsyncValidationState _state;

    public SequentialBenchmarkStartupValidator(IOptions<BenchmarkAsyncStartupValidatorOptions> validatorOptions, AsyncValidationState state)
        : base(validatorOptions, state)
    {
        _validatorOptions = validatorOptions.Value;
        _state = state;
    }

    public override async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        _state.Phase = StartupLifecyclePhase.StartupValidationRunning;
        try
        {
            List<Exception>? exceptions = null;
            foreach (Func<CancellationToken, Task> validator in _validatorOptions.Validators.Values)
            {
                try
                {
                    await validator(cancellationToken).ConfigureAwait(false);
                }
                catch (OptionsValidationException ex)
                {
                    exceptions ??= new();
                    exceptions.Add(ex);
                }
            }

            if (exceptions is null)
            {
                return;
            }

            if (exceptions.Count == 1)
            {
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
            }

            throw new AggregateException(exceptions);
        }
        finally
        {
            _state.Phase = StartupLifecyclePhase.StartupValidationCompleted;
        }
    }
}

internal sealed class SimpleGuardValidateOptions<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly AsyncValidationState _state;

    public SimpleGuardValidateOptions(AsyncValidationState state) => _state = state;

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        if (!_state.StartupValidatorRegistered)
        {
            throw new InvalidOperationException(BenchmarkMessages.MissingStartupValidation);
        }

        return ValidateOptionsResult.Success;
    }
}

internal sealed class LifecycleGuardValidateOptions<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly AsyncValidationState _state;

    public LifecycleGuardValidateOptions(AsyncValidationState state) => _state = state;

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        if (_state.Phase is not StartupLifecyclePhase.StartupValidatorRegistered and not StartupLifecyclePhase.StartupValidationRunning and not StartupLifecyclePhase.StartupValidationCompleted)
        {
            throw new InvalidOperationException(BenchmarkMessages.MissingStartupValidation);
        }

        return ValidateOptionsResult.Success;
    }
}

internal sealed class DualRegistrationValidateOptions<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    public ValidateOptionsResult Validate(string? name, TOptions options) => ValidateOptionsResult.Success;
}

internal sealed class FactoryAwareOptionsFactory<TOptions> : IOptionsFactory<TOptions>
    where TOptions : class
{
    private readonly IConfigureOptions<TOptions>[] _setups;
    private readonly IPostConfigureOptions<TOptions>[] _postConfigures;
    private readonly IValidateOptions<TOptions>[] _validations;
    private readonly AsyncValidationState _state;

    public FactoryAwareOptionsFactory(IEnumerable<IConfigureOptions<TOptions>> setups, IEnumerable<IPostConfigureOptions<TOptions>> postConfigures, IEnumerable<IValidateOptions<TOptions>> validations, AsyncValidationState state)
    {
        _setups = setups as IConfigureOptions<TOptions>[] ?? new List<IConfigureOptions<TOptions>>(setups).ToArray();
        _postConfigures = postConfigures as IPostConfigureOptions<TOptions>[] ?? new List<IPostConfigureOptions<TOptions>>(postConfigures).ToArray();
        _validations = validations as IValidateOptions<TOptions>[] ?? new List<IValidateOptions<TOptions>>(validations).ToArray();
        _state = state;
    }

    public TOptions Create(string name)
    {
        EnsureStartupValidationRegistered();

        TOptions options = CreateInstance();
        foreach (IConfigureOptions<TOptions> setup in _setups)
        {
            if (setup is IConfigureNamedOptions<TOptions> namedSetup)
            {
                namedSetup.Configure(name, options);
            }
            else if (name == Options.DefaultName)
            {
                setup.Configure(options);
            }
        }

        foreach (IPostConfigureOptions<TOptions> post in _postConfigures)
        {
            post.PostConfigure(name, options);
        }

        if (_validations.Length > 0)
        {
            List<string> failures = new();
            foreach (IValidateOptions<TOptions> validate in _validations)
            {
                ValidateOptionsResult result = validate.Validate(name, options);
                if (result is not null && result.Failed)
                {
                    failures.AddRange(result.Failures);
                }
            }

            if (failures.Count > 0)
            {
                throw new OptionsValidationException(name, typeof(TOptions), failures);
            }
        }

        return options;
    }

    private static TOptions CreateInstance() => Activator.CreateInstance<TOptions>();

    private void EnsureStartupValidationRegistered()
    {
        if (!_state.StartupValidatorRegistered)
        {
            throw new InvalidOperationException(BenchmarkMessages.MissingStartupValidation);
        }
    }
}

internal sealed class SucceedingAsyncValidator<TOptions> : IAsyncValidateOptions<TOptions>
    where TOptions : class
{
    public ValueTask<ValidateOptionsResult> ValidateAsync(string? name, TOptions options, CancellationToken cancellationToken = default)
        => new(ValidateOptionsResult.Success);
}

internal sealed class FailingAsyncValidator<TOptions> : IAsyncValidateOptions<TOptions>
    where TOptions : class
{
    private readonly string _error;

    public FailingAsyncValidator(string error) => _error = error;

    public ValueTask<ValidateOptionsResult> ValidateAsync(string? name, TOptions options, CancellationToken cancellationToken = default)
        => new(ValidateOptionsResult.Fail(_error));
}
