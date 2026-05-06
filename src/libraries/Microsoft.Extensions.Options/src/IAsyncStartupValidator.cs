// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Options
{
    /// <summary>
    /// Used by hosts to validate options asynchronously during startup.
    /// </summary>
    /// <remarks>
    /// Options are enabled to be validated asynchronously during startup by calling
    /// <see cref="DependencyInjection.OptionsBuilderExtensions.ValidateOnStartAsync{TOptions}(OptionsBuilder{TOptions})"/>.
    /// </remarks>
    public interface IAsyncStartupValidator
    {
        /// <summary>
        /// Calls the <see cref="IAsyncValidateOptions{TOptions}"/> validators.
        /// </summary>
        /// <param name="cancellationToken">A token to observe while waiting for the operation.</param>
        /// <exception cref="OptionsValidationException">
        /// One or more <see cref="IAsyncValidateOptions{TOptions}"/> return failed
        /// <see cref="ValidateOptionsResult"/> when validating.
        /// </exception>
        Task ValidateAsync(CancellationToken cancellationToken = default);
    }
}
