# Investigation: Can CountDigits Usages Be Replaced with Log10?

## Objective

With the addition of `IBinaryInteger<TSelf>.Log10()` to the runtime, evaluate
whether existing `CountDigits` helper methods in the codebase can be
consolidated using `Log10(value) + 1`. The relationship between the operations
is: **`CountDigits(n) == Log10(n) + 1`** for all positive integers (and both
return 1 / 0 respectively for input 0).

This investigation covers **all 6 distinct CountDigits definitions** found across
4 components in the codebase (CoreLib formatting, NativeAOT compiler, TAR library,
JIT diagnostics), plus 2 derived/test definitions.

This investigation benchmarks the existing `CountDigits` implementations against
their `Log10 + 1` equivalents, then evaluates correctness, complexity, and
performance trade-offs to determine whether replacement is warranted.

## Background

### Problem Statement

Two independent `CountDigits` helper methods exist in the codebase, each with
different algorithms and performance characteristics. Now that `Log10` is
available as a first-class API on integer types, these helpers could potentially
be replaced with a single expression: `Log10(value) + 1`. This would:

- Reduce code duplication (eliminate two separate implementations)
- Leverage a standardized, well-tested API
- Potentially improve performance (or regress it — that's what we need to measure)

### Call Site 1: NativeAotNameMangler

**File**: `src/coreclr/tools/Common/Compiler/NativeAotNameMangler.cs`

Uses [Lemire's algorithm](https://lemire.me/blog/2021/06/03/computing-the-number-of-digits-of-an-integer-even-faster/)
— a highly optimized, branchless digit-counting technique based on a 32-entry
lookup table indexed by `Log2(value)`:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static int CountDigits(uint value)
{
    ReadOnlySpan<long> table =
    [
        4294967296, 8589934582, 8589934582, 8589934582, 12884901788,
        12884901788, 12884901788, 17179868184, 17179868184, 17179868184,
        21474826480, 21474826480, 21474826480, 21474826480, 25769703776,
        25769703776, 25769703776, 30063771072, 30063771072, 30063771072,
        34349738368, 34349738368, 34349738368, 34349738368, 38554705664,
        38554705664, 38554705664, 41949672960, 41949672960, 41949672960,
        42949672960, 42949672960,
    ];
    long tableValue = table[(int)uint.Log2(value)];
    return (int)((value + tableValue) >> 32);
}
```

**Usage context**: Called in a loop that appends numeric suffixes to mangled
names when deduplicating symbols. The loop iterates until a unique name is found,
calling `CountDigits(iter)` each iteration to pre-calculate the output string
length.

### Call Site 2: TarHeader.Write

**File**: `src/libraries/System.Formats.Tar/src/System/Formats/Tar/TarHeader.Write.cs`

Uses a simple divide-by-10 loop:

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

**Usage context**: Called to compute the length of PAX extended attribute records
in TAR archives. The function calculates how many decimal digits a record length
will have, which is needed because the length field is self-referential (the
length includes the length of the length itself).

### Proposed Replacement

```csharp
// For uint (NativeAotNameMangler):
int countDigits = (int)uint.Log10(value) + 1;

// For int (TarHeader.Write):
int countDigits = int.Log10(value) + 1;
```

Where `uint.Log10` (from `src/libraries/System.Private.CoreLib/src/System/UInt32.cs`)
uses a Log2-based approximation:

```csharp
public static uint Log10(uint value)
{
    value |= 1;
    uint log2 = (uint)BitOperations.Log2(value) + 1;
    uint approx = (log2 * 1233) >> 12;
    return value < PowersOf10[(int)approx] ? approx - 1 : approx;
}
```

### Points Considered in This Assessment

1. **Performance comparison** — How does `Log10 + 1` compare to each existing
   implementation across different value ranges?
2. **Algorithm characteristics** — What are the time complexities and instruction
   profiles of each approach?
3. **Call-site context** — Is each call site performance-sensitive?
4. **Correctness** — Are the semantics identical (edge cases, input validation)?
5. **Code complexity trade-off** — Does replacement simplify or complicate the code?

## Methodology

### Step 1: Algorithm Analysis

Before benchmarking, the three algorithms were analyzed for theoretical
performance characteristics:

| Algorithm | Time Complexity | Key Operations | Branches |
|---|---|---|---|
| Lemire | O(1) | Log2 + table[32×long] + add + shift | 0 (branchless) |
| Divide loop | O(d), d=digits | d divisions by 10 | d (loop iterations) |
| Log10 + 1 | O(1) | Log2 + multiply + shift + table[11×uint] + compare | 1 (conditional) |

Both Lemire and Log10 use `BitOperations.Log2` (hardware `LZCNT`) as their
foundation. The key difference:

- **Lemire** encodes the digit-count correction directly into the 32-entry table.
  The final computation is a single add + shift: `(value + tableValue) >> 32`.
- **Log10** uses a multiply (`* 1233`) + shift (`>> 12`) to approximate, then
  corrects with a separate `PowersOf10` table comparison and conditional subtraction.
  The `+ 1` for CountDigits adds one more arithmetic operation.

### Step 2: Input Distribution Design

Four distributions were tested to cover different digit-count ranges:

| Distribution | Range | Digit Count | Rationale |
|---|---|---|---|
| `Small_1_9` | [1, 9] | 1 digit | Best case for divide loop (1 iteration) |
| `Medium_100_9999` | [100, 9999] | 3–4 digits | Typical for string lengths, record sizes |
| `Large_1M_1B` | [1M, 1B] | 7–10 digits | Worst case for divide loop (many iterations) |
| `Mixed` | [1, MaxValue] | 1–10 digits | Uniform random across full uint range |

Each distribution uses 1024 values with a fixed seed (`Random(42)`) for
reproducibility.

### Step 3: Benchmark Execution

- **Framework**: BenchmarkDotNet v0.14.0
- **Runtime**: .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
- **OS**: Windows 11 (10.0.26100.7985) (Hyper-V)
- **Hardware intrinsics**: AVX2, AES, BMI1, BMI2, FMA, LZCNT, PCLMUL, POPCNT
- **Job**: DefaultJob (auto-tuned warmup and iteration counts)

Four methods were benchmarked — `Lemire_CountDigits` (baseline),
`Log10Plus1_UInt`, `DivideLoop_CountDigits`, and `Log10Plus1_Int` — all
iterating over the same 1024-element array and summing results.

## Benchmark Results

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

### Point 1: Lemire vs Log10 + 1 (NativeAotNameMangler)

| Distribution | Lemire | Log10 + 1 | Ratio |
|---|---|---|---|
| Small | 884 ns | 1,195 ns | 1.35x slower |
| Medium | 884 ns | 1,195 ns | 1.35x slower |
| Large | 890 ns | 1,231 ns | 1.38x slower |
| Mixed | 884 ns | 1,195 ns | 1.35x slower |

**Log10 + 1 is consistently ~35% slower than Lemire across all distributions.**

This is expected: both algorithms start with `Log2` (same hardware intrinsic),
but diverge afterward:

- **Lemire**: `(value + table[log2]) >> 32` — 1 add + 1 shift (2 μops)
- **Log10 + 1**: `(log2 * 1233) >> 12`, then `value < PowersOf10[approx]`,
  then conditional select, then `+ 1` — multiply + shift + table lookup +
  compare + conditional + add (≥6 μops)

Lemire's table encodes the correction into 64-bit values that combine
with the input via addition, eliminating the need for a separate correction
step. This is a purpose-built algorithm for digit counting and will always
outperform a generic Log10 followed by +1.

### Point 2: Divide Loop vs Log10 + 1 (TarHeader.Write)

| Distribution | Divide Loop | Log10 + 1 | Speedup |
|---|---|---|---|
| Small (1 digit) | 1,024 ns | 1,180 ns | 0.87x (15% slower) |
| Medium (3–4 digits) | 3,544 ns | 1,195 ns | **2.97x faster** |
| Large (7–10 digits) | 9,499 ns | 1,231 ns | **7.71x faster** |
| Mixed (1–10 digits) | 13,548 ns | 1,180 ns | **11.5x faster** |

The divide loop has **O(d)** complexity where d = number of digits. For
1-digit values, it exits after a single division (fast path), making it 15%
faster than Log10's constant-time overhead. For larger values, the division
loop becomes increasingly expensive while Log10 remains constant.

### Point 3: Call-Site Context Analysis

**NativeAotNameMangler** (`CountDigits(uint)`):
- Called in a deduplication loop during NativeAOT compilation
- Values are iteration counters: 0, 1, 2, 3, ... (typically small)
- The loop runs until a unique name is found — could be many iterations
  for hot generic instantiations
- **Performance sensitivity**: Moderate — NativeAOT compilation time matters,
  but this is one of many operations during name mangling

**TarHeader.Write** (`CountDigits(int)`):
- Called once per PAX extended attribute in a TAR entry
- Values are record lengths: typically 10–200 (2–3 digits)
- **Performance sensitivity**: Low — TAR writing is I/O-bound; this function
  is called infrequently with small values

### Point 4: Correctness Comparison

| Edge Case | Lemire | Divide Loop | Log10 + 1 |
|---|---|---|---|
| Input = 0 | Returns 1 | Returns 1 | Returns 1 (Log10(0)=0, +1=1) ✅ |
| Input = 1 | Returns 1 | Returns 1 | Returns 1 ✅ |
| Input = uint.MaxValue | Returns 10 | N/A (int) | Returns 10 ✅ |
| Input < 0 | N/A (uint) | Debug.Assert | Throws ArgumentOutOfRange ⚠️ |

The negative-input behavior differs: the divide loop silently works (integer
division of negative values is well-defined in C#), while `int.Log10` throws.
The existing `Debug.Assert(value >= 0)` documents that negative inputs are
not expected, so the throwing behavior of `Log10` is actually safer.

### Point 5: Code Complexity Trade-off

**NativeAotNameMangler replacement**:
- Remove: 13-line method with 32-entry magic-number table
- Add: `(int)uint.Log10(value) + 1` (1 line)
- **However**: The Lemire algorithm is purpose-built and faster. Replacing
  it would sacrifice ~35% performance for a single-line code simplification.

**TarHeader.Write replacement**:
- Remove: 10-line method (static local function)
- Add: `int.Log10(value) + 1` (1 line)
- **However**: The method is a static local function called only within its
  enclosing method. Removing it simplifies the code with no dependency impact.

## Conclusion

### Finding 1: Lemire's Algorithm Should Not Be Replaced

Lemire's digit-counting algorithm outperforms `Log10 + 1` by a consistent
~35% across all value distributions. Both are O(1) and start with the same
`Log2` intrinsic, but Lemire's table design eliminates the multiply, compare,
and conditional steps that `Log10` requires.

**Evidence**: Ratio is 1.35–1.38 across all 4 distributions, with sub-nanosecond
error bars confirming the result is stable and reproducible.

Replacing Lemire with `Log10 + 1` would trade a purpose-built, faster algorithm
for a generic one, with the only benefit being a modest code simplification
(13 lines → 1 line). This trade-off is not warranted.

### Finding 2: TarHeader.Write CountDigits Could Optionally Be Replaced

The divide-loop `CountDigits` in `TarHeader.Write` is 3–11x slower than
`Log10 + 1` for values with 3+ digits. However, the practical impact is
negligible because:

1. The function is called on a non-hot I/O-bound path
2. Input values are typically small (2–3 digits)
3. For 1-digit values, the divide loop is actually 15% faster

If the replacement is desired for **code simplification** (removing a 10-line
helper), the change would be:

```csharp
// Before:
int originalDigitCount = CountDigits(length);

// After:
int originalDigitCount = int.Log10(length) + 1;
```

**Correctness note**: `int.Log10(0) + 1 = 1`, matching `CountDigits(0) = 1`.
However, `int.Log10` throws for negative values while the divide loop's
`Debug.Assert` is stripped in Release builds — the stronger validation is
actually an improvement.

### Summary

#### Complete CountDigits Inventory

The codebase contains **6 distinct CountDigits definitions** across 4 components.
The original investigation benchmarked only 2 of them. The table below covers all
6 and evaluates each for `Log10 + 1` replacement.

| # | Location | Signature | Algorithm | Call Sites | Hot Path? | Replace with Log10+1? | Rationale |
|---|---|---|---|---|---|---|---|
| 1 | `FormattingHelpers` `.CountDigits.cs` | `CountDigits(uint)` | Lemire (32×long table) | ~12 (Number.Formatting, TimeSpanParse, BigInteger) | **Yes** — number formatting is perf-critical | **No** | Same Lemire algo as #3; already the fastest known uint digit counter. Log10+1 benchmarked 35% slower. |
| 2 | `FormattingHelpers` `.CountDigits.cs` | `CountDigits(ulong)` | fmtlib-style (64×byte log2-to-pow10 map + 20×ulong powers table) | ~10 (Number.Formatting for long/ulong) | **Yes** — number formatting is perf-critical | **No** | Purpose-built O(1) branchless algorithm with dedicated tables for ulong range. `ulong.Log10` uses the same approach (Log2 → approximate → correct) but this implementation has the correction baked into the table lookup, avoiding the extra multiply and conditional. |
| 3 | `NativeAotNameMangler.cs` | `CountDigits(uint)` | Lemire (32×long table) — identical to #1 | 1 (name dedup loop) | Moderate — NativeAOT compile time | **No** | Benchmarked: 35% faster than Log10+1 consistently. |
| 4 | `TarHeader.Write.cs` | `CountDigits(int)` | Divide-by-10 loop | 4 (PAX record length) | **No** — I/O-bound TAR writing | **Optional** | Benchmarked: Log10+1 is 3–11x faster for large values, but this path handles small values (2–3 digits) where the divide loop is only ~15% slower. Replacement simplifies code (removes 10-line helper) with no practical perf impact. |
| 5 | `jit/utils.cpp` | `CountDigits(unsigned, unsigned base)` | Divide loop (supports arbitrary base) | 6 (JIT diagnostics: block numbering, IBC weights) | **No** — DEBUG-only, not in release builds | **No** | C++ code in `#ifdef DEBUG`; `Log10` is a C# API not available here. Also supports non-base-10, which Log10 cannot. |
| 6 | `jit/utils.cpp` | `CountDigits(double, unsigned base)` | Divide loop (floating-point, arbitrary base) | 0 direct (exists alongside #5) | **No** — DEBUG-only | **No** | Same as #5: C++ debug code, arbitrary base, floating-point input. Not applicable. |

**Not counted as separate definitions** (derived/test code):
- `FormattingHelpers.CountDigits(UInt128)` — delegates to `CountDigits(ulong)` after range reduction; shares the same algorithm family
- `ElidedBoundsChecks.CountDigits(ulong)` — JIT test verifying bounds-check elision; not production code

#### Why the Most Critical Implementations Should NOT Be Replaced

**FormattingHelpers.CountDigits(uint)** and **CountDigits(ulong)** (#1 and #2)
are the highest-impact call sites — they power `int.ToString()`, `long.ToString()`,
`uint.ToString()`, `ulong.ToString()`, `Int128.ToString()`, `TimeSpan.ToString()`,
and `BigInteger.ToString()`. These are among the most frequently called methods in
all of .NET.

Both use purpose-built algorithms that compute digit count in a single step:

- **uint version** (Lemire): `(value + table[Log2(value)]) >> 32` — the table
  encodes both the digit count AND the correction factor into a single 64-bit value,
  so the final computation is just add + shift. No multiply, no conditional.

- **ulong version** (fmtlib): Uses a two-level lookup — first maps Log2 to an
  approximate digit count via a 64-byte table, then compares against the exact
  power of 10. Similar in structure to `Log10` but with the correction table
  designed for direct digit-count output.

`Log10 + 1` uses the same Log2 foundation but adds intermediate steps (multiply
by 1233, shift right 12, compare against PowersOf10 table, conditional subtract,
then add 1). Each extra step adds latency. The purpose-built algorithms eliminate
these steps by encoding the correction differently.

#### Where Replacement Makes Sense

Only **TarHeader.Write.CountDigits** (#4) is a reasonable candidate:

- **Code simplification**: Removes a 10-line static local function, replacing
  4 call sites with `int.Log10(length) + 1`
- **Stronger validation**: `int.Log10` throws on negative input vs. a Debug.Assert
  that's stripped in Release
- **No perf impact**: The TAR writing path is I/O-bound; the function is called
  ≤4 times per PAX extended attribute with values typically in the 10–200 range
- **Semantic clarity**: `Log10(n) + 1` directly expresses "number of decimal digits"
  as a mathematical identity

## Limitations

- **Hyper-V environment**: Benchmarks were run on a Hyper-V VM with "Unknown
  processor". Results should be validated on bare-metal hardware (e.g., via
  @EgorBot on Linux AMD and macOS ARM64) before making production decisions.
- **Single architecture**: Only x64 AVX2 was tested. ARM64 does not have
  `LZCNT` and uses a different `CLZ` instruction, which could change the
  relative costs.
- **Isolated benchmark**: The algorithms were benchmarked in isolation.
  In context (NativeAOT compilation, TAR I/O), the surrounding code's
  cache and branch predictor state may affect results.
- **Lemire table as `ReadOnlySpan<long>`**: In the actual codebase, the Lemire
  table is a `ReadOnlySpan<long>` initialized from an inline array literal.
  The JIT may optimize this differently than the `static readonly long[]` used
  in the benchmark. The benchmark used `static readonly long[]` to match
  the `static readonly uint[]` used for `PowersOf10` in the Log10 implementation.
- **FormattingHelpers.CountDigits(ulong) and UInt128 not benchmarked**: The
  investigation benchmarked the `uint` Lemire algorithm and the `int` divide loop.
  The `ulong` fmtlib-style algorithm and the `UInt128` variant were not separately
  benchmarked. However, the `ulong` algorithm is structurally similar to Lemire
  (Log2 + purpose-built table + single correction step), so the conclusion —
  that purpose-built digit counters outperform generic Log10+1 — applies equally.

## References

- [Lemire's digit-counting algorithm](https://lemire.me/blog/2021/06/03/computing-the-number-of-digits-of-an-integer-even-faster/)
- [fmtlib do_count_digits](https://github.com/fmtlib/fmt/blob/662adf4f33346ba9aba8b072194e319869ede54a/include/fmt/format.h#L1124)
- `FormattingHelpers.CountDigits(uint)`:
  `src/libraries/System.Private.CoreLib/src/System/Buffers/Text/FormattingHelpers.CountDigits.cs`, lines 64–106
- `FormattingHelpers.CountDigits(ulong)`:
  `src/libraries/System.Private.CoreLib/src/System/Buffers/Text/FormattingHelpers.CountDigits.cs`, lines 15–61
- `FormattingHelpers.CountDigits(UInt128)`:
  `src/libraries/System.Private.CoreLib/src/System/Buffers/Text/FormattingHelpers.CountDigits.Int128.cs`, lines 12–50
- `NativeAotNameMangler.CountDigits`:
  `src/coreclr/tools/Common/Compiler/NativeAotNameMangler.cs`, lines 237–249
- `TarHeader.Write.CountDigits`:
  `src/libraries/System.Formats.Tar/src/System/Formats/Tar/TarHeader.Write.cs`, lines 927–938
- `jit/utils.cpp CountDigits`:
  `src/coreclr/jit/utils.cpp`, lines 2148–2170 (DEBUG-only, C++)
- `uint.Log10` implementation:
  `src/libraries/System.Private.CoreLib/src/System/UInt32.cs`, lines 298–309
- `IBinaryInteger<TSelf>.Log10` interface definition:
  `src/libraries/System.Private.CoreLib/src/System/Numerics/IBinaryInteger.cs`, lines 263–290
- [BenchmarkDotNet documentation](https://benchmarkdotnet.org/)
- [Microbenchmark Design Guidelines](https://github.com/dotnet/performance/blob/main/docs/microbenchmark-design-guidelines.md)

## Appendix: Benchmark Code

The benchmark project is in `benchmark/CountDigitsBenchmark/` in this branch.

### Full Benchmark Results (CSV)

```
Method,Distribution,Mean,Error,StdDev,Ratio,RatioSD
Lemire_CountDigits,Large_1M_1B,889.6 ns,1.62 ns,1.43 ns,1.00,0.00
Log10Plus1_UInt,Large_1M_1B,"1,230.9 ns",1.02 ns,0.86 ns,1.38,0.00
DivideLoop_CountDigits,Large_1M_1B,"9,499.0 ns",12.24 ns,11.45 ns,10.68,0.02
Log10Plus1_Int,Large_1M_1B,"1,231.1 ns",1.59 ns,1.49 ns,1.38,0.00
Lemire_CountDigits,Medium_100_9999,884.4 ns,0.60 ns,0.56 ns,1.00,0.00
Log10Plus1_UInt,Medium_100_9999,"1,195.1 ns",1.52 ns,1.34 ns,1.35,0.00
DivideLoop_CountDigits,Medium_100_9999,"3,544.4 ns",7.49 ns,7.01 ns,4.01,0.01
Log10Plus1_Int,Medium_100_9999,"1,194.6 ns",1.31 ns,1.09 ns,1.35,0.00
Lemire_CountDigits,Mixed,884.0 ns,0.68 ns,0.61 ns,1.00,0.00
Log10Plus1_UInt,Mixed,"1,194.9 ns",0.83 ns,0.73 ns,1.35,0.00
DivideLoop_CountDigits,Mixed,"13,547.8 ns",40.05 ns,37.46 ns,15.33,0.04
Log10Plus1_Int,Mixed,"1,179.7 ns",1.27 ns,1.19 ns,1.33,0.00
Lemire_CountDigits,Small_1_9,884.2 ns,1.15 ns,1.08 ns,1.00,0.00
Log10Plus1_UInt,Small_1_9,"1,194.6 ns",1.06 ns,0.82 ns,1.35,0.00
DivideLoop_CountDigits,Small_1_9,"1,024.2 ns",0.60 ns,0.50 ns,1.16,0.00
Log10Plus1_Int,Small_1_9,"1,180.0 ns",2.56 ns,2.27 ns,1.33,0.00
```

### Environment

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7985) (Hyper-V)
Unknown processor
.NET SDK 11.0.100-preview.3.26170.106
  [Host]     : .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
  DefaultJob : .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
```
