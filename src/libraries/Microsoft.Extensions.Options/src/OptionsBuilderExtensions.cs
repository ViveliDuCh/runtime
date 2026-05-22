// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for adding configuration-related options services to the DI container via <see cref="OptionsBuilder{TOptions}"/>.
    /// </summary>
    public static class OptionsBuilderExtensions
    {
        /// <summary>
        /// Enforces options validation check on start rather than at run time.
        /// </summary>
        /// <typeparam name="TOptions">The type of options.</typeparam>
        /// <param name="optionsBuilder">The <see cref="OptionsBuilder{TOptions}"/> to configure options instance.</param>
        /// <returns>The <see cref="OptionsBuilder{TOptions}"/> so that additional calls can be chained.</returns>
        public static OptionsBuilder<TOptions> ValidateOnStart<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions>(this OptionsBuilder<TOptions> optionsBuilder)
            where TOptions : class
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);

            optionsBuilder.Services.TryAddTransient<IStartupValidator, StartupValidator>();
            optionsBuilder.Services.AddOptions<StartupValidatorOptions>()
                .Configure<IOptionsMonitor<TOptions>>((vo, options) =>
                {
                    // This adds an action that resolves the options value to force evaluation
                    // We don't care about the result as duplicates are not important
                    vo._validators[(typeof(TOptions), optionsBuilder.Name)] = () => options.Get(optionsBuilder.Name);
                });

            return optionsBuilder;
        }

#if NET11_0_OR_GREATER
        /// <summary>
        /// Enforces asynchronous options validation check on start rather than at run time.
        /// Supports <see cref="IAsyncValidateOptions{TOptions}"/> validators, including those
        /// backed by async validation attributes.
        /// </summary>
        /// <typeparam name="TOptions">The type of options.</typeparam>
        /// <param name="optionsBuilder">The <see cref="OptionsBuilder{TOptions}"/> to configure options instance.</param>
        /// <returns>The <see cref="OptionsBuilder{TOptions}"/> so that additional calls can be chained.</returns>
        public static OptionsBuilder<TOptions> ValidateOnStartAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions>(this OptionsBuilder<TOptions> optionsBuilder)
            where TOptions : class
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);

            optionsBuilder.Services.TryAddSingleton<AsyncValidationState>();

            // Mark that a startup validator is being registered so the guard knows
            optionsBuilder.Services.AddOptions<AsyncStartupValidatorOptions>()
                .Configure<AsyncValidationState>((_, state) => state.StartupValidatorRegistered = true);

            optionsBuilder.Services.TryAddTransient<IAsyncStartupValidator, AsyncStartupValidator>();
            optionsBuilder.Services.AddOptions<AsyncStartupValidatorOptions>()
                .Configure<IOptionsMonitor<TOptions>, IEnumerable<IAsyncValidateOptions<TOptions>>>((vo, options, validators) =>
                {
                    vo._validators[(typeof(TOptions), optionsBuilder.Name)] = async (ct) =>
                    {
                        TOptions optionsValue = options.Get(optionsBuilder.Name);

                        // Start all validators in parallel
                        var tasks = new List<Task<ValidateOptionsResult>>();
                        foreach (IAsyncValidateOptions<TOptions> validator in validators)
                        {
                            tasks.Add(validator
                                .ValidateAsync(optionsBuilder.Name, optionsValue, ct)
                                .AsTask());
                        }

                        // Await all and collect failures
                        ValidateOptionsResult[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
                        var failures = new List<string>();

                        foreach (ValidateOptionsResult result in results)
                        {
                            if (result is not null && result.Failed)
                            {
                                failures.AddRange(result.Failures);
                            }
                        }

                        if (failures.Count > 0)
                        {
                            throw new OptionsValidationException(
                                optionsBuilder.Name, typeof(TOptions), failures);
                        }
                    };
                });

            return optionsBuilder;
        }
#endif
    }
}
