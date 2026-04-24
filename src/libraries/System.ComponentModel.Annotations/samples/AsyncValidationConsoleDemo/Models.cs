// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncValidationConsoleDemo;

/// <summary>
/// Simulates a repository/service that checks uniqueness against a database.
/// In real apps, this would be resolved via Microsoft.Extensions.DependencyInjection.
/// </summary>
public class UserService
{
    // Simulated "database" of taken emails and usernames (case-insensitive).
    // A static HashSet acts as the "Users table"
    private static readonly HashSet<string> s_takenEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin@example.com",
        "test@example.com"
    };

    private static readonly HashSet<string> s_takenUsernames = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "testuser"
    };

    // Simulate async database lookup for email and username uniqueness. ("Query")
    public async Task<bool> IsEmailTakenAsync(string email, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken); // Simulate network latency
        return s_takenEmails.Contains(email); // HashSet.Contains simulates a SQL WHERE clause
    }

    // "Query"
    public async Task<bool> IsUsernameTakenAsync(string username, CancellationToken cancellationToken = default)
    {
        // The "error-trigger" username simulates an infrastructure failure (e.g., DB connection issue)
        // to demonstrate exception handling in async validation.
        if (username == "error-trigger")
        {
            throw new InvalidOperationException("Database connection failed (simulated infrastructure error).");
        }

        await Task.Delay(10, cancellationToken); // Simulate DB round-trip
        return s_takenUsernames.Contains(username);
    }
}

/// <summary>
/// Minimal IServiceProvider for console demos. In real apps, use Microsoft.Extensions.DependencyInjection.
/// </summary>
public class SimpleServiceProvider : IServiceProvider //Container
{
    private readonly Dictionary<Type, object> _services = new();

    // Used like: var serviceProvider = new SimpleServiceProvider().Register(new UserService());
    public SimpleServiceProvider Register<T>(T service) where T : notnull
    {
        _services[typeof(T)] = service;
        return this;
    }

    public object? GetService(Type serviceType) =>
        _services.TryGetValue(serviceType, out var service) ? service : null;
}

/// <summary>
/// Async validation attribute that checks email uniqueness via UserService DI.
/// Resolves UserService from ValidationContext.GetService(), matching the aspnetcore pattern.
/// </summary>
public class UniqueEmailAttribute : AsyncValidationAttribute
{
    protected override async ValueTask<ValidationResult?> IsValidAsync(
        object? value,
        ValidationContext validationContext,
        CancellationToken cancellationToken)
    {
        // Arrives here if all sync attributes ([Required], [EmailAddress]) pass (Validation phase 2)
        if (value is not string email || string.IsNullOrEmpty(email))
        {
            return ValidationResult.Success;
        }

        // tests that async validation attributes can resolve services from the DI container
        // GetService delegates to SimpleServiceProvider
        var userService = (UserService?)validationContext.GetService(typeof(UserService));
        // → SimpleServiceProvider._services.TryGetValue(typeof(UserService), ...) → returns the instance

        if (userService is null)
        {
            return new ValidationResult("UserService is not available.");
        }

        bool taken = await userService.IsEmailTakenAsync(email, cancellationToken);

        return taken
            ? new ValidationResult($"The email '{email}' is already registered.")
            : ValidationResult.Success;
    }
}

/// <summary>
/// Async validation attribute that checks username uniqueness via UserService DI.
/// </summary>
public class UniqueUsernameAttribute : AsyncValidationAttribute
{
    protected override async ValueTask<ValidationResult?> IsValidAsync(
        object? value,
        ValidationContext validationContext,
        CancellationToken cancellationToken)
    {
        if (value is not string username || string.IsNullOrEmpty(username))
        {
            return ValidationResult.Success;
        }

        // GetService delegates to SimpleServiceProvider which gets the instance of UserService registered in Main.
        var userService = (UserService?)validationContext.GetService(typeof(UserService));
        if (userService is null)
        {
            return new ValidationResult("UserService is not available.");
        }

        bool taken = await userService.IsUsernameTakenAsync(username, cancellationToken);

        return taken
            ? new ValidationResult($"The username '{username}' is already taken.")
            : ValidationResult.Success;
    }
}

/// <summary>
/// Registration model with both sync and async validation attributes.
/// </summary>
public class UserRegistration
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    [UniqueUsername]
    public string? Username { get; set; }

    [Required]
    [EmailAddress]
    [UniqueEmail]
    public string? Email { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string? Password { get; set; }
}

/// <summary>
/// Cross-property async validation using IAsyncValidatableObject.
/// Demonstrates the "Venmo problem" — validating transfer amount against account balance.
/// </summary>
public class MoneyTransfer : IAsyncValidatableObject
{
    [Required]
    public string? FromAccount { get; set; }

    [Required]
    public string? ToAccount { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Transfer amount must be positive.")]
    public decimal Amount { get; set; }

    public async ValueTask<IEnumerable<ValidationResult>> ValidateAsync(
        ValidationContext validationContext,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ValidationResult>();

        // Cross property check, no I/O needed
        if (FromAccount == ToAccount)
        {
            results.Add(new ValidationResult(
                "Cannot transfer to the same account.",
                new[] { nameof(FromAccount), nameof(ToAccount) }));
        }

        // Simulate async balance check
        await Task.Delay(10, cancellationToken);
        decimal balance = 500.00m; // Simulated "database" balance

        if (Amount > balance)
        {
            results.Add(new ValidationResult(
                $"Insufficient funds. Balance: ${balance:F2}, Transfer: ${Amount:F2}.",
                new[] { nameof(Amount) }));
        }

        return results;
    }
}
