// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using SharedModels.EntityClasses;

namespace AsyncToolkitSample.ViewModels;

public partial class UserViewModel : ObservableValidator
{
    private readonly User _user = new() { Name = "Bob", Email = "bob@gmail.com", Delay = 3000 };

    [Required]
    public string? Name
    {
        get => _user.Name;
        set { _user.Name = value; SetProperty(_user.Name, value, _user, (u, v) => u.Name = v, true); }
    }

    [Required]
    [EmailAddress]
    public string? Email
    {
        get => _user.Email;
        set { _user.Email = value; SetProperty(_user.Email, value, _user, (u, v) => u.Email = v, true); }
    }

    public int? Delay
    {
        get => _user.Delay;
        set { _user.Delay = value; OnPropertyChanged(); }
    }

    public async Task<bool> ValidateAllAsync()
    {
        ValidateAllProperties();

        var context = new ValidationContext(_user);
        var results = new List<ValidationResult>();
        bool isValid = await Validator.TryValidateObjectAsync(_user, context, results, validateAllProperties: true);

        return isValid && !HasErrors;
    }
}
