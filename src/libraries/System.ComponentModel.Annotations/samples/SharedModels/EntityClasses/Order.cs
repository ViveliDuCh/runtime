// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace SharedModels.EntityClasses;

/// <summary>
/// Non-reusable entity validation via IAsyncValidatableObject:
/// Self-validating object with cross-property logic.
/// </summary>
public class Order : IAsyncValidatableObject
{
    /// <summary>Gets or sets the product name.</summary>
    [Required]
    public string? ProductName { get; set; }

    /// <summary>Gets or sets the quantity ordered.</summary>
    [Required]
    [Range(1, 10_000)]
    public int Quantity { get; set; }

    /// <summary>Gets or sets the unit price.</summary>
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }

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
            results.Add(new ValidationResult("Delay is not configured."));
            return results;
        }

        await Task.Delay((int)Delay, cancellationToken);

        decimal totalCost = Quantity * UnitPrice;
        if (totalCost > 50_000m)
        {
            results.Add(new ValidationResult(
                $"Total cost ({totalCost:C}) exceeds the $50,000 limit.",
                new[] { nameof(Quantity), nameof(UnitPrice) }));
        }

        return results;
    }
}
