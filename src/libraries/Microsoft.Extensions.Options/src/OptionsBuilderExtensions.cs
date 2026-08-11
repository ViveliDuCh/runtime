// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
        /// Enables asynchronous validation when changes to options are detected.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is opt-in and also enables startup validation. Existing reload behavior is unchanged for
        /// options names that do not call this method.
        /// </para>
        /// <para>
        /// Reload signals for one options name are coalesced through one worker, and only the latest observed
        /// generation can be published. Validators that implement both <see cref="IValidateOptions{TOptions}"/> and
        /// <see cref="IAsyncValidateOptions{TOptions}"/> are invoked through
        /// <see cref="IAsyncValidateOptions{TOptions}.ValidateAsync"/>. Only successfully validated values are
        /// published and passed to change listeners.
        /// </para>
        /// <para>
        /// The exact built-in <see cref="OptionsMonitor{TOptions}"/>, <see cref="OptionsFactory{TOptions}"/>, and
        /// <see cref="OptionsCache{TOptions}"/> implementations are required. Startup validation fails when a custom
        /// or derived implementation is registered because the required atomic publication guarantees cannot be
        /// provided through the public contracts.
        /// </para>
        /// <para>
        /// <see cref="IOptions{TOptions}"/> remains fixed to its startup value.
        /// <see cref="IOptionsSnapshot{TOptions}"/> remains scope-local and synchronous.
        /// <see cref="IOptionsMonitor{TOptions}"/> receives successfully validated reloads.
        /// </para>
        /// <para>
        /// When this method is called more than once for the same options type and name, the last registration
        /// determines the behavior and error callback.
        /// </para>
        /// </remarks>
        /// <typeparam name="TOptions">The type of options.</typeparam>
        /// <param name="optionsBuilder">The options builder.</param>
        /// <param name="behavior">One of the enumeration values that specifies how failed reload validation affects reads.</param>
        /// <param name="onError">
        /// An optional callback invoked once after the selected behavior is applied for a failed current generation.
        /// Superseded failures and cancellation caused by monitor disposal do not invoke the callback. Exceptions
        /// thrown by the callback are reported through EventSource and are not propagated.
        /// </param>
        /// <returns>The options builder so that additional calls can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="optionsBuilder"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="behavior"/> is not a defined value.</exception>
        public static OptionsBuilder<TOptions> ValidateOnChange<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions>(
            this OptionsBuilder<TOptions> optionsBuilder,
            OptionsReloadValidationBehavior behavior = OptionsReloadValidationBehavior.KeepLastGood,
            Action<string?, Exception>? onError = null)
            where TOptions : class
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);

            if (behavior is not OptionsReloadValidationBehavior.KeepLastGood and
                not OptionsReloadValidationBehavior.FailReads)
            {
                throw new ArgumentOutOfRangeException(nameof(behavior));
            }

            optionsBuilder.Services.AddSingleton(
                new OptionsReloadValidationRegistration<TOptions>(optionsBuilder.Name, behavior, onError));
            optionsBuilder.Services.TryAddSingleton<OptionsReloadValidation<TOptions>>();
            optionsBuilder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<TOptions>, OptionsReloadValidationMarker<TOptions>>());

            return optionsBuilder.ValidateOnStart();
        }

        /// <summary>
        /// Enforces options validation check on start rather than at run time.
        /// </summary>
        /// <remarks>
        /// When the built-in <see cref="IOptionsFactory{TOptions}"/> implementation is used, asynchronous validation
        /// runs during startup and seeds the built-in <see cref="IOptions{TOptions}"/> and
        /// <see cref="IOptionsMonitor{TOptions}"/> instances for subsequent synchronous access. If an options value
        /// was successfully created synchronously before startup, that instance retains the singleton slot and is
        /// published to the monitor cache while asynchronous validation runs against a separate startup candidate.
        /// A derived or replacement <see cref="IOptionsFactory{TOptions}"/> uses synchronous startup validation and
        /// does not invoke <see cref="IAsyncValidateOptions{TOptions}.ValidateAsync"/>.
        /// Options that require asynchronous validation cannot be accessed synchronously before startup validation
        /// completes. Default-name asynchronous validation requires the built-in <see cref="IOptions{TOptions}"/>
        /// implementation so the validated value can be installed safely; startup fails when a custom implementation
        /// is registered. The built-in <see cref="IOptionsSnapshot{TOptions}"/> implementation validates instances
        /// synchronously in per-scope caches that startup validation does not populate. The built-in options monitor
        /// reloads synchronously unless <see cref="ValidateOnChange{TOptions}"/>
        /// is enabled for the options name. Publication to the built-in monitor cache is atomic. The
        /// <see cref="IOptionsMonitorCache{TOptions}"/> contract has no atomic replacement operation, so applications
        /// using a custom or derived cache must avoid concurrent cache access during startup validation if atomic
        /// publication is required. Startup validation throws <see cref="InvalidOperationException"/> if publication
        /// to a custom or derived cache does not succeed.
        /// </remarks>
        /// <typeparam name="TOptions">The type of options.</typeparam>
        /// <param name="optionsBuilder">The <see cref="OptionsBuilder{TOptions}"/> to configure options instance.</param>
        /// <returns>The <see cref="OptionsBuilder{TOptions}"/> so that additional calls can be chained.</returns>
        public static OptionsBuilder<TOptions> ValidateOnStart<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions>(this OptionsBuilder<TOptions> optionsBuilder)
            where TOptions : class
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);

            string name = optionsBuilder.Name;

            optionsBuilder.Services.TryAddSingleton<OptionsReloadValidation<TOptions>>();

            // Register the built-in validator as a single IStartupValidator (for back-compatibility)
            // and as an enumerable IAsyncStartupValidator so the host can run it alongside any custom async validators.
            optionsBuilder.Services.TryAddTransient<IStartupValidator, StartupValidator>();
            optionsBuilder.Services.TryAddEnumerable(ServiceDescriptor.Transient<IAsyncStartupValidator, StartupValidator>());
            optionsBuilder.Services.AddOptions<StartupValidatorOptions>()
                .Configure<IOptions<TOptions>, IOptionsMonitor<TOptions>, IOptionsFactory<TOptions>, IOptionsMonitorCache<TOptions>, OptionsReloadValidation<TOptions>>((vo, options, monitor, factory, sharedCache, reloadValidation) =>
                {
                    // Sync path (custom sync-only IStartupValidator): force evaluation through the monitor,
                    // which runs every validator, including an async validator's fail-fast synchronous Validate.
                    vo._validators[(typeof(TOptions), name)] = () => monitor.Get(name);

                    // Async path: run the complete validation (both sync and async validators) for this (type, name)
                    // and seed the monitor cache with the validated instance so the first synchronous access after
                    // startup returns it instead of re-running the throwing synchronous Validate.
                    vo._asyncValidators[(typeof(TOptions), name)] = async (CancellationToken ct) =>
                    {
                        if (reloadValidation.TryGetRegistration(name, out _))
                        {
                            if (monitor is not OptionsMonitor<TOptions> builtInMonitor ||
                                builtInMonitor.GetType() != typeof(OptionsMonitor<TOptions>) ||
                                factory is not OptionsFactory<TOptions> ||
                                factory.GetType() != typeof(OptionsFactory<TOptions>) ||
                                sharedCache is not OptionsCache<TOptions> ||
                                sharedCache.GetType() != typeof(OptionsCache<TOptions>))
                            {
                                throw new InvalidOperationException(
                                    SR.Format(
                                        SR.OptionsReloadValidationUnsupportedServices,
                                        typeof(TOptions),
                                        monitor.GetType(),
                                        factory.GetType(),
                                        sharedCache.GetType()));
                            }

                            UnnamedOptionsManager<TOptions>? optionsManager = null;

                            if (name == Microsoft.Extensions.Options.Options.DefaultName)
                            {
                                optionsManager =
                                    options as UnnamedOptionsManager<TOptions> ??
                                    throw new InvalidOperationException(
                                        SR.Format(
                                            SR.AsyncValidationUnsupportedIOptions,
                                            typeof(TOptions),
                                            options.GetType()));
                            }

                            await builtInMonitor.ValidateOnStartAsync(name, optionsManager, ct).ConfigureAwait(false);
                            return;
                        }

                        if (factory is OptionsFactory<TOptions> asyncFactory &&
                            asyncFactory.GetType() == typeof(OptionsFactory<TOptions>) &&
                            asyncFactory.HasAsyncValidators)
                        {
                            UnnamedOptionsManager<TOptions>? optionsManager = null;

                            if (name == Microsoft.Extensions.Options.Options.DefaultName)
                            {
                                optionsManager =
                                    options as UnnamedOptionsManager<TOptions> ??
                                    throw new InvalidOperationException(
                                        SR.Format(
                                            SR.AsyncValidationUnsupportedIOptions,
                                            typeof(TOptions),
                                            options.GetType()));
                            }

                            TOptions validated = await asyncFactory.CreateAsync(name, ct).ConfigureAwait(false);
                            // A successfully created pre-start IOptions value owns the singleton slot, even though
                            // asynchronous startup validation ran against this separately created candidate.
                            TOptions winner = optionsManager?.GetOrSetValue(validated) ?? validated;

                            if (!OptionsCache<TOptions>.TryAddOrReplace(sharedCache, name, winner))
                            {
                                throw new InvalidOperationException(
                                    SR.Format(
                                        SR.AsyncValidationCachePublicationFailed,
                                        typeof(TOptions),
                                        name,
                                        sharedCache.GetType()));
                            }
                        }
                        else
                        {
                            // Sync-only validation and custom factories use the monitor so an existing cached
                            // instance is preserved and configuration does not run again.
                            monitor.Get(name);
                        }
                    };
                });

            return optionsBuilder;
        }
    }
}
