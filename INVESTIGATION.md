# Investigation: Can CountDigits Usages Be Replaced with Log10?

## Introduction

With the addition of `IBinaryInteger<TSelf>.Log10()` to the runtime, two existing `CountDigits`
helper methods in the codebase could potentially be consolidated using `Log10(value) + 1`.

The relationship between the two operations is: **`CountDigits(n) == Log10(n) + 1`** for all
positive integers (and both return 1 / 0 respectively for the input 0).

This investigation benchmarks the existing `CountDigits` implementations against their
`Log10 + 1` equivalents to determine if the replacement is beneficial.

### Existing CountDigits Implementations

**1. NativeAotNameMangler** (`src/coreclr/tools/Common/Compiler/NativeAotNameMangler.cs`):
Uses Lemire's algorithm — a highly optimized, branchless digit-counting technique based on
a 32-entry lookup table indexed by `Log2(value)`.

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static int CountDigits(uint value)
{
    // Based on https://lemire.me/blog/2021/06/03/computing-the-number-of-digits-of-an-integer-even-faster/
    ReadOnlySpan<long> table = [ /* 32-entry Lemire table */ ];
    long tableValue = table[(int)uint.Log2(value)];
    return (int)((value + tableValue) >> 32);
}
```

**2. TarHeader.Write** (`src/libraries/System.Formats.Tar/src/System/Formats/Tar/TarHeader.Write.cs`):
Uses a simple divide-by-10 loop.

```csharp
static int CountDigits(int value)
{
    Debug.Assert(value >= 0);
    int digits = 1;
    while (true)
    {
        value /= 10;
        if (value == 0) break;
        digits++;
    }
    return digits;
}
```

### Proposed Replacement

```csharp
// For uint (NativeAotNameMangler):
int countDigits = (int)uint.Log10(value) + 1;

// For int (TarHeader.Write):
int countDigits = int.Log10(value) + 1;
```

Where `uint.Log10` uses:
```csharp
public static uint Log10(uint value)
{
    value |= 1;
    uint log2 = (uint)BitOperations.Log2(value) + 1;
    uint approx = (log2 * 1233) >> 12;
    return value < PowersOf10[(int)approx] ? approx - 1 : approx;
}
```

## Methodology

- **Benchmark framework**: BenchmarkDotNet v0.14.0
- **Runtime**: .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
- **OS**: Windows 11 (10.0.26100.7985) (Hyper-V)
- **Data**: 1024 values per iteration, tested across four distributions:
  - `Small_1_9`: values in [1, 9] (1 digit)
  - `Medium_100_9999`: values in [100, 9999] (3-4 digits)
  - `Large_1M_1B`: values in [1,000,000, 1,000,000,000] (7-10 digits)
  - `Mixed`: uniform random across [1, int.MaxValue]
- All implementations use `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
- Four methods compared:
  - `Lemire_CountDigits` (baseline): NativeAotNameMangler's Lemire algorithm
  - `Log10Plus1_UInt`: `uint.Log10(value) + 1`
  - `DivideLoop_CountDigits`: TarHeader.Write's divide loop
  - `Log10Plus1_Int`: `int.Log10(value) + 1` (same algorithm, int cast)

## Results

| Method                 | Distribution    | Mean        | Error    | StdDev   | Ratio  |
|----------------------- |---------------- |------------:|---------:|---------:|-------:|
| **Lemire_CountDigits**     | **Small_1_9**       |    **884.2 ns** |  **1.15 ns** |  **1.08 ns** |   **1.00** |
| Log10Plus1_UInt        | Small_1_9       |  1,194.6 ns |  1.06 ns |  0.82 ns |   1.35 |
| DivideLoop_CountDigits | Small_1_9       |  1,024.2 ns |  0.60 ns |  0.50 ns |   1.16 |
| Log10Plus1_Int         | Small_1_9       |  1,180.0 ns |  2.56 ns |  2.27 ns |   1.33 |
|                        |                 |             |          |          |        |
| **Lemire_CountDigits**     | **Medium_100_9999** |    **884.4 ns** |  **0.60 ns** |  **0.56 ns** |   **1.00** |
| Log10Plus1_UInt        | Medium_100_9999 |  1,195.1 ns |  1.52 ns |  1.34 ns |   1.35 |
| DivideLoop_CountDigits | Medium_100_9999 |  3,544.4 ns |  7.49 ns |  7.01 ns |   4.01 |
| Log10Plus1_Int         | Medium_100_9999 |  1,194.6 ns |  1.31 ns |  1.09 ns |   1.35 |
|                        |                 |             |          |          |        |
| **Lemire_CountDigits**     | **Large_1M_1B**     |    **889.6 ns** |  **1.62 ns** |  **1.43 ns** |   **1.00** |
| Log10Plus1_UInt        | Large_1M_1B     |  1,230.9 ns |  1.02 ns |  0.86 ns |   1.38 |
| DivideLoop_CountDigits | Large_1M_1B     |  9,499.0 ns | 12.24 ns | 11.45 ns |  10.68 |
| Log10Plus1_Int         | Large_1M_1B     |  1,231.1 ns |  1.59 ns |  1.49 ns |   1.38 |
|                        |                 |             |          |          |        |
| **Lemire_CountDigits**     | **Mixed**           |    **884.0 ns** |  **0.68 ns** |  **0.61 ns** |   **1.00** |
| Log10Plus1_UInt        | Mixed           |  1,194.9 ns |  0.83 ns |  0.73 ns |   1.35 |
| DivideLoop_CountDigits | Mixed           | 13,547.8 ns | 40.05 ns | 37.46 ns |  15.33 |
| Log10Plus1_Int         | Mixed           |  1,179.7 ns |  1.27 ns |  1.19 ns |   1.33 |

## Analysis

### Call Site 1: NativeAotNameMangler.CountDigits(uint)

| Metric | Lemire (current) | Log10 + 1 (proposed) |
|--------|-----------------|---------------------|
| Mean (all distributions) | ~884-890 ns | ~1,195-1,231 ns |
| Regression | — | **~35% slower** |
| Algorithm | Single Log2 + table lookup (64-bit shift) | Log2 + multiply + table lookup + comparison |
| Table size | 32 × `long` (256 bytes) | 11 × `uint` (44 bytes) |
| Branches | 0 (branchless) | 1 (conditional) |

**Verdict: Do NOT replace.** Lemire's algorithm is consistently **~35% faster** than `Log10 + 1`
across all distributions. Both algorithms are O(1) and branchless (or near-branchless), but Lemire
avoids the multiply step and the conditional comparison that `Log10` uses. Lemire computes
`(value + tableValue) >> 32` — a single addition and shift — while `Log10` computes
`(log2 * 1233) >> 12` followed by a table comparison and conditional subtraction.

The Lemire implementation is purpose-built for digit counting and is already the fastest known
algorithm for this operation. Replacing it with `Log10 + 1` would be a pure performance regression
with no compensating benefit (both are single-line, table-based algorithms of similar complexity).

### Call Site 2: TarHeader.Write.CountDigits(int)

| Metric | Divide loop (current) | Log10 + 1 (proposed) |
|--------|----------------------|---------------------|
| Small (1-9) | 1,024 ns | 1,180 ns |
| Medium (100-9999) | 3,544 ns | 1,195 ns |
| Large (1M-1B) | 9,499 ns | 1,231 ns |
| Mixed (1-MaxValue) | 13,548 ns | 1,180 ns |

**Verdict: Replacement is beneficial for large values, but the context matters.**

The divide loop has **O(d)** complexity (where d = number of digits), while `Log10 + 1` is **O(1)**.
For large values, `Log10 + 1` is **7.7-11.5x faster**. For small values (1 digit), the divide loop
is actually 15% faster because it exits after a single division vs. Log10's full algorithm.

However, there are important caveats for the TarHeader context:

1. **Call frequency**: `CountDigits` in `TarHeader.Write` is called to compute the length of
   PAX extended attribute records. This is **not a hot path** — it's called once per extended
   attribute, and TAR archive writing is I/O-bound.

2. **Value range**: TAR record lengths are typically small (tens to hundreds of bytes), meaning
   the divide loop usually runs only 2-3 iterations. The performance difference is negligible
   at these sizes (~3,500 ns vs ~1,195 ns for 3-4 digit values).

3. **Complexity trade-off**: The divide loop is self-contained (5 lines, no dependencies),
   while `Log10 + 1` would require either importing `int.Log10` or adding a dependency on
   `System.Numerics`.

4. **Compilation context**: `TarHeader.Write.CountDigits` is a `static local function` inside
   a method. Replacing it with `int.Log10(value) + 1` would actually simplify the code by
   removing the helper entirely.

## Conclusion

### Summary Table

| Call Site | Current Impl | Log10+1 Perf | Replace? | Reason |
|-----------|-------------|-------------|----------|--------|
| **NativeAotNameMangler** | Lemire (branchless) | 35% slower | **No** | Lemire is purpose-built and faster |
| **TarHeader.Write** | Divide loop | 3-11x faster (large), 15% slower (small) | **Maybe** | Perf gain exists but path is not hot |

### Recommendations

1. **NativeAotNameMangler**: **Do not replace.** The Lemire algorithm is a superior
   digit-counting method that outperforms `Log10 + 1` by a consistent ~35%. This is
   not surprising — Lemire's algorithm was specifically designed for digit counting and
   avoids the intermediate multiply/compare steps that `Log10` uses. Both use `Log2`
   as their foundation, but Lemire encodes the correction directly into the table values
   rather than using a separate powers-of-10 lookup.

2. **TarHeader.Write**: **Low priority, optional improvement.** While `int.Log10(value) + 1`
   is significantly faster for larger values, the TAR writing path is I/O-bound and this
   function is called infrequently with small values. The change would simplify the code
   (remove a 10-line helper in favor of a single expression) but the performance impact
   would be imperceptible in practice.

   If the replacement is desired for code simplification, the change would be:
   ```csharp
   // Before:
   int originalDigitCount = CountDigits(length);
   // After:
   int originalDigitCount = int.Log10(length) + 1;
   ```
   Note: `int.Log10(0) + 1 = 1`, which matches `CountDigits(0) = 1` (the divide loop
   returns 1 for input 0 because `0 / 10 == 0` triggers the break immediately). However,
   `int.Log10` throws for negative values while the divide loop's `Debug.Assert` is
   stripped in Release builds, so callers must ensure non-negative input (which the existing
   `Debug.Assert` already documents).
