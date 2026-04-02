using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(CountDigitsULongBench).Assembly).Run(args);

// ──────────────────────────────────────────────────────────────────────
// ULONG benchmark: fmtlib-style CountDigits(ulong) vs ulong.Log10 + 1
// ──────────────────────────────────────────────────────────────────────

[MemoryDiagnoser]
public class CountDigitsULongBench
{
    // PowersOf10 table for ulong.Log10
    private static readonly ulong[] PowersOf10_ULong =
    [
        1, 10, 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000,
        100_000_000, 1_000_000_000, 10_000_000_000, 100_000_000_000,
        1_000_000_000_000, 10_000_000_000_000, 100_000_000_000_000,
        1_000_000_000_000_000, 10_000_000_000_000_000,
        100_000_000_000_000_000, 1_000_000_000_000_000_000,
        10_000_000_000_000_000_000, ulong.MaxValue
    ];

    // fmtlib-style Log2-to-pow10 map (64 entries, from FormattingHelpers)
    private static readonly byte[] Log2ToPow10 =
    [
        1,  1,  1,  2,  2,  2,  3,  3,  3,  4,  4,  4,  4,  5,  5,  5,
        6,  6,  6,  7,  7,  7,  7,  8,  8,  8,  9,  9,  9,  10, 10, 10,
        10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 13, 14, 14, 14, 15, 15,
        15, 16, 16, 16, 16, 17, 17, 17, 18, 18, 18, 19, 19, 19, 19, 20
    ];

    // Powers of 10 for fmtlib-style lookup (from FormattingHelpers)
    private static readonly ulong[] FmtlibPowersOf10 =
    [
        0, // unused entry to avoid needing to subtract
        0,
        10,
        100,
        1000,
        10000,
        100000,
        1000000,
        10000000,
        100000000,
        1000000000,
        10000000000,
        100000000000,
        1000000000000,
        10000000000000,
        100000000000000,
        1000000000000000,
        10000000000000000,
        100000000000000000,
        1000000000000000000,
        10000000000000000000,
    ];

    private ulong[] _values = default!;

    [Params("Small_1_999", "Medium_1M_1B", "Large_1e15_Max", "Mixed")]
    public string Distribution { get; set; } = default!;

    [GlobalSetup]
    public void Setup()
    {
        const int count = 1024;
        _values = new ulong[count];
        var rng = new Random(42);

        for (int i = 0; i < count; i++)
        {
            _values[i] = Distribution switch
            {
                "Small_1_999" => (ulong)rng.Next(1, 1000),
                "Medium_1M_1B" => (ulong)rng.Next(1_000_000, 1_000_000_000),
                "Large_1e15_Max" => ((ulong)(uint)(rng.NextDouble() * uint.MaxValue) << 32)
                                 | (uint)(rng.NextDouble() * uint.MaxValue),
                "Mixed" => (ulong)(rng.NextDouble() * ulong.MaxValue) | 1,
                _ => 1
            };
        }
    }

    [Benchmark(Baseline = true)]
    public long Fmtlib_CountDigits()
    {
        long sum = 0;
        ulong[] values = _values;
        for (int i = 0; i < values.Length; i++)
            sum += CountDigits_Fmtlib(values[i]);
        return sum;
    }

    [Benchmark]
    public long Log10Plus1_ULong()
    {
        long sum = 0;
        ulong[] values = _values;
        for (int i = 0; i < values.Length; i++)
            sum += CountDigits_Log10(values[i]);
        return sum;
    }

    /// <summary>
    /// fmtlib-style: Log2 → approximate digit count via 64-byte table,
    /// then correct via powers-of-10 comparison.
    /// Mirrors FormattingHelpers.CountDigits(ulong).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigits_Fmtlib(ulong value)
    {
        int elementOffset = Log2ToPow10[(int)BitOperations.Log2(value | 1)];
        ulong powerOf10 = FmtlibPowersOf10[elementOffset];
        return elementOffset - (value < powerOf10 ? 1 : 0);
    }

    /// <summary>
    /// ulong.Log10(value) + 1 replacement.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigits_Log10(ulong value)
    {
        return (int)Log10_ULong(value) + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Log10_ULong(ulong value)
    {
        value |= 1;
        uint log2 = (uint)BitOperations.Log2(value) + 1;
        uint approx = (log2 * 1233) >> 12;
        return value < PowersOf10_ULong[(int)approx] ? approx - 1 : approx;
    }
}

// ──────────────────────────────────────────────────────────────────────
// UINT128 benchmark: FormattingHelpers.CountDigits(UInt128) vs
// UInt128.Log10 + 1
// ──────────────────────────────────────────────────────────────────────

[MemoryDiagnoser]
public class CountDigitsUInt128Bench
{
    // Powers of 10 for UInt128.Log10 (39 entries)
    private static readonly UInt128[] PowersOf10_UInt128 =
    [
        1,
        10,
        100,
        1000,
        10000,
        100000,
        1000000,
        10000000,
        100000000,
        1000000000,
        10000000000,
        100000000000,
        1000000000000,
        10000000000000,
        100000000000000,
        1000000000000000,
        10000000000000000,
        100000000000000000,
        1000000000000000000,
        10000000000000000000,
        UInt128.Parse("100000000000000000000"),           // 1e20
        UInt128.Parse("1000000000000000000000"),         // 1e21
        UInt128.Parse("10000000000000000000000"),        // 1e22
        UInt128.Parse("100000000000000000000000"),       // 1e23
        UInt128.Parse("1000000000000000000000000"),      // 1e24
        UInt128.Parse("10000000000000000000000000"),     // 1e25
        UInt128.Parse("100000000000000000000000000"),    // 1e26
        UInt128.Parse("1000000000000000000000000000"),   // 1e27
        UInt128.Parse("10000000000000000000000000000"),  // 1e28
        UInt128.Parse("100000000000000000000000000000"), // 1e29
        UInt128.Parse("1000000000000000000000000000000"),// 1e30
        UInt128.Parse("10000000000000000000000000000000"),
        UInt128.Parse("100000000000000000000000000000000"),
        UInt128.Parse("1000000000000000000000000000000000"),
        UInt128.Parse("10000000000000000000000000000000000"),
        UInt128.Parse("100000000000000000000000000000000000"),
        UInt128.Parse("1000000000000000000000000000000000000"),
        UInt128.Parse("10000000000000000000000000000000000000"),
        UInt128.Parse("100000000000000000000000000000000000000"),
        UInt128.MaxValue
    ];

    // fmtlib-style tables for ulong (used by CountDigits(UInt128) delegation)
    private static readonly byte[] Log2ToPow10 =
    [
        1,  1,  1,  2,  2,  2,  3,  3,  3,  4,  4,  4,  4,  5,  5,  5,
        6,  6,  6,  7,  7,  7,  7,  8,  8,  8,  9,  9,  9,  10, 10, 10,
        10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 13, 14, 14, 14, 15, 15,
        15, 16, 16, 16, 16, 17, 17, 17, 18, 18, 18, 19, 19, 19, 19, 20
    ];

    private static readonly ulong[] FmtlibPowersOf10 =
    [
        0, 0, 10, 100, 1000, 10000, 100000, 1000000, 10000000,
        100000000, 1000000000, 10000000000, 100000000000,
        1000000000000, 10000000000000, 100000000000000,
        1000000000000000, 10000000000000000, 100000000000000000,
        1000000000000000000, 10000000000000000000,
    ];

    private UInt128[] _values = default!;

    [Params("Small_ulong_range", "Large_full_range")]
    public string Distribution { get; set; } = default!;

    [GlobalSetup]
    public void Setup()
    {
        const int count = 1024;
        _values = new UInt128[count];
        var rng = new Random(42);

        for (int i = 0; i < count; i++)
        {
            if (Distribution == "Small_ulong_range")
            {
                // Fits in ulong — exercises the fast path of CountDigits(UInt128)
                _values[i] = (UInt128)(ulong)(rng.NextDouble() * ulong.MaxValue) | 1;
            }
            else
            {
                // Full UInt128 range — exercises the slow path
                ulong hi = (ulong)(rng.NextDouble() * ulong.MaxValue);
                ulong lo = (ulong)(rng.NextDouble() * ulong.MaxValue);
                _values[i] = new UInt128(hi, lo) | 1;
            }
        }
    }

    [Benchmark(Baseline = true)]
    public long FormattingHelpers_CountDigits()
    {
        long sum = 0;
        UInt128[] values = _values;
        for (int i = 0; i < values.Length; i++)
            sum += CountDigits_FormattingHelpers(values[i]);
        return sum;
    }

    [Benchmark]
    public long Log10Plus1_UInt128()
    {
        long sum = 0;
        UInt128[] values = _values;
        for (int i = 0; i < values.Length; i++)
            sum += CountDigits_Log10(values[i]);
        return sum;
    }

    /// <summary>
    /// Mirrors FormattingHelpers.CountDigits(UInt128).
    /// Delegates to ulong CountDigits when upper == 0, otherwise handles large values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigits_FormattingHelpers(UInt128 value)
    {
        ulong upper = (ulong)(value >> 64);

        if (upper == 0)
        {
            return CountDigits_Fmtlib_ULong((ulong)value);
        }

        int digits = 20;

        if (upper > 5)
        {
            value /= new UInt128(0x5, 0x6BC7_5E2D_6310_0000); // /= 1e20
            digits += CountDigits_Fmtlib_ULong((ulong)value);
        }
        else if ((upper == 5) && ((ulong)value >= 0x6BC75E2D63100000))
        {
            digits++;
        }

        return digits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigits_Fmtlib_ULong(ulong value)
    {
        int elementOffset = Log2ToPow10[(int)BitOperations.Log2(value | 1)];
        ulong powerOf10 = FmtlibPowersOf10[elementOffset];
        return elementOffset - (value < powerOf10 ? 1 : 0);
    }

    /// <summary>
    /// UInt128.Log10(value) + 1 replacement.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigits_Log10(UInt128 value)
    {
        return (int)Log10_UInt128(value) + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt128 Log10_UInt128(UInt128 value)
    {
        value |= (UInt128)1;
        uint log2 = (uint)UInt128.Log2(value) + 1;
        uint approx = (log2 * 1233) >> 12;
        return value < PowersOf10_UInt128[(int)approx] ? approx - 1 : approx;
    }
}
