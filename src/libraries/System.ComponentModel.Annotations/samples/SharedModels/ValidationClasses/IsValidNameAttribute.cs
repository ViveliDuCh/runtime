// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using SharedModels.EntityClasses;

namespace SharedModels.ValidationClasses;

/// <summary>
/// Reusable async-only property attribute.
/// Simulates a costly I/O operation (e.g., external service call) by introducing an artificial delay.
/// </summary>
public class IsValidNameAttribute : AsyncValidationAttribute
{
    /// <inheritdoc />
    protected override async ValueTask<ValidationResult?> IsValidAsync(
        object? value,
        ValidationContext validationContext,
        CancellationToken cancellationToken)
    {
        User user = (User)validationContext.ObjectInstance;
        int? delay = user.Delay;

        if (delay is null)
        {
            return new ValidationResult("Delay is not configured.");
        }

        await Task.Delay((int)delay, cancellationToken);

        return ValidationResult.Success;
    }

    // IsValid is intentionally NOT overridden.
    // The base AsyncValidationAttribute.IsValid throws NotSupportedException.
}
