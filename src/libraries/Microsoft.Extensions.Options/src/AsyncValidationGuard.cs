// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET11_0_OR_GREATER

using System;

namespace Microsoft.Extensions.Options
{
    /// <summary>
    /// Sync pipeline sentinel that prevents silent failure when async validators
    /// are registered but <c>ValidateOnStartAsync</c> was not called.
    /// Registered automatically by <c>ValidateDataAnnotationsAsync</c> and <c>ValidateAsync</c>.
    /// </summary>
    internal sealed class AsyncValidationGuard<TOptions> : IValidateOptions<TOptions>
        where TOptions : class
    {
        private readonly AsyncValidationState _state;

        public AsyncValidationGuard(AsyncValidationState state)
        {
            _state = state;
        }

        public ValidateOptionsResult Validate(string? name, TOptions options)
        {
            if (_state.StartupValidatorRegistered)
            {
                return ValidateOptionsResult.Skip;
            }

            throw new InvalidOperationException(
                $"Async validation attributes are registered for '{typeof(TOptions).Name}' " +
                $"(via ValidateDataAnnotationsAsync() or ValidateAsync()), but " +
                $"ValidateOnStartAsync() was not called. Async validators only run " +
                $"during Host.StartAsync(). Add .ValidateOnStartAsync() to your " +
                $"options configuration, or use ValidateDataAnnotations() for " +
                $"sync-only validation.");
        }
    }

    /// <summary>
    /// Tracks whether <c>ValidateOnStartAsync</c> was called during service registration.
    /// This type is not intended for direct use by application code.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public sealed class AsyncValidationState
    {
        /// <summary>
        /// Gets or sets a value indicating whether a startup validator has been registered.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public bool StartupValidatorRegistered { get; set; }
    }
}

#endif
