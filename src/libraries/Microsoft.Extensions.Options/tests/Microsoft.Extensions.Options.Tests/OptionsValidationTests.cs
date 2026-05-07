// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if NET11_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
#endif
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Extensions.Options.Tests
{
    public class OptionsValidationTest
    {
        [Fact]
        public void ValidationResultSuccessIfNameMatched()
        {
            var services = new ServiceCollection();
            services.AddOptions<ComplexOptions>()
                .Validate(o => o.Boolean)
                .Validate(o => o.Integer > 12);

            var sp = services.BuildServiceProvider();

            var validations = sp.GetService<IEnumerable<IValidateOptions<ComplexOptions>>>();
            var options = new ComplexOptions
            {
                Boolean = true,
                Integer = 13
            };
            foreach (var v in validations)
            {
                Assert.True(v.Validate(Options.DefaultName, options).Succeeded);
                Assert.True(v.Validate("Something", options).Skipped);
            }
        }

        [Fact]
        public void ValidateOnStart_NotCalled()
        {
            var services = new ServiceCollection();
            services.AddOptions<ComplexOptions>()
                .Validate(o => o.Integer > 12);

            var sp = services.BuildServiceProvider();

            var validator = sp.GetService<IStartupValidator>();
            Assert.Null(validator);
        }

        [Fact]
        public void ValidateOnStart_Called()
        {
            var services = new ServiceCollection();
            services.AddOptions<ComplexOptions>()
                .Validate(o => o.Integer > 12)
                .ValidateOnStart();

            var sp = services.BuildServiceProvider();

            var validator = sp.GetService<IStartupValidator>();
            Assert.NotNull(validator);
            OptionsValidationException ex = Assert.Throws<OptionsValidationException>(validator.Validate);
            Assert.Equal(1, ex.Failures.Count());
        }

        [Fact]
        public void ValidateOnStart_CalledMultiple()
        {
            var services = new ServiceCollection();
            services.AddOptions<ComplexOptions>()
                .Validate(o => o.Boolean)
                .Validate(o => o.Integer > 12)
                .ValidateOnStart();

            var sp = services.BuildServiceProvider();

            var validator = sp.GetService<IStartupValidator>();
            Assert.NotNull(validator);
            OptionsValidationException ex = Assert.Throws<OptionsValidationException>(validator.Validate);
            Assert.Equal(2, ex.Failures.Count());
        }

        [Fact]
        public void ValidationResultSkippedIfNameNotMatched()
        {
            var services = new ServiceCollection();
            services.AddOptions<ComplexOptions>("Name")
                .Validate(o => o.Boolean);

            var sp = services.BuildServiceProvider();

            var validations = sp.GetService<IEnumerable<IValidateOptions<ComplexOptions>>>();
            var options = new ComplexOptions
            {
                Boolean = true,
            };
            foreach (var v in validations)
            {
                Assert.True(v.Validate(Options.DefaultName, options).Skipped);
                Assert.True(v.Validate("Name", options).Succeeded);
            }
        }

        [Fact]
        public void ValidationResultFailedOrSkipped()
        {
            var services = new ServiceCollection();
            services.AddOptions<ComplexOptions>("Name")
                .Validate(o => o.Boolean);

            var sp = services.BuildServiceProvider();

            var validations = sp.GetService<IEnumerable<IValidateOptions<ComplexOptions>>>();
            var options = new ComplexOptions
            {
                Boolean = false,
            };
            foreach (var v in validations)
            {
                Assert.True(v.Validate(Options.DefaultName, options).Skipped);
                Assert.True(v.Validate("Name", options).Failed);
            }
        }

        [Fact]
        public void ValidationCannotBeNull()
        {
            string validName = "Name";
            string validFailureMessage = "Something's wrong";
            object validDependency = new();

            Assert.Throws<ArgumentNullException>(() => new ValidateOptions<object>(validName, null, validFailureMessage));
            Assert.Throws<ArgumentNullException>(() => new ValidateOptions<object, object>(validName, validDependency, null, validFailureMessage));
            Assert.Throws<ArgumentNullException>(() => new ValidateOptions<object, object, object>(validName, validDependency, validDependency, null, validFailureMessage));
            Assert.Throws<ArgumentNullException>(() => new ValidateOptions<object, object, object, object>(validName, validDependency, validDependency, validDependency, null, validFailureMessage));
            Assert.Throws<ArgumentNullException>(() => new ValidateOptions<object, object, object, object, object>(validName, validDependency, validDependency, validDependency, validDependency, null, validFailureMessage));
            Assert.Throws<ArgumentNullException>(() => new ValidateOptions<object, object, object, object, object, object>(validName, validDependency, validDependency, validDependency, validDependency, validDependency, null, validFailureMessage));
        }

#if NET11_0_OR_GREATER
        [Fact]
        public void ValidateOnStartAsync_NotCalled()
        {
            var services = new ServiceCollection();
            services.AddOptions<ComplexOptions>()
                .Validate(o => o.Integer > 12);

            var sp = services.BuildServiceProvider();

            var validator = sp.GetService<IAsyncStartupValidator>();
            Assert.Null(validator);
        }

        [Fact]
        public async Task ValidateOnStartAsync_Called()
        {
            var services = new ServiceCollection();

            // Register an async validator that will fail
            services.AddSingleton<IAsyncValidateOptions<ComplexOptions>>(
                new FailingAsyncValidator<ComplexOptions>("async error"));

            services.AddOptions<ComplexOptions>()
                .Configure(o => o.Integer = 5)
                .ValidateOnStartAsync();

            var sp = services.BuildServiceProvider();

            var validator = sp.GetService<IAsyncStartupValidator>();
            Assert.NotNull(validator);
            OptionsValidationException ex = await Assert.ThrowsAsync<OptionsValidationException>(
                () => validator.ValidateAsync());
            Assert.Contains("async error", ex.Failures);
        }

        [Fact]
        public async Task ValidateOnStartAsync_CalledMultiple()
        {
            var services = new ServiceCollection();

            // Register two async validators that fail
            services.AddSingleton<IAsyncValidateOptions<ComplexOptions>>(
                new FailingAsyncValidator<ComplexOptions>("error1"));
            services.AddSingleton<IAsyncValidateOptions<ComplexOptions>>(
                new FailingAsyncValidator<ComplexOptions>("error2"));

            services.AddOptions<ComplexOptions>()
                .Configure(o => o.Integer = 5)
                .ValidateOnStartAsync();

            var sp = services.BuildServiceProvider();

            var validator = sp.GetService<IAsyncStartupValidator>();
            Assert.NotNull(validator);
            OptionsValidationException ex = await Assert.ThrowsAsync<OptionsValidationException>(
                () => validator.ValidateAsync());
            Assert.Equal(2, ex.Failures.Count());
            Assert.Contains("error1", ex.Failures);
            Assert.Contains("error2", ex.Failures);
        }

        [Fact]
        public async Task ValidateOnStartAsync_AllValid_Succeeds()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAsyncValidateOptions<ComplexOptions>>(
                new SucceedingAsyncValidator<ComplexOptions>());

            services.AddOptions<ComplexOptions>()
                .Configure(o => o.Integer = 13)
                .ValidateOnStartAsync();

            var sp = services.BuildServiceProvider();

            var validator = sp.GetService<IAsyncStartupValidator>();
            Assert.NotNull(validator);
            await validator.ValidateAsync(); // Should not throw
        }

        [Fact]
        public async Task ValidateOnStartAsync_NamedOptions_Skips()
        {
            var services = new ServiceCollection();

            // Register async validator for named options "Name1"
            services.AddSingleton<IAsyncValidateOptions<ComplexOptions>>(
                new FailingAsyncValidator<ComplexOptions>("error for Name1", name: "Name1"));

            services.AddOptions<ComplexOptions>("Name2")
                .Configure(o => o.Integer = 5)
                .ValidateOnStartAsync();

            var sp = services.BuildServiceProvider();

            var validator = sp.GetService<IAsyncStartupValidator>();
            Assert.NotNull(validator);
            // Validator is for "Name1" but we registered "Name2" — should skip
            await validator.ValidateAsync(); // Should not throw
        }

        [Fact]
        public void ValidateOnStartAsync_Idempotent()
        {
            var services = new ServiceCollection();
            services.AddOptions<ComplexOptions>()
                .ValidateOnStartAsync();
            services.AddOptions<FakeOptions>()
                .ValidateOnStartAsync();

            Assert.Equal(1, services.Count(sd => sd.ServiceType == typeof(IAsyncStartupValidator)));
        }

        private class FailingAsyncValidator<TOptions> : IAsyncValidateOptions<TOptions> where TOptions : class
        {
            private readonly string _error;
            private readonly string? _name;

            public FailingAsyncValidator(string error, string? name = null)
            {
                _error = error;
                _name = name;
            }

            public ValueTask<ValidateOptionsResult> ValidateAsync(string? name, TOptions options, CancellationToken cancellationToken = default)
            {
                if (_name is not null && _name != name)
                {
                    return new ValueTask<ValidateOptionsResult>(ValidateOptionsResult.Skip);
                }

                return new ValueTask<ValidateOptionsResult>(ValidateOptionsResult.Fail(_error));
            }
        }

        private class SucceedingAsyncValidator<TOptions> : IAsyncValidateOptions<TOptions> where TOptions : class
        {
            public ValueTask<ValidateOptionsResult> ValidateAsync(string? name, TOptions options, CancellationToken cancellationToken = default)
            {
                return new ValueTask<ValidateOptionsResult>(ValidateOptionsResult.Success);
            }
        }

        private class DelayedAsyncValidator<TOptions> : IAsyncValidateOptions<TOptions> where TOptions : class
        {
            private readonly TimeSpan _delay;
            private readonly bool _succeed;

            public DelayedAsyncValidator(TimeSpan delay, bool succeed)
            {
                _delay = delay;
                _succeed = succeed;
            }

            public async ValueTask<ValidateOptionsResult> ValidateAsync(
                string? name, TOptions options, CancellationToken cancellationToken = default)
            {
                await Task.Delay(_delay, cancellationToken);

                return _succeed ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("delayed failure");
            }
        }

        [Fact]
        public async Task ValidateOnStartAsync_RunsValidatorsInParallel()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAsyncValidateOptions<ComplexOptions>>(
                new DelayedAsyncValidator<ComplexOptions>(TimeSpan.FromMilliseconds(200), succeed: true));
            services.AddSingleton<IAsyncValidateOptions<ComplexOptions>>(
                new DelayedAsyncValidator<ComplexOptions>(TimeSpan.FromMilliseconds(200), succeed: true));

            services.AddOptions<ComplexOptions>()
                .Configure(o => o.Integer = 5)
                .ValidateOnStartAsync();

            var sp = services.BuildServiceProvider();
            var validator = sp.GetRequiredService<IAsyncStartupValidator>();

            var sw = Stopwatch.StartNew();
            await validator.ValidateAsync();
            sw.Stop();

            // If parallel: ~200ms. If sequential: ~400ms.
            Assert.True(sw.ElapsedMilliseconds < 350,
                $"Validators should run in parallel. Elapsed: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task ValidateOnStartAsync_ParallelCollectsAllFailures()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAsyncValidateOptions<ComplexOptions>>(
                new DelayedAsyncValidator<ComplexOptions>(TimeSpan.FromMilliseconds(50), succeed: false));
            services.AddSingleton<IAsyncValidateOptions<ComplexOptions>>(
                new DelayedAsyncValidator<ComplexOptions>(TimeSpan.FromMilliseconds(50), succeed: false));

            services.AddOptions<ComplexOptions>()
                .Configure(o => o.Integer = 5)
                .ValidateOnStartAsync();

            var sp = services.BuildServiceProvider();
            var validator = sp.GetRequiredService<IAsyncStartupValidator>();

            OptionsValidationException ex = await Assert.ThrowsAsync<OptionsValidationException>(
                () => validator.ValidateAsync());
            Assert.Equal(2, ex.Failures.Count());
        }

        [Fact]
        public async Task AsyncStartupValidator_RunsOptionsTypesInParallel()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IAsyncValidateOptions<ComplexOptions>>(
                new DelayedAsyncValidator<ComplexOptions>(TimeSpan.FromMilliseconds(200), succeed: true));
            services.AddSingleton<IAsyncValidateOptions<FakeOptions>>(
                new DelayedAsyncValidator<FakeOptions>(TimeSpan.FromMilliseconds(200), succeed: true));

            services.AddOptions<ComplexOptions>()
                .Configure(o => o.Integer = 5)
                .ValidateOnStartAsync();
            services.AddOptions<FakeOptions>()
                .ValidateOnStartAsync();

            var sp = services.BuildServiceProvider();
            var validator = sp.GetRequiredService<IAsyncStartupValidator>();

            var sw = Stopwatch.StartNew();
            await validator.ValidateAsync();
            sw.Stop();

            // If parallel: ~200ms. If sequential: ~400ms.
            Assert.True(sw.ElapsedMilliseconds < 350,
                $"Options types should validate in parallel. Elapsed: {sw.ElapsedMilliseconds}ms");
        }
#endif
    }
}
