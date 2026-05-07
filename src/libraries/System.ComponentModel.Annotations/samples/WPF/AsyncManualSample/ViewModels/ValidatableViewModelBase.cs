// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace AsyncManualSample.ViewModels;

/// <summary>
/// Base ViewModel that manually bridges DataAnnotations async validation to WPF's INotifyDataErrorInfo.
/// Uses Validator.TryValidatePropertyAsync and Validator.TryValidateObjectAsync.
/// </summary>
public abstract class ValidatableViewModelBase : INotifyPropertyChanged, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public bool HasErrors => _errors.Any(e => e.Value.Count > 0);

    public IEnumerable GetErrors(string? propertyName)
    {
        if (propertyName is not null && _errors.TryGetValue(propertyName, out var errors))
            return errors;
        return Enumerable.Empty<string>();
    }

    protected void SetAndValidateAsync<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
        _ = ValidatePropertyAsync(value, propertyName!);
    }

    protected async Task ValidatePropertyAsync(object? value, [CallerMemberName] string propertyName = "")
    {
        ClearErrors(propertyName);

        var context = new ValidationContext(GetValidationTarget()) { MemberName = propertyName };
        var results = new List<ValidationResult>();

        bool isValid = await Validator.TryValidatePropertyAsync(value, context, results);
        if (!isValid)
        {
            foreach (var result in results)
            {
                if (result.ErrorMessage is not null)
                    AddError(propertyName, result.ErrorMessage);
            }
        }
    }

    public async Task<bool> ValidateAllAsync()
    {
        var context = new ValidationContext(GetValidationTarget());
        var results = new List<ValidationResult>();
        bool isValid = await Validator.TryValidateObjectAsync(GetValidationTarget(), context, results, validateAllProperties: true);

        foreach (var key in _errors.Keys.ToList())
            ClearErrors(key);

        if (!isValid)
        {
            foreach (var result in results)
            {
                foreach (var memberName in result.MemberNames)
                {
                    if (result.ErrorMessage is not null)
                        AddError(memberName, result.ErrorMessage);
                }
            }
        }

        return isValid;
    }

    protected abstract object GetValidationTarget();

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void AddError(string property, string error)
    {
        if (!_errors.ContainsKey(property))
            _errors[property] = new();
        _errors[property].Add(error);
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property));
    }

    private void ClearErrors(string property)
    {
        if (_errors.Remove(property))
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(property));
    }
}
