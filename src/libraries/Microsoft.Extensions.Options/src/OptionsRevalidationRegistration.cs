// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Options
{
    /// <summary>
    /// Wires <see cref="IOptionsMonitor{TOptions}.OnChangeAsync"/> to
    /// <see cref="IAsyncValidateOptions{TOptions}"/> validators so that
    /// configuration changes trigger async re-validation.
    /// </summary>
    internal sealed class OptionsRevalidationRegistration<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TOptions> : IDisposable
        where TOptions : class
    {
        private readonly IAsyncValidateOptions<TOptions>[] _validators;
        private readonly string _name;
        private readonly Action<OptionsValidationException>? _onFailed;
        private readonly IDisposable? _subscription;

        public OptionsRevalidationRegistration(
            IOptionsMonitor<TOptions> monitor,
            IEnumerable<IAsyncValidateOptions<TOptions>> validators,
            string name,
            Action<OptionsValidationException>? onFailed)
        {
            _validators = validators as IAsyncValidateOptions<TOptions>[]
                ?? new List<IAsyncValidateOptions<TOptions>>(validators).ToArray();
            _name = name;
            _onFailed = onFailed;
            _subscription = monitor.OnChangeAsync(RevalidateAsync);
        }

        private async Task RevalidateAsync(
            TOptions options, string? name, CancellationToken ct)
        {
            if (name is not null && name != _name)
            {
                return;
            }

            var tasks = new List<Task<ValidateOptionsResult>>(_validators.Length);
            foreach (IAsyncValidateOptions<TOptions> validator in _validators)
            {
                tasks.Add(validator.ValidateAsync(_name, options, ct).AsTask());
            }

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
                var ex = new OptionsValidationException(_name, typeof(TOptions), failures);
                if (_onFailed is not null)
                {
                    _onFailed(ex);
                }
                else
                {
                    throw ex;
                }
            }
        }

        public void Dispose() => _subscription?.Dispose();
    }
}
#endif
