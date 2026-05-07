// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using SharedModels.EntityClasses;

namespace SharedModels.ValidationClasses;

/// <summary>
/// Reusable class-level attribute that validates cross-property constraints.
/// Applied to the class, not to individual properties.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class AsyncDateRangeValidAttribute : AsyncValidationAttribute
{
    private readonly string _startProperty;
    private readonly string _endProperty;

    /// <summary>Initializes a new instance that compares the two named date properties.</summary>
    public AsyncDateRangeValidAttribute(string startProperty, string endProperty)
    {
        _startProperty = startProperty;
        _endProperty = endProperty;
    }

    /// <inheritdoc />
    protected override async ValueTask<ValidationResult?> IsValidAsync(
        object? value,
        ValidationContext validationContext,
        CancellationToken cancellationToken)
    {
        Event ev = (Event)validationContext.ObjectInstance;
        int? delay = ev.Delay;

        if (delay is null)
        {
            return new ValidationResult("Delay is not configured.");
        }

        // Simulate an async check (e.g., checking a calendar service)
        await Task.Delay((int)delay, cancellationToken);

        Type type = validationContext.ObjectType;
        object instance = validationContext.ObjectInstance;

        DateTime? start = (DateTime?)type.GetProperty(_startProperty)?.GetValue(instance);
        DateTime? end = (DateTime?)type.GetProperty(_endProperty)?.GetValue(instance);

        if (start.HasValue && end.HasValue && start.Value >= end.Value)
        {
            return new ValidationResult($"'{_startProperty}' must be before '{_endProperty}'.",
                new[] { _startProperty, _endProperty });
        }

        return ValidationResult.Success;
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(
        object? value, ValidationContext validationContext)
    {
        Event ev = (Event)validationContext.ObjectInstance;
        int? delay = ev.Delay;

        if (delay is null)
        {
            return new ValidationResult("Delay is not configured.");
        }

        Thread.Sleep((int)delay);

        Type type = validationContext.ObjectType;
        object instance = validationContext.ObjectInstance;

        DateTime? start = (DateTime?)type.GetProperty(_startProperty)?.GetValue(instance);
        DateTime? end = (DateTime?)type.GetProperty(_endProperty)?.GetValue(instance);

        if (start.HasValue && end.HasValue && start.Value >= end.Value)
        {
            return new ValidationResult(
                $"'{_startProperty}' must be before '{_endProperty}'.",
                new[] { _startProperty, _endProperty });
        }

        return ValidationResult.Success;
    }
}
