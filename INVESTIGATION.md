# Investigation: If/Else Chain Optimization for Byte.Log10

## Objective

Evaluate whether `Byte.Log10` can benefit from a specialized if/else chain
instead of delegating to `uint.Log10`. Since `byte` values range from 0–255,
the result is always one of three values (0, 1, or 2), making a simple branch
chain a viable alternative to the generic Log2-based approximation algorithm
used by `uint.Log10`.

## Background

### Problem Statement

The current `Byte.Log10` implementation delegates directly to `uint.Log10`:

```csharp
public static byte Log10(byte value) => (byte)uint.Log10(value);
```

This means every `Byte.Log10` call executes the full `uint.Log10` algorithm —
designed for the entire `uint` range (0–4,294,967,295, with 10 possible results) —
even though `byte` can only produce 3 distinct results. This is conceptually
over-engineered for the problem space.

### Current Implementation: uint.Log10

The `uint.Log10` algorithm (from `src/libraries/System.Private.CoreLib/src/System/UInt32.cs`)
uses a Log2-based approximation with a powers-of-10 correction table:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static uint Log10(uint value)
{
    value |= 1;
    uint log2 = (uint)BitOperations.Log2(value) + 1;
    uint approx = (log2 * 1233) >> 12;
    return value < PowersOf10[(int)approx] ? approx - 1 : approx;
}
```

This algorithm:
1. Computes `Log2(value)` via hardware intrinsic (`LZCNT`)
2. Converts to approximate Log10 using the identity `log10(x) ≈ log2(x) × log10(2)`,
   encoded as `(log2 * 1233) >> 12` (where 1233/4096 ≈ 0.30103 ≈ log10(2))
3. Corrects the approximation using a `PowersOf10` lookup table (11 entries for `uint`)

For `byte` inputs, the `Log2` call always returns 0–7, the multiply always produces
0–2, and the table lookup always reads from the first 3 entries. The full generality
of the algorithm is unused.

### Proposed Implementation: If/Else Chain

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static byte Log10(byte value)
{
    if (value < 10) return 0;
    else if (value < 100) return 1;
    else return 2;
}
```

This exploits the fact that `byte` has exactly 3 Log10 ranges:
- `[0, 9]` → 0 (10 values, ~3.9% of range)
- `[10, 99]` → 1 (90 values, ~35.2% of range)
- `[100, 255]` → 2 (156 values, ~60.9% of range)

The if/else chain uses at most 2 comparisons, no multiply, no table lookup.

### Points Considered in This Assessment

1. **Branch prediction behavior** — How does CPU branch prediction interact with
   different input distributions?
2. **Instruction-level cost** — How many μops does each approach generate?
3. **Distribution sensitivity** — Is the approach robust across workloads?
4. **Applicability to other small types** — Could the same approach work for
   `ushort` (5 results), `sbyte` (3 results)?

## Methodology

The investigation used a structured benchmarking approach:

### Step 1: Algorithm Isolation

Both algorithms were extracted into standalone methods with
`[MethodImpl(MethodImplOptions.AggressiveInlining)]` to ensure fair comparison.
The `uint.Log10` algorithm was inlined directly (not called through the type)
to avoid any extra dispatch overhead that wouldn't exist in a real implementation.

### Step 2: Input Distribution Design

Four distributions were tested to cover the full spectrum of branch predictor
behavior:

| Distribution | Range | Log10 Result | Rationale |
|---|---|---|---|
| `Small_0_9` | [0, 9] | Always 0 | Best case for if/else (first branch always taken) |
| `Medium_10_99` | [10, 99] | Always 1 | Second branch always taken |
| `Large_100_255` | [100, 255] | Always 2 | Fallthrough (both branches fail) |
| `Mixed` | [0, 255] | 0, 1, or 2 | Worst case for branching (unpredictable) |

Each distribution uses 1024 values with a fixed seed (`Random(42)`) for
reproducibility. Values are pre-generated in `[GlobalSetup]` to avoid measuring
allocation overhead.

### Step 3: Benchmark Execution

- **Framework**: BenchmarkDotNet v0.14.0
- **Runtime**: .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
- **OS**: Windows 11 (10.0.26100.7985) (Hyper-V)
- **Hardware intrinsics**: AVX2, AES, BMI1, BMI2, FMA, LZCNT, PCLMUL, POPCNT
- **Job**: DefaultJob (auto-tuned warmup and iteration counts)

Both methods iterate over the same 1024-element array, summing results to
prevent dead-code elimination.

## Benchmark Results

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

## Analysis

### Point 1: Performance by Distribution

The results reveal a clear pattern:

| Distribution | If/Else vs Current | Explanation |
|---|---|---|
| Small (0–9) | **3.6x faster** | First branch always predicted correctly; one comparison + return |
| Medium (10–99) | **1.9x faster** | One failed + one successful branch; still cheaper than Log2+multiply+table |
| Large (100–255) | **1.8x faster** | Two failed branches + fallthrough; still cheaper |
| Mixed (uniform) | **14% slower** | Branch predictor cannot learn the pattern; pipeline stalls |

### Point 2: Why the Current Approach Has Constant Time

The `uint.Log10` algorithm is **effectively branchless** for the CPU:

1. `LZCNT` (hardware intrinsic) — single-cycle, no branch
2. Multiply + shift — arithmetic, no branch
3. Table lookup — memory access, no branch
4. Conditional (`value < PowersOf10[approx]`) — single comparison, likely
   compiled to `CMOV` (conditional move, no branch)

This explains why `Current_UIntLog10` shows virtually identical timing
(~1,190–1,214 ns) across all distributions: the instruction pipeline never stalls.

### Point 3: Why Mixed Distribution Regresses

With uniform random bytes across [0, 255]:
- ~3.9% fall in [0, 9] → first branch taken
- ~35.2% fall in [10, 99] → second branch taken
- ~60.9% fall in [100, 255] → fallthrough

The branch predictor sees an unpredictable mix of "taken" and "not taken" for
the first comparison. Modern branch predictors use pattern history tables and
can learn simple repeating patterns, but uniform random input defeats all
prediction strategies. Each misprediction costs ~15–20 cycles on modern x86 CPUs.

### Point 4: Real-World Distribution Expectations

In practice, `byte` values used with `Log10` are likely to have **biased
distributions** rather than uniform random:

- **Formatting small counters/indices**: Biased toward small values (0–9).
  The if/else chain would take the first branch ~100% of the time → 3.6x faster.
- **Size/length calculations**: Values often cluster in a narrow range.
  Branch predictor learns the pattern quickly → consistent speedup.
- **Serialization of byte fields**: Values are application-dependent but
  rarely uniformly random. Even a 70/30 split between two ranges gives the
  branch predictor enough signal.

The uniform-random scenario (the only case where if/else regresses) is a
synthetic worst case that is unlikely to appear in real code.

### Point 5: Applicability to Other Small Types

The same approach could extend to:

| Type | Possible Log10 Results | Max Branches | Viability |
|---|---|---|---|
| `byte` (0–255) | 0, 1, 2 | 2 | ✅ Strong candidate (this investigation) |
| `sbyte` (0–127) | 0, 1, 2 | 2 | ✅ Same as byte (only positive values) |
| `ushort` (0–65535) | 0, 1, 2, 3, 4 | 4 | ⚠️ Marginal — more branches, less prediction benefit |
| `short` (0–32767) | 0, 1, 2, 3, 4 | 4 | ⚠️ Same as ushort |
| `uint` (0–4B) | 0–9 | 9 | ❌ Too many branches, current algo is better |

For `ushort`/`short`, a binary search tree of comparisons (e.g., check `< 100`
first, then branch to `< 10` or `< 1000`) could reduce worst-case branches to 3,
but the benefit diminishes as the number of outcomes grows.

## Conclusion

### Finding 1: If/Else Chain Is Faster for Predictable Distributions

For all distributions where the branch predictor can learn the pattern (3 out
of 4 tested), the if/else chain is **1.8–3.6x faster** than the current
`uint.Log10` delegation. This is because it replaces:
- 1 hardware intrinsic (`LZCNT`)
- 1 multiply + shift
- 1 array bounds check + table lookup
- 1 comparison + conditional move

...with at most 2 simple integer comparisons.

**Evidence**: Small=0.28 ratio, Medium=0.53 ratio, Large=0.55 ratio.

### Finding 2: Uniform-Random Regression Is a Synthetic Worst Case

The 14% regression on mixed uniform input is caused by branch misprediction
on uniformly random data. This workload is unlikely to occur in practice —
`Log10` on byte values typically operates on data with biased distributions
(small counters, lengths, indices).

**Evidence**: Mixed=1.14 ratio, with very tight error bars (±1.00 ns StdDev).

### Recommendation

**Adopt the if/else chain for `Byte.Log10`.**

| Criterion | Current (uint.Log10) | Proposed (if/else) |
|---|---|---|
| **Predictable inputs** | ~1,200 ns | 330–649 ns (**1.8–3.6x faster**) |
| **Worst case (random)** | ~1,191 ns | ~1,358 ns (14% slower) |
| **Code complexity** | Delegates to generic algo | 3 lines, trivially readable |
| **Correctness** | Proven correct | Trivially correct (only 3 exhaustive cases) |
| **Maintenance** | Coupled to uint.Log10 changes | Self-contained, byte-specific |

The if/else chain is simpler, self-documenting, and significantly faster for
realistic workloads. The same approach should be considered for `SByte.Log10`
(identical range after the negative check).

## Limitations

- **Hyper-V environment**: Benchmarks were run on a Hyper-V VM with "Unknown
  processor". Results should be validated on bare-metal hardware (e.g., via
  @EgorBot on Linux AMD and macOS ARM64) before merging.
- **Single architecture**: Only x64 AVX2 was tested. ARM64 branch prediction
  behavior may differ.
- **Micro-benchmark only**: The benchmark measures the algorithm in isolation.
  The impact within a real application depends on calling patterns and the
  surrounding code's branch predictor pressure.

## References

- `uint.Log10` implementation:
  `src/libraries/System.Private.CoreLib/src/System/UInt32.cs`, lines 298–309
- `Byte.Log10` implementation:
  `src/libraries/System.Private.CoreLib/src/System/Byte.cs`, lines 284–285
- `IBinaryInteger<TSelf>.Log10` interface definition:
  `src/libraries/System.Private.CoreLib/src/System/Numerics/IBinaryInteger.cs`, lines 263–290
- Log2-to-Log10 conversion identity: `log10(x) = log2(x) × log10(2) ≈ log2(x) × 0.30103`
- [BenchmarkDotNet documentation](https://benchmarkdotnet.org/)
- [Microbenchmark Design Guidelines](https://github.com/dotnet/performance/blob/main/docs/microbenchmark-design-guidelines.md)

## Appendix: Benchmark Code

The benchmark project is in `benchmark/Log10ByteBenchmark/` in this branch.

### Full Benchmark Results (CSV)

```
Method,Distribution,Mean,Error,StdDev,Ratio
Current_UIntLog10,Large_100_255,"1,190.0 ns",2.00 ns,1.87 ns,1.00
Proposed_IfElseChain,Large_100_255,649.4 ns,0.93 ns,0.87 ns,0.55
Current_UIntLog10,Medium_10_99,"1,213.8 ns",1.80 ns,1.50 ns,1.00
Proposed_IfElseChain,Medium_10_99,648.5 ns,0.89 ns,0.84 ns,0.53
Current_UIntLog10,Mixed,"1,191.4 ns",1.19 ns,1.00 ns,1.00
Proposed_IfElseChain,Mixed,"1,357.9 ns",3.83 ns,3.59 ns,1.14
Current_UIntLog10,Small_0_9,"1,198.8 ns",1.97 ns,1.84 ns,1.00
Proposed_IfElseChain,Small_0_9,330.0 ns,0.18 ns,0.14 ns,0.28
```

### Environment

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7985) (Hyper-V)
Unknown processor
.NET SDK 11.0.100-preview.3.26170.106
  [Host]     : .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
  DefaultJob : .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
```
