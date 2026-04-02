using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(CountDigitsVsLog10Bench).Assembly).Run(args);

/// <summary>
/// Compares CountDigits implementations against Log10-based equivalents.
///
/// Two CountDigits implementations exist in the codebase:
/// 1. NativeAotNameMangler: Lemire's algorithm (Log2-based lookup table)
/// 2. TarHeader.Write: Simple divide-by-10 loop
///
/// The proposed replacement: int.Log10(value) + 1 (or uint.Log10(value) + 1)
/// which uses a Log2-based approximation with powers-of-10 correction.
/// </summary>
[MemoryDiagnoser]
public class CountDigitsVsLog10Bench
{
    // PowersOf10 lookup table used by uint.Log10
    private static readonly uint[] PowersOf10Uint =
    [
        1, 10, 100, 1000, 10000, 100000, 1000000, 10000000,
        100000000, 1000000000, uint.MaxValue
    ];

    // Lemire's table used by NativeAotNameMangler.CountDigits
    private static readonly long[] LemireTable =
    [
        4294967296, 8589934582, 8589934582, 8589934582, 12884901788,
        12884901788, 12884901788, 17179868184, 17179868184, 17179868184,
        21474826480, 21474826480, 21474826480, 21474826480, 25769703776,
        25769703776, 25769703776, 30063771072, 30063771072, 30063771072,
        34349738368, 34349738368, 34349738368, 34349738368, 38554705664,
        38554705664, 38554705664, 41949672960, 41949672960, 41949672960,
        42949672960, 42949672960,
    ];

    private uint[] _uintValues = default!;
    private int[] _intValues = default!;

    [Params("Small_1_9", "Medium_100_9999", "Large_1M_1B", "Mixed")]
    public string Distribution { get; set; } = default!;

    [GlobalSetup]
    public void Setup()
    {
        const int count = 1024;
        _uintValues = new uint[count];
        _intValues = new int[count];
        var rng = new Random(42);

        for (int i = 0; i < count; i++)
        {
            uint v = Distribution switch
            {
                "Small_1_9" => (uint)rng.Next(1, 10),
                "Medium_100_9999" => (uint)rng.Next(100, 10000),
                "Large_1M_1B" => (uint)rng.Next(1_000_000, 1_000_000_000),
                "Mixed" => (uint)rng.Next(1, int.MaxValue),
                _ => 1
            };
            _uintValues[i] = v;
            _intValues[i] = (int)v;
        }
    }

    // ========================================================
    // NativeAotNameMangler scenario: CountDigits(uint) variants
    // ========================================================

    [Benchmark(Baseline = true)]
    public int Lemire_CountDigits()
    {
        int sum = 0;
        uint[] values = _uintValues;
        for (int i = 0; i < values.Length; i++)
        {
            sum += CountDigits_Lemire(values[i]);
        }
        return sum;
    }

    [Benchmark]
    public int Log10Plus1_UInt()
    {
        int sum = 0;
        uint[] values = _uintValues;
        for (int i = 0; i < values.Length; i++)
        {
            sum += CountDigits_Log10(values[i]);
        }
        return sum;
    }

    // ========================================================
    // TarHeader.Write scenario: CountDigits(int) variants
    // ========================================================

    [Benchmark]
    public int DivideLoop_CountDigits()
    {
        int sum = 0;
        int[] values = _intValues;
        for (int i = 0; i < values.Length; i++)
        {
            sum += CountDigits_DivideLoop(values[i]);
        }
        return sum;
    }

    [Benchmark]
    public int Log10Plus1_Int()
    {
        int sum = 0;
        int[] values = _intValues;
        for (int i = 0; i < values.Length; i++)
        {
            sum += CountDigits_Log10Int(values[i]);
        }
        return sum;
    }

    // ========================================================
    // Implementation: Lemire's algorithm (NativeAotNameMangler)
    // ========================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigits_Lemire(uint value)
    {
        long tableValue = LemireTable[(int)BitOperations.Log2(value | 1)];
        return (int)((value + tableValue) >> 32);
    }

    // ========================================================
    // Implementation: Divide-by-10 loop (TarHeader.Write)
    // ========================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigits_DivideLoop(int value)
    {
        int digits = 1;
        while (true)
        {
            value /= 10;
            if (value == 0) break;
            digits++;
        }
        return digits;
    }

    // ========================================================
    // Implementation: Log10-based replacement for uint
    // ========================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigits_Log10(uint value)
    {
        return (int)Log10_UInt(value) + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Log10_UInt(uint value)
    {
        value |= 1;
        uint log2 = (uint)BitOperations.Log2(value) + 1;
        uint approx = (log2 * 1233) >> 12;
        return value < PowersOf10Uint[(int)approx] ? approx - 1 : approx;
    }

    // ========================================================
    // Implementation: Log10-based replacement for int
    // ========================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDigits_Log10Int(int value)
    {
        return (int)Log10_UInt((uint)value) + 1;
    }
}
