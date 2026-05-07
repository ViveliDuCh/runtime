// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using SharedModels.EntityClasses;
using SharedModels.ServiceClasses;

public class Program
{
    public static async Task Main()
    {
        Console.WriteLine("=== Async Validation Console Demo ===\n");

        //Simple provider with a UserService registered
        var serviceProvider = new SimpleServiceProvider()
            .Register(new UserService());

        // ───────────────────────────────────────────
        // Scenario 1: DI Service Resolution + Multiple Async Attributes
        // ───────────────────────────────────────────
        Console.WriteLine("--- Scenario 1: DI Service Resolution + Duplicate Detection ---");
        {
            var registration = new UserRegistration
            {
                Username = "admin",       // taken
                Email = "admin@example.com", // taken
                Password = "SecureP@ss123"
            };

            var context = new ValidationContext(registration, serviceProvider, null);
            var results = new List<ValidationResult>();
            bool isValid = await Validator.TryValidateObjectAsync(registration, context, results, true);

            Console.WriteLine($"  Valid: {isValid}");
            foreach (var r in results)
            {
                Console.WriteLine($"  Error: {r.ErrorMessage}");
            }
        }
        Console.WriteLine();

        // ───────────────────────────────────────────
        // Scenario 2: Two-Phase Validation (sync blocks async on same property)
        // ───────────────────────────────────────────
        Console.WriteLine("--- Scenario 2: Two-Phase Validation ---");
        {
            var registration = new UserRegistration
            {
                Username = "newuser",
                Email = "not-an-email",    // fails sync [EmailAddress] → async [UniqueEmail] skipped
                Password = "SecureP@ss123"
            };

            var context = new ValidationContext(registration, serviceProvider, null);
            var results = new List<ValidationResult>();
            bool isValid = await Validator.TryValidateObjectAsync(registration, context, results, true);

            Console.WriteLine($"  Valid: {isValid}");
            foreach (var r in results)
            {
                Console.WriteLine($"  Error: {r.ErrorMessage}");
            }
            Console.WriteLine("  (Note: UniqueEmail async check was skipped because sync EmailAddress failed first)");
        }
        Console.WriteLine();

        // ───────────────────────────────────────────
        // Scenario 3: IAsyncValidatableObject (cross-property validation)
        // ───────────────────────────────────────────
        Console.WriteLine("--- Scenario 3: IAsyncValidatableObject (MoneyTransfer) ---");
        {
            var transfer = new MoneyTransfer
            {
                FromAccount = "checking",
                ToAccount = "checking",  // same account — cross-property error
                Amount = 1000.00m        // exceeds balance — async balance check
            };

            var context = new ValidationContext(transfer, serviceProvider, null);
            var results = new List<ValidationResult>();
            bool isValid = await Validator.TryValidateObjectAsync(transfer, context, results, true);

            Console.WriteLine($"  Valid: {isValid}");
            foreach (var r in results)
            {
                Console.WriteLine($"  Error: {r.ErrorMessage}");
            }
        }
        Console.WriteLine();

        // ───────────────────────────────────────────
        // Scenario 4: Infrastructure Failure Handling
        // ───────────────────────────────────────────
        Console.WriteLine("--- Scenario 4: Infrastructure Failure Handling ---");
        {
            var registration = new UserRegistration
            {
                Username = "error-trigger", // triggers simulated DB failure
                Email = "new@example.com",
                Password = "SecureP@ss123"
            };

            var context = new ValidationContext(registration, serviceProvider, null);
            var results = new List<ValidationResult>();
            try
            {
                await Validator.TryValidateObjectAsync(registration, context, results, true);
                Console.WriteLine("  (Should not reach here)");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"  Caught expected error: {ex.Message}");
            }
        }
        Console.WriteLine();

        // ───────────────────────────────────────────
        // Scenario 5: CancellationToken Propagation
        // Expected to throw OperationCanceledException immediately
        // ───────────────────────────────────────────
        //This tests that CancellationToken flows correctly through the entire pipeline:
        //From the public Validator.TryValidateObjectAsync call → through internal helpers
        //→ into each AsyncValidationAttribute.IsValidAsync and IAsyncValidatableObject.ValidateAsync.
        //When the token is cancelled, the operation should throw OperationCanceledException
        //rather than performing (or waiting for) unnecessary I/O.
        Console.WriteLine("--- Scenario 5: CancellationToken Propagation ---");
        {
            var registration = new UserRegistration
            {
                Username = "newuser",
                Email = "new@example.com",
                Password = "SecureP@ss123"
            };

            // Pre-cancel the token before calling validation
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel before starting validation for deterministic results

            var context = new ValidationContext(registration, serviceProvider, null);
            var results = new List<ValidationResult>();
            try
            {
                await Validator.TryValidateObjectAsync(registration, context, results, true, cts.Token);
                Console.WriteLine("  (Should not reach here)");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("  Caught expected OperationCanceledException from pre-cancelled token.");
            }
        }

        // Real world apps for scenario 5
        // - ASP.NET Core Minimal API:
        // The framework provides a CancellationToken that fires when the
        // client disconnects (closes the browser tab, navigates away, etc.)
        // - Blazor form with timeout:
        // Cancel validation if it takes longer than 5 seconds
    }
}
