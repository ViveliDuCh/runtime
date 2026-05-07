// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using SharedModels.EntityClasses;

namespace AsyncToolkitSample.ViewModels;

public partial class EventViewModel : ObservableValidator
{
    private readonly Event _event = new()
    {
        Title = "Launch Party",
        StartDate = new DateTime(2026, 12, 25),
        EndDate = new DateTime(2026, 12, 20),
        Delay = 3000
    };

    [Required]
    public string? Title
    {
        get => _event.Title;
        set { _event.Title = value; SetProperty(_event.Title, value, _event, (e, v) => e.Title = v, true); }
    }

    public DateTime? StartDate
    {
        get => _event.StartDate;
        set { _event.StartDate = value; OnPropertyChanged(); }
    }

    public DateTime? EndDate
    {
        get => _event.EndDate;
        set { _event.EndDate = value; OnPropertyChanged(); }
    }

    public int? Delay
    {
        get => _event.Delay;
        set { _event.Delay = value; OnPropertyChanged(); }
    }

    public async Task<bool> ValidateAllAsync()
    {
        ValidateAllProperties();

        var context = new ValidationContext(_event);
        var results = new List<ValidationResult>();
        bool isValid = await Validator.TryValidateObjectAsync(_event, context, results, validateAllProperties: true);

        return isValid && !HasErrors;
    }
}
