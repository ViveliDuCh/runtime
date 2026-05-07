// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace AsyncDesignerValidationDemo;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _tabControl = new TabControl();
        _tabRegistration = new TabPage();
        _tabTransfer = new TabPage();
        _tabErrorHandling = new TabPage();
        _tabTwoPhase = new TabPage();

        // Registration tab controls
        lblRegHeader = new Label();
        lblRegUsername = new Label();
        txtRegUsername = new TextBox();
        lblRegEmail = new Label();
        txtRegEmail = new TextBox();
        lblRegPassword = new Label();
        txtRegPassword = new TextBox();
        btnValidateReg = new Button();
        lblRegResult = new Label();

        // Transfer tab controls
        lblTransferHeader = new Label();
        lblTransferFrom = new Label();
        txtTransferFrom = new TextBox();
        lblTransferTo = new Label();
        txtTransferTo = new TextBox();
        lblTransferAmount = new Label();
        txtTransferAmount = new TextBox();
        btnValidateTransfer = new Button();
        lblTransferResult = new Label();

        // Error handling tab controls
        lblErrorHeader = new Label();
        lblErrorUsername = new Label();
        txtErrorUsername = new TextBox();
        lblErrorEmail = new Label();
        txtErrorEmail = new TextBox();
        lblErrorPassword = new Label();
        txtErrorPassword = new TextBox();
        btnValidateError = new Button();
        lblErrorResult = new Label();

        // Two-phase tab controls
        lblTwoPhaseHeader = new Label();
        lblTwoPhaseUsername = new Label();
        txtTwoPhaseUsername = new TextBox();
        lblTwoPhaseEmail = new Label();
        txtTwoPhaseEmail = new TextBox();
        lblTwoPhasePassword = new Label();
        txtTwoPhasePassword = new TextBox();
        btnValidateTwoPhase = new Button();
        lblTwoPhaseResult = new Label();

        SuspendLayout();

        // TabControl
        _tabControl.Dock = DockStyle.Fill;
        _tabControl.TabPages.AddRange(new TabPage[] { _tabRegistration, _tabTransfer, _tabErrorHandling, _tabTwoPhase });

        // --- Registration Tab ---
        _tabRegistration.Text = "Registration";
        _tabRegistration.Padding = new Padding(10);

        lblRegHeader.Text = "DI + Async Duplicate Detection (UniqueUsername + UniqueEmail)";
        lblRegHeader.AutoSize = true;
        lblRegHeader.Location = new System.Drawing.Point(15, 15);
        lblRegHeader.Font = new System.Drawing.Font(lblRegHeader.Font, System.Drawing.FontStyle.Bold);

        lblRegUsername.Text = "Username:";
        lblRegUsername.AutoSize = true;
        lblRegUsername.Location = new System.Drawing.Point(15, 45);
        txtRegUsername.Text = "admin";
        txtRegUsername.Width = 400;
        txtRegUsername.Location = new System.Drawing.Point(15, 65);

        lblRegEmail.Text = "Email:";
        lblRegEmail.AutoSize = true;
        lblRegEmail.Location = new System.Drawing.Point(15, 95);
        txtRegEmail.Text = "admin@example.com";
        txtRegEmail.Width = 400;
        txtRegEmail.Location = new System.Drawing.Point(15, 115);

        lblRegPassword.Text = "Password:";
        lblRegPassword.AutoSize = true;
        lblRegPassword.Location = new System.Drawing.Point(15, 145);
        txtRegPassword.Text = "SecureP@ss123";
        txtRegPassword.Width = 400;
        txtRegPassword.Location = new System.Drawing.Point(15, 165);

        btnValidateReg.Text = "Validate (Async)";
        btnValidateReg.AutoSize = true;
        btnValidateReg.Location = new System.Drawing.Point(15, 200);
        btnValidateReg.Click += BtnValidateReg_Click;

        lblRegResult.AutoSize = true;
        lblRegResult.ForeColor = System.Drawing.Color.DarkRed;
        lblRegResult.Location = new System.Drawing.Point(15, 235);
        lblRegResult.MaximumSize = new System.Drawing.Size(550, 0);

        _tabRegistration.Controls.AddRange(new Control[] {
            lblRegHeader, lblRegUsername, txtRegUsername, lblRegEmail, txtRegEmail,
            lblRegPassword, txtRegPassword, btnValidateReg, lblRegResult });

        // --- Transfer Tab ---
        _tabTransfer.Text = "Transfer";
        _tabTransfer.Padding = new Padding(10);

        lblTransferHeader.Text = "IAsyncValidatableObject — MoneyTransfer";
        lblTransferHeader.AutoSize = true;
        lblTransferHeader.Location = new System.Drawing.Point(15, 15);
        lblTransferHeader.Font = new System.Drawing.Font(lblTransferHeader.Font, System.Drawing.FontStyle.Bold);

        lblTransferFrom.Text = "From Account:";
        lblTransferFrom.AutoSize = true;
        lblTransferFrom.Location = new System.Drawing.Point(15, 45);
        txtTransferFrom.Text = "checking";
        txtTransferFrom.Width = 300;
        txtTransferFrom.Location = new System.Drawing.Point(15, 65);

        lblTransferTo.Text = "To Account:";
        lblTransferTo.AutoSize = true;
        lblTransferTo.Location = new System.Drawing.Point(15, 95);
        txtTransferTo.Text = "checking";
        txtTransferTo.Width = 300;
        txtTransferTo.Location = new System.Drawing.Point(15, 115);

        lblTransferAmount.Text = "Amount:";
        lblTransferAmount.AutoSize = true;
        lblTransferAmount.Location = new System.Drawing.Point(15, 145);
        txtTransferAmount.Text = "1000.00";
        txtTransferAmount.Width = 150;
        txtTransferAmount.Location = new System.Drawing.Point(15, 165);

        btnValidateTransfer.Text = "Validate (Async)";
        btnValidateTransfer.AutoSize = true;
        btnValidateTransfer.Location = new System.Drawing.Point(15, 200);
        btnValidateTransfer.Click += BtnValidateTransfer_Click;

        lblTransferResult.AutoSize = true;
        lblTransferResult.ForeColor = System.Drawing.Color.DarkRed;
        lblTransferResult.Location = new System.Drawing.Point(15, 235);
        lblTransferResult.MaximumSize = new System.Drawing.Size(550, 0);

        _tabTransfer.Controls.AddRange(new Control[] {
            lblTransferHeader, lblTransferFrom, txtTransferFrom, lblTransferTo, txtTransferTo,
            lblTransferAmount, txtTransferAmount, btnValidateTransfer, lblTransferResult });

        // --- Error Handling Tab ---
        _tabErrorHandling.Text = "Error Handling";
        _tabErrorHandling.Padding = new Padding(10);

        lblErrorHeader.Text = "Infrastructure Failure — 'error-trigger' throws exception";
        lblErrorHeader.AutoSize = true;
        lblErrorHeader.Location = new System.Drawing.Point(15, 15);
        lblErrorHeader.Font = new System.Drawing.Font(lblErrorHeader.Font, System.Drawing.FontStyle.Bold);

        lblErrorUsername.Text = "Username:";
        lblErrorUsername.AutoSize = true;
        lblErrorUsername.Location = new System.Drawing.Point(15, 45);
        txtErrorUsername.Text = "error-trigger";
        txtErrorUsername.Width = 400;
        txtErrorUsername.Location = new System.Drawing.Point(15, 65);

        lblErrorEmail.Text = "Email:";
        lblErrorEmail.AutoSize = true;
        lblErrorEmail.Location = new System.Drawing.Point(15, 95);
        txtErrorEmail.Text = "new@example.com";
        txtErrorEmail.Width = 400;
        txtErrorEmail.Location = new System.Drawing.Point(15, 115);

        lblErrorPassword.Text = "Password:";
        lblErrorPassword.AutoSize = true;
        lblErrorPassword.Location = new System.Drawing.Point(15, 145);
        txtErrorPassword.Text = "SecureP@ss123";
        txtErrorPassword.Width = 400;
        txtErrorPassword.Location = new System.Drawing.Point(15, 165);

        btnValidateError.Text = "Validate (Async)";
        btnValidateError.AutoSize = true;
        btnValidateError.Location = new System.Drawing.Point(15, 200);
        btnValidateError.Click += BtnValidateError_Click;

        lblErrorResult.AutoSize = true;
        lblErrorResult.ForeColor = System.Drawing.Color.DarkRed;
        lblErrorResult.Location = new System.Drawing.Point(15, 235);
        lblErrorResult.MaximumSize = new System.Drawing.Size(550, 0);

        _tabErrorHandling.Controls.AddRange(new Control[] {
            lblErrorHeader, lblErrorUsername, txtErrorUsername, lblErrorEmail, txtErrorEmail,
            lblErrorPassword, txtErrorPassword, btnValidateError, lblErrorResult });

        // --- Two-Phase Tab ---
        _tabTwoPhase.Text = "Two-Phase";
        _tabTwoPhase.Padding = new Padding(10);

        lblTwoPhaseHeader.Text = "Two-Phase: sync [EmailAddress] fails → async [UniqueEmail] skipped";
        lblTwoPhaseHeader.AutoSize = true;
        lblTwoPhaseHeader.Location = new System.Drawing.Point(15, 15);
        lblTwoPhaseHeader.Font = new System.Drawing.Font(lblTwoPhaseHeader.Font, System.Drawing.FontStyle.Bold);

        lblTwoPhaseUsername.Text = "Username:";
        lblTwoPhaseUsername.AutoSize = true;
        lblTwoPhaseUsername.Location = new System.Drawing.Point(15, 45);
        txtTwoPhaseUsername.Text = "newuser";
        txtTwoPhaseUsername.Width = 400;
        txtTwoPhaseUsername.Location = new System.Drawing.Point(15, 65);

        lblTwoPhaseEmail.Text = "Email:";
        lblTwoPhaseEmail.AutoSize = true;
        lblTwoPhaseEmail.Location = new System.Drawing.Point(15, 95);
        txtTwoPhaseEmail.Text = "not-an-email";
        txtTwoPhaseEmail.Width = 400;
        txtTwoPhaseEmail.Location = new System.Drawing.Point(15, 115);

        lblTwoPhasePassword.Text = "Password:";
        lblTwoPhasePassword.AutoSize = true;
        lblTwoPhasePassword.Location = new System.Drawing.Point(15, 145);
        txtTwoPhasePassword.Text = "SecureP@ss123";
        txtTwoPhasePassword.Width = 400;
        txtTwoPhasePassword.Location = new System.Drawing.Point(15, 165);

        btnValidateTwoPhase.Text = "Validate (Async)";
        btnValidateTwoPhase.AutoSize = true;
        btnValidateTwoPhase.Location = new System.Drawing.Point(15, 200);
        btnValidateTwoPhase.Click += BtnValidateTwoPhase_Click;

        lblTwoPhaseResult.AutoSize = true;
        lblTwoPhaseResult.ForeColor = System.Drawing.Color.DarkRed;
        lblTwoPhaseResult.Location = new System.Drawing.Point(15, 235);
        lblTwoPhaseResult.MaximumSize = new System.Drawing.Size(550, 0);

        _tabTwoPhase.Controls.AddRange(new Control[] {
            lblTwoPhaseHeader, lblTwoPhaseUsername, txtTwoPhaseUsername, lblTwoPhaseEmail, txtTwoPhaseEmail,
            lblTwoPhasePassword, txtTwoPhasePassword, btnValidateTwoPhase, lblTwoPhaseResult });

        // Form
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(700, 550);
        Controls.Add(_tabControl);
        Text = "WinForms AsyncDesignerValidationDemo — Designer + DI + Async";

        ResumeLayout(false);
    }

    private TabControl _tabControl;
    private TabPage _tabRegistration;
    private TabPage _tabTransfer;
    private TabPage _tabErrorHandling;
    private TabPage _tabTwoPhase;

    // Registration
    private Label lblRegHeader;
    private Label lblRegUsername;
    private TextBox txtRegUsername;
    private Label lblRegEmail;
    private TextBox txtRegEmail;
    private Label lblRegPassword;
    private TextBox txtRegPassword;
    private Button btnValidateReg;
    private Label lblRegResult;

    // Transfer
    private Label lblTransferHeader;
    private Label lblTransferFrom;
    private TextBox txtTransferFrom;
    private Label lblTransferTo;
    private TextBox txtTransferTo;
    private Label lblTransferAmount;
    private TextBox txtTransferAmount;
    private Button btnValidateTransfer;
    private Label lblTransferResult;

    // Error handling
    private Label lblErrorHeader;
    private Label lblErrorUsername;
    private TextBox txtErrorUsername;
    private Label lblErrorEmail;
    private TextBox txtErrorEmail;
    private Label lblErrorPassword;
    private TextBox txtErrorPassword;
    private Button btnValidateError;
    private Label lblErrorResult;

    // Two-phase
    private Label lblTwoPhaseHeader;
    private Label lblTwoPhaseUsername;
    private TextBox txtTwoPhaseUsername;
    private Label lblTwoPhaseEmail;
    private TextBox txtTwoPhaseEmail;
    private Label lblTwoPhasePassword;
    private TextBox txtTwoPhasePassword;
    private Button btnValidateTwoPhase;
    private Label lblTwoPhaseResult;
}
