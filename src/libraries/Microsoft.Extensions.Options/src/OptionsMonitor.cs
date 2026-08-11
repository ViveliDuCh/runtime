// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.Options
{
    /// <summary>
    /// Implements <see cref="IOptionsMonitor{TOptions}"/>.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    public class OptionsMonitor<[DynamicallyAccessedMembers(Options.DynamicallyAccessedMembers)] TOptions> :
        IOptionsMonitor<TOptions>,
        IDisposable
        where TOptions : class
    {
        private readonly IOptionsMonitorCache<TOptions> _cache;
        private readonly IOptionsFactory<TOptions> _factory;
        private readonly List<IDisposable> _registrations = new List<IDisposable>();
        private readonly ReloadCoordinator? _reloadCoordinator;
        private bool _disposed;
        internal event Action<TOptions, string>? _onChange;

        /// <summary>
        /// Initializes a new instance of <see cref="OptionsMonitor{TOptions}"/> with the specified factory, sources, and cache.
        /// </summary>
        /// <param name="factory">The factory to use to create options.</param>
        /// <param name="sources">The sources used to listen for changes to the options instance.</param>
        /// <param name="cache">The cache used to store options.</param>
        public OptionsMonitor(IOptionsFactory<TOptions> factory, IEnumerable<IOptionsChangeTokenSource<TOptions>> sources, IOptionsMonitorCache<TOptions> cache)
        {
            _factory = factory;
            _cache = cache;

            if (GetType() == typeof(OptionsMonitor<TOptions>) &&
                factory is OptionsFactory<TOptions> optionsFactory &&
                optionsFactory.GetType() == typeof(OptionsFactory<TOptions>) &&
                optionsFactory.ReloadValidation is OptionsReloadValidation<TOptions> reloadValidation &&
                cache is OptionsCache<TOptions> optionsCache &&
                optionsCache.GetType() == typeof(OptionsCache<TOptions>))
            {
                _reloadCoordinator = new ReloadCoordinator(optionsFactory, optionsCache, reloadValidation);
            }

            void RegisterSource(IOptionsChangeTokenSource<TOptions> source)
            {
                IDisposable registration = ChangeToken.OnChange(
                          source.GetChangeToken,
                          InvokeChanged,
                          source.Name);

                _registrations.Add(registration);
            }

            // The default DI container uses arrays under the covers. Take advantage of this knowledge
            // by checking for an array and enumerate over that, so we don't need to allocate an enumerator.
            if (sources is IOptionsChangeTokenSource<TOptions>[] sourcesArray)
            {
                foreach (IOptionsChangeTokenSource<TOptions> source in sourcesArray)
                {
                    RegisterSource(source);
                }
            }
            else
            {
                foreach (IOptionsChangeTokenSource<TOptions> source in sources)
                {
                    RegisterSource(source);
                }
            }
        }

        private void InvokeChanged(string? name)
        {
            name ??= Options.DefaultName;

            if (TryScheduleReload(name))
            {
                return;
            }

            _cache.TryRemove(name);
            TOptions options = Get(name);
            _onChange?.Invoke(options, name);
        }

        private bool TryScheduleReload(string name)
        {
            ReloadCoordinator? coordinator = _reloadCoordinator;

            if (coordinator is null ||
                !coordinator.States.TryGetValue(name, out ReloadState? state))
            {
                return false;
            }

            bool startWorker = false;

            lock (state.SyncObj)
            {
                state.Generation++;

                if (Volatile.Read(ref _disposed))
                {
                    return true;
                }

                if (state.StartupValidated && !state.WorkerRunning)
                {
                    state.WorkerRunning = true;
                    startWorker = true;
                }
            }

            if (startWorker)
            {
                _ = ProcessReloadsAsync(name, state, coordinator);
            }

            return true;
        }

        internal async Task ValidateOnStartAsync(
            string name,
            UnnamedOptionsManager<TOptions>? optionsManager,
            CancellationToken cancellationToken)
        {
            ReloadCoordinator? coordinator = _reloadCoordinator;

            if (coordinator is null ||
                !coordinator.States.TryGetValue(name, out ReloadState? state))
            {
                throw new InvalidOperationException(
                    SR.Format(SR.OptionsReloadValidationUnsupported, typeof(TOptions)));
            }

            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                coordinator.Cancellation.Token);
            CancellationToken validationToken = linkedCancellation.Token;

            await state.StartupGate.WaitAsync(validationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    validationToken.ThrowIfCancellationRequested();

                    long generation;
                    lock (state.SyncObj)
                    {
                        if (state.StartupValidated)
                        {
                            return;
                        }

                        generation = state.Generation;
                    }

                    TOptions candidate;
                    try
                    {
                        candidate = await coordinator.Factory.CreateAsync(name, validationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (validationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        validationToken.ThrowIfCancellationRequested();

                        lock (state.SyncObj)
                        {
                            if (generation != state.Generation)
                            {
                                continue;
                            }
                        }

                        throw;
                    }

                    lock (state.SyncObj)
                    {
                        if (Volatile.Read(ref _disposed))
                        {
                            throw new OperationCanceledException(coordinator.Cancellation.Token);
                        }

                        if (generation != state.Generation)
                        {
                            continue;
                        }

                        TOptions winner = optionsManager?.GetOrSetValue(candidate) ?? candidate;
                        coordinator.Cache.SetValidated(name, winner);
                        state.StartupValidated = true;
                        return;
                    }
                }
            }
            finally
            {
                state.StartupGate.Release();
            }
        }

        private async Task ProcessReloadsAsync(string name, ReloadState state, ReloadCoordinator coordinator)
        {
            long observedGeneration = -1;

            try
            {
                while (true)
                {
                    lock (state.SyncObj)
                    {
                        if (Volatile.Read(ref _disposed))
                        {
                            return;
                        }

                        observedGeneration = state.Generation;
                    }

                    TOptions candidate;
                    try
                    {
                        candidate = await coordinator.Factory
                            .CreateAsync(name, coordinator.Cancellation.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (coordinator.Cancellation.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception error)
                    {
                        lock (state.SyncObj)
                        {
                            if (Volatile.Read(ref _disposed))
                            {
                                return;
                            }

                            if (observedGeneration != state.Generation)
                            {
                                continue;
                            }

                            if (state.Registration.Behavior == OptionsReloadValidationBehavior.FailReads)
                            {
                                coordinator.Cache.SetException(name, error);
                            }
                        }

                        ReportReloadFailure(name, state.Registration, error);

                        lock (state.SyncObj)
                        {
                            if (Volatile.Read(ref _disposed))
                            {
                                return;
                            }

                            if (observedGeneration != state.Generation)
                            {
                                continue;
                            }

                            return;
                        }
                    }

                    lock (state.SyncObj)
                    {
                        if (Volatile.Read(ref _disposed))
                        {
                            return;
                        }

                        if (observedGeneration != state.Generation)
                        {
                            continue;
                        }

                        coordinator.Cache.SetValidated(name, candidate);
                    }

                    NotifyChanged(name, candidate);

                    lock (state.SyncObj)
                    {
                        if (Volatile.Read(ref _disposed))
                        {
                            return;
                        }

                        if (observedGeneration != state.Generation)
                        {
                            continue;
                        }

                        return;
                    }
                }
            }
            catch (Exception error)
            {
                if (!Volatile.Read(ref _disposed))
                {
                    OptionsEventSource.Log.ReloadWorkerFailed(
                        typeof(TOptions).ToString(),
                        name,
                        error.GetType().ToString());
                }
            }
            finally
            {
                bool restart = false;

                lock (state.SyncObj)
                {
                    state.WorkerRunning = false;

                    if (!Volatile.Read(ref _disposed) &&
                        state.StartupValidated &&
                        observedGeneration != state.Generation)
                    {
                        state.WorkerRunning = true;
                        restart = true;
                    }
                }

                if (restart)
                {
                    _ = ProcessReloadsAsync(name, state, coordinator);
                }
            }
        }

        private void ReportReloadFailure(
            string name,
            OptionsReloadValidationRegistration<TOptions> registration,
            Exception error)
        {
            OptionsEventSource.Log.ReloadValidationFailed(
                typeof(TOptions).ToString(),
                name,
                error.GetType().ToString(),
                (int)registration.Behavior);

            if (registration.OnError is null || Volatile.Read(ref _disposed))
            {
                return;
            }

            try
            {
                registration.OnError(name, error);
            }
            catch (Exception callbackError)
            {
                OptionsEventSource.Log.ReloadErrorCallbackFailed(
                    typeof(TOptions).ToString(),
                    name,
                    callbackError.GetType().ToString());
            }
        }

        private void NotifyChanged(string name, TOptions options)
        {
            if (Volatile.Read(ref _disposed))
            {
                return;
            }

            try
            {
                _onChange?.Invoke(options, name);
            }
            catch (Exception error)
            {
                OptionsEventSource.Log.ChangeListenerFailed(
                    typeof(TOptions).ToString(),
                    name,
                    error.GetType().ToString());
            }
        }

        /// <summary>
        /// Gets the present value of the options (equivalent to <c>Get(Options.DefaultName)</c>).
        /// </summary>
        /// <exception cref="OptionsValidationException">One or more <see cref="IValidateOptions{TOptions}"/> return failed <see cref="ValidateOptionsResult"/> when validating the <typeparamref name="TOptions"/> instance created.</exception>
        /// <exception cref="MissingMethodException">The <typeparamref name="TOptions"/> does not have a public parameterless constructor or <typeparamref name="TOptions"/> is <see langword="abstract"/>.</exception>
        public TOptions CurrentValue
        {
            get => Get(Options.DefaultName);
        }

        /// <summary>
        /// Returns a configured <typeparamref name="TOptions"/> instance with the given <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the <typeparamref name="TOptions"/> instance. If <see langword="null"/>, <see cref="Options.DefaultName"/>, which is the empty string, is used.</param>
        /// <returns>The <typeparamref name="TOptions"/> instance that matches the given <paramref name="name"/>.</returns>
        /// <exception cref="OptionsValidationException">One or more <see cref="IValidateOptions{TOptions}"/> return failed <see cref="ValidateOptionsResult"/> when validating the <typeparamref name="TOptions"/> instance created.</exception>
        /// <exception cref="MissingMethodException">The <typeparamref name="TOptions"/> does not have a public parameterless constructor or <typeparamref name="TOptions"/> is <see langword="abstract"/>.</exception>
        public virtual TOptions Get(string? name)
        {
            if (_cache is not OptionsCache<TOptions> optionsCache)
            {
                // copying captured variables to locals avoids allocating a closure if we don't enter the if
                string localName = name ?? Options.DefaultName;
                IOptionsFactory<TOptions> localFactory = _factory;
                return _cache.GetOrAdd(localName, () => localFactory.Create(localName));
            }

            // non-allocating fast path
            return optionsCache.GetOrAdd(name, static (name, factory) => factory.Create(name), _factory);

        }

        /// <summary>
        /// Registers a listener to be called whenever <typeparamref name="TOptions"/> changes.
        /// </summary>
        /// <param name="listener">The action to be invoked when <typeparamref name="TOptions"/> has changed.</param>
        /// <returns>An <see cref="IDisposable"/> that should be disposed to stop listening for changes.</returns>
        public IDisposable OnChange(Action<TOptions, string> listener)
        {
            var disposable = new ChangeTrackerDisposable(this, listener);
            _onChange += disposable.OnChange;
            return disposable;
        }

        /// <summary>
        /// Removes all change registration subscriptions.
        /// </summary>
        public void Dispose()
        {
            Volatile.Write(ref _disposed, true);
            _reloadCoordinator?.Cancellation.Cancel();

            // Remove all subscriptions to the change tokens
            foreach (IDisposable registration in _registrations)
            {
                registration.Dispose();
            }

            _registrations.Clear();
        }

        private sealed class ReloadCoordinator
        {
            internal ReloadCoordinator(
                OptionsFactory<TOptions> factory,
                OptionsCache<TOptions> cache,
                OptionsReloadValidation<TOptions> reloadValidation)
            {
                Factory = factory;
                Cache = cache;
                States = new Dictionary<string, ReloadState>(StringComparer.Ordinal);

                foreach (OptionsReloadValidationRegistration<TOptions> registration in reloadValidation.Registrations)
                {
                    States[registration.Name] = new ReloadState(registration);
                }
            }

            internal OptionsFactory<TOptions> Factory { get; }

            internal OptionsCache<TOptions> Cache { get; }

            internal Dictionary<string, ReloadState> States { get; }

            internal CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();
        }

        private sealed class ReloadState
        {
            internal ReloadState(OptionsReloadValidationRegistration<TOptions> registration)
            {
                Registration = registration;
            }

            internal object SyncObj { get; } = new object();

            internal SemaphoreSlim StartupGate { get; } = new SemaphoreSlim(1, 1);

            internal OptionsReloadValidationRegistration<TOptions> Registration { get; }

            internal long Generation;

            internal bool StartupValidated;

            internal bool WorkerRunning;
        }

        internal sealed class ChangeTrackerDisposable : IDisposable
        {
            private readonly Action<TOptions, string> _listener;
            private readonly OptionsMonitor<TOptions> _monitor;

            public ChangeTrackerDisposable(OptionsMonitor<TOptions> monitor, Action<TOptions, string> listener)
            {
                _listener = listener;
                _monitor = monitor;
            }

            public void OnChange(TOptions options, string name) => _listener.Invoke(options, name);

            public void Dispose() => _monitor._onChange -= OnChange;
        }
    }
}
