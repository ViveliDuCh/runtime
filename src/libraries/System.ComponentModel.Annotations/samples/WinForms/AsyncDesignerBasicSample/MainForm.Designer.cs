// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace AsyncDesignerBasicSample;

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
        _errorProvider = new ErrorProvider(components);
        _tabControl = new TabControl();
        _tabUserPage = new TabPage();
        _tabEventPage = new TabPage();
        _tabOrderPage = new TabPage();
        _tabProfilePage = new TabPage();

        // User tab controls
        lblUserHeader = new Label();
        lblUserName = new Label();
        txtUserName = new TextBox();
        lblUserEmail = new Label();
        txtUserEmail = new TextBox();
        lblUserDelay = new Label();
        txtUserDelay = new TextBox();
        btnValidateUser = new Button();
        lblUserResult = new Label();

        // Event tab controls
        lblEventHeader = new Label();
        lblEventTitle = new Label();
        txtEventTitle = new TextBox();
        lblEventStart = new Label();
        dtpEventStart = new DateTimePicker();
        lblEventEnd = new Label();
        dtpEventEnd = new DateTimePicker();
        lblEventDelay = new Label();
        txtEventDelay = new TextBox();
        btnValidateEvent = new Button();
        lblEventResult = new Label();

        // Order tab controls
        lblOrderHeader = new Label();
        lblOrderProduct = new Label();
        txtOrderProduct = new TextBox();
        lblOrderQty = new Label();
        txtOrderQty = new TextBox();
        lblOrderPrice = new Label();
        txtOrderPrice = new TextBox();
        lblOrderDelay = new Label();
        txtOrderDelay = new TextBox();
        btnValidateOrder = new Button();
        lblOrderResult = new Label();

        // Profile tab controls
        lblProfileHeader = new Label();
        lblProfileUsername = new Label();
        txtProfileUsername = new TextBox();
        lblProfileBio = new Label();
        txtProfileBio = new TextBox();
        lblProfileDelay = new Label();
        txtProfileDelay = new TextBox();
        btnValidateProfile = new Button();
        lblProfileResult = new Label();

        ((System.ComponentModel.ISupportInitialize)_errorProvider).BeginInit();
        SuspendLayout();

        // TabControl
        _tabControl.Dock = DockStyle.Fill;
        _tabControl.TabPages.AddRange(new TabPage[] { _tabUserPage, _tabEventPage, _tabOrderPage, _tabProfilePage });

        // --- User Tab ---
        _tabUserPage.Text = "User";
        _tabUserPage.Padding = new Padding(10);

        lblUserHeader.Text = "Scenarios 1 & 2: IsValidName + AsyncOnlyEmailDomain (async)";
        lblUserHeader.AutoSize = true;
        lblUserHeader.Location = new System.Drawing.Point(15, 15);
        lblUserHeader.Font = new System.Drawing.Font(lblUserHeader.Font, System.Drawing.FontStyle.Bold);

        lblUserName.Text = "Name:";
        lblUserName.AutoSize = true;
        lblUserName.Location = new System.Drawing.Point(15, 45);
        txtUserName.Text = "Bob";
        txtUserName.Width = 400;
        txtUserName.Location = new System.Drawing.Point(15, 65);

        lblUserEmail.Text = "Email:";
        lblUserEmail.AutoSize = true;
        lblUserEmail.Location = new System.Drawing.Point(15, 95);
        txtUserEmail.Text = "bob@gmail.com";
        txtUserEmail.Width = 400;
        txtUserEmail.Location = new System.Drawing.Point(15, 115);

        lblUserDelay.Text = "Delay (ms):";
        lblUserDelay.AutoSize = true;
        lblUserDelay.Location = new System.Drawing.Point(15, 145);
        txtUserDelay.Text = "3000";
        txtUserDelay.Width = 100;
        txtUserDelay.Location = new System.Drawing.Point(15, 165);

        btnValidateUser.Text = "Validate All (Async)";
        btnValidateUser.AutoSize = true;
        btnValidateUser.Location = new System.Drawing.Point(15, 200);
        btnValidateUser.Click += BtnValidateUser_Click;

        lblUserResult.AutoSize = true;
        lblUserResult.ForeColor = System.Drawing.Color.DarkRed;
        lblUserResult.Location = new System.Drawing.Point(15, 235);
        lblUserResult.MaximumSize = new System.Drawing.Size(550, 0);

        _tabUserPage.Controls.AddRange(new Control[] {
            lblUserHeader, lblUserName, txtUserName, lblUserEmail, txtUserEmail,
            lblUserDelay, txtUserDelay, btnValidateUser, lblUserResult });

        // --- Event Tab ---
        _tabEventPage.Text = "Event";
        _tabEventPage.Padding = new Padding(10);

        lblEventHeader.Text = "Scenario 3: AsyncDateRangeValid (async)";
        lblEventHeader.AutoSize = true;
        lblEventHeader.Location = new System.Drawing.Point(15, 15);
        lblEventHeader.Font = new System.Drawing.Font(lblEventHeader.Font, System.Drawing.FontStyle.Bold);

        lblEventTitle.Text = "Title:";
        lblEventTitle.AutoSize = true;
        lblEventTitle.Location = new System.Drawing.Point(15, 45);
        txtEventTitle.Text = "Launch Party";
        txtEventTitle.Width = 400;
        txtEventTitle.Location = new System.Drawing.Point(15, 65);

        lblEventStart.Text = "Start Date:";
        lblEventStart.AutoSize = true;
        lblEventStart.Location = new System.Drawing.Point(15, 95);
        dtpEventStart.Value = new DateTime(2026, 12, 25);
        dtpEventStart.Width = 200;
        dtpEventStart.Location = new System.Drawing.Point(15, 115);

        lblEventEnd.Text = "End Date:";
        lblEventEnd.AutoSize = true;
        lblEventEnd.Location = new System.Drawing.Point(15, 145);
        dtpEventEnd.Value = new DateTime(2026, 12, 20);
        dtpEventEnd.Width = 200;
        dtpEventEnd.Location = new System.Drawing.Point(15, 165);

        lblEventDelay.Text = "Delay (ms):";
        lblEventDelay.AutoSize = true;
        lblEventDelay.Location = new System.Drawing.Point(15, 195);
        txtEventDelay.Text = "3000";
        txtEventDelay.Width = 100;
        txtEventDelay.Location = new System.Drawing.Point(15, 215);

        btnValidateEvent.Text = "Validate All (Async)";
        btnValidateEvent.AutoSize = true;
        btnValidateEvent.Location = new System.Drawing.Point(15, 250);
        btnValidateEvent.Click += BtnValidateEvent_Click;

        lblEventResult.AutoSize = true;
        lblEventResult.ForeColor = System.Drawing.Color.DarkRed;
        lblEventResult.Location = new System.Drawing.Point(15, 285);
        lblEventResult.MaximumSize = new System.Drawing.Size(550, 0);

        _tabEventPage.Controls.AddRange(new Control[] {
            lblEventHeader, lblEventTitle, txtEventTitle, lblEventStart, dtpEventStart,
            lblEventEnd, dtpEventEnd, lblEventDelay, txtEventDelay, btnValidateEvent, lblEventResult });

        // --- Order Tab ---
        _tabOrderPage.Text = "Order";
        _tabOrderPage.Padding = new Padding(10);

        lblOrderHeader.Text = "Scenario 4: IAsyncValidatableObject cross-property (async)";
        lblOrderHeader.AutoSize = true;
        lblOrderHeader.Location = new System.Drawing.Point(15, 15);
        lblOrderHeader.Font = new System.Drawing.Font(lblOrderHeader.Font, System.Drawing.FontStyle.Bold);

        lblOrderProduct.Text = "Product:";
        lblOrderProduct.AutoSize = true;
        lblOrderProduct.Location = new System.Drawing.Point(15, 45);
        txtOrderProduct.Text = "Widget";
        txtOrderProduct.Width = 400;
        txtOrderProduct.Location = new System.Drawing.Point(15, 65);

        lblOrderQty.Text = "Quantity:";
        lblOrderQty.AutoSize = true;
        lblOrderQty.Location = new System.Drawing.Point(15, 95);
        txtOrderQty.Text = "10000";
        txtOrderQty.Width = 100;
        txtOrderQty.Location = new System.Drawing.Point(15, 115);

        lblOrderPrice.Text = "Unit Price:";
        lblOrderPrice.AutoSize = true;
        lblOrderPrice.Location = new System.Drawing.Point(15, 145);
        txtOrderPrice.Text = "10";
        txtOrderPrice.Width = 100;
        txtOrderPrice.Location = new System.Drawing.Point(15, 165);

        lblOrderDelay.Text = "Delay (ms):";
        lblOrderDelay.AutoSize = true;
        lblOrderDelay.Location = new System.Drawing.Point(15, 195);
        txtOrderDelay.Text = "3000";
        txtOrderDelay.Width = 100;
        txtOrderDelay.Location = new System.Drawing.Point(15, 215);

        btnValidateOrder.Text = "Validate All (Async)";
        btnValidateOrder.AutoSize = true;
        btnValidateOrder.Location = new System.Drawing.Point(15, 250);
        btnValidateOrder.Click += BtnValidateOrder_Click;

        lblOrderResult.AutoSize = true;
        lblOrderResult.ForeColor = System.Drawing.Color.DarkRed;
        lblOrderResult.Location = new System.Drawing.Point(15, 285);
        lblOrderResult.MaximumSize = new System.Drawing.Size(550, 0);

        _tabOrderPage.Controls.AddRange(new Control[] {
            lblOrderHeader, lblOrderProduct, txtOrderProduct, lblOrderQty, txtOrderQty,
            lblOrderPrice, txtOrderPrice, lblOrderDelay, txtOrderDelay, btnValidateOrder, lblOrderResult });

        // --- Profile Tab ---
        _tabProfilePage.Text = "Profile";
        _tabProfilePage.Padding = new Padding(10);

        lblProfileHeader.Text = "Scenario 5: IAsyncValidatableObject property-scoped (async)";
        lblProfileHeader.AutoSize = true;
        lblProfileHeader.Location = new System.Drawing.Point(15, 15);
        lblProfileHeader.Font = new System.Drawing.Font(lblProfileHeader.Font, System.Drawing.FontStyle.Bold);

        lblProfileUsername.Text = "Username:";
        lblProfileUsername.AutoSize = true;
        lblProfileUsername.Location = new System.Drawing.Point(15, 45);
        txtProfileUsername.Text = "admin";
        txtProfileUsername.Width = 400;
        txtProfileUsername.Location = new System.Drawing.Point(15, 65);

        lblProfileBio.Text = "Bio:";
        lblProfileBio.AutoSize = true;
        lblProfileBio.Location = new System.Drawing.Point(15, 95);
        txtProfileBio.Text = new string('x', 201);
        txtProfileBio.Width = 400;
        txtProfileBio.Location = new System.Drawing.Point(15, 115);

        lblProfileDelay.Text = "Delay (ms):";
        lblProfileDelay.AutoSize = true;
        lblProfileDelay.Location = new System.Drawing.Point(15, 145);
        txtProfileDelay.Text = "3000";
        txtProfileDelay.Width = 100;
        txtProfileDelay.Location = new System.Drawing.Point(15, 165);

        btnValidateProfile.Text = "Validate All (Async)";
        btnValidateProfile.AutoSize = true;
        btnValidateProfile.Location = new System.Drawing.Point(15, 200);
        btnValidateProfile.Click += BtnValidateProfile_Click;

        lblProfileResult.AutoSize = true;
        lblProfileResult.ForeColor = System.Drawing.Color.DarkRed;
        lblProfileResult.Location = new System.Drawing.Point(15, 235);
        lblProfileResult.MaximumSize = new System.Drawing.Size(550, 0);

        _tabProfilePage.Controls.AddRange(new Control[] {
            lblProfileHeader, lblProfileUsername, txtProfileUsername, lblProfileBio, txtProfileBio,
            lblProfileDelay, txtProfileDelay, btnValidateProfile, lblProfileResult });

        // Form
        AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new System.Drawing.Size(650, 500);
        Controls.Add(_tabControl);
        Text = "WinForms AsyncDesignerBasicSample — Designer + Async Validator";

        ((System.ComponentModel.ISupportInitialize)_errorProvider).EndInit();
        ResumeLayout(false);
    }

    private ErrorProvider _errorProvider;
    private TabControl _tabControl;
    private TabPage _tabUserPage;
    private TabPage _tabEventPage;
    private TabPage _tabOrderPage;
    private TabPage _tabProfilePage;

    // User
    private Label lblUserHeader;
    private Label lblUserName;
    private TextBox txtUserName;
    private Label lblUserEmail;
    private TextBox txtUserEmail;
    private Label lblUserDelay;
    private TextBox txtUserDelay;
    private Button btnValidateUser;
    private Label lblUserResult;

    // Event
    private Label lblEventHeader;
    private Label lblEventTitle;
    private TextBox txtEventTitle;
    private Label lblEventStart;
    private DateTimePicker dtpEventStart;
    private Label lblEventEnd;
    private DateTimePicker dtpEventEnd;
    private Label lblEventDelay;
    private TextBox txtEventDelay;
    private Button btnValidateEvent;
    private Label lblEventResult;

    // Order
    private Label lblOrderHeader;
    private Label lblOrderProduct;
    private TextBox txtOrderProduct;
    private Label lblOrderQty;
    private TextBox txtOrderQty;
    private Label lblOrderPrice;
    private TextBox txtOrderPrice;
    private Label lblOrderDelay;
    private TextBox txtOrderDelay;
    private Button btnValidateOrder;
    private Label lblOrderResult;

    // Profile
    private Label lblProfileHeader;
    private Label lblProfileUsername;
    private TextBox txtProfileUsername;
    private Label lblProfileBio;
    private TextBox txtProfileBio;
    private Label lblProfileDelay;
    private TextBox txtProfileDelay;
    private Button btnValidateProfile;
    private Label lblProfileResult;
}
