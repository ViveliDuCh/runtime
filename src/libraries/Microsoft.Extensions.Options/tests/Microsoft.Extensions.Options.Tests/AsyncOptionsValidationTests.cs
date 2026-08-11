// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.Extensions.Options.Tests
{
    public class AsyncOptionsValidationTests
    {
        private static IAsyncStartupValidator GetAsyncStartupValidator(IServiceProvider sp) =>
            Assert.IsAssignableFrom<IAsyncStartupValidator>(sp.GetRequiredService<IStartupValidator>());

        [Fact]
        public async Task AsyncValidateOptions_SkipsWhenNameDoesNotMatch()
        {
            var validator = new AsyncValidateOptions<FakeOptions>(
                "expected",
                (options, ct) => Task.FromResult(false),
                "Should not run");

            ValidateOptionsResult result = await validator.ValidateAsync("other", new FakeOptions(), CancellationToken.None);

            Assert.True(result.Skipped);
        }

        [Fact]
        public async Task AsyncValidateOptions_ValidatesWhenNameMatches()
        {
            var validator = new AsyncValidateOptions<FakeOptions>(
                "expected",
                (options, ct) => Task.FromResult(false),
                "Validation failed");

            ValidateOptionsResult result = await validator.ValidateAsync("expected", new FakeOptions(), CancellationToken.None);

            Assert.True(result.Failed);
            Assert.Contains("Validation failed", result.Failures);
        }

        [Fact]
        public async Task AsyncValidateOptions_ValidatesAllWhenNameIsNull()
        {
            var validator = new AsyncValidateOptions<FakeOptions>(
                null,
                (options, ct) => Task.FromResult(true),
                "fail");

            ValidateOptionsResult result = await validator.ValidateAsync("any-name", new FakeOptions(), CancellationToken.None);

            Assert.True(result.Succeeded);
        }

        [Fact]
        public async Task OptionsBuilder_AsyncValidate_RegistersAndExecutes()
        {
            var services = new ServiceCollection();
            bool asyncRan = false;

            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "test")
                .Validate(async (FakeOptions o, CancellationToken ct) =>
                {
                    asyncRan = true;
                    return await Task.FromResult(true);
                }, "async fail")
                .ValidateOnStart();

            ServiceProvider sp = services.BuildServiceProvider();
            IAsyncStartupValidator validator = GetAsyncStartupValidator(sp);

            await validator.ValidateAsync(CancellationToken.None);

            Assert.True(asyncRan);
        }

        [Fact]
        public async Task StartupValidator_SinglePath_RunsBothSyncAndAsyncValidators()
        {
            var services = new ServiceCollection();
            bool syncRan = false;
            bool asyncRan = false;

            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "test")
                .Validate(o => { syncRan = true; return true; }, "sync fail")
                .Validate(async (FakeOptions o, CancellationToken ct) =>
                {
                    asyncRan = true;
                    return await Task.FromResult(true);
                }, "async fail")
                .ValidateOnStart();

            ServiceProvider sp = services.BuildServiceProvider();

            // Single-path orchestration: one ValidateAsync runs every validator (sync and async) for the type,
            // dispatching each by capability.
            IAsyncStartupValidator validator = GetAsyncStartupValidator(sp);
            await validator.ValidateAsync(CancellationToken.None);

            Assert.True(syncRan);
            Assert.True(asyncRan);
        }

        [Fact]
        public async Task StartupValidator_SinglePath_AggregatesSyncAndAsyncFailures()
        {
            var services = new ServiceCollection();
            bool asyncRan = false;

            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "test")
                .Validate(o => false, "sync validation failed")
                .Validate(async (FakeOptions o, CancellationToken ct) =>
                {
                    asyncRan = true;
                    return await Task.FromResult(false);
                }, "async validation failed")
                .ValidateOnStart();

            ServiceProvider sp = services.BuildServiceProvider();
            IAsyncStartupValidator validator = GetAsyncStartupValidator(sp);

            // The single path does not short-circuit on the first failure: every validator runs and
            // all failures are aggregated into one OptionsValidationException.
            OptionsValidationException ex = await Assert.ThrowsAsync<OptionsValidationException>(
                () => validator.ValidateAsync(CancellationToken.None));

            Assert.True(asyncRan);
            Assert.Contains("sync validation failed", ex.Failures);
            Assert.Contains("async validation failed", ex.Failures);
        }

        [Fact]
        public async Task StartupValidator_ValidateAsync_OnlyAsyncValidators()
        {
            var services = new ServiceCollection();
            bool asyncRan = false;

            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "test")
                .Validate(async (FakeOptions o, CancellationToken ct) =>
                {
                    asyncRan = true;
                    return await Task.FromResult(true);
                }, "async fail")
                .ValidateOnStart();

            ServiceProvider sp = services.BuildServiceProvider();
            IAsyncStartupValidator validator = GetAsyncStartupValidator(sp);

            await validator.ValidateAsync(CancellationToken.None);

            Assert.True(asyncRan);
        }

        [Fact]
        public async Task StartupValidator_ValidateAsync_AsyncFailureThrowsOptionsValidationException()
        {
            var services = new ServiceCollection();

            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "test")
                .Validate(async (FakeOptions o, CancellationToken ct) =>
                {
                    await Task.CompletedTask;
                    return false;
                }, "async validation failed")
                .ValidateOnStart();

            ServiceProvider sp = services.BuildServiceProvider();
            IAsyncStartupValidator validator = GetAsyncStartupValidator(sp);

            OptionsValidationException ex = await Assert.ThrowsAsync<OptionsValidationException>(
                () => validator.ValidateAsync(CancellationToken.None));
            Assert.Contains("async validation failed", ex.Failures);
        }

        [Fact]
        public void ValidateOnStart_CustomSyncOnlyValidator_UsesSyncPath()
        {
            var services = new ServiceCollection();

            // A custom sync-only IStartupValidator registered before ValidateOnStart wins the
            // TryAddTransient, so it is the resolved IStartupValidator.
            services.AddSingleton<IStartupValidator>(new CustomSyncOnlyValidator());

            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "test")
                .Validate(async (FakeOptions o, CancellationToken ct) => await Task.FromResult(true), "async")
                .ValidateOnStart();

            ServiceProvider sp = services.BuildServiceProvider();

            // The custom validator is not async-capable, so the host falls back to the sync path (validator.Validate())
            // This means no InvalidCastException and no async validation.
            IStartupValidator validator = sp.GetRequiredService<IStartupValidator>();
            Assert.IsType<CustomSyncOnlyValidator>(validator);
            Assert.False(validator is IAsyncStartupValidator);
            validator.Validate();
        }

        [Fact]
        public void ValidateOnStart_RegistersBuiltInValidatorAsBothInterfaces()
        {
            var services = new ServiceCollection();

            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "test")
                .Validate(o => true)
                .ValidateOnStart();

            ServiceProvider sp = services.BuildServiceProvider();

            IStartupValidator sync = sp.GetRequiredService<IStartupValidator>();
            Assert.IsType<IAsyncStartupValidator>(sync, exactMatch: false);
            Assert.Single(sp.GetServices<IAsyncStartupValidator>());
        }

        [Fact]
        public void ValidateOnStart_CalledMultipleTimes_RegistersSingleAsyncStartupValidator()
        {
            var services = new ServiceCollection();

            services.AddOptions<FakeOptions>("a").Configure(o => o.Message = "a").Validate(o => true).ValidateOnStart();
            services.AddOptions<FakeOptions>("b").Configure(o => o.Message = "b").Validate(o => true).ValidateOnStart();

            ServiceProvider sp = services.BuildServiceProvider();

            Assert.Single(sp.GetServices<IAsyncStartupValidator>());
        }

        [Fact]
        public void ValidateOnStart_CustomAsyncStartupValidator_CoexistsWithBuiltInInEnumerable()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAsyncStartupValidator>(new TrackingAsyncStartupValidator());
            services.AddOptions<FakeOptions>().Configure(o => o.Message = "test").Validate(o => true).ValidateOnStart();

            ServiceProvider sp = services.BuildServiceProvider();

            // A custom async startup validator (a different implementation type) coexists with the built-in one.
            Assert.Equal(2, sp.GetServices<IAsyncStartupValidator>().Count());
        }

        [Fact]
        public async Task StartupValidator_ValidateAsync_CancellationTokenPropagated()
        {
            var services = new ServiceCollection();
            using var cts = new CancellationTokenSource();

            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "test")
                .Validate(async (FakeOptions o, CancellationToken ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return await Task.FromResult(true);
                }, "async")
                .ValidateOnStart();

            ServiceProvider sp = services.BuildServiceProvider();
            IAsyncStartupValidator validator = GetAsyncStartupValidator(sp);

            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => validator.ValidateAsync(cts.Token));
        }

        [Theory]
        [InlineData("named1")]
        [InlineData(null)]
        public async Task AsyncValidateOptions_NameMatching_DefaultAndNamed(string? registeredName)
        {
            var validator = new AsyncValidateOptions<FakeOptions>(
                registeredName,
                (options, ct) => Task.FromResult(false),
                "fail");

            ValidateOptionsResult defaultResult = await validator.ValidateAsync(Options.DefaultName, new FakeOptions(), CancellationToken.None);

            if (registeredName is null)
            {
                Assert.True(defaultResult.Failed);
            }
            else
            {
                Assert.True(defaultResult.Skipped);
            }
        }

        [Fact]
        public async Task StartupValidator_ValidateAsync_MultipleFailures_ThrowsAggregateException()
        {
            var services = new ServiceCollection();

            services.AddOptions<FakeOptions>("instance1")
                .Configure(o => o.Message = "")
                .Validate(async (FakeOptions o, CancellationToken ct) =>
                {
                    await Task.CompletedTask;
                    return o.Message.Length > 0;
                }, "Message required for instance1")
                .ValidateOnStart();

            services.AddOptions<FakeOptions>("instance2")
                .Configure(o => o.Message = "")
                .Validate(async (FakeOptions o, CancellationToken ct) =>
                {
                    await Task.CompletedTask;
                    return o.Message.Length > 0;
                }, "Message required for instance2")
                .ValidateOnStart();

            using ServiceProvider sp = services.BuildServiceProvider();
            IAsyncStartupValidator validator = GetAsyncStartupValidator(sp);

            AggregateException ex = await Assert.ThrowsAsync<AggregateException>(() => validator.ValidateAsync());
            Assert.Equal(2, ex.InnerExceptions.Count);
            Assert.All(ex.InnerExceptions, e => Assert.IsType<OptionsValidationException>(e));
        }

        [Fact]
        public async Task ValidateWithValidatorType_PreservesAsyncCapability()
        {
            var services = new ServiceCollection();

            services.AddOptions<FakeOptions>()
                .Validate<AsyncValidator>()
                .ValidateOnStart();

            using ServiceProvider sp = services.BuildServiceProvider();

            await GetAsyncStartupValidator(sp).ValidateAsync();
        }

        [Fact]
        public async Task StartupValidator_ValidatorImplementingBoth_DispatchesToAsync()
        {
            var spy = new CapabilitySpyValidator();
            var services = new ServiceCollection();

            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "test")
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<FakeOptions>>(spy);

            ServiceProvider sp = services.BuildServiceProvider();
            IAsyncStartupValidator validator = GetAsyncStartupValidator(sp);

            await validator.ValidateAsync(CancellationToken.None);

            // A validator that implements both contracts is dispatched through ValidateAsync only.
            Assert.True(spy.AsyncCalled);
            Assert.False(spy.SyncCalled);
        }

        [Fact]
        public void SyncOnlyValidatedOptions_SyncAccessorsBehaviorUnchanged()
        {
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "sync")
                .Validate(o => o.Message == "sync", "sync fail");
            using ServiceProvider sp = services.BuildServiceProvider();

            // A sync-only type is not async-capable, so the accessors create and validate synchronously as before.
            Assert.Equal("sync", sp.GetRequiredService<IOptions<FakeOptions>>().Value.Message);
            using IServiceScope scope = sp.CreateScope();
            Assert.Equal("sync", scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<FakeOptions>>().Get(null).Message);
        }

        [Fact]
        public async Task AsyncValidatedOptions_IOptionsValue_ThrowsBeforeStartupAndServesSeededValueAfter()
        {
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "validated")
                .Validate(async (FakeOptions o, CancellationToken ct) => await Task.FromResult(true), "async fail")
                .ValidateOnStart();
            using ServiceProvider sp = services.BuildServiceProvider();

            // Before startup nothing has been validated, so synchronous access fails fast (the async validator's
            // synchronous Validate is unsupported) rather than silently returning an unvalidated value.
            Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<IOptions<FakeOptions>>().Value);

            await GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None);

            // After startup seeds the singleton slot, IOptions<T>.Value returns the validated value.
            Assert.Equal("validated", sp.GetRequiredService<IOptions<FakeOptions>>().Value.Message);
        }

        [Fact]
        public void AsyncValidatedOptions_IOptionsValue_WithoutValidateOnStart_Throws()
        {
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "validated")
                .Validate(async (FakeOptions o, CancellationToken ct) => await Task.FromResult(true), "async fail");
            using ServiceProvider sp = services.BuildServiceProvider();

            // Without ValidateOnStart nothing seeds the singleton slot, so a synchronous read of an async-validated type
            // always fails fast; the value is never silently served unvalidated.
            Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<IOptions<FakeOptions>>().Value);
        }

        [Fact]
        public async Task AsyncOnlyValidation_PoisonedPreStartCacheIsReplacedByStartupSeed()
        {
            FakeOptions? startupCandidate = null;
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "validated")
                .Validate((FakeOptions o, CancellationToken ct) =>
                {
                    startupCandidate = o;
                    return Task.FromResult(true);
                }, "async fail")
                .ValidateOnStart();
            using ServiceProvider sp = services.BuildServiceProvider();

            IOptions<FakeOptions> options = sp.GetRequiredService<IOptions<FakeOptions>>();
            IOptionsMonitor<FakeOptions> monitor = sp.GetRequiredService<IOptionsMonitor<FakeOptions>>();

            Assert.Throws<OptionsValidationException>(() => options.Value);
            Assert.Throws<OptionsValidationException>(() => monitor.Get(Options.DefaultName));

            await GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None);

            Assert.NotNull(startupCandidate);
            Assert.Same(startupCandidate, options.Value);
            Assert.Same(startupCandidate, monitor.Get(Options.DefaultName));
        }

        [Fact]
        public async Task AsyncOnlyValidation_IOptionsSnapshotRemainsUnsupportedAfterStartup()
        {
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "validated")
                .Validate((FakeOptions o, CancellationToken ct) => Task.FromResult(true), "async fail")
                .ValidateOnStart();
            using ServiceProvider sp = services.BuildServiceProvider();

            using (IServiceScope scope = sp.CreateScope())
            {
                OptionsValidationException beforeStartupError = Assert.Throws<OptionsValidationException>(
                    () => scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<FakeOptions>>().Value);
                AssertAsyncOnlySnapshotFailure(beforeStartupError);
            }

            await GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None);

            using IServiceScope newScope = sp.CreateScope();
            OptionsValidationException afterStartupError = Assert.Throws<OptionsValidationException>(
                () => newScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<FakeOptions>>().Value);
            AssertAsyncOnlySnapshotFailure(afterStartupError);
        }

        [Fact]
        public async Task AsyncOnlyValidation_StartupFirstSeedsExactInstance()
        {
            FakeOptions? startupCandidate = null;
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "validated")
                .Validate((FakeOptions o, CancellationToken ct) =>
                {
                    startupCandidate = o;
                    return Task.FromResult(true);
                }, "async fail")
                .ValidateOnStart();
            using ServiceProvider sp = services.BuildServiceProvider();

            await GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None);

            Assert.NotNull(startupCandidate);
            Assert.Same(startupCandidate, sp.GetRequiredService<IOptions<FakeOptions>>().Value);
            Assert.Same(startupCandidate, sp.GetRequiredService<IOptionsMonitor<FakeOptions>>().CurrentValue);
        }

        [Fact]
        public async Task BothCapableValidator_PreStartIOptionsValueRemainsWinnerAndAsyncValidationRuns()
        {
            int configureCalls = 0;
            var validator = new CountingAsyncValidator();
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = (++configureCalls).ToString())
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<FakeOptions>>(validator);
            using ServiceProvider sp = services.BuildServiceProvider();

            IOptions<FakeOptions> options = sp.GetRequiredService<IOptions<FakeOptions>>();
            IOptionsMonitor<FakeOptions> monitor = sp.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            FakeOptions preStartWinner = options.Value;

            await GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None);

            using (IServiceScope scope = sp.CreateScope())
            {
                _ = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<FakeOptions>>().Value;
            }

            Assert.Equal(3, configureCalls);
            Assert.Equal(2, validator.SyncCalls);
            Assert.Equal(1, validator.AsyncCalls);
            Assert.Same(preStartWinner, options.Value);
            Assert.Same(preStartWinner, monitor.CurrentValue);
        }

        private static void AssertAsyncOnlySnapshotFailure(OptionsValidationException error)
        {
            string failure = Assert.Single(error.Failures);
            Assert.Contains("IOptionsSnapshot<TOptions>", failure);
            Assert.Contains("cannot execute or await ValidateAsync", failure);
            Assert.Contains("not populated by startup or reload validation", failure);
        }

        [Fact]
        public async Task AsyncStartupValidation_CustomOptionsImplementation_ThrowsInvalidOperationException()
        {
            bool asyncValidationCalled = false;
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Validate((FakeOptions o, CancellationToken ct) =>
                {
                    asyncValidationCalled = true;
                    return Task.FromResult(true);
                }, "async fail")
                .ValidateOnStart();
            services.AddSingleton<IOptions<FakeOptions>>(Options.Create(new FakeOptions()));
            using ServiceProvider sp = services.BuildServiceProvider();

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None));

            Assert.False(asyncValidationCalled);
            Assert.Contains(typeof(FakeOptions).ToString(), error.Message);
            Assert.Contains(typeof(OptionsWrapper<FakeOptions>).ToString(), error.Message);
        }

        [Fact]
        public async Task AsyncStartupValidation_DerivedFactoryUsesSynchronousFallback()
        {
            var validator = new CountingAsyncValidator();
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<FakeOptions>>(validator);
            services.AddSingleton<IOptionsFactory<FakeOptions>, DerivedOptionsFactory<FakeOptions>>();
            using ServiceProvider sp = services.BuildServiceProvider();

            await GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None);

            Assert.Equal(1, validator.SyncCalls);
            Assert.Equal(0, validator.AsyncCalls);
        }

        [Fact]
        public async Task FailedAsyncStartupValidation_DoesNotSeedOptions()
        {
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "invalid")
                .Validate((FakeOptions o, CancellationToken ct) => Task.FromResult(false), "async fail")
                .ValidateOnStart();
            using ServiceProvider sp = services.BuildServiceProvider();

            await Assert.ThrowsAsync<OptionsValidationException>(
                () => GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None));

            Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<IOptions<FakeOptions>>().Value);
            Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<IOptionsMonitor<FakeOptions>>().CurrentValue);
        }

        [Fact]
        public async Task IOptionsValue_RemainsStableAfterMonitorCacheEviction()
        {
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "validated")
                .Validate((FakeOptions o, CancellationToken ct) => Task.FromResult(true), "async fail")
                .ValidateOnStart();
            using ServiceProvider sp = services.BuildServiceProvider();

            await GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None);

            IOptions<FakeOptions> options = sp.GetRequiredService<IOptions<FakeOptions>>();
            IOptionsMonitor<FakeOptions> monitor = sp.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            IOptionsMonitorCache<FakeOptions> sharedCache = sp.GetRequiredService<IOptionsMonitorCache<FakeOptions>>();
            FakeOptions winner = options.Value;

            Assert.True(sharedCache.TryRemove(Options.DefaultName));
            Assert.Same(winner, options.Value);
            Assert.Throws<OptionsValidationException>(() => monitor.CurrentValue);
        }

        [Fact]
        public async Task NamedAsyncOptions_StartupPublishesExactCandidate()
        {
            var startupCandidates = new Dictionary<string, FakeOptions>();
            var services = new ServiceCollection();
            foreach (string name in new[] { "one", "two" })
            {
                services.AddOptions<FakeOptions>(name)
                    .Configure(o => o.Message = name)
                    .Validate((FakeOptions o, CancellationToken ct) =>
                    {
                        startupCandidates[name] = o;
                        return Task.FromResult(true);
                    }, "async fail")
                    .ValidateOnStart();
            }

            using ServiceProvider sp = services.BuildServiceProvider();

            await GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None);

            IOptionsMonitor<FakeOptions> monitor = sp.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            Assert.Equal(2, startupCandidates.Count);
            Assert.Same(startupCandidates["one"], monitor.Get("one"));
            Assert.Same(startupCandidates["two"], monitor.Get("two"));
        }

        [Fact]
        public async Task AsyncStartupValidation_CustomCachePublishesExactCandidate()
        {
            FakeOptions? startupCandidate = null;
            var customCache = new DelegatingOptionsCache<FakeOptions>();
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "validated")
                .Validate((FakeOptions o, CancellationToken ct) =>
                {
                    startupCandidate = o;
                    return Task.FromResult(true);
                }, "async fail")
                .ValidateOnStart();
            services.AddSingleton<IOptionsMonitorCache<FakeOptions>>(customCache);
            using ServiceProvider sp = services.BuildServiceProvider();

            await GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None);

            Assert.NotNull(startupCandidate);
            Assert.True(customCache.TryGetValue(Options.DefaultName, out FakeOptions? cached));
            Assert.Same(startupCandidate, cached);
            Assert.Same(startupCandidate, sp.GetRequiredService<IOptions<FakeOptions>>().Value);
            Assert.Same(startupCandidate, sp.GetRequiredService<IOptionsMonitor<FakeOptions>>().CurrentValue);
        }

        [Fact]
        public async Task AsyncValidatedOptions_ValidateOnStart_DerivedCacheReplace_RetriesPastConcurrentInsert()
        {
            var raceCache = new RaceInjectingOptionsCache<FakeOptions>(() => new FakeOptions { Message = "competing" });
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "validated")
                .Validate(async (FakeOptions o, CancellationToken ct) => await Task.FromResult(true), "async fail")
                .ValidateOnStart();
            services.AddSingleton<IOptionsMonitorCache<FakeOptions>>(raceCache);
            using ServiceProvider sp = services.BuildServiceProvider();

            await GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None);

            // The public cache contract has no atomic replace. This cache injects a concurrent insert into the first
            // TryRemove + TryAdd gap, making TryAdd a no-op; the bounded fallback must retry and publish the validated
            // value rather than leave the competing (unvalidated) value behind.
            Assert.True(raceCache.RaceInjected);
            Assert.Equal("validated", sp.GetRequiredService<IOptionsMonitor<FakeOptions>>().CurrentValue.Message);
        }

        [Fact]
        public async Task AsyncStartupValidation_CacheRejectsWinner_ThrowsInvalidOperationException()
        {
            FakeOptions? startupCandidate = null;
            var rejectingCache = new RejectingOptionsCache<FakeOptions>();
            var services = new ServiceCollection();
            services.AddOptions<FakeOptions>()
                .Configure(o => o.Message = "validated")
                .Validate((FakeOptions o, CancellationToken ct) =>
                {
                    startupCandidate = o;
                    return Task.FromResult(true);
                }, "async fail")
                .ValidateOnStart();
            services.AddSingleton<IOptionsMonitorCache<FakeOptions>>(rejectingCache);
            using ServiceProvider sp = services.BuildServiceProvider();
            IOptions<FakeOptions> options = sp.GetRequiredService<IOptions<FakeOptions>>();
            IOptionsMonitor<FakeOptions> monitor = sp.GetRequiredService<IOptionsMonitor<FakeOptions>>();

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => GetAsyncStartupValidator(sp).ValidateAsync(CancellationToken.None));

            Assert.Contains(typeof(FakeOptions).ToString(), error.Message);
            Assert.Contains($"name '{Options.DefaultName}'", error.Message);
            Assert.Contains(typeof(RejectingOptionsCache<FakeOptions>).ToString(), error.Message);
            Assert.Equal(3, rejectingCache.TryAddCalls);
            Assert.Same(startupCandidate, options.Value);
            Assert.Throws<OptionsValidationException>(() => monitor.CurrentValue);
        }

        private class CustomSyncOnlyValidator : IStartupValidator
        {
            public void Validate() { }
        }

        private sealed class RejectingOptionsCache<T> : IOptionsMonitorCache<T> where T : class
        {
            public int TryAddCalls { get; private set; }

            public T GetOrAdd(string? name, Func<T> createOptions) => createOptions();

            public bool TryAdd(string? name, T options)
            {
                TryAddCalls++;
                return false;
            }

            public bool TryRemove(string? name) => false;

            public void Clear() { }
        }

        private sealed class RaceInjectingOptionsCache<T> : OptionsCache<T> where T : class
        {
            private readonly Func<T> _competingValueFactory;

            public RaceInjectingOptionsCache(Func<T> competingValueFactory) => _competingValueFactory = competingValueFactory;

            public bool RaceInjected { get; private set; }

            public override bool TryAdd(string? name, T options)
            {
                if (!RaceInjected)
                {
                    RaceInjected = true;

                    // Simulate a concurrent GetOrAdd winning the gap between AddOrReplace's TryRemove and TryAdd:
                    // a competing value is already present, so this add becomes a no-op and returns false.
                    base.TryAdd(name, _competingValueFactory());
                    return base.TryAdd(name, options);
                }

                return base.TryAdd(name, options);
            }
        }

        private sealed class DelegatingOptionsCache<T> : IOptionsMonitorCache<T> where T : class
        {
            private readonly ConcurrentDictionary<string, T> _cache = new(StringComparer.Ordinal);

            public T GetOrAdd(string? name, Func<T> createOptions) =>
                _cache.GetOrAdd(name ?? Options.DefaultName, _ => createOptions());

            public bool TryGetValue(string? name, out T options) =>
                _cache.TryGetValue(name ?? Options.DefaultName, out options!);

            public bool TryAdd(string? name, T options) => _cache.TryAdd(name ?? Options.DefaultName, options);

            public bool TryRemove(string? name) => _cache.TryRemove(name ?? Options.DefaultName, out _);

            public void Clear() => _cache.Clear();
        }

        private sealed class DerivedOptionsFactory<T> : OptionsFactory<T> where T : class
        {
            public DerivedOptionsFactory(
                IEnumerable<IConfigureOptions<T>> setups,
                IEnumerable<IPostConfigureOptions<T>> postConfigures,
                IEnumerable<IValidateOptions<T>> validations)
                : base(setups, postConfigures, validations)
            {
            }
        }

        private sealed class TrackingAsyncStartupValidator : IAsyncStartupValidator
        {
            public bool Validated { get; private set; }

            public Task ValidateAsync(CancellationToken cancellationToken = default)
            {
                Validated = true;
                return Task.CompletedTask;
            }
        }

        private sealed class CapabilitySpyValidator : IValidateOptions<FakeOptions>, IAsyncValidateOptions<FakeOptions>
        {
            public bool SyncCalled { get; private set; }
            public bool AsyncCalled { get; private set; }

            public ValidateOptionsResult Validate(string? name, FakeOptions options)
            {
                SyncCalled = true;
                return ValidateOptionsResult.Success;
            }

            public Task<ValidateOptionsResult> ValidateAsync(string? name, FakeOptions options, CancellationToken cancellationToken = default)
            {
                AsyncCalled = true;
                return Task.FromResult(ValidateOptionsResult.Success);
            }
        }

        private sealed class CountingAsyncValidator : IAsyncValidateOptions<FakeOptions>
        {
            public int SyncCalls { get; private set; }
            public int AsyncCalls { get; private set; }

            public ValidateOptionsResult Validate(string? name, FakeOptions options)
            {
                SyncCalls++;
                return ValidateOptionsResult.Success;
            }

            public Task<ValidateOptionsResult> ValidateAsync(
                string? name,
                FakeOptions options,
                CancellationToken cancellationToken = default)
            {
                AsyncCalls++;
                return Task.FromResult(ValidateOptionsResult.Success);
            }
        }

        private sealed class AsyncValidator : IAsyncValidateOptions<FakeOptions>
        {
            public ValidateOptionsResult Validate(string? name, FakeOptions options) =>
                throw new InvalidOperationException("Synchronous validation should not run.");

            public Task<ValidateOptionsResult> ValidateAsync(
                string? name,
                FakeOptions options,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(ValidateOptionsResult.Success);
        }

        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

        [Fact]
        public void ValidateOnChange_NullBuilder_ThrowsArgumentNullException()
        {
            ArgumentNullException error = Assert.Throws<ArgumentNullException>(
                () => OptionsBuilderExtensions.ValidateOnChange<FakeOptions>(null!));

            Assert.Equal("optionsBuilder", error.ParamName);
        }

        [Fact]
        public void ValidateOnChange_UndefinedBehavior_ThrowsArgumentOutOfRangeException()
        {
            var services = new ServiceCollection();
            OptionsBuilder<FakeOptions> builder = services.AddOptions<FakeOptions>();
            int serviceCount = services.Count;

            ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(
                () => builder.ValidateOnChange((OptionsReloadValidationBehavior)42));

            Assert.Equal("behavior", error.ParamName);
            Assert.Equal(serviceCount, services.Count);
        }

        [Fact]
        public void IAsyncValidateOptions_Contract_InheritsIValidateOptionsAndIsInvariant()
        {
            Assert.Contains(
                typeof(IValidateOptions<FakeOptions>),
                typeof(IAsyncValidateOptions<FakeOptions>).GetInterfaces());

            Type genericParameter = Assert.Single(typeof(IAsyncValidateOptions<>).GetGenericArguments());
            GenericParameterAttributes variance =
                genericParameter.GenericParameterAttributes & GenericParameterAttributes.VarianceMask;
            Assert.Equal(GenericParameterAttributes.None, variance);
        }

        [Fact]
        public async Task ValidateOnChange_EnablesAsyncStartupValidationAndSeedsExactCandidate()
        {
            using var controlledValidator = new ControlledAsyncValidator();
            var services = new ServiceCollection();
            int configureCalls = 0;

            services.AddOptions<FakeOptions>()
                .Configure(options => options.Message = Interlocked.Increment(ref configureCalls).ToString())
                .ValidateOnChange();
            services.AddSingleton<IValidateOptions<FakeOptions>>(controlledValidator);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();

            Assert.Single(serviceProvider.GetServices<IAsyncStartupValidator>());
            IAsyncStartupValidator startupValidator = GetAsyncStartupValidator(serviceProvider);
            Task startupValidation = startupValidator.ValidateAsync(CancellationToken.None);
            ValidationInvocation startup = controlledValidator.TakeNextInvocation(TestTimeout);

            Assert.Equal(Options.DefaultName, startup.Name);

            startup.Complete(ValidateOptionsResult.Success);
            await startupValidation;

            Assert.Equal(1, controlledValidator.AsyncCalls);
            Assert.Same(startup.Options, serviceProvider.GetRequiredService<IOptions<FakeOptions>>().Value);
            Assert.Same(startup.Options, serviceProvider.GetRequiredService<IOptionsMonitor<FakeOptions>>().CurrentValue);
        }

        [Fact]
        public async Task ValidateOnChange_SuccessfulDefaultReload_PublishesExactCandidateAndNotifiesOnce()
        {
            using var controlledValidator = new ControlledAsyncValidator();
            var changeSource = new ReloadChangeTokenSource<FakeOptions>(Options.DefaultName);
            var services = new ServiceCollection();
            int configureCalls = 0;

            services.AddSingleton<IOptionsChangeTokenSource<FakeOptions>>(changeSource);
            services.AddOptions<FakeOptions>()
                .Configure(options => options.Message = Interlocked.Increment(ref configureCalls).ToString())
                .ValidateOnChange();
            services.AddSingleton<IValidateOptions<FakeOptions>>(controlledValidator);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            Task startupValidation = GetAsyncStartupValidator(serviceProvider).ValidateAsync(CancellationToken.None);
            ValidationInvocation startup = controlledValidator.TakeNextInvocation(TestTimeout);
            startup.Complete(ValidateOptionsResult.Success);
            await startupValidation;

            IOptions<FakeOptions> options = serviceProvider.GetRequiredService<IOptions<FakeOptions>>();
            IOptionsMonitor<FakeOptions> monitor = serviceProvider.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            FakeOptions startupCandidate = startup.Options;
            FakeOptions? callbackOptions = null;
            string? callbackName = null;
            int callbackCalls = 0;
            using var listenerCalled = new ManualResetEventSlim();
            using IDisposable? listener = monitor.OnChange((value, name) =>
            {
                callbackOptions = value;
                callbackName = name;
                Interlocked.Increment(ref callbackCalls);
                listenerCalled.Set();
            });

            changeSource.Trigger();
            ValidationInvocation reload = controlledValidator.TakeNextInvocation(TestTimeout);

            Assert.Equal(Options.DefaultName, reload.Name);
            Assert.False(reload.Completion.IsCompleted);
            Assert.NotSame(startupCandidate, reload.Options);

            reload.Complete(ValidateOptionsResult.Success);

            Assert.True(listenerCalled.Wait(TestTimeout));
            Assert.Equal(1, Volatile.Read(ref callbackCalls));
            Assert.Equal(Options.DefaultName, callbackName);
            Assert.Same(reload.Options, callbackOptions);
            Assert.Same(reload.Options, monitor.CurrentValue);
            Assert.Same(startupCandidate, options.Value);
            Assert.Equal(2, controlledValidator.AsyncCalls);
        }

        [Fact]
        public async Task ValidateOnChange_SuccessfulNamedReload_UpdatesOnlyMatchingName()
        {
            const string WatchedName = "watched";
            const string OtherName = "other";

            using var controlledValidator = new ControlledAsyncValidator();
            var changeSource = new ReloadChangeTokenSource<FakeOptions>(WatchedName);
            var services = new ServiceCollection();
            int configureCalls = 0;

            services.AddSingleton<IOptionsChangeTokenSource<FakeOptions>>(changeSource);
            services.AddOptions<FakeOptions>(WatchedName)
                .Configure(options => options.Message = Interlocked.Increment(ref configureCalls).ToString())
                .ValidateOnChange();
            services.AddOptions<FakeOptions>(OtherName)
                .Configure(options => options.Message = Interlocked.Increment(ref configureCalls).ToString());
            services.AddSingleton<IValidateOptions<FakeOptions>>(controlledValidator);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            Task startupValidation = GetAsyncStartupValidator(serviceProvider).ValidateAsync(CancellationToken.None);
            ValidationInvocation startup = controlledValidator.TakeNextInvocation(TestTimeout);

            Assert.Equal(WatchedName, startup.Name);
            startup.Complete(ValidateOptionsResult.Success);
            await startupValidation;

            IOptionsMonitor<FakeOptions> monitor = serviceProvider.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            Assert.Same(startup.Options, monitor.Get(WatchedName));
            FakeOptions other = monitor.Get(OtherName);
            FakeOptions? callbackOptions = null;
            string? callbackName = null;
            int callbackCalls = 0;
            using var listenerCalled = new ManualResetEventSlim();
            using IDisposable? listener = monitor.OnChange((value, name) =>
            {
                callbackOptions = value;
                callbackName = name;
                Interlocked.Increment(ref callbackCalls);
                listenerCalled.Set();
            });

            changeSource.Trigger();
            ValidationInvocation reload = controlledValidator.TakeNextInvocation(TestTimeout);

            Assert.Equal(WatchedName, reload.Name);
            Assert.NotSame(startup.Options, reload.Options);
            reload.Complete(ValidateOptionsResult.Success);

            Assert.True(listenerCalled.Wait(TestTimeout));
            Assert.Equal(1, Volatile.Read(ref callbackCalls));
            Assert.Equal(WatchedName, callbackName);
            Assert.Same(reload.Options, callbackOptions);
            Assert.Same(reload.Options, monitor.Get(WatchedName));
            Assert.Same(other, monitor.Get(OtherName));
            Assert.Equal(2, controlledValidator.AsyncCalls);
        }

        [Theory]
        [InlineData(OptionsReloadValidationBehavior.KeepLastGood)]
        [InlineData(OptionsReloadValidationBehavior.FailReads)]
        public async Task ValidateOnChange_FailedCurrentReload_AppliesBehaviorBeforeInvokingOnError(
            OptionsReloadValidationBehavior behavior)
        {
            using var controlledValidator = new ControlledAsyncValidator();
            var changeSource = new ReloadChangeTokenSource<FakeOptions>(Options.DefaultName);
            var services = new ServiceCollection();
            int configureCalls = 0;
            IOptionsMonitor<FakeOptions>? monitor = null;
            FakeOptions? callbackRead = null;
            Exception? callbackReadError = null;
            string? callbackName = null;
            Exception? callbackError = null;
            int callbackCalls = 0;
            int listenerCalls = 0;
            using var onErrorCalled = new ManualResetEventSlim();

            services.AddSingleton<IOptionsChangeTokenSource<FakeOptions>>(changeSource);
            services.AddOptions<FakeOptions>()
                .Configure(options => options.Message = Interlocked.Increment(ref configureCalls).ToString())
                .ValidateOnChange(behavior, (name, error) =>
                {
                    callbackName = name;
                    callbackError = error;

                    try
                    {
                        callbackRead = monitor!.CurrentValue;
                    }
                    catch (Exception readError)
                    {
                        callbackReadError = readError;
                    }
                    finally
                    {
                        Interlocked.Increment(ref callbackCalls);
                        onErrorCalled.Set();
                    }
                });
            services.AddSingleton<IValidateOptions<FakeOptions>>(controlledValidator);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            monitor = serviceProvider.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            Task startupValidation = GetAsyncStartupValidator(serviceProvider).ValidateAsync(CancellationToken.None);
            ValidationInvocation startup = controlledValidator.TakeNextInvocation(TestTimeout);
            startup.Complete(ValidateOptionsResult.Success);
            await startupValidation;

            FakeOptions startupCandidate = startup.Options;
            Assert.Same(startupCandidate, monitor.CurrentValue);
            using IDisposable? listener = monitor.OnChange((_, _) => Interlocked.Increment(ref listenerCalls));

            changeSource.Trigger();
            ValidationInvocation reload = controlledValidator.TakeNextInvocation(TestTimeout);

            Assert.Equal(Options.DefaultName, reload.Name);
            Assert.NotSame(startupCandidate, reload.Options);
            reload.Complete(ValidateOptionsResult.Fail("reload failed"));

            Assert.True(onErrorCalled.Wait(TestTimeout));
            Assert.Equal(1, Volatile.Read(ref callbackCalls));
            Assert.Equal(Options.DefaultName, callbackName);
            OptionsValidationException validationError = Assert.IsType<OptionsValidationException>(callbackError);
            Assert.Equal(typeof(FakeOptions), validationError.OptionsType);
            Assert.Equal(Options.DefaultName, validationError.OptionsName);
            Assert.Equal("reload failed", Assert.Single(validationError.Failures));
            Assert.Equal(0, Volatile.Read(ref listenerCalls));

            if (behavior == OptionsReloadValidationBehavior.KeepLastGood)
            {
                Assert.Null(callbackReadError);
                Assert.Same(startupCandidate, callbackRead);
                Assert.Same(startupCandidate, monitor.CurrentValue);
            }
            else
            {
                Assert.Null(callbackRead);
                Assert.Same(validationError, callbackReadError);
                Assert.Same(
                    validationError,
                    Assert.Throws<OptionsValidationException>(() => monitor.CurrentValue));
            }

            Assert.Equal(2, controlledValidator.AsyncCalls);
        }

        [Fact]
        public async Task ValidateOnChange_IOptionsValue_RemainsStartupWinnerAcrossSuccessfulAndFailedReloads()
        {
            using var controlledValidator = new ControlledAsyncValidator();
            var changeSource = new ReloadChangeTokenSource<FakeOptions>(Options.DefaultName);
            var services = new ServiceCollection();
            int configureCalls = 0;
            string? callbackName = null;
            Exception? callbackError = null;
            int callbackCalls = 0;
            FakeOptions? listenerValue = null;
            string? listenerName = null;
            int listenerCalls = 0;
            using var onErrorCalled = new ManualResetEventSlim();
            using var listenerCalled = new ManualResetEventSlim();

            services.AddSingleton<IOptionsChangeTokenSource<FakeOptions>>(changeSource);
            services.AddOptions<FakeOptions>()
                .Configure(options => options.Message = Interlocked.Increment(ref configureCalls).ToString())
                .ValidateOnChange(OptionsReloadValidationBehavior.FailReads, (name, error) =>
                {
                    callbackName = name;
                    callbackError = error;
                    Interlocked.Increment(ref callbackCalls);
                    onErrorCalled.Set();
                });
            services.AddSingleton<IValidateOptions<FakeOptions>>(controlledValidator);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IOptionsMonitor<FakeOptions> monitor =
                serviceProvider.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            Task startupValidation = GetAsyncStartupValidator(serviceProvider).ValidateAsync(CancellationToken.None);
            ValidationInvocation startup = controlledValidator.TakeNextInvocation(TestTimeout);
            startup.Complete(ValidateOptionsResult.Success);
            await startupValidation;

            IOptions<FakeOptions> options = serviceProvider.GetRequiredService<IOptions<FakeOptions>>();
            FakeOptions startupCandidate = startup.Options;
            Assert.Same(startupCandidate, options.Value);
            Assert.Same(startupCandidate, monitor.CurrentValue);
            using IDisposable? listener = monitor.OnChange((value, name) =>
            {
                listenerValue = value;
                listenerName = name;
                Interlocked.Increment(ref listenerCalls);
                listenerCalled.Set();
            });

            changeSource.Trigger();
            ValidationInvocation successfulReload = controlledValidator.TakeNextInvocation(TestTimeout);
            successfulReload.Complete(ValidateOptionsResult.Success);

            Assert.True(listenerCalled.Wait(TestTimeout));
            Assert.Equal(1, Volatile.Read(ref listenerCalls));
            Assert.Equal(Options.DefaultName, listenerName);
            Assert.Same(successfulReload.Options, listenerValue);
            Assert.Same(successfulReload.Options, monitor.CurrentValue);
            Assert.Same(startupCandidate, options.Value);

            changeSource.Trigger();
            ValidationInvocation failedReload = controlledValidator.TakeNextInvocation(TestTimeout);
            failedReload.Complete(ValidateOptionsResult.Fail("reload failed"));

            Assert.True(onErrorCalled.Wait(TestTimeout));
            Assert.Equal(1, Volatile.Read(ref callbackCalls));
            Assert.Equal(Options.DefaultName, callbackName);
            OptionsValidationException validationError = Assert.IsType<OptionsValidationException>(callbackError);
            Assert.Equal(typeof(FakeOptions), validationError.OptionsType);
            Assert.Equal(Options.DefaultName, validationError.OptionsName);
            Assert.Equal("reload failed", Assert.Single(validationError.Failures));
            Assert.Same(
                validationError,
                Assert.Throws<OptionsValidationException>(() => monitor.CurrentValue));
            Assert.Equal(1, Volatile.Read(ref listenerCalls));
            Assert.Same(startupCandidate, options.Value);
            Assert.Equal(3, controlledValidator.AsyncCalls);
        }

        [Theory]
        [InlineData("")]
        [InlineData("named")]
        public async Task ValidateOnChange_IOptionsSnapshot_RemainsScopeLocalAndSynchronousAfterMonitorReload(
            string name)
        {
            using var controlledValidator = new ControlledAsyncValidator();
            var changeSource = new ReloadChangeTokenSource<FakeOptions>(name);
            var services = new ServiceCollection();
            int configureCalls = 0;
            FakeOptions? listenerValue = null;
            string? listenerName = null;
            int listenerCalls = 0;
            using var listenerCalled = new ManualResetEventSlim();

            services.AddSingleton<IOptionsChangeTokenSource<FakeOptions>>(changeSource);
            services.AddOptions<FakeOptions>(name)
                .Configure(options => options.Message = Interlocked.Increment(ref configureCalls).ToString())
                .ValidateOnChange();
            services.AddSingleton<IValidateOptions<FakeOptions>>(controlledValidator);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IOptionsMonitor<FakeOptions> monitor =
                serviceProvider.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            Task startupValidation = GetAsyncStartupValidator(serviceProvider).ValidateAsync(CancellationToken.None);
            ValidationInvocation startup = controlledValidator.TakeNextInvocation(TestTimeout);

            Assert.Equal(name, startup.Name);
            startup.Complete(ValidateOptionsResult.Success);
            await startupValidation;

            Assert.Same(startup.Options, monitor.Get(name));
            Assert.Equal(1, controlledValidator.AsyncCalls);
            Assert.Equal(0, controlledValidator.SyncCalls);

            using IServiceScope scope1 = serviceProvider.CreateScope();
            IOptionsSnapshot<FakeOptions> snapshot1 =
                scope1.ServiceProvider.GetRequiredService<IOptionsSnapshot<FakeOptions>>();
            FakeOptions scope1Value = snapshot1.Get(name);

            Assert.Same(scope1Value, snapshot1.Get(name));
            Assert.NotSame(startup.Options, scope1Value);
            Assert.Equal(1, controlledValidator.SyncCalls);
            Assert.Equal(1, controlledValidator.AsyncCalls);

            using IDisposable? listener = monitor.OnChange((value, changedName) =>
            {
                listenerValue = value;
                listenerName = changedName;
                Interlocked.Increment(ref listenerCalls);
                listenerCalled.Set();
            });

            changeSource.Trigger();
            ValidationInvocation reload = controlledValidator.TakeNextInvocation(TestTimeout);

            Assert.Equal(name, reload.Name);
            reload.Complete(ValidateOptionsResult.Success);

            Assert.True(listenerCalled.Wait(TestTimeout));
            Assert.Equal(1, Volatile.Read(ref listenerCalls));
            Assert.Equal(name, listenerName);
            Assert.Same(reload.Options, listenerValue);
            Assert.Same(reload.Options, monitor.Get(name));
            Assert.NotSame(reload.Options, scope1Value);
            Assert.Same(scope1Value, snapshot1.Get(name));
            Assert.Equal(1, controlledValidator.SyncCalls);
            Assert.Equal(2, controlledValidator.AsyncCalls);

            using IServiceScope scope2 = serviceProvider.CreateScope();
            IOptionsSnapshot<FakeOptions> snapshot2 =
                scope2.ServiceProvider.GetRequiredService<IOptionsSnapshot<FakeOptions>>();
            FakeOptions scope2Value = snapshot2.Get(name);

            Assert.NotSame(scope1Value, scope2Value);
            Assert.NotSame(reload.Options, scope2Value);
            Assert.Same(scope2Value, snapshot2.Get(name));
            Assert.Equal(2, controlledValidator.SyncCalls);
            Assert.Equal(2, controlledValidator.AsyncCalls);
        }

        [Fact]
        public async Task ValidateOnChange_ValidatorThrowsThenNextValidReloadRecovers()
        {
            using var controlledValidator = new ControlledAsyncValidator();
            var changeSource = new ReloadChangeTokenSource<FakeOptions>(Options.DefaultName);
            var services = new ServiceCollection();
            int configureCalls = 0;
            Exception? callbackError = null;
            int callbackCalls = 0;
            FakeOptions? listenerValue = null;
            int listenerCalls = 0;
            using var onErrorCalled = new ManualResetEventSlim();
            using var listenerCalled = new ManualResetEventSlim();

            services.AddSingleton<IOptionsChangeTokenSource<FakeOptions>>(changeSource);
            services.AddOptions<FakeOptions>()
                .Configure(options => options.Message = Interlocked.Increment(ref configureCalls).ToString())
                .ValidateOnChange(onError: (_, error) =>
                {
                    callbackError = error;
                    Interlocked.Increment(ref callbackCalls);
                    onErrorCalled.Set();
                });
            services.AddSingleton<IValidateOptions<FakeOptions>>(controlledValidator);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IOptionsMonitor<FakeOptions> monitor =
                serviceProvider.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            Task startupValidation = GetAsyncStartupValidator(serviceProvider).ValidateAsync(CancellationToken.None);
            ValidationInvocation startup = controlledValidator.TakeNextInvocation(TestTimeout);
            startup.Complete(ValidateOptionsResult.Success);
            await startupValidation;

            Assert.Equal("1", startup.Options.Message);
            Assert.Same(startup.Options, monitor.CurrentValue);
            using IDisposable? listener = monitor.OnChange((value, _) =>
            {
                listenerValue = value;
                Interlocked.Increment(ref listenerCalls);
                listenerCalled.Set();
            });

            changeSource.Trigger();
            ValidationInvocation failedReload = controlledValidator.TakeNextInvocation(TestTimeout);
            var expectedError = new InvalidOperationException("reload validator threw");

            Assert.Equal("2", failedReload.Options.Message);
            Assert.NotSame(startup.Options, failedReload.Options);
            failedReload.Fail(expectedError);

            Assert.True(onErrorCalled.Wait(TestTimeout));
            Assert.Equal(1, Volatile.Read(ref callbackCalls));
            Assert.Same(expectedError, callbackError);
            Assert.Equal(0, Volatile.Read(ref listenerCalls));
            Assert.Same(startup.Options, monitor.CurrentValue);

            changeSource.Trigger();
            ValidationInvocation successfulReload = controlledValidator.TakeNextInvocation(TestTimeout);

            Assert.Equal("3", successfulReload.Options.Message);
            Assert.NotSame(failedReload.Options, successfulReload.Options);
            successfulReload.Complete(ValidateOptionsResult.Success);

            Assert.True(listenerCalled.Wait(TestTimeout));
            Assert.Equal(1, Volatile.Read(ref callbackCalls));
            Assert.Equal(1, Volatile.Read(ref listenerCalls));
            Assert.Same(successfulReload.Options, listenerValue);
            Assert.Same(successfulReload.Options, monitor.CurrentValue);
            Assert.Equal(3, controlledValidator.AsyncCalls);
            Assert.Equal(3, Volatile.Read(ref configureCalls));
        }

        [Fact]
        public async Task ValidateOnChange_OnErrorThrows_ReportsEventAndNextValidReloadSucceeds()
        {
            using var controlledValidator = new ControlledAsyncValidator();
            using var eventListener = new OptionsTestEventListener(expectedEventId: 2);
            var changeSource = new ReloadChangeTokenSource<FakeOptions>(Options.DefaultName);
            var services = new ServiceCollection();
            int callbackCalls = 0;
            int listenerCalls = 0;
            using var listenerCalled = new ManualResetEventSlim();

            services.AddSingleton<IOptionsChangeTokenSource<FakeOptions>>(changeSource);
            services.AddOptions<FakeOptions>()
                .ValidateOnChange(onError: (_, _) =>
                {
                    Interlocked.Increment(ref callbackCalls);
                    throw new InvalidOperationException("callback failed");
                });
            services.AddSingleton<IValidateOptions<FakeOptions>>(controlledValidator);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IOptionsMonitor<FakeOptions> monitor =
                serviceProvider.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            Task startupValidation = GetAsyncStartupValidator(serviceProvider).ValidateAsync(CancellationToken.None);
            ValidationInvocation startup = controlledValidator.TakeNextInvocation(TestTimeout);
            startup.Complete(ValidateOptionsResult.Success);
            await startupValidation;

            using IDisposable? listener = monitor.OnChange((_, _) =>
            {
                Interlocked.Increment(ref listenerCalls);
                listenerCalled.Set();
            });

            changeSource.Trigger();
            ValidationInvocation failedReload = controlledValidator.TakeNextInvocation(TestTimeout);
            failedReload.Complete(ValidateOptionsResult.Fail("reload failed"));

            Assert.True(eventListener.Wait(TestTimeout));
            Assert.Equal(typeof(InvalidOperationException).ToString(), eventListener.ExceptionType);
            Assert.Equal(1, Volatile.Read(ref callbackCalls));
            Assert.Equal(0, Volatile.Read(ref listenerCalls));
            Assert.Same(startup.Options, monitor.CurrentValue);

            changeSource.Trigger();
            ValidationInvocation successfulReload = controlledValidator.TakeNextInvocation(TestTimeout);
            successfulReload.Complete(ValidateOptionsResult.Success);

            Assert.True(listenerCalled.Wait(TestTimeout));
            Assert.Equal(1, Volatile.Read(ref callbackCalls));
            Assert.Equal(1, Volatile.Read(ref listenerCalls));
            Assert.Same(successfulReload.Options, monitor.CurrentValue);
        }

        [Fact]
        public async Task ValidateOnChange_SupersededFailure_IsIgnoredAndLatestGenerationPublishes()
        {
            using var controlledValidator = new ControlledAsyncValidator();
            var changeSource = new ReloadChangeTokenSource<FakeOptions>(Options.DefaultName);
            var services = new ServiceCollection();
            int callbackCalls = 0;
            FakeOptions? listenerValue = null;
            int listenerCalls = 0;
            using var listenerCalled = new ManualResetEventSlim();

            services.AddSingleton<IOptionsChangeTokenSource<FakeOptions>>(changeSource);
            services.AddOptions<FakeOptions>()
                .ValidateOnChange(
                    OptionsReloadValidationBehavior.FailReads,
                    (_, _) => Interlocked.Increment(ref callbackCalls));
            services.AddSingleton<IValidateOptions<FakeOptions>>(controlledValidator);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IOptionsMonitor<FakeOptions> monitor =
                serviceProvider.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            Task startupValidation = GetAsyncStartupValidator(serviceProvider).ValidateAsync(CancellationToken.None);
            ValidationInvocation startup = controlledValidator.TakeNextInvocation(TestTimeout);
            startup.Complete(ValidateOptionsResult.Success);
            await startupValidation;

            using IDisposable? listener = monitor.OnChange((value, _) =>
            {
                listenerValue = value;
                Interlocked.Increment(ref listenerCalls);
                listenerCalled.Set();
            });

            changeSource.Trigger();
            ValidationInvocation supersededReload = controlledValidator.TakeNextInvocation(TestTimeout);
            changeSource.Trigger();
            supersededReload.Fail(new InvalidOperationException("superseded"));

            ValidationInvocation latestReload = controlledValidator.TakeNextInvocation(TestTimeout);

            Assert.Equal(0, Volatile.Read(ref callbackCalls));
            Assert.Equal(0, Volatile.Read(ref listenerCalls));
            Assert.Same(startup.Options, monitor.CurrentValue);
            Assert.Equal(1, controlledValidator.MaximumActiveInvocations);

            latestReload.Complete(ValidateOptionsResult.Success);

            Assert.True(listenerCalled.Wait(TestTimeout));
            Assert.Equal(0, Volatile.Read(ref callbackCalls));
            Assert.Equal(1, Volatile.Read(ref listenerCalls));
            Assert.Same(latestReload.Options, listenerValue);
            Assert.Same(latestReload.Options, monitor.CurrentValue);
            Assert.Equal(3, controlledValidator.AsyncCalls);
            Assert.Equal(1, controlledValidator.MaximumActiveInvocations);
        }

        [Fact]
        public async Task ValidateOnChange_DisposalDuringStartupValidation_CancelsValidation()
        {
            using var controlledValidator = new ControlledAsyncValidator(honorCancellation: true);
            var services = new ServiceCollection();

            services.AddOptions<FakeOptions>().ValidateOnChange();
            services.AddSingleton<IValidateOptions<FakeOptions>>(controlledValidator);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            OptionsMonitor<FakeOptions> monitor = Assert.IsType<OptionsMonitor<FakeOptions>>(
                serviceProvider.GetRequiredService<IOptionsMonitor<FakeOptions>>());
            Task startupValidation = GetAsyncStartupValidator(serviceProvider).ValidateAsync(CancellationToken.None);
            ValidationInvocation startup = controlledValidator.TakeNextInvocation(TestTimeout);

            monitor.Dispose();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startupValidation);
            Assert.True(startup.CancellationToken.IsCancellationRequested);
            Assert.Equal(1, controlledValidator.AsyncCalls);
        }

        [Fact]
        public async Task ValidateOnChange_MultipleRegistrations_LastRegistrationWins()
        {
            using var controlledValidator = new ControlledAsyncValidator();
            var changeSource = new ReloadChangeTokenSource<FakeOptions>(Options.DefaultName);
            var services = new ServiceCollection();
            int firstCallbackCalls = 0;
            Exception? secondCallbackError = null;
            int secondCallbackCalls = 0;
            using var secondCallbackCalled = new ManualResetEventSlim();

            services.AddSingleton<IOptionsChangeTokenSource<FakeOptions>>(changeSource);
            services.AddOptions<FakeOptions>()
                .ValidateOnChange(
                    OptionsReloadValidationBehavior.KeepLastGood,
                    (_, _) => Interlocked.Increment(ref firstCallbackCalls))
                .ValidateOnChange(
                    OptionsReloadValidationBehavior.FailReads,
                    (_, error) =>
                    {
                        secondCallbackError = error;
                        Interlocked.Increment(ref secondCallbackCalls);
                        secondCallbackCalled.Set();
                    });
            services.AddSingleton<IValidateOptions<FakeOptions>>(controlledValidator);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IOptionsMonitor<FakeOptions> monitor =
                serviceProvider.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            Task startupValidation = GetAsyncStartupValidator(serviceProvider).ValidateAsync(CancellationToken.None);
            ValidationInvocation startup = controlledValidator.TakeNextInvocation(TestTimeout);
            startup.Complete(ValidateOptionsResult.Success);
            await startupValidation;

            changeSource.Trigger();
            ValidationInvocation reload = controlledValidator.TakeNextInvocation(TestTimeout);
            reload.Complete(ValidateOptionsResult.Fail("reload failed"));

            Assert.True(secondCallbackCalled.Wait(TestTimeout));
            Assert.Equal(0, Volatile.Read(ref firstCallbackCalls));
            Assert.Equal(1, Volatile.Read(ref secondCallbackCalls));
            OptionsValidationException validationError =
                Assert.IsType<OptionsValidationException>(secondCallbackError);
            Assert.Same(
                validationError,
                Assert.Throws<OptionsValidationException>(() => monitor.CurrentValue));
        }

        [Fact]
        public async Task ValidateOnChange_FailReads_RecoversOnNextSuccessfulReload()
        {
            using var controlledValidator = new ControlledAsyncValidator();
            var changeSource = new ReloadChangeTokenSource<FakeOptions>(Options.DefaultName);
            var services = new ServiceCollection();
            int configureCalls = 0;
            string? callbackName = null;
            Exception? callbackError = null;
            int callbackCalls = 0;
            FakeOptions? listenerValue = null;
            string? listenerName = null;
            int listenerCalls = 0;
            using var onErrorCalled = new ManualResetEventSlim();
            using var listenerCalled = new ManualResetEventSlim();

            services.AddSingleton<IOptionsChangeTokenSource<FakeOptions>>(changeSource);
            services.AddOptions<FakeOptions>()
                .Configure(options => options.Message = Interlocked.Increment(ref configureCalls).ToString())
                .ValidateOnChange(OptionsReloadValidationBehavior.FailReads, (name, error) =>
                {
                    callbackName = name;
                    callbackError = error;
                    Interlocked.Increment(ref callbackCalls);
                    onErrorCalled.Set();
                });
            services.AddSingleton<IValidateOptions<FakeOptions>>(controlledValidator);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IOptionsMonitor<FakeOptions> monitor =
                serviceProvider.GetRequiredService<IOptionsMonitor<FakeOptions>>();
            Task startupValidation = GetAsyncStartupValidator(serviceProvider).ValidateAsync(CancellationToken.None);
            ValidationInvocation startup = controlledValidator.TakeNextInvocation(TestTimeout);
            startup.Complete(ValidateOptionsResult.Success);
            await startupValidation;

            Assert.Same(startup.Options, monitor.CurrentValue);
            using IDisposable? listener = monitor.OnChange((value, name) =>
            {
                listenerValue = value;
                listenerName = name;
                Interlocked.Increment(ref listenerCalls);
                listenerCalled.Set();
            });

            changeSource.Trigger();
            ValidationInvocation failedReload = controlledValidator.TakeNextInvocation(TestTimeout);
            failedReload.Complete(ValidateOptionsResult.Fail("reload failed"));

            Assert.True(onErrorCalled.Wait(TestTimeout));
            Assert.Equal(1, Volatile.Read(ref callbackCalls));
            Assert.Equal(Options.DefaultName, callbackName);
            OptionsValidationException validationError = Assert.IsType<OptionsValidationException>(callbackError);
            Assert.Equal(typeof(FakeOptions), validationError.OptionsType);
            Assert.Equal(Options.DefaultName, validationError.OptionsName);
            Assert.Equal("reload failed", Assert.Single(validationError.Failures));
            Assert.Same(
                validationError,
                Assert.Throws<OptionsValidationException>(() => monitor.CurrentValue));
            Assert.Equal(0, Volatile.Read(ref listenerCalls));

            changeSource.Trigger();
            ValidationInvocation successfulReload = controlledValidator.TakeNextInvocation(TestTimeout);
            successfulReload.Complete(ValidateOptionsResult.Success);

            Assert.True(listenerCalled.Wait(TestTimeout));
            Assert.Equal(1, Volatile.Read(ref callbackCalls));
            Assert.Equal(1, Volatile.Read(ref listenerCalls));
            Assert.Equal(Options.DefaultName, listenerName);
            Assert.Same(successfulReload.Options, listenerValue);
            Assert.Same(successfulReload.Options, monitor.CurrentValue);
            Assert.Equal(3, controlledValidator.AsyncCalls);
        }

        private sealed class OptionsTestEventListener : EventListener
        {
            private readonly ManualResetEventSlim _eventWritten = new ManualResetEventSlim();
            private readonly int _expectedEventId;

            internal OptionsTestEventListener(int expectedEventId)
            {
                _expectedEventId = expectedEventId;
            }

            internal string? ExceptionType { get; private set; }

            internal bool Wait(TimeSpan timeout) => _eventWritten.Wait(timeout);

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "Microsoft-Extensions-Options")
                {
                    EnableEvents(eventSource, EventLevel.LogAlways);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (eventData.EventId == _expectedEventId)
                {
                    ExceptionType = eventData.Payload is { Count: > 2 } payload
                        ? payload[2]?.ToString()
                        : null;
                    _eventWritten.Set();
                }
            }

            public override void Dispose()
            {
                base.Dispose();
                _eventWritten.Dispose();
            }
        }

        private sealed class ReloadChangeTokenSource<TOptions> : IOptionsChangeTokenSource<TOptions>
        {
            private readonly object _sync = new object();
            private FakeChangeToken _token = CreateToken();

            internal ReloadChangeTokenSource(string? name) => Name = name;

            public string? Name { get; }

            public IChangeToken GetChangeToken()
            {
                lock (_sync)
                {
                    return _token;
                }
            }

            internal void Trigger()
            {
                FakeChangeToken token;
                lock (_sync)
                {
                    token = _token;
                    _token = CreateToken();
                }

                token.HasChanged = true;
                token.InvokeChangeCallback();
            }

            private static FakeChangeToken CreateToken() => new FakeChangeToken
            {
                ActiveChangeCallbacks = true,
            };
        }

        private sealed class ControlledAsyncValidator : IAsyncValidateOptions<FakeOptions>, IDisposable
        {
            private readonly ConcurrentQueue<ValidationInvocation> _invocations = new();
            private readonly SemaphoreSlim _entered = new SemaphoreSlim(0);
            private readonly object _maximumActiveLock = new object();
            private readonly bool _honorCancellation;
            private int _activeInvocations;
            private int _asyncCalls;
            private int _maximumActiveInvocations;
            private int _syncCalls;

            internal ControlledAsyncValidator(bool honorCancellation = false) =>
                _honorCancellation = honorCancellation;

            internal int AsyncCalls => Volatile.Read(ref _asyncCalls);

            internal int MaximumActiveInvocations => Volatile.Read(ref _maximumActiveInvocations);

            internal int SyncCalls => Volatile.Read(ref _syncCalls);

            public ValidateOptionsResult Validate(string? name, FakeOptions options)
            {
                Interlocked.Increment(ref _syncCalls);
                return ValidateOptionsResult.Success;
            }

            public async Task<ValidateOptionsResult> ValidateAsync(
                string? name,
                FakeOptions options,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _asyncCalls);
                int active = Interlocked.Increment(ref _activeInvocations);
                lock (_maximumActiveLock)
                {
                    if (active > _maximumActiveInvocations)
                    {
                        Interlocked.Exchange(ref _maximumActiveInvocations, active);
                    }
                }

                var invocation = new ValidationInvocation(name, options, cancellationToken, _honorCancellation);
                _invocations.Enqueue(invocation);
                _entered.Release();

                try
                {
                    return await invocation.Completion.ConfigureAwait(false);
                }
                finally
                {
                    invocation.Dispose();
                    Interlocked.Decrement(ref _activeInvocations);
                }
            }

            internal ValidationInvocation TakeNextInvocation(TimeSpan timeout)
            {
                Assert.True(_entered.Wait(timeout), "Timed out waiting for an asynchronous validation invocation.");
                Assert.True(_invocations.TryDequeue(out ValidationInvocation? invocation));
                return invocation;
            }

            public void Dispose() => _entered.Dispose();
        }

        private sealed class ValidationInvocation : IDisposable
        {
            private readonly TaskCompletionSource<ValidateOptionsResult> _completion =
                new TaskCompletionSource<ValidateOptionsResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly CancellationTokenRegistration _cancellationRegistration;

            internal ValidationInvocation(
                string? name,
                FakeOptions options,
                CancellationToken cancellationToken,
                bool honorCancellation)
            {
                Name = name;
                Options = options;
                CancellationToken = cancellationToken;

                if (honorCancellation)
                {
                    _cancellationRegistration = cancellationToken.Register(
                        static state => ((ValidationInvocation)state!)._completion.TrySetCanceled(),
                        this);
                }
            }

            internal CancellationToken CancellationToken { get; }

            internal Task<ValidateOptionsResult> Completion => _completion.Task;

            internal string? Name { get; }

            internal FakeOptions Options { get; }

            internal void Cancel() => _completion.SetCanceled();

            internal void Complete(ValidateOptionsResult result) => _completion.SetResult(result);

            internal void Fail(Exception error) => _completion.SetException(error);

            public void Dispose() => _cancellationRegistration.Dispose();
        }
    }
}
