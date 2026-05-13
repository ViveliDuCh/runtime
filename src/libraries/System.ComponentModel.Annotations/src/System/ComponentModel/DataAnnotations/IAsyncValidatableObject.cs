// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;

namespace System.ComponentModel.DataAnnotations
{
    /// <summary>
    ///     Provides a way for an object to be validated asynchronously, streaming results as they become available.
    /// </summary>
    public interface IAsyncValidatableObject
    {
        /// <summary>
        ///     Determines whether the specified object is valid asynchronously, yielding validation results as they are produced.
        /// </summary>
        IAsyncEnumerable<ValidationResult> ValidateAsync(ValidationContext validationContext, CancellationToken cancellationToken = default);
    }
}
