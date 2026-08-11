// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.Options
{
    internal sealed class OptionsReloadValidationRegistration<TOptions>
        where TOptions : class
    {
        internal OptionsReloadValidationRegistration(
            string name,
            OptionsReloadValidationBehavior behavior,
            Action<string?, Exception>? onError)
        {
            Name = name;
            Behavior = behavior;
            OnError = onError;
        }

        internal string Name { get; }

        internal OptionsReloadValidationBehavior Behavior { get; }

        internal Action<string?, Exception>? OnError { get; }
    }

    internal sealed class OptionsReloadValidation<TOptions>
        where TOptions : class
    {
        private readonly Dictionary<string, OptionsReloadValidationRegistration<TOptions>> _registrations;

        public OptionsReloadValidation(IEnumerable<OptionsReloadValidationRegistration<TOptions>> registrations)
        {
            _registrations = new Dictionary<string, OptionsReloadValidationRegistration<TOptions>>(StringComparer.Ordinal);

            foreach (OptionsReloadValidationRegistration<TOptions> registration in registrations)
            {
                _registrations[registration.Name] = registration;
            }
        }

        internal IEnumerable<OptionsReloadValidationRegistration<TOptions>> Registrations => _registrations.Values;

        internal bool TryGetRegistration(
            string name,
            [NotNullWhen(true)] out OptionsReloadValidationRegistration<TOptions>? registration) =>
            _registrations.TryGetValue(name, out registration);
    }

    internal sealed class OptionsReloadValidationMarker<TOptions> : IValidateOptions<TOptions>
        where TOptions : class
    {
        public OptionsReloadValidationMarker(OptionsReloadValidation<TOptions> reloadValidation)
        {
            ReloadValidation = reloadValidation;
        }

        internal OptionsReloadValidation<TOptions> ReloadValidation { get; }

        public ValidateOptionsResult Validate(string? name, TOptions options) => ValidateOptionsResult.Skip;
    }
}
