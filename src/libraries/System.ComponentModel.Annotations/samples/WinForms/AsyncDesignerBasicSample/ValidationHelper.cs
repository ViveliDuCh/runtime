// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;

namespace AsyncDesignerBasicSample;

/// <summary>
/// Async helper that bridges DataAnnotations async validation to WinForms ErrorProvider.
/// </summary>
public static class ValidationHelper
{
    public static async Task<bool> ValidatePropertyAsync(
        ErrorProvider errorProvider,
        Control control,
        object model,
        string propertyName,
        object? value)
    {
        var context = new ValidationContext(model) { MemberName = propertyName };
        var results = new List<ValidationResult>();

        bool isValid = await Validator.TryValidatePropertyAsync(value, context, results);

        errorProvider.SetError(control, isValid
            ? string.Empty
            : string.Join("; ", results.Select(r => r.ErrorMessage)));

        return isValid;
    }

    public static async Task<(bool IsValid, List<ValidationResult> Results)> ValidateObjectAsync(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        bool isValid = await Validator.TryValidateObjectAsync(model, context, results, validateAllProperties: true);
        return (isValid, results);
    }
}
