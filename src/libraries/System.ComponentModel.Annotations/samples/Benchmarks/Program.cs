// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Standalone BenchmarkDotNet project comparing two return-type patterns for
// IAsyncValidatableObject.ValidateAsync():
//
//   Pattern A: ValueTask<IEnumerable<ValidationResult>>  (batch — collect all, return at once)
//   Pattern B: IAsyncEnumerable<ValidationResult>        (stream — yield results as produced)
//
// Usage:
//   dotnet run -c Release -- --filter *

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(PatternComparisonBenchmarks).Assembly).Run(args);

// =====================================================================================
// Contracts — define both return-type patterns
// =====================================================================================

/// <summary>
/// Pattern A: ValueTask&lt;IEnumerable&lt;ValidationResult&gt;&gt;
/// Batch — all results collected into a list, returned at once.
/// </summary>
public interface IBatchValidatable
{
    ValueTask<IEnumerable<ValidationResult>> ValidateAsync(
        ValidationContext ctx, CancellationToken ct = default);
}

/// <summary>
/// Pattern B: IAsyncEnumerable&lt;ValidationResult&gt;
/// Stream — results yielded as they are produced.
/// </summary>
public interface IStreamValidatable
{
    IAsyncEnumerable<ValidationResult> ValidateAsync(
        ValidationContext ctx, CancellationToken ct = default);
}

// =====================================================================================
// Entity implementations — identical logic, two patterns
// =====================================================================================

#region Order (cross-property validation, single async step)

public sealed class OrderBatch : IBatchValidatable
{
    public string ProductName { get; set; } = "Widget";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public async ValueTask<IEnumerable<ValidationResult>> ValidateAsync(
        ValidationContext ctx, CancellationToken ct = default)
    {
        var results = new List<ValidationResult>();
        await Task.Yield();

        decimal totalCost = Quantity * UnitPrice;
        if (totalCost > 50_000m)
        {
            results.Add(new ValidationResult(
                $"Total cost ({totalCost:C}) exceeds $50,000.",
                new[] { nameof(Quantity), nameof(UnitPrice) }));
        }

        return results;
    }
}

public sealed class OrderStream : IStreamValidatable
{
    public string ProductName { get; set; } = "Widget";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public async IAsyncEnumerable<ValidationResult> ValidateAsync(
        ValidationContext ctx,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();

        decimal totalCost = Quantity * UnitPrice;
        if (totalCost > 50_000m)
        {
            yield return new ValidationResult(
                $"Total cost ({totalCost:C}) exceeds $50,000.",
                new[] { nameof(Quantity), nameof(UnitPrice) });
        }
    }
}

#endregion

#region MoneyTransfer (two sequential async checks, potentially 2 errors)

public sealed class TransferBatch : IBatchValidatable
{
    public string FromAccount { get; set; } = "A";
    public string ToAccount { get; set; } = "B";
    public decimal Amount { get; set; }

    public async ValueTask<IEnumerable<ValidationResult>> ValidateAsync(
        ValidationContext ctx, CancellationToken ct = default)
    {
        var results = new List<ValidationResult>();

        if (FromAccount == ToAccount)
        {
            results.Add(new ValidationResult("Cannot transfer to the same account.",
                new[] { nameof(FromAccount), nameof(ToAccount) }));
        }

        await Task.Yield();
        decimal balance = 500.00m;
        if (Amount > balance)
        {
            results.Add(new ValidationResult(
                $"Insufficient funds. Balance: ${balance:F2}, Transfer: ${Amount:F2}.",
                new[] { nameof(Amount) }));
        }

        return results;
    }
}

public sealed class TransferStream : IStreamValidatable
{
    public string FromAccount { get; set; } = "A";
    public string ToAccount { get; set; } = "B";
    public decimal Amount { get; set; }

    public async IAsyncEnumerable<ValidationResult> ValidateAsync(
        ValidationContext ctx,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (FromAccount == ToAccount)
        {
            yield return new ValidationResult("Cannot transfer to the same account.",
                new[] { nameof(FromAccount), nameof(ToAccount) });
        }

        await Task.Yield();
        decimal balance = 500.00m;
        if (Amount > balance)
        {
            yield return new ValidationResult(
                $"Insufficient funds. Balance: ${balance:F2}, Transfer: ${Amount:F2}.",
                new[] { nameof(Amount) });
        }
    }
}

#endregion

#region Profile (two sequential async checks yielding errors)

public sealed class ProfileBatch : IBatchValidatable
{
    public string Username { get; set; } = "user";
    public string Bio { get; set; } = "short bio";

    public async ValueTask<IEnumerable<ValidationResult>> ValidateAsync(
        ValidationContext ctx, CancellationToken ct = default)
    {
        var results = new List<ValidationResult>();

        await Task.Yield();
        if (string.Equals(Username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            results.Add(new ValidationResult("Username 'admin' is reserved.",
                new[] { nameof(Username) }));
        }

        await Task.Yield();
        if (Bio is not null && Bio.Length > 200)
        {
            results.Add(new ValidationResult("Bio exceeds 200-character limit.",
                new[] { nameof(Bio) }));
        }

        return results;
    }
}

public sealed class ProfileStream : IStreamValidatable
{
    public string Username { get; set; } = "user";
    public string Bio { get; set; } = "short bio";

    public async IAsyncEnumerable<ValidationResult> ValidateAsync(
        ValidationContext ctx,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        if (string.Equals(Username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult("Username 'admin' is reserved.",
                new[] { nameof(Username) });
        }

        await Task.Yield();
        if (Bio is not null && Bio.Length > 200)
        {
            yield return new ValidationResult("Bio exceeds 200-character limit.",
                new[] { nameof(Bio) });
        }
    }
}

#endregion

#region ManyResults (N errors — stress allocation patterns)

public sealed class ManyBatch : IBatchValidatable
{
    public int Count { get; set; }

    public async ValueTask<IEnumerable<ValidationResult>> ValidateAsync(
        ValidationContext ctx, CancellationToken ct = default)
    {
        var results = new List<ValidationResult>(Count);
        for (int i = 0; i < Count; i++)
        {
            await Task.Yield();
            results.Add(new ValidationResult($"Error {i}"));
        }

        return results;
    }
}

public sealed class ManyStream : IStreamValidatable
{
    public int Count { get; set; }

    public async IAsyncEnumerable<ValidationResult> ValidateAsync(
        ValidationContext ctx,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < Count; i++)
        {
            await Task.Yield();
            yield return new ValidationResult($"Error {i}");
        }
    }
}

#endregion

// =====================================================================================
// Consumption helpers — mirror the Validator.cs consumption patterns
// =====================================================================================

public static class Consume
{
    /// <summary>
    /// Mirrors how Validator.cs consumes ValueTask&lt;IEnumerable&lt;ValidationResult&gt;&gt;:
    /// await the task, then iterate the returned collection synchronously.
    /// </summary>
    public static async ValueTask<List<ValidationResult>> Batch(
        IBatchValidatable instance, ValidationContext ctx, CancellationToken ct = default)
    {
        var errors = new List<ValidationResult>();
        IEnumerable<ValidationResult> results = await instance.ValidateAsync(ctx, ct)
            .ConfigureAwait(false);
        if (results is not null)
        {
            foreach (ValidationResult r in results)
            {
                if (r != ValidationResult.Success)
                    errors.Add(r);
            }
        }

        return errors;
    }

    /// <summary>
    /// Mirrors how Validator.cs consumes IAsyncEnumerable&lt;ValidationResult&gt;:
    /// await foreach to stream results one at a time.
    /// </summary>
    public static async ValueTask<List<ValidationResult>> Stream(
        IStreamValidatable instance, ValidationContext ctx, CancellationToken ct = default)
    {
        var errors = new List<ValidationResult>();
        await foreach (ValidationResult r in instance.ValidateAsync(ctx, ct).ConfigureAwait(false))
        {
            if (r != ValidationResult.Success)
                errors.Add(r);
        }

        return errors;
    }
}

// =====================================================================================
// Benchmarks
// =====================================================================================

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[HideColumns("Error", "StdDev", "RatioSD", "Median")]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class PatternComparisonBenchmarks
{
    // --- Order ---
    private OrderBatch _orderBatch_Valid = null!;
    private OrderStream _orderStream_Valid = null!;
    private OrderBatch _orderBatch_Invalid = null!;
    private OrderStream _orderStream_Invalid = null!;

    // --- Transfer ---
    private TransferBatch _transferBatch_Valid = null!;
    private TransferStream _transferStream_Valid = null!;
    private TransferBatch _transferBatch_Invalid = null!;
    private TransferStream _transferStream_Invalid = null!;

    // --- Profile ---
    private ProfileBatch _profileBatch_Valid = null!;
    private ProfileStream _profileStream_Valid = null!;
    private ProfileBatch _profileBatch_Invalid = null!;
    private ProfileStream _profileStream_Invalid = null!;

    [GlobalSetup]
    public void Setup()
    {
        _orderBatch_Valid = new OrderBatch { Quantity = 5, UnitPrice = 10m };
        _orderStream_Valid = new OrderStream { Quantity = 5, UnitPrice = 10m };
        _orderBatch_Invalid = new OrderBatch { Quantity = 10_000, UnitPrice = 10m };
        _orderStream_Invalid = new OrderStream { Quantity = 10_000, UnitPrice = 10m };

        _transferBatch_Valid = new TransferBatch { FromAccount = "A", ToAccount = "B", Amount = 100m };
        _transferStream_Valid = new TransferStream { FromAccount = "A", ToAccount = "B", Amount = 100m };
        _transferBatch_Invalid = new TransferBatch { FromAccount = "A", ToAccount = "A", Amount = 1000m };
        _transferStream_Invalid = new TransferStream { FromAccount = "A", ToAccount = "A", Amount = 1000m };

        _profileBatch_Valid = new ProfileBatch();
        _profileStream_Valid = new ProfileStream();
        _profileBatch_Invalid = new ProfileBatch { Username = "admin", Bio = new string('x', 201) };
        _profileStream_Invalid = new ProfileStream { Username = "admin", Bio = new string('x', 201) };
    }

    // ---------- Order — Valid (no errors, happy path) ----------

    [Benchmark(Baseline = true, Description = "Batch")]
    [BenchmarkCategory("Order_Valid")]
    public ValueTask<List<ValidationResult>> Order_Valid_Batch()
        => Consume.Batch(_orderBatch_Valid, new ValidationContext(_orderBatch_Valid));

    [Benchmark(Description = "Stream")]
    [BenchmarkCategory("Order_Valid")]
    public ValueTask<List<ValidationResult>> Order_Valid_Stream()
        => Consume.Stream(_orderStream_Valid, new ValidationContext(_orderStream_Valid));

    // ---------- Order — Invalid (1 error) ----------

    [Benchmark(Baseline = true, Description = "Batch")]
    [BenchmarkCategory("Order_Invalid")]
    public ValueTask<List<ValidationResult>> Order_Invalid_Batch()
        => Consume.Batch(_orderBatch_Invalid, new ValidationContext(_orderBatch_Invalid));

    [Benchmark(Description = "Stream")]
    [BenchmarkCategory("Order_Invalid")]
    public ValueTask<List<ValidationResult>> Order_Invalid_Stream()
        => Consume.Stream(_orderStream_Invalid, new ValidationContext(_orderStream_Invalid));

    // ---------- Transfer — Valid ----------

    [Benchmark(Baseline = true, Description = "Batch")]
    [BenchmarkCategory("Transfer_Valid")]
    public ValueTask<List<ValidationResult>> Transfer_Valid_Batch()
        => Consume.Batch(_transferBatch_Valid, new ValidationContext(_transferBatch_Valid));

    [Benchmark(Description = "Stream")]
    [BenchmarkCategory("Transfer_Valid")]
    public ValueTask<List<ValidationResult>> Transfer_Valid_Stream()
        => Consume.Stream(_transferStream_Valid, new ValidationContext(_transferStream_Valid));

    // ---------- Transfer — Invalid (2 errors) ----------

    [Benchmark(Baseline = true, Description = "Batch")]
    [BenchmarkCategory("Transfer_Invalid")]
    public ValueTask<List<ValidationResult>> Transfer_Invalid_Batch()
        => Consume.Batch(_transferBatch_Invalid, new ValidationContext(_transferBatch_Invalid));

    [Benchmark(Description = "Stream")]
    [BenchmarkCategory("Transfer_Invalid")]
    public ValueTask<List<ValidationResult>> Transfer_Invalid_Stream()
        => Consume.Stream(_transferStream_Invalid, new ValidationContext(_transferStream_Invalid));

    // ---------- Profile — Valid ----------

    [Benchmark(Baseline = true, Description = "Batch")]
    [BenchmarkCategory("Profile_Valid")]
    public ValueTask<List<ValidationResult>> Profile_Valid_Batch()
        => Consume.Batch(_profileBatch_Valid, new ValidationContext(_profileBatch_Valid));

    [Benchmark(Description = "Stream")]
    [BenchmarkCategory("Profile_Valid")]
    public ValueTask<List<ValidationResult>> Profile_Valid_Stream()
        => Consume.Stream(_profileStream_Valid, new ValidationContext(_profileStream_Valid));

    // ---------- Profile — Invalid (2 errors from sequential async) ----------

    [Benchmark(Baseline = true, Description = "Batch")]
    [BenchmarkCategory("Profile_Invalid")]
    public ValueTask<List<ValidationResult>> Profile_Invalid_Batch()
        => Consume.Batch(_profileBatch_Invalid, new ValidationContext(_profileBatch_Invalid));

    [Benchmark(Description = "Stream")]
    [BenchmarkCategory("Profile_Invalid")]
    public ValueTask<List<ValidationResult>> Profile_Invalid_Stream()
        => Consume.Stream(_profileStream_Invalid, new ValidationContext(_profileStream_Invalid));
}

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[HideColumns("Error", "StdDev", "RatioSD", "Median")]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ManyResultsBenchmarks
{
    private ManyBatch _batch = null!;
    private ManyStream _stream = null!;

    [Params(0, 1, 5, 20, 50)]
    public int N { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _batch = new ManyBatch { Count = N };
        _stream = new ManyStream { Count = N };
    }

    [Benchmark(Baseline = true, Description = "Batch")]
    [BenchmarkCategory("ManyResults")]
    public ValueTask<List<ValidationResult>> Batch()
        => Consume.Batch(_batch, new ValidationContext(new object()));

    [Benchmark(Description = "Stream")]
    [BenchmarkCategory("ManyResults")]
    public ValueTask<List<ValidationResult>> Stream()
        => Consume.Stream(_stream, new ValidationContext(new object()));
}

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[HideColumns("Error", "StdDev", "RatioSD", "Median")]
public class FullPipelineBenchmarks
{
    private OrderBatch _orderBatch = null!;
    private OrderStream _orderStream = null!;
    private TransferBatch _transferBatch = null!;
    private TransferStream _transferStream = null!;
    private ProfileBatch _profileBatch = null!;
    private ProfileStream _profileStream = null!;

    [GlobalSetup]
    public void Setup()
    {
        _orderBatch = new OrderBatch { Quantity = 10_000, UnitPrice = 10m };
        _orderStream = new OrderStream { Quantity = 10_000, UnitPrice = 10m };
        _transferBatch = new TransferBatch { FromAccount = "A", ToAccount = "A", Amount = 1000m };
        _transferStream = new TransferStream { FromAccount = "A", ToAccount = "A", Amount = 1000m };
        _profileBatch = new ProfileBatch { Username = "admin", Bio = new string('x', 201) };
        _profileStream = new ProfileStream { Username = "admin", Bio = new string('x', 201) };
    }

    [Benchmark(Baseline = true, Description = "FullPipeline_Batch")]
    public async ValueTask<int> FullPipeline_Batch()
    {
        int total = 0;
        total += (await Consume.Batch(_orderBatch, new ValidationContext(_orderBatch))).Count;
        total += (await Consume.Batch(_transferBatch, new ValidationContext(_transferBatch))).Count;
        total += (await Consume.Batch(_profileBatch, new ValidationContext(_profileBatch))).Count;

        return total;
    }

    [Benchmark(Description = "FullPipeline_Stream")]
    public async ValueTask<int> FullPipeline_Stream()
    {
        int total = 0;
        total += (await Consume.Stream(_orderStream, new ValidationContext(_orderStream))).Count;
        total += (await Consume.Stream(_transferStream, new ValidationContext(_transferStream))).Count;
        total += (await Consume.Stream(_profileStream, new ValidationContext(_profileStream))).Count;

        return total;
    }
}
