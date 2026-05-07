// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using SharedModels.EntityClasses;

namespace SharedModels.ValidationClasses;

/// <summary>
/// Async-only property attribute. Calling the sync Validator on a model
/// that uses this attribute will throw NotSupportedException, enforcing the async path.
/// </summary>
public class AsyncOnlyEmailDomainAttribute : AsyncValidationAttribute
{
    private readonly string _requiredDomain;

    /// <summary>Initializes a new instance requiring emails to end with the specified domain.</summary>
    public AsyncOnlyEmailDomainAttribute(string requiredDomain)
        : base($"Email must belong to the '{requiredDomain}' domain.")
    {
        _requiredDomain = requiredDomain;
    }

    /// <inheritdoc />
    protected override async ValueTask<ValidationResult?> IsValidAsync(
        object? value,
        ValidationContext validationContext,
        CancellationToken cancellationToken)
    {
        if (value is not string email)
        {
            return new ValidationResult("A valid email string is required.");
        }

        User user = (User)validationContext.ObjectInstance;
        int? delay = user.Delay;

        if (delay is null)
        {
            return new ValidationResult("Delay is not configured.");
        }

        // Simulate an async DNS / external lookup
        await Task.Delay((int)delay, cancellationToken);

        if (!email.EndsWith($"@{_requiredDomain}", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult($"'{email}' is not in the '{_requiredDomain}' domain.",
                new[] { validationContext.MemberName! });
        }

        return ValidationResult.Success;
    }

    // IsValid is intentionally NOT overridden.
    // The base AsyncValidationAttribute.IsValid throws NotSupportedException,
    // which is the desired behavior for async-only attributes.
}
