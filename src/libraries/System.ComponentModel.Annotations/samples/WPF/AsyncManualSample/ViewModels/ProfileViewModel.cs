// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SharedModels.EntityClasses;

namespace AsyncManualSample.ViewModels;

public class ProfileViewModel : ValidatableViewModelBase
{
    private readonly Profile _profile = new()
    {
        Username = "admin",
        Bio = new string('x', 201),
        Delay = 3000
    };

    public string? Username
    {
        get => _profile.Username;
        set { _profile.Username = value; SetAndValidateAsync(ref value, value); }
    }

    public string? Bio
    {
        get => _profile.Bio;
        set { _profile.Bio = value; SetAndValidateAsync(ref value, value); }
    }

    public int? Delay
    {
        get => _profile.Delay;
        set { _profile.Delay = value; OnPropertyChanged(); }
    }

    protected override object GetValidationTarget() => _profile;
}
