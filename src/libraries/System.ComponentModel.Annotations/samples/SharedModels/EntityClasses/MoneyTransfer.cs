// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SharedModels.EntityClasses;

/// <summary>
/// Cross-property async validation using IAsyncValidatableObject.
/// Demonstrates the "Venmo problem" — validating transfer amount against account balance.
/// </summary>
public class MoneyTransfer : IAsyncValidatableObject
{
    [Required]
    public string? FromAccount { get; set; }

    [Required]
    public string? ToAccount { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Transfer amount must be positive.")]
    public decimal Amount { get; set; }

    public async IAsyncEnumerable<ValidationResult> ValidateAsync(
        ValidationContext validationContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Cross property check, no I/O needed
        if (FromAccount == ToAccount)
        {
            yield return new ValidationResult(
                "Cannot transfer to the same account.",
                new[] { nameof(FromAccount), nameof(ToAccount) });
        }

        // Simulate async balance check
        await Task.Delay(10, cancellationToken);
        decimal balance = 500.00m; // Simulated "database" balance

        if (Amount > balance)
        {
            yield return new ValidationResult(
                $"Insufficient funds. Balance: ${balance:F2}, Transfer: ${Amount:F2}.",
                new[] { nameof(Amount) });
        }
    }
}
