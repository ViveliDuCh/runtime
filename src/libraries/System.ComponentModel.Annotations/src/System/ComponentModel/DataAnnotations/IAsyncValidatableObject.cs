// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;

namespace System.ComponentModel.DataAnnotations
{
    /// <summary>
    ///     Provides a way for an object to be validated asynchronously.
    ///     Inherits from <see cref="IValidatableObject"/> and provides a default implementation
    ///     of <see cref="IValidatableObject.Validate"/> that throws <see cref="NotSupportedException"/>,
    ///     mirroring the <see cref="AsyncValidationAttribute"/> pattern where sync paths fail clearly
    ///     rather than silently skipping async validation.
    /// </summary>
    public interface IAsyncValidatableObject : IValidatableObject
    {
        /// <summary>
        ///     Default implementation of the sync <see cref="IValidatableObject.Validate"/> method.
        ///     Throws <see cref="NotSupportedException"/> to indicate that this object requires
        ///     asynchronous validation via <see cref="ValidateAsync"/>.
        /// </summary>
        IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext) =>
            throw new NotSupportedException(SR.IAsyncValidatableObject_RequiresAsync);

        /// <summary>
        ///     Determines whether the specified object is valid asynchronously, yielding
        ///     validation results as each check completes.
        /// </summary>
        IAsyncEnumerable<ValidationResult> ValidateAsync(
            ValidationContext validationContext,
            CancellationToken cancellationToken = default);
    }
}
