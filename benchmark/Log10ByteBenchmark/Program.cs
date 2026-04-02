using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Log10ByteBench).Assembly).Run(args);

/// <summary>
/// Compares two approaches for computing Log10 on byte values:
/// 1. Current: delegates to uint.Log10 which uses Log2 + lookup table
/// 2. Proposed: simple if/else chain (byte only has 3 possible results: 0, 1, 2)
/// </summary>
[MemoryDiagnoser]
public class Log10ByteBench
{
    // PowersOf10 lookup table used by the current uint.Log10 implementation
    private static readonly uint[] PowersOf10 =
    [
        1, 10, 100, 1000, 10000, 100000, 1000000, 10000000,
        100000000, 1000000000, uint.MaxValue
    ];

    private byte[] _testValues = default!;

    [Params("Small_0_9", "Medium_10_99", "Large_100_255", "Mixed")]
    public string Distribution { get; set; } = default!;

    [GlobalSetup]
    public void Setup()
    {
        const int count = 1024;
        _testValues = new byte[count];
        var rng = new Random(42);

        for (int i = 0; i < count; i++)
        {
            _testValues[i] = Distribution switch
            {
                "Small_0_9" => (byte)rng.Next(0, 10),
                "Medium_10_99" => (byte)rng.Next(10, 100),
                "Large_100_255" => (byte)rng.Next(100, 256),
                "Mixed" => (byte)rng.Next(0, 256),
                _ => 0
            };
        }
    }

    [Benchmark(Baseline = true)]
    public int Current_UIntLog10()
    {
        int sum = 0;
        byte[] values = _testValues;
        for (int i = 0; i < values.Length; i++)
        {
            sum += Log10_Current(values[i]);
        }
        return sum;
    }

    [Benchmark]
    public int Proposed_IfElseChain()
    {
        int sum = 0;
        byte[] values = _testValues;
        for (int i = 0; i < values.Length; i++)
        {
            sum += Log10_IfElse(values[i]);
        }
        return sum;
    }

    /// <summary>
    /// Current implementation: (byte)uint.Log10(value)
    /// Inlined uint.Log10 algorithm: Log2-based approximation + table correction
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Log10_Current(byte value)
    {
        uint v = (uint)value | 1u;
        uint log2 = (uint)BitOperations.Log2(v) + 1;
        uint approx = (log2 * 1233) >> 12;
        return (byte)(v < PowersOf10[(int)approx] ? approx - 1 : approx);
    }

    /// <summary>
    /// Proposed: simple if/else chain.
    /// Byte values are 0-255, so only 3 possible results.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Log10_IfElse(byte value)
    {
        if (value < 10) return 0;
        else if (value < 100) return 1;
        else return 2;
    }
}
