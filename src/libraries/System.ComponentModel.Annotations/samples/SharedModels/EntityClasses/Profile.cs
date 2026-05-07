// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace SharedModels.EntityClasses;

/// <summary>
/// Property-scoped self-validating entity via IAsyncValidatableObject.
/// No reusable attribute classes. Each result targets a specific member.
/// </summary>
public class Profile : IAsyncValidatableObject
{
    /// <summary>Gets or sets the username.</summary>
    [Required]
    public string? Username { get; set; }

    /// <summary>Gets or sets the user biography.</summary>
    [Required]
    public string? Bio { get; set; }

    /// <summary>Gets or sets the simulated I/O delay in milliseconds.</summary>
    [Required]
    public int? Delay { get; set; }

    /// <inheritdoc />
    public async ValueTask<IEnumerable<ValidationResult>> ValidateAsync(
        ValidationContext validationContext,
        CancellationToken cancellationToken)
    {
        var results = new List<ValidationResult>();

        if (Delay is null)
        {
            results.Add(new ValidationResult("Delay is not configured.", new[] { nameof(Delay) }));
            return results;
        }

        // Simulate async uniqueness check for Username
        await Task.Delay((int)Delay, cancellationToken);

        if (string.Equals(Username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            results.Add(new ValidationResult("The username 'admin' is reserved.", new[] { nameof(Username) }));
        }

        // Simulate async content-moderation check for Bio
        await Task.Delay((int)Delay, cancellationToken);

        if (Bio is not null && Bio.Length > 200)
        {
            results.Add(new ValidationResult(
                "Bio exceeds the 200-character limit after moderation review.",
                new[] { nameof(Bio) }));
        }

        return results;
    }
}
