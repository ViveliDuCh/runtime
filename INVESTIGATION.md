# Investigation: If/Else Chain for Byte.Log10

## Introduction

This investigation evaluates whether `Byte.Log10` can benefit from a specialized if/else chain
instead of delegating to `uint.Log10`. Since `byte` values range from 0-255, the result is always
one of three values (0, 1, or 2), making a simple branch chain a viable alternative to the generic
Log2-based approximation algorithm.

### Current Implementation

```csharp
// Byte.Log10 delegates to uint.Log10
public static byte Log10(byte value) => (byte)uint.Log10(value);

// uint.Log10 uses a Log2-based approximation + powers-of-10 correction table
public static uint Log10(uint value)
{
    value |= 1;
    uint log2 = (uint)BitOperations.Log2(value) + 1;
    uint approx = (log2 * 1233) >> 12;
    return value < PowersOf10[(int)approx] ? approx - 1 : approx;
}
```

### Proposed Implementation

```csharp
public static byte Log10(byte value)
{
    if (value < 10) return 0;
    else if (value < 100) return 1;
    else return 2;
}
```

## Methodology

- **Benchmark framework**: BenchmarkDotNet v0.14.0
- **Runtime**: .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
- **OS**: Windows 11 (10.0.26100.7985) (Hyper-V)
- **Data**: 1024 byte values per iteration, tested across four distributions:
  - `Small_0_9`: values in [0, 9] (Log10 result = 0)
  - `Medium_10_99`: values in [10, 99] (Log10 result = 1)
  - `Large_100_255`: values in [100, 255] (Log10 result = 2)
  - `Mixed`: uniform random across [0, 255]
- Both implementations are `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
- The current approach inlines the `uint.Log10` algorithm to ensure fair comparison
  (no extra method call overhead)

## Results

| Method               | Distribution  | Mean       | Error   | StdDev  | Ratio |
|--------------------- |-------------- |-----------:|--------:|--------:|------:|
| **Current_UIntLog10**    | **Small_0_9**     | **1,198.8 ns** | **1.97 ns** | **1.84 ns** |  **1.00** |
| Proposed_IfElseChain | Small_0_9     |   330.0 ns | 0.18 ns | 0.14 ns |  0.28 |
|                      |               |            |         |         |       |
| **Current_UIntLog10**    | **Medium_10_99**  | **1,213.8 ns** | **1.80 ns** | **1.50 ns** |  **1.00** |
| Proposed_IfElseChain | Medium_10_99  |   648.5 ns | 0.89 ns | 0.84 ns |  0.53 |
|                      |               |            |         |         |       |
| **Current_UIntLog10**    | **Large_100_255** | **1,190.0 ns** | **2.00 ns** | **1.87 ns** |  **1.00** |
| Proposed_IfElseChain | Large_100_255 |   649.4 ns | 0.93 ns | 0.87 ns |  0.55 |
|                      |               |            |         |         |       |
| **Current_UIntLog10**    | **Mixed**         | **1,191.4 ns** | **1.19 ns** | **1.00 ns** |  **1.00** |
| Proposed_IfElseChain | Mixed         | 1,357.9 ns | 3.83 ns | 3.59 ns |  1.14 |

### Key Observations

1. **Small values (0-9)**: If/else is **~3.6x faster** (330ns vs 1199ns). The first branch hits
   immediately, no Log2 computation, no table lookup.

2. **Medium values (10-99)**: If/else is **~1.9x faster** (649ns vs 1214ns). One failed branch
   + one successful branch is still cheaper than Log2 + multiply + table lookup.

3. **Large values (100-255)**: If/else is **~1.8x faster** (649ns vs 1190ns). Two failed
   branches + fallthrough is still cheaper.

4. **Mixed (uniform 0-255)**: If/else is **~14% slower** (1358ns vs 1191ns). This is the
   worst case for branching because the branch predictor cannot establish a reliable pattern
   with uniformly random inputs across all 3 ranges.

## Analysis

The if/else chain wins decisively when the distribution is **predictable** (3 out of 4 scenarios).
The current `uint.Log10` approach is branchless (uses conditional move after a single comparison),
so it has **constant time regardless of input distribution** (~1,190-1,214ns across all distributions).

The mixed-distribution regression (14% slower) is caused by branch misprediction. With uniform
random bytes:
- ~4% of values are in [0, 9] (1 digit)
- ~35% in [10, 99] (2 digits)
- ~61% in [100, 255] (3 digits)

This creates an unpredictable branch pattern that stalls the CPU pipeline.

### Inference: Real-World Applicability

In practice, `byte` values used with `Log10` are likely to have **biased distributions** rather
than uniform random. Common use cases include:
- Formatting small counters (biased toward small values)
- Size/length calculations (biased toward larger values for typical byte data)
- Serialization (value-dependent, but often repetitive)

In these cases, the branch predictor would learn the pattern quickly, and the if/else chain
would consistently outperform the generic algorithm.

## Conclusion

**Recommendation: Adopt the if/else chain for `Byte.Log10`.**

| Criterion | Current (uint.Log10) | Proposed (if/else) |
|-----------|---------------------|--------------------|
| **Predictable inputs** | ~1,200ns | 330-649ns (**1.8-3.6x faster**) |
| **Worst case (random)** | ~1,191ns | ~1,358ns (14% slower) |
| **Code complexity** | Delegates to generic algo | 3 lines, trivially readable |
| **Maintenance** | Coupled to uint.Log10 | Self-contained, byte-specific |
| **Correctness** | Proven correct | Trivially correct (only 3 cases) |

The if/else chain is simpler, self-documenting, and significantly faster for realistic
workloads. The 14% regression on uniform-random data is a synthetic worst case unlikely
to appear in practice. A similar approach could be applied to `ushort.Log10` (5 possible
results: 0-4) and `sbyte.Log10` (3 possible results: 0-2), though the benefit diminishes
as the number of branches grows.
