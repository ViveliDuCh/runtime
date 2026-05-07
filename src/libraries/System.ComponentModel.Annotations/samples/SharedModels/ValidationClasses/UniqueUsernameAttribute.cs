// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using SharedModels.ServiceClasses;

namespace SharedModels.ValidationClasses;

/// <summary>
/// Async validation attribute that checks username uniqueness via UserService DI.
/// </summary>
public class UniqueUsernameAttribute : AsyncValidationAttribute
{
    protected override async ValueTask<ValidationResult?> IsValidAsync(
        object? value,
        ValidationContext validationContext,
        CancellationToken cancellationToken)
    {
        if (value is not string username || string.IsNullOrEmpty(username))
        {
            return ValidationResult.Success;
        }

        var userService = (UserService?)validationContext.GetService(typeof(UserService));
        if (userService is null)
        {
            return new ValidationResult("UserService is not available.");
        }

        bool taken = await userService.IsUsernameTakenAsync(username, cancellationToken);

        return taken
            ? new ValidationResult($"The username '{username}' is already taken.")
            : ValidationResult.Success;
    }
}
