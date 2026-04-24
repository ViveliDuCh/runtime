// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace System.ComponentModel.DataAnnotations
{
    /// <summary>
    ///     Provides a way for an object to be validated asynchronously.
    /// </summary>
    public interface IAsyncValidatableObject
    {
        /// <summary>
        ///     Determines whether the specified object is valid asynchronously.
        /// </summary>
        ValueTask<IEnumerable<ValidationResult>> ValidateAsync(ValidationContext validationContext, CancellationToken cancellationToken = default);
    }
}
