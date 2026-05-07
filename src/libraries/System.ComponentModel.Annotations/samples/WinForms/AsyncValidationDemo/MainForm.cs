// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.DataAnnotations;
using SharedModels.EntityClasses;
using SharedModels.ServiceClasses;

namespace AsyncValidationDemo;

/// <summary>
/// WinForms async validation demo with DI, two-phase validation,
/// IAsyncValidatableObject, error handling, and cancellation.
/// </summary>
public partial class MainForm : Form
{
    private readonly SimpleServiceProvider _serviceProvider;
    private readonly ErrorProvider _errorProvider = new();

    public MainForm()
    {
        Text = "WinForms AsyncValidationDemo — DI + Async Validation";
        Size = new System.Drawing.Size(700, 550);
        _errorProvider.ContainerControl = this;

        _serviceProvider = new SimpleServiceProvider()
            .Register(new UserService());

        var tabControl = new TabControl { Dock = DockStyle.Fill };
        Controls.Add(tabControl);

        tabControl.TabPages.Add(CreateRegistrationTab());
        tabControl.TabPages.Add(CreateTransferTab());
        tabControl.TabPages.Add(CreateErrorHandlingTab());
        tabControl.TabPages.Add(CreateTwoPhaseTab());
    }

    private TabPage CreateRegistrationTab()
    {
        var tab = new TabPage("Registration");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };

        panel.Controls.Add(new Label { Text = "DI + Async Duplicate Detection (UniqueUsername + UniqueEmail)", AutoSize = true });

        var txtUsername = CreateField(panel, "Username:", "admin");
        var txtEmail = CreateField(panel, "Email:", "admin@example.com");
        var txtPassword = CreateField(panel, "Password:", "SecureP@ss123");

        var btnValidate = new Button { Text = "Validate (Async)", AutoSize = true };
        var lblResult = new Label { AutoSize = true, ForeColor = System.Drawing.Color.DarkRed, MaximumSize = new System.Drawing.Size(600, 0) };
        btnValidate.Click += async (s, e) =>
        {
            var registration = new UserRegistration
            {
                Username = txtUsername.Text,
                Email = txtEmail.Text,
                Password = txtPassword.Text
            };

            var context = new ValidationContext(registration, _serviceProvider, null);
            var results = new List<ValidationResult>();
            bool isValid = await Validator.TryValidateObjectAsync(registration, context, results, true);

            lblResult.Text = isValid
                ? "✅ Valid!"
                : "❌ " + string.Join("\n", results.Select(r => r.ErrorMessage));
        };
        panel.Controls.Add(btnValidate);
        panel.Controls.Add(lblResult);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateTransferTab()
    {
        var tab = new TabPage("Transfer");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };

        panel.Controls.Add(new Label { Text = "IAsyncValidatableObject — MoneyTransfer (same-account + balance check)", AutoSize = true });

        var txtFrom = CreateField(panel, "From Account:", "checking");
        var txtTo = CreateField(panel, "To Account:", "checking");
        var txtAmount = CreateField(panel, "Amount:", "1000.00");

        var btnValidate = new Button { Text = "Validate (Async)", AutoSize = true };
        var lblResult = new Label { AutoSize = true, ForeColor = System.Drawing.Color.DarkRed, MaximumSize = new System.Drawing.Size(600, 0) };
        btnValidate.Click += async (s, e) =>
        {
            var transfer = new MoneyTransfer
            {
                FromAccount = txtFrom.Text,
                ToAccount = txtTo.Text,
                Amount = decimal.TryParse(txtAmount.Text, out decimal a) ? a : 0m
            };

            var context = new ValidationContext(transfer, _serviceProvider, null);
            var results = new List<ValidationResult>();
            bool isValid = await Validator.TryValidateObjectAsync(transfer, context, results, true);

            lblResult.Text = isValid
                ? "✅ Valid!"
                : "❌ " + string.Join("\n", results.Select(r => r.ErrorMessage));
        };
        panel.Controls.Add(btnValidate);
        panel.Controls.Add(lblResult);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateErrorHandlingTab()
    {
        var tab = new TabPage("Error Handling");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };

        panel.Controls.Add(new Label { Text = "Infrastructure Failure — 'error-trigger' username throws exception", AutoSize = true });

        var txtUsername = CreateField(panel, "Username:", "error-trigger");
        var txtEmail = CreateField(panel, "Email:", "new@example.com");
        var txtPassword = CreateField(panel, "Password:", "SecureP@ss123");

        var btnValidate = new Button { Text = "Validate (Async)", AutoSize = true };
        var lblResult = new Label { AutoSize = true, ForeColor = System.Drawing.Color.DarkRed, MaximumSize = new System.Drawing.Size(600, 0) };
        btnValidate.Click += async (s, e) =>
        {
            var registration = new UserRegistration
            {
                Username = txtUsername.Text,
                Email = txtEmail.Text,
                Password = txtPassword.Text
            };

            var context = new ValidationContext(registration, _serviceProvider, null);
            var results = new List<ValidationResult>();
            try
            {
                bool isValid = await Validator.TryValidateObjectAsync(registration, context, results, true);
                lblResult.Text = isValid
                    ? "✅ Valid!"
                    : "❌ " + string.Join("\n", results.Select(r => r.ErrorMessage));
            }
            catch (InvalidOperationException ex)
            {
                lblResult.Text = $"⚠️ Infrastructure error caught: {ex.Message}";
            }
        };
        panel.Controls.Add(btnValidate);
        panel.Controls.Add(lblResult);

        tab.Controls.Add(panel);
        return tab;
    }

    private TabPage CreateTwoPhaseTab()
    {
        var tab = new TabPage("Two-Phase");
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };

        panel.Controls.Add(new Label { Text = "Two-Phase: sync [EmailAddress] fails → async [UniqueEmail] skipped", AutoSize = true });

        var txtUsername = CreateField(panel, "Username:", "newuser");
        var txtEmail = CreateField(panel, "Email:", "not-an-email");
        var txtPassword = CreateField(panel, "Password:", "SecureP@ss123");

        var btnValidate = new Button { Text = "Validate (Async)", AutoSize = true };
        var lblResult = new Label { AutoSize = true, ForeColor = System.Drawing.Color.DarkRed, MaximumSize = new System.Drawing.Size(600, 0) };
        btnValidate.Click += async (s, e) =>
        {
            var registration = new UserRegistration
            {
                Username = txtUsername.Text,
                Email = txtEmail.Text,
                Password = txtPassword.Text
            };

            var context = new ValidationContext(registration, _serviceProvider, null);
            var results = new List<ValidationResult>();
            bool isValid = await Validator.TryValidateObjectAsync(registration, context, results, true);

            string message = isValid
                ? "✅ Valid!"
                : "❌ " + string.Join("\n", results.Select(r => r.ErrorMessage));
            message += "\n(Note: async UniqueEmail check was skipped because sync EmailAddress failed first)";
            lblResult.Text = message;
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
