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

Multiple independent `CountDigits` helper methods exist in the codebase, each with
different algorithms and performance characteristics. Now that `Log10` is
available as a first-class API on integer types, these helpers could potentially
be replaced with a single expression: `Log10(value) + 1`. This would:

- Reduce code duplication (eliminate separate implementations)
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

### Part 1: uint (Lemire) and int (Divide Loop) vs Log10+1

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

### Part 2: ulong (fmtlib) and UInt128 (FormattingHelpers) vs Log10+1

**Environment**: .NET 10.0.4, X64 RyuJIT AVX2, Windows 11 (Hyper-V)

| Type | Method | Distribution | Mean | Error | StdDev | Ratio |
|------|--------|-------------|-----:|------:|-------:|------:|
| **ulong** | **Fmtlib_CountDigits** | **Small_1_999** | **1.276 μs** | **0.006 μs** | **0.006 μs** | **1.00** |
| ulong | Log10Plus1_ULong | Small_1_999 | 1.540 μs | 0.002 μs | 0.002 μs | 1.21 |
| | | | | | | |
| **ulong** | **Fmtlib_CountDigits** | **Medium_1M_1B** | **1.272 μs** | **0.003 μs** | **0.003 μs** | **1.00** |
| ulong | Log10Plus1_ULong | Medium_1M_1B | 1.447 μs | 0.001 μs | 0.001 μs | 1.14 |
| | | | | | | |
| **ulong** | **Fmtlib_CountDigits** | **Large_1e15_Max** | **1.274 μs** | **0.002 μs** | **0.002 μs** | **1.00** |
| ulong | Log10Plus1_ULong | Large_1e15_Max | 1.426 μs | 0.002 μs | 0.002 μs | 1.12 |
| | | | | | | |
| **ulong** | **Fmtlib_CountDigits** | **Mixed** | **1.272 μs** | **0.001 μs** | **0.001 μs** | **1.00** |
| ulong | Log10Plus1_ULong | Mixed | 1.419 μs | 0.002 μs | 0.002 μs | 1.12 |
| | | | | | | |
| **UInt128** | **FormattingHelpers** | **Small_ulong_range** | **1.774 μs** | **0.005 μs** | **0.004 μs** | **1.00** |
| UInt128 | Log10Plus1_UInt128 | Small_ulong_range | 2.223 μs | 0.021 μs | 0.018 μs | 1.25 |
| | | | | | | |
| **UInt128** | **FormattingHelpers** | **Large_full_range** | **33.065 μs** | **0.229 μs** | **0.214 μs** | **1.00** |
| UInt128 | Log10Plus1_UInt128 | Large_full_range | 2.266 μs | 0.022 μs | 0.019 μs | **0.07** |

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

### Point 6: fmtlib CountDigits(ulong) vs Log10+1 (Benchmarked)

| Distribution | fmtlib | Log10 + 1 | Ratio |
|---|---|---|---|
| Small (1–999) | 1.276 μs | 1.540 μs | 1.21x slower |
| Medium (1M–1B) | 1.272 μs | 1.447 μs | 1.14x slower |
| Large (1e15–Max) | 1.274 μs | 1.426 μs | 1.12x slower |
| Mixed | 1.272 μs | 1.419 μs | 1.12x slower |

**Log10 + 1 is 12–21% slower than fmtlib across all distributions.**

The fmtlib-style algorithm follows the same pattern as Lemire: a two-level
lookup (Log2 → approximate digit count via 64-byte table → compare against
exact power of 10) that avoids the multiply step in `Log10`. The `ulong`
`CountDigits` encodes the mapping directly, while `Log10` computes it via
`(log2 * 1233) >> 12` + correction. The extra arithmetic shows up as a
consistent 12–21% overhead.

**Recommendation**: Do not replace. This powers `long.ToString()`,
`ulong.ToString()`, and is on the hottest formatting path in .NET.

### Point 7: FormattingHelpers.CountDigits(UInt128) vs Log10+1 — Surprise Finding

| Distribution | CountDigits | Log10 + 1 | Ratio |
|---|---|---|---|
| Small (fits in ulong) | 1.774 μs | 2.223 μs | 1.25x slower |
| **Large (full UInt128)** | **33.065 μs** | **2.266 μs** | **0.07 (14.6x faster)** |

**For large UInt128 values, `Log10 + 1` is 14.6x faster than the current
`CountDigits` implementation.**

This is because `FormattingHelpers.CountDigits(UInt128)` performs a
`UInt128 / 1e20` division for values with `upper > 5`. UInt128 division
is implemented in software and is extremely expensive (~30 μs for 1024
iterations = ~29 ns per division). In contrast, `UInt128.Log10` uses the
same O(1) `Log2 → approximate → correct` pattern with a 40-entry table
lookup — no division required.

For small UInt128 values (fits in ulong), `CountDigits` delegates to the
fast fmtlib ulong path and is 25% faster. But for values that exceed the
ulong range — which is the entire purpose of UInt128 — the current
implementation is catastrophically slower.

**Recommendation**: `FormattingHelpers.CountDigits(UInt128)` is a strong
candidate for replacement with `(int)UInt128.Log10(value) + 1`. The large-value
path (which exercises the UInt128-specific logic) shows a 14.6x improvement.
The small-value regression (25%) could be mitigated by keeping the `upper == 0`
fast path that delegates to `CountDigits(ulong)`:

```csharp
public static int CountDigits(UInt128 value)
{
    ulong upper = (ulong)(value >> 64);
    if (upper == 0)
        return CountDigits((ulong)value);  // keep fast ulong path
    return (int)UInt128.Log10(value) + 1;  // avoid UInt128 division
}
```

### Point 8: Maintenance Trade-off Summary

| Concern | Keep Existing | Replace with Log10+1 |
|---|---|---|
| **Code duplication** | 6 independent implementations across 4 components | Consolidated to one API |
| **Algorithm transparency** | Lemire/fmtlib tables are opaque magic numbers | `Log10(n) + 1` is a mathematical identity |
| **Bug risk** | Each implementation must be independently correct | Delegates to centralized, well-tested `Log10` |
| **Perf risk on changes** | Algorithm is frozen; won't accidentally regress | `Log10` changes could affect all call sites |
| **Discoverability** | Developers may re-implement CountDigits unaware of existing helpers | `Log10` is a first-class API on the type itself |
| **Testing burden** | Each copy needs its own edge-case tests | `Log10` is tested via GenericMath test matrix |

## Conclusion

### Finding 1: Lemire's Algorithm (uint) Should Not Be Replaced

Lemire's digit-counting algorithm outperforms `Log10 + 1` by a consistent
~35% across all value distributions. Both are O(1) and start with the same
`Log2` intrinsic, but Lemire's table design eliminates the multiply, compare,
and conditional steps that `Log10` requires.

**Evidence**: Ratio is 1.35–1.38 across all 4 distributions, with sub-nanosecond
error bars confirming the result is stable and reproducible.

Replacing Lemire with `Log10 + 1` would trade a purpose-built, faster algorithm
for a generic one, with the only benefit being a modest code simplification
(13 lines → 1 line). This trade-off is not warranted.

### Finding 2: fmtlib CountDigits(ulong) Should Not Be Replaced

The fmtlib-style ulong digit counter is 12–21% faster than `ulong.Log10 + 1`
across all distributions, consistent with the uint findings. This powers
`long.ToString()` and `ulong.ToString()` — among the most frequently called
formatting methods in .NET.

**Evidence**: Ratio is 1.12–1.21 across all 4 distributions.

### Finding 3: CountDigits(UInt128) Large-Value Path SHOULD Be Replaced

`FormattingHelpers.CountDigits(UInt128)` is **14.6x slower** than
`UInt128.Log10 + 1` for values in the full UInt128 range. The bottleneck is
the `UInt128 / 1e20` software division in the large-value path.

**Evidence**: 33.065 μs vs 2.266 μs (ratio 0.07) for full-range UInt128 values.

The small-value path (where upper == 0, delegating to ulong) is 25% faster
with the current implementation. A hybrid approach preserves this fast path
while eliminating the expensive division:

```csharp
public static int CountDigits(UInt128 value)
{
    ulong upper = (ulong)(value >> 64);
    if (upper == 0)
        return CountDigits((ulong)value);  // keep fast ulong path
    return (int)UInt128.Log10(value) + 1;  // avoid UInt128 division
}
```

### Finding 4: TarHeader.Write CountDigits Could Optionally Be Replaced

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

The codebase contains **6 distinct CountDigits definitions** across 4 components,
plus 2 derived/test definitions. All 6 have been evaluated; the 4 C# definitions
have been benchmarked.

| # | Location | Signature | Algorithm | Call Sites | Hot Path? | Benchmarked? | Replace? | Rationale |
|---|---|---|---|---|---|---|---|---|
| 1 | `FormattingHelpers` `.CountDigits.cs` | `CountDigits(uint)` | Lemire (32×long table) | ~12 (Number.Formatting, TimeSpanParse, BigInteger) | **Yes** — number formatting is perf-critical | ✅ Yes | **No** | 35% faster than Log10+1. |
| 2 | `FormattingHelpers` `.CountDigits.cs` | `CountDigits(ulong)` | fmtlib-style (64×byte log2-to-pow10 map + 20×ulong powers table) | ~10 (Number.Formatting for long/ulong) | **Yes** — number formatting is perf-critical | ✅ Yes | **No** | 12–21% faster than Log10+1. |
| 2a | `FormattingHelpers` `.CountDigits.Int128.cs` | `CountDigits(UInt128)` | Delegates to ulong CountDigits; large values use UInt128/1e20 division | ~2 (Int128.ToString, UInt128.ToString) | **Yes** — number formatting | ✅ Yes | **Yes (large-value path)** | **14.6x slower** than Log10+1 for large values due to software UInt128 division. Hybrid approach recommended. |
| 3 | `NativeAotNameMangler.cs` | `CountDigits(uint)` | Lemire (32×long table) — identical to #1 | 1 (name dedup loop) | Moderate — NativeAOT compile time | ✅ Yes (same algo as #1) | **No** | 35% faster than Log10+1. |
| 4 | `TarHeader.Write.cs` | `CountDigits(int)` | Divide-by-10 loop | 4 (PAX record length) | **No** — I/O-bound TAR writing | ✅ Yes | **Optional** | Log10+1 is 3–11x faster for large values; no practical impact on this cold path. Replacement simplifies code. |
| 5 | `jit/utils.cpp` | `CountDigits(unsigned, unsigned base)` | Divide loop (supports arbitrary base) | 6 (JIT diagnostics: block numbering, IBC weights) | **No** — DEBUG-only, not in release builds | ❌ Not applicable | **No** | C++ code in `#ifdef DEBUG`; `Log10` is a C# API. Also supports non-base-10, which Log10 cannot replace. |
| 6 | `jit/utils.cpp` | `CountDigits(double, unsigned base)` | Divide loop (floating-point, arbitrary base) | 0 direct (exists alongside #5) | **No** — DEBUG-only | ❌ Not applicable | **No** | Same as #5: C++ debug code, arbitrary base, floating-point input. |

**Not counted as separate definitions** (derived/test code):
- `ElidedBoundsChecks.CountDigits(ulong)` — JIT test verifying bounds-check elision; not production code

#### Why the Most Critical uint/ulong Implementations Should NOT Be Replaced

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

**FormattingHelpers.CountDigits(UInt128)** (#2a) large-value path:

- **Performance**: The current implementation is **14.6x slower** for large values
  because it performs a `UInt128 / 1e20` software division (~29 ns per call)
- **Hybrid fix**: Keep the `upper == 0` fast path (delegates to ulong CountDigits),
  replace only the large-value path with `UInt128.Log10 + 1`
- **Call sites**: `Int128.ToString()` and `UInt128.ToString()` — while less common
  than int/uint formatting, a 14.6x regression on large values is significant

**TarHeader.Write.CountDigits** (#4):

- **Code simplification**: Removes a 10-line static local function, replacing
  4 call sites with `int.Log10(length) + 1`
- **Stronger validation**: `int.Log10` throws on negative input vs. a Debug.Assert
  that's stripped in Release
- **No perf impact**: The TAR writing path is I/O-bound; the function is called
  ≤4 times per PAX extended attribute with values typically in the 10–200 range
- **Semantic clarity**: `Log10(n) + 1` directly expresses "number of decimal digits"
  as a mathematical identity

#### Why Definitions #5 and #6 Were Not Benchmarked

The `jit/utils.cpp` `CountDigits` functions (#5 and #6) were excluded from
benchmarking because:

1. **Language barrier**: They are C++ code; `Log10` is a C# API on
   `IBinaryInteger<TSelf>`. There is no C++ equivalent to replace them with.
2. **Arbitrary-base support**: Both accept a `base` parameter (e.g., base 16
   for hex formatting). `Log10` is base-10 only and cannot substitute.
3. **DEBUG-only**: Both are compiled only under `#ifdef DEBUG` and do not
   appear in release builds. Performance is not a concern.
4. **No C# equivalent exists**: Even if Log10 were available in C++, these
   functions serve JIT diagnostic formatting where base-10 is just one of
   several bases used.

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
- **Lemire/fmtlib tables as `static readonly` arrays**: In the actual codebase,
  the Lemire table uses `ReadOnlySpan<long>` initialized from an inline array
  literal, and the fmtlib tables use `ReadOnlySpan<byte>` / `ReadOnlySpan<ulong>`.
  The JIT may optimize `ReadOnlySpan` differently than `static readonly` arrays
  (e.g., embedding data in the code segment vs. heap allocation). This means the
  production code may be slightly faster than our benchmarks indicate, widening
  the gap further in favor of the existing implementations.
- **UInt128 CountDigits reproduction**: The benchmark accesses `UInt128` upper/lower
  halves via `(ulong)(value >> 64)` and `(ulong)value` rather than the internal
  `_upper`/`_lower` fields used by `FormattingHelpers`. The shift-based extraction
  may add a small overhead not present in the actual code, meaning the production
  CountDigits(UInt128) may be slightly faster than benchmarked — but not enough
  to close the 14.6x gap.
- **C++ definitions not benchmarked**: `jit/utils.cpp` CountDigits (#5, #6) cannot
  be benchmarked with BenchmarkDotNet because they are C++ code, support arbitrary
  bases, and only compile under `#ifdef DEBUG`. See "Point 8" for detailed rationale.

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

- **uint/int benchmark**: `benchmark/CountDigitsBenchmark/`
- **ulong/UInt128 benchmark**: `benchmark/CountDigitsULongBenchmark/`

### Full Benchmark Results (CSV)

#### Part 1: uint (Lemire) and int (Divide Loop)

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

#### Part 2: ulong (fmtlib) and UInt128 (FormattingHelpers)

```
Type,Method,Distribution,Mean,Error,StdDev,Ratio
CountDigitsULongBench,Fmtlib_CountDigits,Small_1_999,1.276 μs,0.006 μs,0.006 μs,1.00
CountDigitsULongBench,Log10Plus1_ULong,Small_1_999,1.540 μs,0.002 μs,0.002 μs,1.21
CountDigitsULongBench,Fmtlib_CountDigits,Medium_1M_1B,1.272 μs,0.003 μs,0.003 μs,1.00
CountDigitsULongBench,Log10Plus1_ULong,Medium_1M_1B,1.447 μs,0.001 μs,0.001 μs,1.14
CountDigitsULongBench,Fmtlib_CountDigits,Large_1e15_Max,1.274 μs,0.002 μs,0.002 μs,1.00
CountDigitsULongBench,Log10Plus1_ULong,Large_1e15_Max,1.426 μs,0.002 μs,0.002 μs,1.12
CountDigitsULongBench,Fmtlib_CountDigits,Mixed,1.272 μs,0.001 μs,0.001 μs,1.00
CountDigitsULongBench,Log10Plus1_ULong,Mixed,1.419 μs,0.002 μs,0.002 μs,1.12
CountDigitsUInt128Bench,FormattingHelpers_CountDigits,Small_ulong_range,1.774 μs,0.005 μs,0.004 μs,1.00
CountDigitsUInt128Bench,Log10Plus1_UInt128,Small_ulong_range,2.223 μs,0.021 μs,0.018 μs,1.25
CountDigitsUInt128Bench,FormattingHelpers_CountDigits,Large_full_range,33.065 μs,0.229 μs,0.214 μs,1.00
CountDigitsUInt128Bench,Log10Plus1_UInt128,Large_full_range,2.266 μs,0.022 μs,0.019 μs,0.07
```

### Environment

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7985) (Hyper-V)
Unknown processor
.NET SDK 11.0.100-preview.3.26170.106
  [Host]     : .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
  DefaultJob : .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
```
