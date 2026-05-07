// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SharedModels.EntityClasses;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using System.Xml.Linq;

const int DelayMs = 3000;

// Reusable async property attribute with sync fallback
// Expected to succeed: Most basic validation context walkthrough
// and I/O operation simulation
Console.WriteLine("Reusable property attribute (Valid user)");
User goodUser = new User { Name = "Alice", Email = "alice@contoso.com", Delay = DelayMs };
var results = new List<ValidationResult>();
bool valid = await Validator.TryValidateObjectAsync(goodUser, new ValidationContext(goodUser), results, true);
Console.WriteLine($"  User valid (async): {valid}");
foreach (var r in results)
    Console.WriteLine($"  Error: {r.ErrorMessage}");



// [Event] Reusable entity-level attribute — timing comparison (async parallel vs sync sequential)
// Uses AsyncDateRangeValid which overrides both IsValidAsync and IsValid (sync fallback).
// Valid events so validation passes and we measure only the simulated I/O delay.
Console.WriteLine("\nReusable entity-level attribute (sync fallback) — timing comparison");
Event event1 = new Event { Title = "Event A", StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), Delay = DelayMs };
Event event2 = new Event { Title = "Event B", StartDate = new DateTime(2026, 2, 1), EndDate = new DateTime(2026, 11, 30), Delay = DelayMs };
// Parallel async (all at once)
var parResults1 = new List<ValidationResult>();
var parResults2 = new List<ValidationResult>();
var sw = Stopwatch.StartNew();
var task1 = Validator.TryValidateObjectAsync(event1, new ValidationContext(event1), parResults1, true).AsTask();
var task2 = Validator.TryValidateObjectAsync(event2, new ValidationContext(event2), parResults2, true).AsTask();
await Task.WhenAll(task1, task2);
sw.Stop();
Console.WriteLine($"Parallel async:   {sw.ElapsedMilliseconds}ms  (expected ~{DelayMs}ms)");
// Sync (blocking, sequential)
var syncResults1 = new List<ValidationResult>();
var syncResults2 = new List<ValidationResult>();
sw.Restart();
Validator.TryValidateObject(event1, new ValidationContext(event1), syncResults1, true);
Validator.TryValidateObject(event2, new ValidationContext(event2), syncResults2, true);
sw.Stop();
Console.WriteLine($"Sync (blocking):  {sw.ElapsedMilliseconds}ms  (expected ~{DelayMs * 2}ms)");



// Reusable property attribute, async-only
// Expected to throw: Bob's gmail domain ≠ contoso.com
Console.WriteLine("\nAsync-only property attribute");
User userBadEmail = new User
{ Name = "Bob",
  Email = "bob@gmail.com",
  Delay = DelayMs };
results = new List<ValidationResult>();
valid = await Validator.TryValidateObjectAsync(userBadEmail, new ValidationContext(userBadEmail), results, true);
Console.WriteLine($"  User valid (async): {valid}");
foreach (var r in results)
    Console.WriteLine($"  Error: {r.ErrorMessage}");


// [Event] Reusable entity-level attribute (cross-field)
// Expected to throw:  StartDate after EndDate
// NoTimeTravel example from https://jeffhandley.com/2010-10-10/crossfieldvalidation
Console.WriteLine("\nReusable entity-level attribute");
Event badEvent = new Event
{
    Title = "Launch Party",
    StartDate = new DateTime(2026, 12, 25),
    EndDate = new DateTime(2026, 12, 20),
    Delay = DelayMs
};
results = new List<ValidationResult>();
valid = await Validator.TryValidateObjectAsync(badEvent, new ValidationContext(badEvent), results, true);
Console.WriteLine($"  Event valid (async): {valid}");
foreach (var r in results)
    Console.WriteLine($"  Error: {r.ErrorMessage}");



// [Order] Self-validating entity via IAsyncValidatableObject (cross-field)
// Expected to throw: Total cost $100k > $50k limit
Console.WriteLine("\nIAsyncValidatableObject (cross-property)");
Order bigOrder = new Order
{
    ProductName = "Widget",
    Quantity = 10_000,
    UnitPrice = 10m,
    Delay = DelayMs
};
results = new List<ValidationResult>();
valid = await Validator.TryValidateObjectAsync(bigOrder, new ValidationContext(bigOrder), results, true);
Console.WriteLine($"  Order valid (async): {valid}");
foreach (var r in results)
    Console.WriteLine($"  Error: {r.ErrorMessage}");


// [Profile] Self-validating entity via IAsyncValidatableObject (property-scoped)
// Expected to Throw: "admin" reserved + bio too long
Console.WriteLine("\nIAsyncValidatableObject (property-scoped)");
var profile = new Profile
{
    Username = "admin",
    Bio = new string('x', 201),
    Delay = DelayMs
};
results = new List<ValidationResult>();
valid = await Validator.TryValidateObjectAsync(profile, new ValidationContext(profile), results, true);
Console.WriteLine($"  Profile valid (async): {valid}");
foreach (var r in results)
    Console.WriteLine($"  Error: {r.ErrorMessage}  [Members: {string.Join(", ", r.MemberNames)}]");
