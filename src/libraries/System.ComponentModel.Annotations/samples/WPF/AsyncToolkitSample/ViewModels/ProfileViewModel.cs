// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using SharedModels.EntityClasses;

namespace AsyncToolkitSample.ViewModels;

public partial class ProfileViewModel : ObservableValidator
{
    private readonly Profile _profile = new()
    {
        Username = "admin",
        Bio = new string('x', 201),
        Delay = 3000
    };

    [Required]
    public string? Username
    {
        get => _profile.Username;
        set { _profile.Username = value; SetProperty(_profile.Username, value, _profile, (p, v) => p.Username = v, true); }
    }

    [Required]
    public string? Bio
    {
        get => _profile.Bio;
        set { _profile.Bio = value; SetProperty(_profile.Bio, value, _profile, (p, v) => p.Bio = v, true); }
    }

    public int? Delay
    {
        get => _profile.Delay;
        set { _profile.Delay = value; OnPropertyChanged(); }
    }

    public async Task<bool> ValidateAllAsync()
    {
        ValidateAllProperties();

        var context = new ValidationContext(_profile);
        var results = new List<ValidationResult>();
        bool isValid = await Validator.TryValidateObjectAsync(_profile, context, results, validateAllProperties: true);

        return isValid && !HasErrors;
    }
}
