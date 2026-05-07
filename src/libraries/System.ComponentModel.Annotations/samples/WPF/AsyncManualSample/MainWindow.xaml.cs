// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows;
using AsyncManualSample.ViewModels;

namespace AsyncManualSample;

public partial class MainWindow : Window
{
    public UserViewModel UserVM { get; } = new();
    public EventViewModel EventVM { get; } = new();
    public OrderViewModel OrderVM { get; } = new();
    public ProfileViewModel ProfileVM { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void ValidateUser_Click(object sender, RoutedEventArgs e)
    {
        bool valid = await UserVM.ValidateAllAsync();
        UserResult.Text = valid ? "✅ Valid!" : "❌ Validation failed (async, UI stayed responsive).";
    }

    private async void ValidateEvent_Click(object sender, RoutedEventArgs e)
    {
        bool valid = await EventVM.ValidateAllAsync();
        EventResult.Text = valid ? "✅ Valid!" : "❌ Validation failed (async, UI stayed responsive).";
    }

    private async void ValidateOrder_Click(object sender, RoutedEventArgs e)
    {
        bool valid = await OrderVM.ValidateAllAsync();
        OrderResult.Text = valid ? "✅ Valid!" : "❌ Validation failed (async, UI stayed responsive).";
    }

    private async void ValidateProfile_Click(object sender, RoutedEventArgs e)
    {
        bool valid = await ProfileVM.ValidateAllAsync();
        ProfileResult.Text = valid ? "✅ Valid!" : "❌ Validation failed (async, UI stayed responsive).";
    }
}
