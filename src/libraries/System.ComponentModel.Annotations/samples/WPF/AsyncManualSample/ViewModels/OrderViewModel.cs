// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SharedModels.EntityClasses;

namespace AsyncManualSample.ViewModels;

public class OrderViewModel : ValidatableViewModelBase
{
    private readonly Order _order = new()
    {
        ProductName = "Widget",
        Quantity = 10_000,
        UnitPrice = 10m,
        Delay = 3000
    };

    public string? ProductName
    {
        get => _order.ProductName;
        set { _order.ProductName = value; SetAndValidateAsync(ref value, value); }
    }

    public int Quantity
    {
        get => _order.Quantity;
        set { _order.Quantity = value; OnPropertyChanged(); }
    }

    public decimal UnitPrice
    {
        get => _order.UnitPrice;
        set { _order.UnitPrice = value; OnPropertyChanged(); }
    }

    public int? Delay
    {
        get => _order.Delay;
        set { _order.Delay = value; OnPropertyChanged(); }
    }

    protected override object GetValidationTarget() => _order;
}
