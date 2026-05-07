// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using SharedModels.ServiceClasses;

namespace SharedModels.ValidationClasses;

/// <summary>
/// Async validation attribute that checks email uniqueness via UserService DI.
/// Resolves UserService from ValidationContext.GetService(), matching the aspnetcore pattern.
/// </summary>
public class UniqueEmailAttribute : AsyncValidationAttribute
{
    protected override async ValueTask<ValidationResult?> IsValidAsync(
        object? value,
        ValidationContext validationContext,
        CancellationToken cancellationToken)
    {
        // Arrives here if all sync attributes ([Required], [EmailAddress]) pass (Validation phase 2)
        if (value is not string email || string.IsNullOrEmpty(email))
        {
            return ValidationResult.Success;
        }

        var userService = (UserService?)validationContext.GetService(typeof(UserService));

        if (userService is null)
        {
            return new ValidationResult("UserService is not available.");
        }

        bool taken = await userService.IsEmailTakenAsync(email, cancellationToken);

        return taken
            ? new ValidationResult($"The email '{email}' is already registered.")
            : ValidationResult.Success;
    }
}
