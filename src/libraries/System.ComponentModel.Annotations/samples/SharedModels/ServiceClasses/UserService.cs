// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SharedModels.ServiceClasses;

/// <summary>
/// Simulates a repository/service that checks uniqueness against a database.
/// In real apps, this would be resolved via Microsoft.Extensions.DependencyInjection.
/// </summary>
public class UserService
{
    // Simulated "database" of taken emails and usernames (case-insensitive).
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

    // Simulate async database lookup for email uniqueness.
    public async Task<bool> IsEmailTakenAsync(string email, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        return s_takenEmails.Contains(email);
    }

    // Simulate async database lookup for username uniqueness.
    public async Task<bool> IsUsernameTakenAsync(string username, CancellationToken cancellationToken = default)
    {
        // The "error-trigger" username simulates an infrastructure failure
        if (username == "error-trigger")
        {
            throw new InvalidOperationException("Database connection failed (simulated infrastructure error).");
        }

        await Task.Delay(10, cancellationToken);
        return s_takenUsernames.Contains(username);
    }
}
