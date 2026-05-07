// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using SharedModels.EntityClasses;

namespace AsyncBasicSample;

/// <summary>
/// WinForms async DataAnnotations validation with ErrorProvider.
/// Demonstrates the same 4 scenarios as the console samples using async validation.
/// </summary>
public partial class MainForm : Form
{
    private readonly ErrorProvider _errorProvider = new();
    private readonly User _user = new() { Name = "Bob", Email = "bob@gmail.com", Delay = 3000 };
    private readonly Event _event = new() { Title = "Launch Party", StartDate = new DateTime(2026, 12, 25), EndDate = new DateTime(2026, 12, 20), Delay = 3000 };
    private readonly Order _order = new() { ProductName = "Widget", Quantity = 10_000, UnitPrice = 10m, Delay = 3000 };
    private readonly Profile _profile = new() { Username = "admin", Bio = new string('x', 201), Delay = 3000 };

    private TabControl _tabControl = null!;

    public MainForm()
    {
        Text = "WinForms AsyncBasicSample — Async ErrorProvider + Validator";
        Size = new System.Drawing.Size(650, 500);
        _errorProvider.ContainerControl = this;

        _tabControl = new TabControl { Dock = DockStyle.Fill };
        Controls.Add(_tabControl);

        _tabControl.TabPages.Add(CreateUserTab());
        _tabControl.TabPages.Add(CreateEventTab());
        _tabControl.TabPages.Add(CreateOrderTab());
        _tabControl.TabPages.Add(CreateProfileTab());
    }

    private TabPage CreateUserTab()
    {
        var tab = new TabPage("User");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };

        panel.Controls.Add(new Label { Text = "Scenarios 1 & 2: IsValidName + AsyncOnlyEmailDomain (async, non-blocking)", AutoSize = true });

        var txtName = CreateField(panel, "Name:", _user.Name ?? "");
        txtName.Validating += async (s, e) =>
        {
            _user.Name = txtName.Text;
            await ValidationHelper.ValidatePropertyAsync(_errorProvider, txtName, _user, nameof(User.Name), txtName.Text);
        };

        var txtEmail = CreateField(panel, "Email:", _user.Email ?? "");
        txtEmail.Validating += async (s, e) =>
        {
            _user.Email = txtEmail.Text;
            await ValidationHelper.ValidatePropertyAsync(_errorProvider, txtEmail, _user, nameof(User.Email), txtEmail.Text);
        };

        var txtDelay = CreateField(panel, "Delay (ms):", _user.Delay?.ToString() ?? "3000");

        var btnValidate = new Button { Text = "Validate All (Async)", AutoSize = true };
        var lblResult = new Label { AutoSize = true, ForeColor = System.Drawing.Color.DarkRed };
        btnValidate.Click += async (s, e) =>
        {
            _user.Name = txtName.Text;
            _user.Email = txtEmail.Text;
            if (int.TryParse(txtDelay.Text, out int d)) _user.Delay = d;
            var (valid, results) = await ValidationHelper.ValidateObjectAsync(_user);
            lblResult.Text = valid ? "✅ Valid!" : "❌ " + string.Join("; ", results.Select(r => r.ErrorMessage));
        };
        panel.Controls.Add(btnValidate);
        panel.Controls.Add(lblResult);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateEventTab()
    {
        var tab = new TabPage("Event");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };

        panel.Controls.Add(new Label { Text = "Scenario 3: AsyncDateRangeValid (async, non-blocking)", AutoSize = true });

        var txtTitle = CreateField(panel, "Title:", _event.Title ?? "");
        var txtStart = CreateField(panel, "Start Date:", _event.StartDate?.ToString("yyyy-MM-dd") ?? "");
        var txtEnd = CreateField(panel, "End Date:", _event.EndDate?.ToString("yyyy-MM-dd") ?? "");
        var txtDelay = CreateField(panel, "Delay (ms):", _event.Delay?.ToString() ?? "3000");

        var btnValidate = new Button { Text = "Validate All (Async)", AutoSize = true };
        var lblResult = new Label { AutoSize = true, ForeColor = System.Drawing.Color.DarkRed };
        btnValidate.Click += async (s, e) =>
        {
            _event.Title = txtTitle.Text;
            if (DateTime.TryParse(txtStart.Text, out var sd)) _event.StartDate = sd;
            if (DateTime.TryParse(txtEnd.Text, out var ed)) _event.EndDate = ed;
            if (int.TryParse(txtDelay.Text, out int d)) _event.Delay = d;
            var (valid, results) = await ValidationHelper.ValidateObjectAsync(_event);
            lblResult.Text = valid ? "✅ Valid!" : "❌ " + string.Join("; ", results.Select(r => r.ErrorMessage));
        };
        panel.Controls.Add(btnValidate);
        panel.Controls.Add(lblResult);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateOrderTab()
    {
        var tab = new TabPage("Order");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };

        panel.Controls.Add(new Label { Text = "Scenario 4: IAsyncValidatableObject cross-property (async, non-blocking)", AutoSize = true });

        var txtProduct = CreateField(panel, "Product:", _order.ProductName ?? "");
        var txtQty = CreateField(panel, "Quantity:", _order.Quantity.ToString());
        var txtPrice = CreateField(panel, "Unit Price:", _order.UnitPrice.ToString());
        var txtDelay = CreateField(panel, "Delay (ms):", _order.Delay?.ToString() ?? "3000");

        var btnValidate = new Button { Text = "Validate All (Async)", AutoSize = true };
        var lblResult = new Label { AutoSize = true, ForeColor = System.Drawing.Color.DarkRed };
        btnValidate.Click += async (s, e) =>
        {
            _order.ProductName = txtProduct.Text;
            if (int.TryParse(txtQty.Text, out int q)) _order.Quantity = q;
            if (decimal.TryParse(txtPrice.Text, out decimal p)) _order.UnitPrice = p;
            if (int.TryParse(txtDelay.Text, out int d)) _order.Delay = d;
            var (valid, results) = await ValidationHelper.ValidateObjectAsync(_order);
            lblResult.Text = valid ? "✅ Valid!" : "❌ " + string.Join("; ", results.Select(r => r.ErrorMessage));
        };
        panel.Controls.Add(btnValidate);
        panel.Controls.Add(lblResult);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateProfileTab()
    {
        var tab = new TabPage("Profile");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };

        panel.Controls.Add(new Label { Text = "Scenario 5: IAsyncValidatableObject property-scoped (async, non-blocking)", AutoSize = true });

        var txtUsername = CreateField(panel, "Username:", _profile.Username ?? "");
        var txtBio = CreateField(panel, "Bio:", _profile.Bio ?? "");
        var txtDelay = CreateField(panel, "Delay (ms):", _profile.Delay?.ToString() ?? "3000");

        var btnValidate = new Button { Text = "Validate All (Async)", AutoSize = true };
        var lblResult = new Label { AutoSize = true, ForeColor = System.Drawing.Color.DarkRed };
        btnValidate.Click += async (s, e) =>
        {
            _profile.Username = txtUsername.Text;
            _profile.Bio = txtBio.Text;
            if (int.TryParse(txtDelay.Text, out int d)) _profile.Delay = d;
            var (valid, results) = await ValidationHelper.ValidateObjectAsync(_profile);
            lblResult.Text = valid ? "✅ Valid!" : "❌ " + string.Join("; ", results.Select(r => r.ErrorMessage));
        };
        panel.Controls.Add(btnValidate);
        panel.Controls.Add(lblResult);

        tab.Controls.Add(panel);
        return tab;
    }

    private static TextBox CreateField(FlowLayoutPanel panel, string label, string defaultValue)
    {
        panel.Controls.Add(new Label { Text = label, AutoSize = true });
        var txt = new TextBox { Text = defaultValue, Width = 400 };
        panel.Controls.Add(txt);
        return txt;
    }
}
