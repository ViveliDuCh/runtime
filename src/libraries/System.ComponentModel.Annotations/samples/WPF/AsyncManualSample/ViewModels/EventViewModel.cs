// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SharedModels.EntityClasses;

namespace AsyncManualSample.ViewModels;

public class EventViewModel : ValidatableViewModelBase
{
    private readonly Event _event = new()
    {
        Title = "Launch Party",
        StartDate = new DateTime(2026, 12, 25),
        EndDate = new DateTime(2026, 12, 20),
        Delay = 3000
    };

    public string? Title
    {
        get => _event.Title;
        set { _event.Title = value; SetAndValidateAsync(ref value, value); }
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

    protected override object GetValidationTarget() => _event;
}
