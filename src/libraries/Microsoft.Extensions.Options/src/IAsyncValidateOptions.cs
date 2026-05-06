// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Options
{
    /// <summary>
    /// Validates options asynchronously. Used to support validation attributes
    /// that require I/O operations such as database lookups or API calls.
    /// </summary>
    /// <typeparam name="TOptions">The options type to validate.</typeparam>
    public interface IAsyncValidateOptions<in TOptions> where TOptions : class
    {
        /// <summary>
        /// Validates a specified named options instance (or all if <paramref name="name"/> is <see langword="null"/>).
        /// </summary>
        /// <param name="name">The name of the options instance being validated.</param>
        /// <param name="options">The options instance.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the operation.</param>
        /// <returns>The <see cref="ValidateOptionsResult"/> result.</returns>
        ValueTask<ValidateOptionsResult> ValidateAsync(
            string? name,
            TOptions options,
            CancellationToken cancellationToken = default);
    }
}
