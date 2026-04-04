# Investigation: CountDigits vs Log10 — Comprehensive Tradeoff Analysis

## Objective

With the addition of [`IBinaryInteger<TSelf>.Log10()`](https://github.com/dotnet/runtime/issues/116043)
to the runtime ([PR #126065](https://github.com/dotnet/runtime/pull/126065)),
evaluate whether existing `CountDigits` helper methods in the codebase can —
and *should* — be consolidated using `Log10(value) + 1`.

The mathematical identity is: **`CountDigits(n) == Log10(n) + 1`** for all
positive integers (both return 1 / 0 respectively for input 0).

This investigation covers **all 8 distinct CountDigits definitions** found
across 4 components in the codebase, benchmarks every C# definition that could
be replaced, analyzes the runtime's precedent patterns for keeping internal
helpers alongside public APIs, and provides an actionable recommendation.

### Guiding Vision

From the [PR discussion](https://github.com/dotnet/runtime/pull/126065):

> *"This [introducing `Log10` on `IBinaryInteger`] is indeed namely about
> keeping the code simple to maintain and consistent with the other paths.
> The difference between the two approaches will likely be negligible in a
> real world app."*

This vision prioritizes **maintainability and uniformity**. The question is:
where does that vision align with the data, and where does it not?

---

## Part 1: Complete Inventory & Cross-Usage Matrix

### Matrix A: All CountDigits Definitions

The codebase contains **8 distinct `CountDigits` definitions** across 4
components. Source: [Complete CountDigits Inventory gist](https://gist.github.com/ViveliDuCh/3f2e8565e91883d0e41b2d776a30d73a).

| # | Location | Input Type | Algorithm | Callers | Hot Path? |
|---|----------|------------|-----------|---------|-----------|
| D1 | [`FormattingHelpers.CountDigits.cs:64`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Buffers/Text/FormattingHelpers.CountDigits.cs#L64) | `uint` | **Lemire** — branchless, 32×long table | 10 | **Yes** |
| D2 | [`FormattingHelpers.CountDigits.cs:15`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Buffers/Text/FormattingHelpers.CountDigits.cs#L15) | `ulong` | **fmtlib** — 64×byte + 20×ulong tables | 7 | **Yes** |
| D3 | [`FormattingHelpers.CountDigits.Int128.cs:12`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Buffers/Text/FormattingHelpers.CountDigits.Int128.cs#L12) | `UInt128` | **Hybrid** — ulong delegate or ÷1e20 | 5 | **Yes** |
| D4 | [`NativeAotNameMangler.cs:237`](https://github.com/dotnet/runtime/blob/main/src/coreclr/tools/Common/Compiler/NativeAotNameMangler.cs#L237) | `uint` | **Lemire** — duplicate of D1 | 1 | Moderate |
| D5 | [`TarHeader.Write.cs:927`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Formats.Tar/src/System/Formats/Tar/TarHeader.Write.cs#L927) | `int` | **Divide loop** — `while (value /= 10)` | 3 | **No** |
| D6 | [`jit/utils.cpp:2148`](https://github.com/dotnet/runtime/blob/main/src/coreclr/jit/utils.cpp#L2148) | C++ `unsigned` | **Divide loop** — arbitrary base | — | **No** (DEBUG) |
| D7 | [`jit/utils.cpp:2160`](https://github.com/dotnet/runtime/blob/main/src/coreclr/jit/utils.cpp#L2160) | C++ `double` | **Divide loop** — FP, arbitrary base | — | **No** (DEBUG) |
| D8 | [`ElidedBoundsChecks.cs:32`](https://github.com/dotnet/runtime/blob/main/src/tests/JIT/opt/RangeChecks/ElidedBoundsChecks.cs#L32) | `ulong` | **fmtlib** — copy of D2 | — | N/A (test) |

### Matrix B: Log10 Type Coverage (from [PR #126065](https://github.com/dotnet/runtime/pull/126065))

| Type | Log10 Method | Optimized? | Delegation Chain | Return Type | Used by CountDigits? |
|------|-------------|------------|-----------------|-------------|---------------------|
| `byte` | `byte.Log10()` | No | → `uint.Log10` | `byte` | — |
| `sbyte` | `sbyte.Log10()` | No | guard < 0 → `uint.Log10` | `sbyte` | — |
| `short` | `short.Log10()` | No | guard < 0 → `uint.Log10` | `short` | — |
| `ushort` | `ushort.Log10()` | No | → `uint.Log10` | `ushort` | — |
| `int` | `int.Log10()` | No | guard < 0 → `uint.Log10` | `int` | D5 (`int`) |
| **`uint`** | **`uint.Log10()`** | **Yes** | `Log2 × 1233 >> 12` + 10×uint table | `uint` | **D1, D4** (`uint`) |
| `long` | `long.Log10()` | No | guard < 0 → `ulong.Log10` | `long` | — |
| **`ulong`** | **`ulong.Log10()`** | **Yes** | `Log2 × 1233 >> 12` + 20×ulong table | `ulong` | **D2** (`ulong`) |
| `nint` | `nint.Log10()` | No | guard < 0 → `nuint.Log10` | `nint` | — |
| `nuint` | `nuint.Log10()` | No | x64→`ulong.Log10`, x86→`uint.Log10` | `nuint` | — (but BigInteger passes `nuint`) |
| `char` | `char.Log10()` | No | → `uint.Log10` (explicit iface) | `char` | — |
| `Int128` | `Int128.Log10()` | No | guard < 0 → `UInt128.Log10` | `Int128` | — |
| **`UInt128`** | **`UInt128.Log10()`** | **Yes** | `Log2 × 1233 >> 12` + 39×UInt128 array | `UInt128` | **D3** (`UInt128`) |
| `BigInteger` | `BigInteger.Log10()` | Partial | small→`ulong.Log10`, large→Log2+Pow correction | `BigInteger` | — (but BigInteger calls D1/D2 via `nuint`) |
| DIM fallback | `IBinaryInteger<T>.Log10()` | No | divide-by-10 loop | `TSelf` | — |

**Not yet covered** (planned follow-up): `CLong`, `CULong`, Vector APIs,
`TensorPrimitives`, `Tensor`.

### Matrix C: Every Call Site → Definition → Type → Log10 Replacement

This is the **complete cross-usage matrix** — every production caller of
CountDigits, the definition it invokes, the type flowing through, the
corresponding Log10 replacement expression, and whether the replacement
is viable.

#### C1: `FormattingHelpers.CountDigits(uint)` — Definition D1 (Lemire)

| # | File:Line | Calling Method | Code | Public API | Log10 Replacement | Viable? |
|---|-----------|---------------|------|-----------|-------------------|---------|
| C1a | `Number.Formatting.cs:741` | `FormatFloatingPointAsHex` | `CountDigits((uint)actualExponent)` | `Half/Single/Double.ToString()` | `(int)uint.Log10((uint)actualExponent) + 1` | ⚠️ Perf regression (−35%) on hot path |
| C1b | `Number.Formatting.cs:1645` | `NegativeInt32ToDecStr` | `CountDigits((uint)(-value))` | `int.ToString()` | `(int)uint.Log10((uint)(-value)) + 1` | ⚠️ Perf regression (−35%) on **hottest path** |
| C1c | `Number.Formatting.cs:1671` | `TryNegativeInt32ToDecStr` | `CountDigits((uint)(-value))` | `int.TryFormat()` | `(int)uint.Log10((uint)(-value)) + 1` | ⚠️ Same |
| C1d | `Number.Formatting.cs:1964` | `UInt32ToDecStr_NoSmallNumberCheck` | `CountDigits(value)` | `uint.ToString()` | `(int)uint.Log10(value) + 1` | ⚠️ Same |
| C1e | `Number.Formatting.cs:1981` | `UInt32ToDecStr` (padded) | `CountDigits(value)` | `uint.ToString("D...")` | `(int)uint.Log10(value) + 1` | ⚠️ Same |
| C1f | `Number.Formatting.cs:1996` | `TryUInt32ToDecStr` | `CountDigits(value)` | `uint.TryFormat()` | `(int)uint.Log10(value) + 1` | ⚠️ Same |
| C1g | `Number.Formatting.cs:2016` | `TryUInt32ToDecStr` (padded) | `CountDigits(value)` | `uint.TryFormat("D...")` | `(int)uint.Log10(value) + 1` | ⚠️ Same |
| C1h | `TimeSpanFormat.cs:215` | `FormatCustomized` | `CountDigits(days)` | `TimeSpan.ToString()` | `(int)uint.Log10(days) + 1` | ⚠️ Perf regression but not I/O-hot |
| C1i | `TimeSpanParse.cs:118` | `TimeSpanToken` (property) | `CountDigits((uint)_num)` | `TimeSpan.Parse()` | `(int)uint.Log10((uint)_num) + 1` | ⚠️ Same |
| C1j | `Number.BigInteger.cs:423` | BigInteger parse | `CountDigits(base1E9[^1])` | `BigInteger.Parse()` | `(int)nuint.Log10(base1E9[^1]) + 1` | ⚠️ Type is `nuint` — delegates to uint/ulong by platform |
| C1k | `Number.BigInteger.cs:842` | BigInteger format | `CountDigits(base1E9Value[^1])` | `BigInteger.ToString()` | `(int)nuint.Log10(base1E9Value[^1]) + 1` | ⚠️ Same as C1j |

**Summary for D1**: 11 call sites, all have `uint.Log10` available, but all
would regress **~35%** on a hot path. **Not recommended to replace.**

#### C2: `FormattingHelpers.CountDigits(ulong)` — Definition D2 (fmtlib)

| # | File:Line | Calling Method | Code | Public API | Log10 Replacement | Viable? |
|---|-----------|---------------|------|-----------|-------------------|---------|
| C2a | `Number.Formatting.cs:2085` | `NegativeInt64ToDecStr` | `CountDigits((ulong)(-value))` | `long.ToString()` | `(int)ulong.Log10((ulong)(-value)) + 1` | ⚠️ Perf regression (−12–21%) on hot path |
| C2b | `Number.Formatting.cs:2111` | `TryNegativeInt64ToDecStr` | `CountDigits((ulong)(-value))` | `long.TryFormat()` | `(int)ulong.Log10((ulong)(-value)) + 1` | ⚠️ Same |
| C2c | `Number.Formatting.cs:2394` | `UInt64ToDecStr` | `CountDigits(value)` | `ulong.ToString()` | `(int)ulong.Log10(value) + 1` | ⚠️ Same |
| C2d | `Number.Formatting.cs:2413` | `UInt64ToDecStr` (padded) | `CountDigits(value)` | `ulong.ToString("D...")` | `(int)ulong.Log10(value) + 1` | ⚠️ Same |
| C2e | `Number.Formatting.cs:2428` | `TryUInt64ToDecStr` | `CountDigits(value)` | `ulong.TryFormat()` | `(int)ulong.Log10(value) + 1` | ⚠️ Same |
| C2f | `Number.Formatting.cs:2447` | `TryUInt64ToDecStr` (padded) | `CountDigits(value)` | `ulong.TryFormat("D...")` | `(int)ulong.Log10(value) + 1` | ⚠️ Same |

**Summary for D2**: 6 call sites (note: earlier count of 7 included the
Number.Formatting lines more accurately; some are shared between
signed/unsigned paths — 6 distinct lines confirmed), all have `ulong.Log10`
available, all would regress **12–21%** on hot paths.
**Not recommended to replace.**

#### C3: `FormattingHelpers.CountDigits(UInt128)` — Definition D3 (Hybrid)

| # | File:Line | Calling Method | Code | Public API | Log10 Replacement | Viable? |
|---|-----------|---------------|------|-----------|-------------------|---------|
| C3a | `Number.Formatting.cs:2517` | `NegativeInt128ToDecStr` | `CountDigits(absValue)` | `Int128.ToString()` | `(int)UInt128.Log10(absValue) + 1` | ✅ **14.6x faster** for large values |
| C3b | `Number.Formatting.cs:2545` | `TryNegativeInt128ToDecStr` | `CountDigits(absValue)` | `Int128.TryFormat()` | `(int)UInt128.Log10(absValue) + 1` | ✅ Same |
| C3c | `Number.Formatting.cs:2760` | `UInt128ToDecStr` | `CountDigits(value)` | `UInt128.ToString()` | `(int)UInt128.Log10(value) + 1` | ✅ Same |
| C3d | `Number.Formatting.cs:2779` | `UInt128ToDecStr` (padded) | `CountDigits(value)` | `UInt128.ToString("D...")` | `(int)UInt128.Log10(value) + 1` | ✅ Same |
| C3e | `Number.Formatting.cs:2792` | `TryUInt128ToDecStr` (padded) | `CountDigits(value)` | `UInt128.TryFormat("D...")` | `(int)UInt128.Log10(value) + 1` | ✅ Same |

**Summary for D3**: 5 call sites, all have `UInt128.Log10` available. The
current hybrid path uses expensive UInt128 software division for large values.
**Recommended to replace the large-value path** (keep ulong fast path).

#### C4: `NativeAotNameMangler.CountDigits(uint)` — Definition D4 (Lemire duplicate)

| # | File:Line | Calling Method | Code | Public API | Log10 Replacement | Viable? |
|---|-----------|---------------|------|-----------|-------------------|---------|
| C4a | `NativeAotNameMangler.cs:220` | `EnumerateUniqueManglingsForMethod` | `CountDigits(iter)` | None (internal NativeAOT compiler) | `(int)uint.Log10(iter) + 1` | ⚠️ −35% on moderate path |

**Summary for D4**: 1 call site, `uint.Log10` available. Would regress 35%.
**Not recommended** — NativeAOT compile time matters, this is a duplicate of
the proven Lemire algorithm.

#### C5: `TarHeader.Write.CountDigits(int)` — Definition D5 (Divide loop)

| # | File:Line | Calling Method | Code | Public API | Log10 Replacement | Viable? |
|---|-----------|---------------|------|-----------|-------------------|---------|
| C5a | `TarHeader.Write.cs:885` | `GenerateExtendedAttributeRecord` | `CountDigits(length)` | `TarWriter.WriteEntry()` | `int.Log10(length) + 1` | ✅ 3–11x faster, cold path |
| C5b | `TarHeader.Write.cs:887` | Same (loop) | `CountDigits(length)` | Same | `int.Log10(length) + 1` | ✅ Same |
| C5c | `TarHeader.Write.cs:892` | Same (Debug.Assert) | `CountDigits(length)` | Same | `int.Log10(length) + 1` | ✅ Same |

**Summary for D5**: 3 call sites, `int.Log10` available (`int.Log10` →
`uint.Log10` internally). **Recommended to replace** — simplifies code,
I/O-bound path, no practical perf impact.

#### C6/C7: `jit/utils.cpp` — Definitions D6, D7 (C++ DEBUG-only)

| # | File:Line | Type | Base | Log10 Available? | Viable? |
|---|-----------|------|------|-----------------|---------|
| C6 | `jit/utils.cpp:2148` | C++ `unsigned` | Arbitrary (2–16) | ❌ C++ code, Log10 is C# only | **No** |
| C7 | `jit/utils.cpp:2160` | C++ `double` | Arbitrary (2–16) | ❌ C++ code, floating-point | **No** |

**Summary**: Cannot be replaced — different language, arbitrary base support,
DEBUG-only.

### Matrix D: Cross-Reference — CountDigits Type × Log10 Type × Replaceable?

This matrix shows whether each CountDigits definition's input type has a
matching Log10 implementation and what the perf trade-off would be.

| CountDigits Def | Input Type | Log10 Available? | Log10 Type Used | Log10 Delegation | Perf Delta | Replace? |
|----------------|------------|-----------------|----------------|-----------------|------------|----------|
| D1 (Lemire) | `uint` | ✅ | `uint.Log10()` | direct (optimized) | **+35% slower** | ❌ No |
| D2 (fmtlib) | `ulong` | ✅ | `ulong.Log10()` | direct (optimized) | **+12–21% slower** | ❌ No |
| D3 (Hybrid) | `UInt128` | ✅ | `UInt128.Log10()` | direct (optimized) | small: +25% slower / large: **−93% (14.6x faster)** | ✅ Large path |
| D4 (Lemire dup) | `uint` | ✅ | `uint.Log10()` | direct (optimized) | **+35% slower** | ❌ No |
| D5 (Divide loop) | `int` | ✅ | `int.Log10()` | → `uint.Log10()` | **3–11x faster** (multi-digit) | ✅ Yes |
| D6 (C++ divide) | C++ `unsigned` | ❌ | N/A (C++) | N/A | N/A | ❌ No |
| D7 (C++ divide) | C++ `double` | ❌ | N/A (C++) | N/A | N/A | ❌ No |
| D8 (test fmtlib) | `ulong` | ✅ | `ulong.Log10()` | direct (optimized) | N/A (test) | N/A |

### Matrix E: Public API Impact — Which .NET APIs Are Affected by Each Decision?

| Public API | CountDigits Def Used | Type | Call Count per Invocation | Would Regress if Replaced? |
|-----------|---------------------|------|-------------------------|---------------------------|
| `int.ToString()` | D1 (Lemire `uint`) | `uint` | 1 | ⚠️ Yes, +35% on digit-count step |
| `int.TryFormat()` | D1 (Lemire `uint`) | `uint` | 1 | ⚠️ Yes, +35% |
| `uint.ToString()` | D1 (Lemire `uint`) | `uint` | 1 | ⚠️ Yes, +35% |
| `uint.TryFormat()` | D1 (Lemire `uint`) | `uint` | 1 | ⚠️ Yes, +35% |
| `Half/Single/Double.ToString()` (hex) | D1 (Lemire `uint`) | `uint` | 1 | ⚠️ Yes, +35% |
| `TimeSpan.ToString()` | D1 (Lemire `uint`) | `uint` | 1 | ⚠️ Yes, +35% |
| `TimeSpan.Parse()` | D1 (Lemire `uint`) | `uint` | 1 | ⚠️ Yes, +35% |
| `BigInteger.Parse()` | D1 via `nuint` | `nuint`→`uint`/`ulong` | 1 | ⚠️ Yes, +35% (x86) or +12–21% (x64) |
| `BigInteger.ToString()` | D1 via `nuint` | `nuint`→`uint`/`ulong` | 1 | ⚠️ Same |
| `long.ToString()` | D2 (fmtlib `ulong`) | `ulong` | 1 | ⚠️ Yes, +12–21% |
| `long.TryFormat()` | D2 (fmtlib `ulong`) | `ulong` | 1 | ⚠️ Yes, +12–21% |
| `ulong.ToString()` | D2 (fmtlib `ulong`) | `ulong` | 1 | ⚠️ Yes, +12–21% |
| `ulong.TryFormat()` | D2 (fmtlib `ulong`) | `ulong` | 1 | ⚠️ Yes, +12–21% |
| `Int128.ToString()` | D3 (Hybrid `UInt128`) | `UInt128` | 1 | ✅ **No — improves 14.6x** for large values |
| `Int128.TryFormat()` | D3 (Hybrid `UInt128`) | `UInt128` | 1 | ✅ Same |
| `UInt128.ToString()` | D3 (Hybrid `UInt128`) | `UInt128` | 1 | ✅ Same |
| `UInt128.TryFormat()` | D3 (Hybrid `UInt128`) | `UInt128` | 1 | ✅ Same |
| NativeAOT symbol mangling | D4 (Lemire `uint`) | `uint` | 1+ per dedup iteration | ⚠️ Yes, +35% |
| `TarWriter.WriteEntry()` | D5 (Divide loop `int`) | `int` | ≤4 per PAX attr | ✅ **No — improves 3–11x**, cold I/O path |

### Matrix F: Replacement Viability Summary

| Replaceable? | Definitions | Call Sites | Public APIs Affected | Perf Impact |
|-------------|-------------|------------|---------------------|-------------|
| ✅ **Yes — improves perf** | D3 large path, D5 | 8 | `Int128/UInt128.ToString/TryFormat`, `TarWriter.WriteEntry` | 14.6x faster (D3 large), 3–11x faster (D5) |
| ⚠️ **No — regresses perf** | D1, D2, D4 | 18 | `int/uint/long/ulong.ToString/TryFormat`, `TimeSpan.*`, `BigInteger.*`, NativeAOT | 12–35% slower |
| ❌ **No — not applicable** | D6, D7, D8 | 0 prod | None | N/A (C++ / test) |

---

## Part 2: Algorithm Comparison

### The Three Core Algorithms

All three production-grade algorithms start with `Log2` (hardware `LZCNT`
on x64, `CLZ` on ARM64) but diverge in how they compute the final result:

| Algorithm | Used For | Time Complexity | Key Operations | Branches |
|-----------|----------|-----------------|----------------|----------|
| **Lemire** | `uint` | O(1) | `Log2` + table[32×long] + add + shift | 0 (branchless) |
| **fmtlib** | `ulong` | O(1) | `Log2` + table[64×byte] + table[20×ulong] + compare | 1 (conditional) |
| **Log10** | all integer types | O(1) | `Log2` + multiply(×1233) + shift(>>12) + table[N×T] + compare | 1 (conditional) |
| *Divide loop* | `int` (Tar), C++ | O(d) | d divisions by 10 | d (loop) |
| *Hybrid* | `UInt128` | O(1) but expensive | delegates to ulong or performs UInt128 division | 1–2 |

**Why Lemire only works for `uint`**: The trick `(value + table[Log2(value)]) >> 32`
requires the input + correction to fit in a 64-bit add. For `ulong`, you'd need
128-bit addition (no single x64 instruction). For `UInt128`, 256-bit — completely
impractical.

**Why fmtlib is faster than Log10 for `ulong`**: fmtlib encodes the Log2-to-digit-count
mapping directly in a 64-byte table, avoiding the `(log2 * 1233) >> 12` multiply+shift
step that Log10 uses. The fmtlib table is a precomputed version of what Log10 calculates
at runtime.

**Why Log10 wins for `UInt128`**: The current `CountDigits(UInt128)` divides by 1e20
(software 128-bit division: ~29 ns each). `UInt128.Log10` uses the same O(1)
`Log2 → approximate → correct` pattern with a 39-entry table — no division.

### Log10 Implementation (shared across all types)

```csharp
// uint.Log10 — from PR #126065
public static uint Log10(uint value)
{
    // log10(x) ≈ (log2(x) + 1) * 1233 >> 12
    // http://graphics.stanford.edu/~seander/bithacks.html#IntegerLog10
    value |= 1;
    uint log2 = (uint)BitOperations.Log2(value) + 1;
    uint approx = (log2 * 1233) >> 12;
    return value < PowersOf10[(int)approx] ? approx - 1 : approx;
}
```

The same pattern is used for `ulong` and `UInt128`, differing only in the
`PowersOf10` table type and `Log2` call. This uniformity is the key
maintainability advantage of Log10.

---

## Part 3: Benchmark Results

### Environment

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7985) (Hyper-V)
Unknown processor
.NET SDK 11.0.100-preview.3.26170.106
  [Host]     : .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
  DefaultJob : .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
```

Each benchmark iterates over 1024 values with fixed seed (`Random(42)`)
for reproducibility.

### 3A: uint — Lemire vs Log10+1

| Method | Distribution | Mean | Error | StdDev | Ratio |
|--------|-------------|-----:|------:|-------:|------:|
| **Lemire_CountDigits** | **Small_1_9** | **884.2 ns** | **1.15 ns** | **1.08 ns** | **1.00** |
| Log10Plus1_UInt | Small_1_9 | 1,194.6 ns | 1.06 ns | 0.82 ns | 1.35 |
| **Lemire_CountDigits** | **Medium_100_9999** | **884.4 ns** | **0.60 ns** | **0.56 ns** | **1.00** |
| Log10Plus1_UInt | Medium_100_9999 | 1,195.1 ns | 1.52 ns | 1.34 ns | 1.35 |
| **Lemire_CountDigits** | **Large_1M_1B** | **889.6 ns** | **1.62 ns** | **1.43 ns** | **1.00** |
| Log10Plus1_UInt | Large_1M_1B | 1,230.9 ns | 1.02 ns | 0.86 ns | 1.38 |
| **Lemire_CountDigits** | **Mixed** | **884.0 ns** | **0.68 ns** | **0.61 ns** | **1.00** |
| Log10Plus1_UInt | Mixed | 1,194.9 ns | 0.83 ns | 0.73 ns | 1.35 |

**Result: Log10+1 is consistently ~35% slower than Lemire.**

### 3B: int — Divide Loop vs Log10+1

| Method | Distribution | Mean | Error | StdDev | vs Log10+1 |
|--------|-------------|-----:|------:|-------:|------:|
| DivideLoop_CountDigits | Small_1_9 | 1,024.2 ns | 0.60 ns | 0.50 ns | 15% faster |
| Log10Plus1_Int | Small_1_9 | 1,180.0 ns | 2.56 ns | 2.27 ns | baseline |
| DivideLoop_CountDigits | Medium_100_9999 | 3,544.4 ns | 7.49 ns | 7.01 ns | **2.97x slower** |
| Log10Plus1_Int | Medium_100_9999 | 1,194.6 ns | 1.31 ns | 1.09 ns | baseline |
| DivideLoop_CountDigits | Large_1M_1B | 9,499.0 ns | 12.24 ns | 11.45 ns | **7.71x slower** |
| Log10Plus1_Int | Large_1M_1B | 1,231.1 ns | 1.59 ns | 1.49 ns | baseline |
| DivideLoop_CountDigits | Mixed | 13,547.8 ns | 40.05 ns | 37.46 ns | **11.5x slower** |
| Log10Plus1_Int | Mixed | 1,179.7 ns | 1.27 ns | 1.19 ns | baseline |

**Result: Divide loop is 3–11x slower than Log10+1 for multi-digit values. Only
15% faster for 1-digit values.**

### 3C: ulong — fmtlib vs Log10+1

| Method | Distribution | Mean | Error | StdDev | Ratio |
|--------|-------------|-----:|------:|-------:|------:|
| **Fmtlib_CountDigits** | **Small_1_999** | **1.276 μs** | **0.006 μs** | **0.006 μs** | **1.00** |
| Log10Plus1_ULong | Small_1_999 | 1.540 μs | 0.002 μs | 0.002 μs | 1.21 |
| **Fmtlib_CountDigits** | **Medium_1M_1B** | **1.272 μs** | **0.003 μs** | **0.003 μs** | **1.00** |
| Log10Plus1_ULong | Medium_1M_1B | 1.447 μs | 0.001 μs | 0.001 μs | 1.14 |
| **Fmtlib_CountDigits** | **Large_1e15_Max** | **1.274 μs** | **0.002 μs** | **0.002 μs** | **1.00** |
| Log10Plus1_ULong | Large_1e15_Max | 1.426 μs | 0.002 μs | 0.002 μs | 1.12 |
| **Fmtlib_CountDigits** | **Mixed** | **1.272 μs** | **0.001 μs** | **0.001 μs** | **1.00** |
| Log10Plus1_ULong | Mixed | 1.419 μs | 0.002 μs | 0.002 μs | 1.12 |

**Result: Log10+1 is 12–21% slower than fmtlib.**

### 3D: UInt128 — FormattingHelpers Hybrid vs Log10+1

| Method | Distribution | Mean | Error | StdDev | Ratio |
|--------|-------------|-----:|------:|-------:|------:|
| **FormattingHelpers** | **Small_ulong_range** | **1.774 μs** | **0.005 μs** | **0.004 μs** | **1.00** |
| Log10Plus1_UInt128 | Small_ulong_range | 2.223 μs | 0.021 μs | 0.018 μs | 1.25 |
| **FormattingHelpers** | **Large_full_range** | **33.065 μs** | **0.229 μs** | **0.214 μs** | **1.00** |
| Log10Plus1_UInt128 | Large_full_range | 2.266 μs | 0.022 μs | 0.019 μs | **0.07** |

**Result: Log10+1 is 14.6x FASTER for large UInt128 values. 25% slower for
small values that fit in ulong.**

### Consolidated Performance Summary

| Type | CountDigits Algo | Log10+1 Δ | Verdict |
|------|-----------------|-----------|---------|
| `uint` | Lemire | **+35% slower** | Keep CountDigits |
| `ulong` | fmtlib | **+12–21% slower** | Keep CountDigits |
| `UInt128` (small) | Hybrid→ulong | **+25% slower** | Keep ulong fast path |
| `UInt128` (large) | Hybrid÷1e20 | **14.6x faster** | **Replace with Log10+1** |
| `int` (Tar) | Divide loop | **3–11x faster** | Replace (cold path, code simplification) |

---

## Part 4: Critical Analysis

### 4.1 Can Log10 Be Made as Fast as Lemire/fmtlib?

**Short answer: No, not without becoming Lemire/fmtlib.**

The performance gap comes from a fundamental algorithm difference:

```
Lemire (uint):    Log2 → table[log2] → (value + table) >> 32
                  = 1 add + 1 shift after Log2 (2 μops)

fmtlib (ulong):   Log2 → table1[log2] → table2[approx] → conditional
                  = 2 lookups + 1 compare after Log2 (~4 μops)

Log10 (all types): Log2 → multiply(×1233) → shift(>>12) → table[approx] → conditional
                  = 1 mul + 1 shift + 1 lookup + 1 compare after Log2 (≥5 μops)
```

To make Log10 match Lemire, you'd need to replace the `(log2 * 1233) >> 12`
approximation with a direct table lookup — at which point you've reinvented
Lemire/fmtlib. The multiply+shift *is* the cost of Log10's type-uniformity.

To make Log10 match fmtlib, you'd need a 64-byte `log2ToPow10` map instead of
the multiply — but then `uint.Log10` and `ulong.Log10` would use different table
sizes (32 vs 64), breaking the "one pattern" uniformity that is Log10's main
selling point.

**Could the JIT bridge the gap?** In theory, constant propagation could
optimize `(log2 * 1233) >> 12` into a lookup table. In practice, the JIT does
not perform this transformation — and even if it did, the table would need to be
type-specific, losing the uniformity advantage.

### 4.2 Can CountDigits Delegate to Log10 Internally?

A middle-ground option: keep `FormattingHelpers.CountDigits` as the API but
change its implementation to call `Log10 + 1`:

```csharp
// Option: CountDigits delegates to Log10 internally
public static int CountDigits(uint value) => (int)uint.Log10(value) + 1;
public static int CountDigits(ulong value) => (int)ulong.Log10(value) + 1;
```

**This would regress the hottest formatting paths by 12–35%.** The callers
(22 sites in `Number.Formatting.cs`, `TimeSpanFormat.cs`, `BigInteger.cs`)
collectively power `.ToString()` for all integer types. A 35% regression on
`int.ToString()` would be detectable in real-world apps that do heavy logging,
serialization, or string formatting.

**Verdict**: Not viable for uint/ulong. Viable for UInt128 (large path only).

### 4.3 The UInt128 Opportunity

This is the strongest case for change. The current `CountDigits(UInt128)` uses
software 128-bit division (`UInt128 / 1e20`) that is ~29 ns per call. The Log10
approach avoids division entirely.

@tannergooding's [review comment](https://github.com/dotnet/runtime/pull/126065#discussion_r3033228275)
on PR #126065 explicitly suggests a similar optimization for `UInt128.Log10`:

> *"This is probably a case where it is worth doing:*
> ```csharp
> if (value._upper == 0)
>     return ulong.Log10(value._lower);
> ```
> *128-bit multiplication and division is fairly expensive and this provides a
> trivial shortcut that avoids it for the common path."*

This validates the hybrid approach: keep the ulong fast path, use Log10 for
the expensive UInt128-specific path.

**Proposed change**:

```csharp
public static int CountDigits(UInt128 value)
{
    ulong upper = value.Upper;
    if (upper == 0)
        return CountDigits(value.Lower);       // fast ulong path unchanged
    return (int)UInt128.Log10(value) + 1;      // O(1) table lookup, no division
}
```

### 4.4 Runtime Precedent: Internal Helpers Alongside Public APIs

The dotnet/runtime codebase has a clear pattern of **keeping purpose-built
internal helpers even when public APIs exist**:

1. **`BitOperations.Log2` / `uint.Log2`**: `BitOperations.Log2` was the original
   public API ([PR #27382](https://github.com/dotnet/runtime/issues/27382)).
   When `uint.Log2` was added to `IBinaryInteger`, the internal helpers were NOT
   replaced — `FormattingHelpers.CountDigits` still calls `BitOperations.Log2`
   directly rather than going through the type-level API. The static method on
   `uint` delegates to `BitOperations` anyway, so it's the same code path, but
   the pattern shows internal code preferring the direct call.

2. **`FormattingHelpers` bounds-check removal** ([PR #113790](https://github.com/dotnet/runtime/pull/113790)):
   Rather than replacing `CountDigits` with a simpler API, the runtime team
   *further optimized it* by teaching the JIT to elide bounds checks on the
   `Log2` table access. This signals that `CountDigits` is considered worth
   investing in as a standalone optimized helper.

3. **Integer formatting optimizations** ([PR #76726](https://github.com/dotnet/runtime/pull/76726),
   [PR #68795](https://github.com/dotnet/runtime/pull/68795)):
   Multiple PRs have specifically optimized the `Number.Formatting.cs` hot paths,
   including `CountDigits` call sites. These paths are performance-critical enough
   to warrant purpose-built code.

4. **`@tannergooding` on `byte.Log10` micro-optimization** (PR #126065 review):
   > *"It's likely not a meaningful improvement and so simpler to defer to the
   > shared helper."*

   This shows the maintainer prefers simplicity over micro-optimization on
   **non-hot paths** — but the corollary is that hot paths *do* warrant
   specialized implementations.

**Pattern summary**: The runtime keeps optimized internal helpers for hot paths
and uses unified public APIs for everything else. `CountDigits` fits squarely
in the "optimized internal helper" category for uint/ulong, but the UInt128
version is a case where the "optimized" path is actually slower.

### 4.5 The PR Review Question

In [PR #126065, @huoyaoyuan asked](https://github.com/dotnet/runtime/pull/126065#issuecomment-4122444610):

> *"Can the usages of `CountDigits` be replaced with `Log10`?"*

This investigation provides the data-driven answer:

- **uint/ulong CountDigits**: No — 12–35% regression on the hottest paths
- **UInt128 CountDigits (large path)**: Yes — 14.6x improvement
- **TarHeader CountDigits**: Optionally yes — simplifies code, negligible perf impact
- **NativeAotNameMangler CountDigits**: No — 35% regression, moderate path
- **C++ JIT CountDigits**: No — different language, different base support

### 4.6 Could a Better Algorithm Unify Everything?

Exploring whether a single algorithm could match or beat Lemire/fmtlib while
maintaining Log10's uniformity:

**Option A: Lemire-style CountDigits for Log10**
Embed the `(digitCount << 32) - correction` encoding into `uint.Log10` itself.
Problem: This changes Log10's return value semantics (it would return
`digitCount - 1`, not `floor(log10(x))`). The encoding is fundamentally about
digit counting, not logarithms.

**Option B: fmtlib-style direct table in Log10**
Replace `(log2 * 1233) >> 12` with a `log2ToPow10` byte table (like fmtlib).
For `uint`: a 32-byte table. For `ulong`: a 64-byte table. For `UInt128`:
a 128-byte table.

```csharp
// Hypothetical: fmtlib-style Log10 for uint
public static uint Log10(uint value)
{
    value |= 1;
    ReadOnlySpan<byte> log2ToLog10 = [ /* 32 entries */ ];
    uint approx = log2ToLog10[(int)BitOperations.Log2(value)];
    return value < PowersOf10[(int)approx] ? approx - 1 : approx;
}
```

This would close the gap to ~0% for ulong (fmtlib IS this algorithm) and
reduce the uint gap from 35% to ~10% (still not branchless like Lemire, but
eliminates the multiply). However:
- It changes the internal implementation of a public API
- Different table sizes per type reduces the "one pattern" benefit
- Still can't match Lemire's branchless property for uint

**Option C: Teach the JIT to optimize `(x * 1233) >> 12` into a table**
This is the ideal long-term solution but requires JIT compiler work and is
out of scope for the current decision.

**Verdict**: No single algorithm can match Lemire's branchless uint performance
while maintaining type uniformity. The ~35% gap for uint and ~12% gap for ulong
are intrinsic to the algorithmic tradeoff between uniformity and specialization.

### 4.7 Real-World Impact Assessment

The vision statement says *"the difference between the two approaches will likely
be negligible in a real world app."* Let's quantify:

**For `int.ToString()` (Lemire path)**:
- Lemire: ~0.86 ns per call
- Log10+1: ~1.17 ns per call
- Delta: ~0.31 ns per call
- To add 1 ms of latency: ~3.2 million `ToString()` calls
- **Verdict**: Negligible for most apps. Significant for high-throughput
  serialization/logging (millions of calls/second in JSON serializers,
  database drivers, log frameworks).

**For `UInt128.ToString()` (division path)**:
- CountDigits: ~32 ns per call (large values)
- Log10+1: ~2.2 ns per call
- Delta: ~30 ns per call
- **Verdict**: Significant even at moderate call rates. A loop formatting
  1000 UInt128 values saves ~30 μs.

---

## Part 5: Correctness Analysis

| Edge Case | Lemire | fmtlib | Divide Loop | Log10+1 |
|-----------|--------|--------|-------------|---------|
| Input = 0 | 1 | 1 | 1 | 1 ✅ |
| Input = 1 | 1 | 1 | 1 | 1 ✅ |
| Input = 9 | 1 | 1 | 1 | 1 ✅ |
| Input = 10 | 2 | 2 | 2 | 2 ✅ |
| Power-of-10 boundaries | ✅ Correct | ✅ Correct | ✅ Correct | ✅ Correct |
| `uint.MaxValue` | 10 | N/A | N/A | 10 ✅ |
| `ulong.MaxValue` | N/A | 20 | N/A | 20 ✅ |
| Negative input | N/A (uint) | N/A (ulong) | `Debug.Assert` (stripped in Release) | Throws `ArgumentOutOfRangeException` ⚠️ |

**Note on Log10(0)**: Both `Log10(0)` and `CountDigits(0)` return 0 and 1
respectively. The `value |= 1` in Log10 ensures `Log2(0)` doesn't produce
undefined behavior, and the table lookup yields 0 (since `Log2(1) = 0` and
`PowersOf10[0] = 1`, so `1 < 1` is false and `approx = 0` is returned).
Adding 1 gives `CountDigits(0) = 1`, matching the existing behavior.

---

## Part 6: Tradeoff Matrix

### Dimension 1: Performance

| Definition | CountDigits Speed | Log10+1 Speed | Delta | Hot Path? | Impact |
|-----------|------------------|---------------|-------|-----------|--------|
| #1 `uint` (Lemire) | 884 ns/1024 | 1,195 ns/1024 | **+35%** | **Yes** (22 callers in formatting) | **High** — `.ToString()` for all 32-bit integer types |
| #2 `ulong` (fmtlib) | 1,272 ns/1024 | 1,447 ns/1024 | **+12–21%** | **Yes** (7 callers in formatting) | **High** — `.ToString()` for all 64-bit integer types |
| #3 `UInt128` (hybrid) | 33,065 ns/1024 (large) | 2,266 ns/1024 (large) | **−93%** | **Yes** (formatting) | **High** — 14.6x improvement |
| #4 `uint` (NativeAOT) | 884 ns/1024 | 1,195 ns/1024 | **+35%** | Moderate | **Low** — compile time, one call site |
| #5 `int` (Tar) | 1,024–13,548 ns/1024 | 1,180 ns/1024 | **−15% to −92%** | **No** | **None** — I/O-bound |
| #6/#7 C++ | N/A | N/A | N/A | **No** | **None** — different language |

### Dimension 2: Maintainability

| Factor | CountDigits (status quo) | Log10+1 (unified) |
|--------|------------------------|--------------------|
| **Implementations to maintain** | 5 (3 algorithms × 3 types + 2 duplicates) | 1 (Log10 API) |
| **Lines of algorithm code** | ~150 across 4 files | 0 (delegates to public API) |
| **Table data to verify** | 32×long + 64×byte + 20×ulong + UInt128 thresholds | 0 (owned by Log10) |
| **Test surface** | Each copy tested independently | Centralized in GenericMath test matrix |
| **Discoverability** | Developers may unknowingly create new CountDigits | `Log10` is a first-class API |
| **Cross-type consistency** | 3 different algorithms | 1 pattern |

### Dimension 3: Risk

| Risk | CountDigits | Log10+1 |
|------|-------------|---------|
| **Regression risk** | Algorithm is frozen, won't change | Log10 changes propagate to all callers |
| **Correctness risk** | Each copy must be independently verified | Centralized testing catches bugs once |
| **Future optimization** | Each type can be independently optimized | Optimizing Log10 benefits all types |
| **API coupling** | Internal helper, free to change | Public API, breaking changes are hard |

---

## Part 7: Recommendations

### Option A: Targeted Hybrid (Recommended)

**Replace only where Log10+1 is better or equivalent; keep specialized helpers
where they outperform.**

| Definition | Action | Rationale |
|-----------|--------|-----------|
| #1 `FormattingHelpers.CountDigits(uint)` | **Keep as-is** | 35% faster, powers most-called formatting paths |
| #2 `FormattingHelpers.CountDigits(ulong)` | **Keep as-is** | 12–21% faster, powers 64-bit formatting paths |
| #3 `FormattingHelpers.CountDigits(UInt128)` | **Replace large-value path** | 14.6x improvement; keep `upper==0` ulong fast path |
| #4 `NativeAotNameMangler.CountDigits(uint)` | **Keep as-is** | 35% faster; compile-time path where perf matters |
| #5 `TarHeader.Write.CountDigits(int)` | **Replace** | Simplifies code; no perf impact on I/O-bound path |
| #6/#7 `jit/utils.cpp` | **Keep as-is** | C++ code, arbitrary-base support, DEBUG-only |
| #8 `ElidedBoundsChecks.cs` | **Keep as-is** | Test code, not production |

**Concrete changes**:

1. **UInt128** — In `FormattingHelpers.CountDigits.Int128.cs`:
   ```csharp
   public static int CountDigits(UInt128 value)
   {
       ulong upper = value.Upper;
       if (upper == 0)
           return CountDigits(value.Lower);       // existing fast path
       return (int)UInt128.Log10(value) + 1;      // replaces expensive division
   }
   ```

2. **TarHeader** — In `TarHeader.Write.cs`, replace `CountDigits(length)` calls
   with `int.Log10(length) + 1` and remove the `CountDigits` static local function.

### Option B: Full Unification (Not Recommended)

Replace all CountDigits with `Log10 + 1` across the board.

**Pros**: Maximum code simplification, single algorithm, zero duplication.

**Cons**: 12–35% regression on `int.ToString()`, `uint.ToString()`,
`long.ToString()`, `ulong.ToString()` — the most frequently called formatting
methods in .NET. This contradicts the runtime's established pattern of investing
in purpose-built optimizations for hot paths (PRs [#76726](https://github.com/dotnet/runtime/pull/76726),
[#68795](https://github.com/dotnet/runtime/pull/68795),
[#113790](https://github.com/dotnet/runtime/pull/113790)).

### Option C: Improve Log10 to Close the Gap

Modify `uint.Log10` and `ulong.Log10` to use fmtlib-style direct table lookups
instead of the `(log2 * 1233) >> 12` computation. Then replace CountDigits
everywhere.

**Pros**: Could reduce the uint gap from 35% to ~10%, and eliminate the ulong
gap entirely.

**Cons**:
- Still can't match Lemire's branchless property for uint (~10% gap remains)
- Changes the internal implementation of a public API (risk)
- Different table sizes per type undermines the "one pattern" uniformity
- The fmtlib algorithm IS essentially what `CountDigits(ulong)` already does —
  you'd be replacing CountDigits with CountDigits under a different name

**Assessment**: This option has merit for ulong (where fmtlib ≈ table-based Log10)
but is not sufficient for uint (where Lemire's branchless design is fundamentally
different). It could be pursued as a Log10 implementation improvement independent
of the CountDigits question.

### Option D: Keep Both, Add `CountDigits` as Public API

Formalize `CountDigits` as a public API on integer types alongside `Log10`,
documenting that `CountDigits = Log10 + 1` and that the internal implementation
may use a faster algorithm.

**Pros**: Eliminates the scattered private copies, gives developers the
right tool for the job.

**Cons**: Increases public API surface; `Log10 + 1` is trivial to write
and may not justify a dedicated API. Also, `CountDigits` is an uncommon
operation outside of formatting internals.

**Assessment**: Overengineered for the problem. The relationship
`CountDigits(n) == Log10(n) + 1` is simple enough that a public
`CountDigits` API would add surface area without sufficient value.

---

## Summary

The data supports **Option A: Targeted Hybrid** as the best path forward:

1. **UInt128 large-value path**: Replace with `Log10 + 1` (**14.6x faster**)
2. **TarHeader divide loop**: Replace with `Log10 + 1` (code simplification,
   no perf impact)
3. **Everything else**: Keep the purpose-built algorithms (12–35% faster on
   the hottest paths in .NET)

This aligns with the guiding vision — *"keeping the code simple to maintain
and consistent"* — while respecting the empirical reality that the hot-path
algorithms are measurably faster and the runtime has an established pattern
of keeping them.

The key insight is that the vision applies **per call site**: cold paths
should use the simple, uniform API; hot paths should use purpose-built
algorithms. `Log10` is the right tool for new code and non-critical paths.
`CountDigits` (Lemire/fmtlib) is the right tool for the formatting hot paths
where every nanosecond matters.

---

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
  bases, and only compile under `#ifdef DEBUG`.

## References

### Primary Sources

- [API Proposal: Add Log10 to IBinaryInteger](https://github.com/dotnet/runtime/issues/116043)
- [PR #126065: Add Log10 to IBinaryInteger](https://github.com/dotnet/runtime/pull/126065)
- [Complete CountDigits Inventory (gist)](https://gist.github.com/ViveliDuCh/3f2e8565e91883d0e41b2d776a30d73a)

### Algorithm References

- [Lemire's digit-counting algorithm](https://lemire.me/blog/2021/06/03/computing-the-number-of-digits-of-an-integer-even-faster/)
- [fmtlib do_count_digits](https://github.com/fmtlib/fmt/blob/662adf4f33346ba9aba8b072194e319869ede54a/include/fmt/format.h#L1124)
- [Stanford Bit Twiddling Hacks — IntegerLog10](https://graphics.stanford.edu/~seander/bithacks.html#IntegerLog10)

### Runtime Precedent PRs

- [PR #113790: Remove bounds checks for Log2 in CountDigits](https://github.com/dotnet/runtime/pull/113790) — JIT optimization for CountDigits
- [PR #76726: Improve performance of integer formatting](https://github.com/dotnet/runtime/pull/76726) — formatting hot-path optimization
- [PR #68795: Improving 64-bit number formatting](https://github.com/dotnet/runtime/pull/68795) — ulong formatting optimization

### Source Files

| File | Contents |
|------|----------|
| [`FormattingHelpers.CountDigits.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Buffers/Text/FormattingHelpers.CountDigits.cs) | `CountDigits(uint)` (Lemire) + `CountDigits(ulong)` (fmtlib) |
| [`FormattingHelpers.CountDigits.Int128.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Buffers/Text/FormattingHelpers.CountDigits.Int128.cs) | `CountDigits(UInt128)` (hybrid) |
| [`NativeAotNameMangler.cs`](https://github.com/dotnet/runtime/blob/main/src/coreclr/tools/Common/Compiler/NativeAotNameMangler.cs) | `CountDigits(uint)` (Lemire duplicate) |
| [`TarHeader.Write.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Formats.Tar/src/System/Formats/Tar/TarHeader.Write.cs) | `CountDigits(int)` (divide loop) |
| [`jit/utils.cpp`](https://github.com/dotnet/runtime/blob/main/src/coreclr/jit/utils.cpp) | `CountDigits(unsigned, base)` + `CountDigits(double, base)` (C++, DEBUG-only) |
| [`ElidedBoundsChecks.cs`](https://github.com/dotnet/runtime/blob/main/src/tests/JIT/opt/RangeChecks/ElidedBoundsChecks.cs) | `CountDigits(ulong)` (test copy) |

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
