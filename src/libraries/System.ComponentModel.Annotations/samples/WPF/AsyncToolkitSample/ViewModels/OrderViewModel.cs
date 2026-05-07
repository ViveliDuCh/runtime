// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using SharedModels.EntityClasses;

namespace AsyncToolkitSample.ViewModels;

public partial class OrderViewModel : ObservableValidator
{
    private readonly Order _order = new()
    {
        ProductName = "Widget",
        Quantity = 10_000,
        UnitPrice = 10m,
        Delay = 3000
    };

    [Required]
    public string? ProductName
    {
        get => _order.ProductName;
        set { _order.ProductName = value; SetProperty(_order.ProductName, value, _order, (o, v) => o.ProductName = v, true); }
    }

    [Range(1, 10_000)]
    public int Quantity
    {
        get => _order.Quantity;
        set { _order.Quantity = value; OnPropertyChanged(); }
    }

    [Range(0.01, double.MaxValue)]
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

    public async Task<bool> ValidateAllAsync()
    {
        ValidateAllProperties();

        var context = new ValidationContext(_order);
        var results = new List<ValidationResult>();
        bool isValid = await Validator.TryValidateObjectAsync(_order, context, results, validateAllProperties: true);

        return isValid && !HasErrors;
    }
}
