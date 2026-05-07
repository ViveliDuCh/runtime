// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SharedModels.EntityClasses;

namespace AsyncDesignerBasicSample;

/// <summary>
/// WinForms designer-based async validation with ErrorProvider.
/// Same 4 scenarios as AsyncBasicSample but using designer-generated controls.
/// </summary>
public partial class MainForm : Form
{
    private readonly User _user = new() { Name = "Bob", Email = "bob@gmail.com", Delay = 3000 };
    private readonly Event _event = new() { Title = "Launch Party", StartDate = new DateTime(2026, 12, 25), EndDate = new DateTime(2026, 12, 20), Delay = 3000 };
    private readonly Order _order = new() { ProductName = "Widget", Quantity = 10_000, UnitPrice = 10m, Delay = 3000 };
    private readonly Profile _profile = new() { Username = "admin", Bio = new string('x', 201), Delay = 3000 };

    public MainForm()
    {
        InitializeComponent();
    }

    private async void BtnValidateUser_Click(object? sender, EventArgs e)
    {
        _user.Name = txtUserName.Text;
        _user.Email = txtUserEmail.Text;
        if (int.TryParse(txtUserDelay.Text, out int d)) _user.Delay = d;
        var (valid, results) = await ValidationHelper.ValidateObjectAsync(_user);
        lblUserResult.Text = valid ? "✅ Valid!" : "❌ " + string.Join("; ", results.Select(r => r.ErrorMessage));
    }

    private async void BtnValidateEvent_Click(object? sender, EventArgs e)
    {
        _event.Title = txtEventTitle.Text;
        _event.StartDate = dtpEventStart.Value;
        _event.EndDate = dtpEventEnd.Value;
        if (int.TryParse(txtEventDelay.Text, out int d)) _event.Delay = d;
        var (valid, results) = await ValidationHelper.ValidateObjectAsync(_event);
        lblEventResult.Text = valid ? "✅ Valid!" : "❌ " + string.Join("; ", results.Select(r => r.ErrorMessage));
    }

    private async void BtnValidateOrder_Click(object? sender, EventArgs e)
    {
        _order.ProductName = txtOrderProduct.Text;
        if (int.TryParse(txtOrderQty.Text, out int q)) _order.Quantity = q;
        if (decimal.TryParse(txtOrderPrice.Text, out decimal p)) _order.UnitPrice = p;
        if (int.TryParse(txtOrderDelay.Text, out int d)) _order.Delay = d;
        var (valid, results) = await ValidationHelper.ValidateObjectAsync(_order);
        lblOrderResult.Text = valid ? "✅ Valid!" : "❌ " + string.Join("; ", results.Select(r => r.ErrorMessage));
    }

    private async void BtnValidateProfile_Click(object? sender, EventArgs e)
    {
        _profile.Username = txtProfileUsername.Text;
        _profile.Bio = txtProfileBio.Text;
        if (int.TryParse(txtProfileDelay.Text, out int d)) _profile.Delay = d;
        var (valid, results) = await ValidationHelper.ValidateObjectAsync(_profile);
        lblProfileResult.Text = valid ? "✅ Valid!" : "❌ " + string.Join("; ", results.Select(r => r.ErrorMessage));
    }
}
