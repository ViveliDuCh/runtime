// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SharedModels.EntityClasses;

namespace AsyncManualSample.ViewModels;

public class UserViewModel : ValidatableViewModelBase
{
    private readonly User _user = new() { Name = "Bob", Email = "bob@gmail.com", Delay = 3000 };

    public string? Name
    {
        get => _user.Name;
        set { _user.Name = value; SetAndValidateAsync(ref value, value); }
    }

    public string? Email
    {
        get => _user.Email;
        set { _user.Email = value; SetAndValidateAsync(ref value, value); }
    }

    public int? Delay
    {
        get => _user.Delay;
        set { _user.Delay = value; OnPropertyChanged(); }
    }

    protected override object GetValidationTarget() => _user;
}
