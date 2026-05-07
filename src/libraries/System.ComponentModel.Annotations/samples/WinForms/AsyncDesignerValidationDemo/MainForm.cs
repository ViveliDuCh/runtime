// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using SharedModels.EntityClasses;
using SharedModels.ServiceClasses;

namespace AsyncDesignerValidationDemo;

/// <summary>
/// WinForms designer-based async validation demo with DI,
/// two-phase validation, error handling, and IAsyncValidatableObject.
/// </summary>
public partial class MainForm : Form
{
    private readonly SimpleServiceProvider _serviceProvider;

    public MainForm()
    {
        InitializeComponent();
        _serviceProvider = new SimpleServiceProvider()
            .Register(new UserService());
    }

    private async void BtnValidateReg_Click(object? sender, EventArgs e)
    {
        var registration = new UserRegistration
        {
            Username = txtRegUsername.Text,
            Email = txtRegEmail.Text,
            Password = txtRegPassword.Text
        };

        var context = new ValidationContext(registration, _serviceProvider, null);
        var results = new List<ValidationResult>();
        bool isValid = await Validator.TryValidateObjectAsync(registration, context, results, true);

        lblRegResult.Text = isValid
            ? "✅ Valid!"
            : "❌ " + string.Join("\n", results.Select(r => r.ErrorMessage));
    }

    private async void BtnValidateTransfer_Click(object? sender, EventArgs e)
    {
        var transfer = new MoneyTransfer
        {
            FromAccount = txtTransferFrom.Text,
            ToAccount = txtTransferTo.Text,
            Amount = decimal.TryParse(txtTransferAmount.Text, out decimal a) ? a : 0m
        };

        var context = new ValidationContext(transfer, _serviceProvider, null);
        var results = new List<ValidationResult>();
        bool isValid = await Validator.TryValidateObjectAsync(transfer, context, results, true);

        lblTransferResult.Text = isValid
            ? "✅ Valid!"
            : "❌ " + string.Join("\n", results.Select(r => r.ErrorMessage));
    }

    private async void BtnValidateError_Click(object? sender, EventArgs e)
    {
        var registration = new UserRegistration
        {
            Username = txtErrorUsername.Text,
            Email = txtErrorEmail.Text,
            Password = txtErrorPassword.Text
        };

        var context = new ValidationContext(registration, _serviceProvider, null);
        var results = new List<ValidationResult>();
        try
        {
            bool isValid = await Validator.TryValidateObjectAsync(registration, context, results, true);
            lblErrorResult.Text = isValid
                ? "✅ Valid!"
                : "❌ " + string.Join("\n", results.Select(r => r.ErrorMessage));
        }
        catch (InvalidOperationException ex)
        {
            lblErrorResult.Text = $"⚠️ Infrastructure error caught: {ex.Message}";
        }
    }

    private async void BtnValidateTwoPhase_Click(object? sender, EventArgs e)
    {
        var registration = new UserRegistration
        {
            Username = txtTwoPhaseUsername.Text,
            Email = txtTwoPhaseEmail.Text,
            Password = txtTwoPhasePassword.Text
        };

        var context = new ValidationContext(registration, _serviceProvider, null);
        var results = new List<ValidationResult>();
        bool isValid = await Validator.TryValidateObjectAsync(registration, context, results, true);

        string message = isValid
            ? "✅ Valid!"
            : "❌ " + string.Join("\n", results.Select(r => r.ErrorMessage));
        message += "\n(Note: async UniqueEmail check was skipped because sync EmailAddress failed first)";
        lblTwoPhaseResult.Text = message;
    }
}
