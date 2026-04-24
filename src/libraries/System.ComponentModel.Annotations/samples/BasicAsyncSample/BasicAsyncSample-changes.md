# BasicAsyncSample — Changes Diff

This document shows what was changed in the `BasicAsyncSample` project to implement
all five async custom validation scenarios with proper directory organization.

---

## Structural Change

The original sample had a flat layout with a single `Model.cs` containing both the
entity class and the validation attribute. The new layout separates concerns:

```
Before:                          After:
BasicAsyncSample/                BasicAsyncSample/
├── BasicAsyncSample.csproj      ├── BasicAsyncSample.csproj  (unchanged)
├── Model.cs                     ├── Program.cs               (rewritten)
└── Program.cs                   ├── EntityClasses/
                                 │   ├── User.cs
                                 │   ├── Event.cs
                                 │   ├── Order.cs
                                 │   └── Profile.cs
                                 └── ValidationClasses/
                                     ├── IsValidNameAttribute.cs
                                     ├── AsyncOnlyEmailDomainAttribute.cs
                                     └── AsyncDateRangeValidAttribute.cs
```

`Model.cs` was **deleted** and its contents split across the new files.

---

## Deleted: `Model.cs`

```diff
-// Licensed to the .NET Foundation under one or more agreements.
-// The .NET Foundation licenses this file to you under the MIT license.
-
-using System.ComponentModel.DataAnnotations;
-using System.Threading;
-using System.Threading.Tasks;
-
-namespace BasicAsyncSample;
-
-/// <summary>
-/// Hybrid validation attribute that supports both sync and async paths.
-/// The async path uses <see cref="Task.Delay(int, CancellationToken)"/> (non-blocking).
-/// The sync path uses <see cref="Thread.Sleep(int)"/> (blocks the calling thread).
-/// </summary>
-public class IsValidNameAttribute : AsyncValidationAttribute
-{
-    /// <summary>
-    /// Async path — called by <c>Validator.TryValidateObjectAsync</c>.
-    /// </summary>
-    protected override async ValueTask<ValidationResult?> IsValidAsync(
-        object? value,
-        ValidationContext validationContext,
-        CancellationToken cancellationToken)
-    {
-        User user = (User)validationContext.ObjectInstance;
-        int? delay = user.Delay;
-
-        if (delay is null)
-        {
-            return new ValidationResult("Delay is not configured.");
-        }
-
-        await Task.Delay((int)delay, cancellationToken);
-
-        return ValidationResult.Success;
-    }
-
-    /// <summary>
-    /// Sync path — called by the sync <c>Validator.TryValidateObject</c> overloads.
-    /// </summary>
-    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
-    {
-        User user = (User)validationContext.ObjectInstance;
-        int? delay = user.Delay;
-
-        if (delay is null)
-        {
-            return new ValidationResult("Delay is not configured.");
-        }
-
-        Thread.Sleep((int)delay);
-
-        return ValidationResult.Success;
-    }
-}
-
-/// <summary>
-/// Represents a user with a name.
-/// </summary>
-public class User
-{
-    /// <summary>
-    /// Gets or sets the name of the user.
-    /// </summary>
-    [Required]
-    [IsValidName]
-    public string? Name { get; set; }
-
-    /// <summary>
-    /// Gets or sets the delay for the user.
-    /// </summary>
-    [Required]
-    public int? Delay { get; set; }
-}
```

---

## Added: `ValidationClasses/IsValidNameAttribute.cs`

Moved from `Model.cs`, unchanged logic. Namespace changed to `BasicAsyncSample.ValidationClasses`.

```diff
+// Licensed to the .NET Foundation under one or more agreements.
+// The .NET Foundation licenses this file to you under the MIT license.
+
+using System.ComponentModel.DataAnnotations;
+using System.Threading;
+using System.Threading.Tasks;
+using BasicAsyncSample.EntityClasses;
+
+namespace BasicAsyncSample.ValidationClasses;
+
+/// <summary>
+/// Scenario 1 — Reusable property attribute with sync fallback.
+/// The async path uses <see cref="Task.Delay(int, CancellationToken)"/> (non-blocking).
+/// The sync path uses <see cref="Thread.Sleep(int)"/> (blocks the calling thread).
+/// </summary>
+public class IsValidNameAttribute : AsyncValidationAttribute
+{
+    protected override async ValueTask<ValidationResult?> IsValidAsync(
+        object? value,
+        ValidationContext validationContext,
+        CancellationToken cancellationToken)
+    {
+        User user = (User)validationContext.ObjectInstance;
+        int? delay = user.Delay;
+
+        if (delay is null)
+        {
+            return new ValidationResult("Delay is not configured.");
+        }
+
+        await Task.Delay((int)delay, cancellationToken);
+
+        return ValidationResult.Success;
+    }
+
+    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
+    {
+        User user = (User)validationContext.ObjectInstance;
+        int? delay = user.Delay;
+
+        if (delay is null)
+        {
+            return new ValidationResult("Delay is not configured.");
+        }
+
+        Thread.Sleep((int)delay);
+
+        return ValidationResult.Success;
+    }
+}
```

---

## Added: `ValidationClasses/AsyncOnlyEmailDomainAttribute.cs`

New — Scenario 2 (async-only, no sync fallback).

```diff
+// Licensed to the .NET Foundation under one or more agreements.
+// The .NET Foundation licenses this file to you under the MIT license.
+
+using System;
+using System.ComponentModel.DataAnnotations;
+using System.Threading;
+using System.Threading.Tasks;
+using BasicAsyncSample.EntityClasses;
+
+namespace BasicAsyncSample.ValidationClasses;
+
+/// <summary>
+/// Scenario 2 — Async-only property attribute. Calling the sync Validator on a model
+/// that uses this attribute will throw NotSupportedException, enforcing the async path.
+/// </summary>
+public class AsyncOnlyEmailDomainAttribute : AsyncValidationAttribute
+{
+    private readonly string _requiredDomain;
+
+    public AsyncOnlyEmailDomainAttribute(string requiredDomain)
+        : base($"Email must belong to the '{requiredDomain}' domain.")
+    {
+        _requiredDomain = requiredDomain;
+    }
+
+    protected override async ValueTask<ValidationResult?> IsValidAsync(
+        object? value,
+        ValidationContext validationContext,
+        CancellationToken cancellationToken)
+    {
+        if (value is not string email)
+        {
+            return new ValidationResult("A valid email string is required.");
+        }
+
+        User user = (User)validationContext.ObjectInstance;
+        int? delay = user.Delay;
+
+        if (delay is null)
+        {
+            return new ValidationResult("Delay is not configured.");
+        }
+
+        await Task.Delay((int)delay, cancellationToken);
+
+        if (!email.EndsWith($"@{_requiredDomain}", StringComparison.OrdinalIgnoreCase))
+        {
+            return new ValidationResult(
+                $"'{email}' is not in the '{_requiredDomain}' domain.",
+                new[] { validationContext.MemberName! });
+        }
+
+        return ValidationResult.Success;
+    }
+
+    // IsValid is intentionally NOT overridden.
+    // The base AsyncValidationAttribute.IsValid throws NotSupportedException,
+    // which is the desired behavior for async-only attributes.
+}
```

---

## Added: `ValidationClasses/AsyncDateRangeValidAttribute.cs`

New — Scenario 3 (reusable class-level attribute with sync fallback).

```diff
+// Licensed to the .NET Foundation under one or more agreements.
+// The .NET Foundation licenses this file to you under the MIT license.
+
+using System;
+using System.ComponentModel.DataAnnotations;
+using System.Threading;
+using System.Threading.Tasks;
+using BasicAsyncSample.EntityClasses;
+
+namespace BasicAsyncSample.ValidationClasses;
+
+/// <summary>
+/// Scenario 3 — Reusable class-level attribute that validates cross-property constraints.
+/// Applied to the class, not to individual properties.
+/// </summary>
+[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
+public class AsyncDateRangeValidAttribute : AsyncValidationAttribute
+{
+    private readonly string _startProperty;
+    private readonly string _endProperty;
+
+    public AsyncDateRangeValidAttribute(string startProperty, string endProperty)
+    {
+        _startProperty = startProperty;
+        _endProperty = endProperty;
+    }
+
+    protected override async ValueTask<ValidationResult?> IsValidAsync(
+        object? value,
+        ValidationContext validationContext,
+        CancellationToken cancellationToken)
+    {
+        Event ev = (Event)validationContext.ObjectInstance;
+        int? delay = ev.Delay;
+
+        if (delay is null)
+        {
+            return new ValidationResult("Delay is not configured.");
+        }
+
+        await Task.Delay((int)delay, cancellationToken);
+
+        Type type = validationContext.ObjectType;
+        object instance = validationContext.ObjectInstance;
+
+        DateTime? start = (DateTime?)type.GetProperty(_startProperty)?.GetValue(instance);
+        DateTime? end = (DateTime?)type.GetProperty(_endProperty)?.GetValue(instance);
+
+        if (start.HasValue && end.HasValue && start.Value >= end.Value)
+        {
+            return new ValidationResult(
+                $"'{_startProperty}' must be before '{_endProperty}'.",
+                new[] { _startProperty, _endProperty });
+        }
+
+        return ValidationResult.Success;
+    }
+
+    protected override ValidationResult? IsValid(
+        object? value, ValidationContext validationContext)
+    {
+        Event ev = (Event)validationContext.ObjectInstance;
+        int? delay = ev.Delay;
+
+        if (delay is null)
+        {
+            return new ValidationResult("Delay is not configured.");
+        }
+
+        Thread.Sleep((int)delay);
+
+        Type type = validationContext.ObjectType;
+        object instance = validationContext.ObjectInstance;
+
+        DateTime? start = (DateTime?)type.GetProperty(_startProperty)?.GetValue(instance);
+        DateTime? end = (DateTime?)type.GetProperty(_endProperty)?.GetValue(instance);
+
+        if (start.HasValue && end.HasValue && start.Value >= end.Value)
+        {
+            return new ValidationResult(
+                $"'{_startProperty}' must be before '{_endProperty}'.",
+                new[] { _startProperty, _endProperty });
+        }
+
+        return ValidationResult.Success;
+    }
+}
```

---

## Added: `EntityClasses/User.cs`

Moved from `Model.cs`. Added `Email` property with `AsyncOnlyEmailDomain` attribute.

```diff
+// Licensed to the .NET Foundation under one or more agreements.
+// The .NET Foundation licenses this file to you under the MIT license.
+
+using System.ComponentModel.DataAnnotations;
+using BasicAsyncSample.ValidationClasses;
+
+namespace BasicAsyncSample.EntityClasses;
+
+/// <summary>
+/// Used by Scenarios 1 and 2 — demonstrates reusable property-level attributes.
+/// </summary>
+public class User
+{
+    [Required]
+    [IsValidName]
+    public string? Name { get; set; }
+
+    [Required]
+    [AsyncOnlyEmailDomain("contoso.com")]
+    public string? Email { get; set; }
+
+    [Required]
+    public int? Delay { get; set; }
+}
```

---

## Added: `EntityClasses/Event.cs`

New — Scenario 3 (reusable class-level attribute).

```diff
+// Licensed to the .NET Foundation under one or more agreements.
+// The .NET Foundation licenses this file to you under the MIT license.
+
+using System;
+using System.ComponentModel.DataAnnotations;
+using BasicAsyncSample.ValidationClasses;
+
+namespace BasicAsyncSample.EntityClasses;
+
+/// <summary>
+/// Used by Scenario 3 — demonstrates a reusable class-level async attribute.
+/// </summary>
+[AsyncDateRangeValid(nameof(StartDate), nameof(EndDate))]
+public class Event
+{
+    [Required]
+    public string? Title { get; set; }
+
+    [Required]
+    public DateTime? StartDate { get; set; }
+
+    [Required]
+    public DateTime? EndDate { get; set; }
+
+    [Required]
+    public int? Delay { get; set; }
+}
```

---

## Added: `EntityClasses/Order.cs`

New — Scenario 4 (IAsyncValidatableObject, cross-property).

```diff
+// Licensed to the .NET Foundation under one or more agreements.
+// The .NET Foundation licenses this file to you under the MIT license.
+
+using System.Collections.Generic;
+using System.ComponentModel.DataAnnotations;
+using System.Threading;
+using System.Threading.Tasks;
+
+namespace BasicAsyncSample.EntityClasses;
+
+/// <summary>
+/// Scenario 4 — Non-reusable entity validation via IAsyncValidatableObject.
+/// Cross-property logic lives inside the model — not reusable, but simple and direct.
+/// </summary>
+public class Order : IAsyncValidatableObject
+{
+    [Required]
+    public string? ProductName { get; set; }
+
+    [Required]
+    [Range(1, 10_000)]
+    public int Quantity { get; set; }
+
+    [Required]
+    [Range(0.01, double.MaxValue)]
+    public decimal UnitPrice { get; set; }
+
+    [Required]
+    public int? Delay { get; set; }
+
+    public async ValueTask<IEnumerable<ValidationResult>> ValidateAsync(
+        ValidationContext validationContext,
+        CancellationToken cancellationToken)
+    {
+        var results = new List<ValidationResult>();
+
+        if (Delay is null)
+        {
+            results.Add(new ValidationResult("Delay is not configured."));
+            return results;
+        }
+
+        await Task.Delay((int)Delay, cancellationToken);
+
+        decimal totalCost = Quantity * UnitPrice;
+        if (totalCost > 50_000m)
+        {
+            results.Add(new ValidationResult(
+                $"Total cost ({totalCost:C}) exceeds the $50,000 limit.",
+                new[] { nameof(Quantity), nameof(UnitPrice) }));
+        }
+
+        return results;
+    }
+}
```

---

## Added: `EntityClasses/Profile.cs`

New — Scenario 5 (IAsyncValidatableObject, property-scoped).

```diff
+// Licensed to the .NET Foundation under one or more agreements.
+// The .NET Foundation licenses this file to you under the MIT license.
+
+using System;
+using System.Collections.Generic;
+using System.ComponentModel.DataAnnotations;
+using System.Threading;
+using System.Threading.Tasks;
+
+namespace BasicAsyncSample.EntityClasses;
+
+/// <summary>
+/// Scenario 5 — Non-reusable property-scoped validation via IAsyncValidatableObject.
+/// Each result targets a specific member via MemberNames.
+/// </summary>
+public class Profile : IAsyncValidatableObject
+{
+    [Required]
+    public string? Username { get; set; }
+
+    [Required]
+    public string? Bio { get; set; }
+
+    [Required]
+    public int? Delay { get; set; }
+
+    public async ValueTask<IEnumerable<ValidationResult>> ValidateAsync(
+        ValidationContext validationContext,
+        CancellationToken cancellationToken)
+    {
+        var results = new List<ValidationResult>();
+
+        if (Delay is null)
+        {
+            results.Add(new ValidationResult("Delay is not configured."));
+            return results;
+        }
+
+        // Simulate async uniqueness check for Username
+        await Task.Delay((int)Delay, cancellationToken);
+        if (string.Equals(Username, "admin", StringComparison.OrdinalIgnoreCase))
+        {
+            results.Add(new ValidationResult(
+                "The username 'admin' is reserved.",
+                new[] { nameof(Username) }));
+        }
+
+        // Simulate async content-moderation check for Bio
+        await Task.Delay((int)Delay, cancellationToken);
+        if (Bio is not null && Bio.Length > 200)
+        {
+            results.Add(new ValidationResult(
+                "Bio exceeds the 200-character limit after moderation review.",
+                new[] { nameof(Bio) }));
+        }
+
+        return results;
+    }
+}
```

---

## Changed: `Program.cs`

The timing comparison (parallel async vs sync sequential) was moved from `User` to `Event`,
because `User` now has an async-only attribute (`AsyncOnlyEmailDomain`) that throws
`NotSupportedException` on the sync path. `Event` uses `AsyncDateRangeValid` which
overrides both `IsValidAsync` and `IsValid`, making it safe for both paths.

The original Event validation-failure scenario is kept as a separate block below.

```diff
 // Licensed to the .NET Foundation under one or more agreements.
 // The .NET Foundation licenses this file to you under the MIT license.

-using System;
-using System.Collections.Generic;
-using System.ComponentModel.DataAnnotations;
-using System.Diagnostics;
-using System.Threading.Tasks;
-using BasicAsyncSample;
+using BasicAsyncSample.EntityClasses;
+using System;
+using System.Collections.Generic;
+using System.ComponentModel.DataAnnotations;
+using System.Diagnostics;
+using System.Threading.Tasks;

-const int DelayMs = 1000;
+const int DelayMs = 3000;

-User user1 = new User { Name = "ddd", Delay = DelayMs };
-User user2 = new User { Name = "Mario", Delay = DelayMs };
-User user3 = new User { Name = "Marie", Delay = DelayMs };
-
-// --- Sequential async (one at a time) ---
-var seqResults1 = new List<ValidationResult>();
-var seqResults2 = new List<ValidationResult>();
-var seqResults3 = new List<ValidationResult>();
-
-var sw = Stopwatch.StartNew();
-await Validator.TryValidateObjectAsync(user1, new ValidationContext(user1), seqResults1, true);
-await Validator.TryValidateObjectAsync(user2, new ValidationContext(user2), seqResults2, true);
-await Validator.TryValidateObjectAsync(user3, new ValidationContext(user3), seqResults3, true);
-sw.Stop();
-Console.WriteLine($"Sequential async: {sw.ElapsedMilliseconds}ms  (expected ~{DelayMs * 3}ms)");
-
-// --- Parallel async (all at once) ---
-var parResults1 = new List<ValidationResult>();
-var parResults2 = new List<ValidationResult>();
-var parResults3 = new List<ValidationResult>();
-
-sw.Restart();
-var task1 = Validator.TryValidateObjectAsync(user1, new ValidationContext(user1), parResults1, true).AsTask();
-var task2 = Validator.TryValidateObjectAsync(user2, new ValidationContext(user2), parResults2, true).AsTask();
-var task3 = Validator.TryValidateObjectAsync(user3, new ValidationContext(user3), parResults3, true).AsTask();
-await Task.WhenAll(task1, task2, task3);
-sw.Stop();
-Console.WriteLine($"Parallel async:   {sw.ElapsedMilliseconds}ms  (expected ~{DelayMs}ms)");
-
-// --- Sync (blocking, sequential) ---
-var syncResults1 = new List<ValidationResult>();
-var syncResults2 = new List<ValidationResult>();
-var syncResults3 = new List<ValidationResult>();
-
-sw.Restart();
-Validator.TryValidateObject(user1, new ValidationContext(user1), syncResults1, true);
-Validator.TryValidateObject(user2, new ValidationContext(user2), syncResults2, true);
-Validator.TryValidateObject(user3, new ValidationContext(user3), syncResults3, true);
-sw.Stop();
-Console.WriteLine($"Sync (blocking):  {sw.ElapsedMilliseconds}ms  (expected ~{DelayMs * 3}ms)");
+// [Event] Reusable entity-level attribute — timing comparison (async parallel vs sync sequential)
+// Uses AsyncDateRangeValid which overrides both IsValidAsync and IsValid (sync fallback).
+// Valid events so validation passes and we measure only the simulated I/O delay.
+Console.WriteLine("Reusable entity-level attribute (sync fallback) — timing comparison");
+Event event1 = new Event { Title = "Event A", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), Delay = DelayMs };
+Event event2 = new Event { Title = "Event B", StartDate = new DateTime(2026, 2, 1), EndDate = new DateTime(2026, 11, 30), Delay = DelayMs };
+// Parallel async (all at once)
+var parResults1 = new List<ValidationResult>();
+var parResults2 = new List<ValidationResult>();
+var sw = Stopwatch.StartNew();
+var task1 = Validator.TryValidateObjectAsync(event1, new ValidationContext(event1), parResults1, true).AsTask();
+var task2 = Validator.TryValidateObjectAsync(event2, new ValidationContext(event2), parResults2, true).AsTask();
+await Task.WhenAll(task1, task2);
+sw.Stop();
+Console.WriteLine($"Parallel async:   {sw.ElapsedMilliseconds}ms  (expected ~{DelayMs}ms)");
+// Sync (blocking, sequential)
+var syncResults1 = new List<ValidationResult>();
+var syncResults2 = new List<ValidationResult>();
+sw.Restart();
+Validator.TryValidateObject(event1, new ValidationContext(event1), syncResults1, true);
+Validator.TryValidateObject(event2, new ValidationContext(event2), syncResults2, true);
+sw.Stop();
+Console.WriteLine($"Sync (blocking):  {sw.ElapsedMilliseconds}ms  (expected ~{DelayMs * 2}ms)");
+
+
+// Reusable async property attribute with sync fallback
+// Expected to succeed: Most basic validation context walkthrough
+// and I/O operation simulation
+Console.WriteLine("Reusable property attribute (Valid user)");
+User goodUser = new User{ Name = "Alice", Email = "alice@contoso.com", Delay = DelayMs };
+var results = new List<ValidationResult>();
+bool valid = await Validator.TryValidateObjectAsync(goodUser, new ValidationContext(goodUser), results, true);
+Console.WriteLine($"  User valid (async): {valid}");
+foreach (var r in results)
+    Console.WriteLine($"  Error: {r.ErrorMessage}");
+
+
+// Reusable property attribute, async-only
+// Expected to throw: Bob's gmail domain ≠ contoso.com
+Console.WriteLine("Async-only property attribute");
+User userBadEmail = new User
+{ Name = "Bob",
+  Email = "bob@gmail.com",
+  Delay = DelayMs };
+results = new List<ValidationResult>();
+valid = await Validator.TryValidateObjectAsync(userBadEmail, new ValidationContext(userBadEmail), results, true);
+Console.WriteLine($"  User valid (async): {valid}");
+foreach (var r in results)
+    Console.WriteLine($"  Error: {r.ErrorMessage}");
+
+
+// [Event] Reusable entity-level attribute (cross-field)
+// Expected to throw:  StartDate after EndDate
+// NoTimeTravel example from https://jeffhandley.com/2010-10-10/crossfieldvalidation
+Console.WriteLine("Reusable entity-level attribute");
+Event badEvent = new Event
+{
+    Title = "Launch Party",
+    StartDate = new DateTime(2026, 12, 25),
+    EndDate = new DateTime(2026, 12, 20),
+    Delay = DelayMs
+};
+results = new List<ValidationResult>();
+valid = await Validator.TryValidateObjectAsync(badEvent, new ValidationContext(badEvent), results, true);
+Console.WriteLine($"  Event valid (async): {valid}");
+foreach (var r in results)
+    Console.WriteLine($"  Error: {r.ErrorMessage}");
+
+
+// [Order] Self-validating entity via IAsyncValidatableObject (cross-field)
+// Expected to throw: Total cost $100k > $50k limit
+Console.WriteLine("\nIAsyncValidatableObject (cross-property)");
+Order bigOrder = new Order
+{
+    ProductName = "Widget",
+    Quantity = 10_000,
+    UnitPrice = 10m,
+    Delay = DelayMs
+};
+results = new List<ValidationResult>();
+valid = await Validator.TryValidateObjectAsync(bigOrder, new ValidationContext(bigOrder), results, true);
+Console.WriteLine($"  Order valid (async): {valid}");
+foreach (var r in results)
+    Console.WriteLine($"  Error: {r.ErrorMessage}");
+
+
+// [Profile] Self-validating entity via IAsyncValidatableObject (property-scoped)
+// Expected to Throw: "admin" reserved + bio too long
+Console.WriteLine("\nIAsyncValidatableObject (property-scoped)");
+var profile = new Profile
+{
+    Username = "admin",
+    Bio = new string('x', 201),
+    Delay = DelayMs
+};
+results = new List<ValidationResult>();
+valid = await Validator.TryValidateObjectAsync(profile, new ValidationContext(profile), results, true);
+Console.WriteLine($"  Profile valid (async): {valid}");
+foreach (var r in results)
+    Console.WriteLine($"  Error: {r.ErrorMessage}  [Members: {string.Join(", ", r.MemberNames)}]");
```

---

## Key Change: Timing Comparison Moved from `User` to `Event`

The original sample used `User` for the parallel-async-vs-sync-sequential timing comparison.
After adding `AsyncOnlyEmailDomain` (an async-only attribute) to `User.Email`, the sync
`Validator.TryValidateObject` throws `NotSupportedException` on `User`.

The timing comparison was moved to `Event`, which uses `AsyncDateRangeValid` — an attribute
that overrides **both** `IsValidAsync` and `IsValid`, making it safe for both async and
sync validation paths. The original `Event` validation-failure scenario (bad dates) is kept
as a separate block below the timing comparison.

### Run output

```
Reusable entity-level attribute (sync fallback) — timing comparison
Parallel async:   3100ms  (expected ~3000ms)
Sync (blocking):  6025ms  (expected ~6000ms)
Reusable property attribute (Valid user)
  User valid (async): True
Async-only property attribute
  User valid (async): False
  Error: 'bob@gmail.com' is not in the 'contoso.com' domain.
Reusable entity-level attribute
  Event valid (async): False
  Error: 'StartDate' must be before 'EndDate'.
IAsyncValidatableObject (cross-property)
  Order valid (async): False
  Error: Total cost ($100,000.00) exceeds the $50,000 limit.
IAsyncValidatableObject (property-scoped)
  Profile valid (async): False
  Error: The username 'admin' is reserved.  [Members: Username]
  Error: Bio exceeds the 200-character limit after moderation review.  [Members: Bio]
```
