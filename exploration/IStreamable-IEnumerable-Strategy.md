# IStreamable: IEnumerable-Strategy Assessment

## Objective

Evaluate whether adopting the **IEnumerable pattern** (immutable source + separate mutable
cursor) solves the DIM+boxing showstopper identified in the
[ISpanOwner-strategy exploration](./IStreamable-Exploration.md), and compare performance
against the dedicated Stream subclass approach from [dotnet/runtime#82801](https://github.com/dotnet/runtime/issues/82801).

## Background: Why This Iteration?

The previous exploration found that using DIM (Default Interface Methods) on **mutable structs**
causes a fatal showstopper: the CLR boxes value types when dispatching to DIM methods through
generic constraints, causing position mutations to be lost (silent infinite loops).

The IEnumerable pattern avoids this by **separating** the data source (immutable) from the
reading cursor (mutable, separate object):

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  IEnumerable<T> Pattern              IStreamSource Pattern                  │
│  ─────────────────────               ──────────────────────                 │
│  IEnumerable<T>        (immutable)   IStreamSource          (immutable)    │
│    └─ GetEnumerator()                  └─ CreateReader()                    │
│         ↓                                   ↓                               │
│  IEnumerator<T>        (mutable)     StreamSourceReader     (mutable)      │
│    ├─ .Current                         ├─ .Position                         │
│    ├─ .MoveNext()                      ├─ .Read(Span<byte>)                │
│    └─ (state = position in seq)        └─ (state = byte offset in data)    │
│                                                                             │
│  KEY INSIGHT: Source never mutates.                                          │
│  Boxing an immutable source loses NOTHING.                                  │
│  All mutation lives in the reader (a CLASS → no boxing).                    │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Pre-Implementation Prediction

| Dimension | Prediction | Rationale |
|---|---|---|
| **DIM correctness** | ✅ Fixed | Source is immutable → boxing is harmless |
| **DIM defaults work** | ✅ Yes | `ReadByteAt(offset)` and `CopyTo(offset, dst)` are stateless on the source |
| **Performance** | ⚠️ Slower | Extra indirection: Stream → Reader → Source (2 virtual calls instead of 1 direct) |
| **Allocation** | ⚠️ Higher | 2 objects (Stream + Reader) + boxed source vs 1 object (Dedicated) |
| **JIT specialization** | ❌ Lost | Reader holds `IStreamSource` reference (interface), not a generic `T` |

## Design

### Interface: IStreamSource (Immutable Data Provider)

```csharp
public interface IStreamSource
{
    // Core — MUST implement (like IEnumerable.GetEnumerator)
    long Length { get; }
    int ReadAt(long offset, Span<byte> buffer);  // STATELESS — no position tracking

    // DIM defaults — free behavior (like LINQ on IEnumerable)
    StreamSourceReader CreateReader() => new StreamSourceReader(this);
    int ReadByteAt(long offset) { /* 1-byte ReadAt */ }
    void CopyTo(long offset, Stream destination) { /* rented-buffer loop */ }

    // Optional override for writable sources
    bool CanWrite => false;
    void WriteAt(long offset, ReadOnlySpan<byte> buffer) => throw new NotSupportedException();
}
```

### Key Architectural Difference from ISpanOwner Strategy

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  ISpanOwner Strategy (previous)                                              │
│                                                                              │
│  StreamableStream<T>  ──embeds──▶  T : struct, IStreamable                  │
│  (Stream subclass)                 (MUTABLE struct with _position)           │
│        │                                    │                                │
│        └── .ReadByte() ──▶ _streamable.ReadByte()                           │
│                            ┌─ if T overrides: direct call (fast, correct)   │
│                            └─ if DIM default: BOX → call on copy → ∞ LOOP  │
│                                                                              │
│  IEnumerable Strategy (this iteration)                                       │
│                                                                              │
│  EnumerableSourceStream ──owns──▶ StreamSourceReader ──refs──▶ IStreamSource│
│  (Stream subclass)                (CLASS, mutable)             (IMMUTABLE)   │
│        │                               │                           │         │
│        └── .ReadByte() ──▶ reader.ReadByte()                       │         │
│                              └── source.ReadByteAt(pos)            │         │
│                                  ┌─ if source overrides: direct    │         │
│                                  └─ if DIM: box source (harmless!) │         │
│                                     DIM calls ReadAt on box        │         │
│                                     ReadAt is STATELESS → correct  │         │
└──────────────────────────────────────────────────────────────────────────────┘
```

## Correctness Results

### The Critical Test: DIM-Only Minimal Source Through Stream

```
=== IEnumerable-Strategy Correctness Tests ===

  ✅ DIM_ReadByteAt_NoInfiniteLoop
  ✅ DIM_CreateReader_Works
  ✅ Reader_ReadByte_AdvancesPosition
  ✅ CRITICAL_DIMOnly_Stream_NoInfiniteLoop     ← THE KEY TEST
  ✅ Optimized_Stream_ReadByte
  ✅ Stream_BulkRead
  ✅ Stream_Seek
  ✅ Stream_CopyTo
  ✅ DIM_CopyTo_MinimalSource
  ✅ Writable_Stream
  ✅ Compare_DIM_Behavior

=== ALL TESTS PASSED ✅ ===
```

### Side-by-Side DIM Behavior Comparison

```
  ISpanOwner DIM-only:  ReadByte() → 1, 1, 1      ← BROKEN (infinite loop)
  IEnumerable DIM-only: ReadByte() → 1, 2, 3, -1  ← CORRECT
```

The IEnumerable strategy **solves** the DIM+boxing showstopper. When
`ReadOnlyMemorySourceMinimal` (which only implements `ReadAt` + `Length`, relying on DIM
defaults for everything else) is used through `EnumerableSourceStream`:

1. `stream.ReadByte()` → `reader.ReadByte()` → `source.ReadByteAt(pos)`
2. `ReadByteAt` is a DIM default → source gets boxed
3. DIM calls `this.ReadAt(offset, buf)` on the boxed copy
4. `ReadAt` is **stateless** — it reads at a given offset, no mutation
5. Box is discarded — but **no state was lost** because the source is immutable
6. Reader increments `_position` (reader is a class, no boxing)
7. Next call uses `_position + 1` → reads next byte → **correct progression**

### Why the Showstopper Is Solved (Root Cause)

| | ISpanOwner (broken) | IEnumerable (fixed) |
|---|---|---|
| **What gets boxed?** | The mutable struct (has `_position`) | The immutable source (has only data) |
| **What mutates?** | `_position` inside the boxed copy | Nothing — source is `readonly` |
| **Where does position live?** | In the struct (lost on box) | In the reader CLASS (never boxed) |
| **State after box discarded?** | Lost → infinite loop | Nothing lost → correct |

## Benchmark Results

### Read Operations

| Operation | Size | MemoryStream | Dedicated | Streamable (ISpanOwner) | Enum Optimized | Enum DIM-Only |
|---|---:|---:|---:|---:|---:|---:|
| **ReadByte** | 100 | 211 ns | 323 ns | 315 ns | 379 ns | 805 ns |
| **ReadByte** | 10K | 13,789 ns | 26,624 ns | 30,415 ns | 34,873 ns | 77,161 ns |
| **ReadSpan** | 100 | 20.6 ns | 20.7 ns | 22.9 ns | 32.5 ns | — |
| **ReadSpan** | 10K | 218 ns | 167 ns | 108 ns | 125 ns | — |
| **CopyTo** | 100 | 38 ns | 56 ns | 51 ns | 55 ns | — |
| **CopyTo** | 10K | 1,132 ns | 1,151 ns | 1,134 ns | 1,268 ns | — |

### Write Operations

| Operation | Size | MemoryStream | Dedicated | Streamable (ISpanOwner) | Enum Optimized |
|---|---:|---:|---:|---:|---:|
| **WriteByte** | 100 | 266 ns | 362 ns | 401 ns | 852 ns |
| **WriteByte** | 10K | 21,183 ns | 35,269 ns | 36,057 ns | 79,706 ns |
| **WriteSpan** | 100 | 21.1 ns | 21.1 ns | 22.2 ns | 33.0 ns |
| **WriteSpan** | 10K | 247 ns | 263 ns | 244 ns | 297 ns |

### Allocation

| Implementation | Instance Size | Time | Ratio vs MemoryStream |
|---|---:|---:|---:|
| MemoryStream | 64 B | 12.8 ns | 1.00x |
| Dedicated | 48 B | 9.4 ns | 0.73x |
| Streamable (ISpanOwner) | 56 B | 10.8 ns | 0.84x |
| **Enum Optimized** | **104 B** | **25.1 ns** | **1.96x** |

## Analysis

### Point 1: DIM Showstopper Is Solved — But Performance Is Worse

The IEnumerable strategy **does solve** the correctness bug. DIM defaults work correctly
because the source is immutable. However, this correctness comes at a significant
performance cost:

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  ReadByte Performance (10K iterations) — Ratio vs MemoryStream              │
│                                                                              │
│  MemoryStream          ████████████████████                        1.00x     │
│  Dedicated             ████████████████████████████████████████    1.93x     │
│  Streamable (ISpanOw.) ████████████████████████████████████████    2.21x     │
│  Enum Optimized        ██████████████████████████████████████████  2.53x     │
│  Enum DIM-Only         ███████████████████████████████████████████ 5.60x     │
│                        ██████████████████████████████████████████████████    │
│                                                                              │
│  WriteByte Performance (10K iterations) — Ratio vs MemoryStream              │
│                                                                              │
│  MemoryStream          ████████████████████████                    1.00x     │
│  Dedicated             ████████████████████████████████████████    1.67x     │
│  Streamable (ISpanOw.) ████████████████████████████████████████    1.70x     │
│  Enum Optimized        ██████████████████████████████████████████  3.76x     │
│                        ██████████████████████████████████████████████████    │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Point 2: Why the IEnumerable Strategy Is Slower

The performance degradation has three root causes:

**2a. Double virtual dispatch (Stream → Reader → Source)**

```
Dedicated:        stream.ReadByte()  → direct field access (_memory.Span[_position++])
                  1 virtual call (Stream vtable)

ISpanOwner:       stream.ReadByte()  → _streamable.ReadByte()  → direct Span access
                  1 virtual + 1 devirtualized direct call (JIT-specialized)

IEnumerable:      stream.ReadByte()  → reader.ReadByte()  → source.ReadByteAt(pos)
                  1 virtual + 1 class method + 1 interface dispatch (VSD)
```

The extra indirection through `StreamSourceReader` and then through the `IStreamSource`
interface adds ~1-2 ns per call. For per-byte operations (10,000 calls), this compounds
to ~15,000-45,000 ns additional overhead.

**2b. Interface dispatch on the source**

The reader holds an `IStreamSource` reference (interface type). When calling
`_source.ReadByteAt(pos)`, the CLR uses Virtual Stub Dispatch (VSD) — the slower
interface dispatch path. Unlike the ISpanOwner strategy where the `struct` constraint
enables JIT devirtualization, the IEnumerable strategy cannot devirtualize the
source calls because the reader doesn't have a generic constraint.

**2c. Extra allocation (104B vs 48B)**

The IEnumerable strategy allocates:
- `EnumerableSourceStream`: ~40B (object header + vtable + reader ref + bool)
- `StreamSourceReader`: ~40B (object header + vtable + source ref + long + padding)
- Boxed `IStreamSource`: ~24B (object header + ReadOnlyMemory<byte> struct data)
- **Total: ~104B** vs Dedicated's 48B (+117%)

### Point 3: Bulk Operations Converge

For bulk operations (`ReadSpan`, `WriteSpan`, `CopyTo` with large buffers), the per-call
overhead is amortized across the entire buffer:

| Operation (10K) | Dedicated | Enum Optimized | Overhead |
|---|---:|---:|---:|
| ReadSpan | 167 ns | 125 ns | ~0.75x (faster!) |
| WriteSpan | 263 ns | 297 ns | ~1.13x |
| CopyTo | 1,151 ns | 1,268 ns | ~1.10x |

The overhead is negligible for bulk operations because the fixed per-call cost is paid
once, and the actual data transfer (memcpy) dominates.

### Point 4: DIM-Only is Extremely Slow (5.6x)

The DIM-only variant (`EnumDIMOnly_ReadByte`) is 5.6x slower than MemoryStream and 2.2x
slower than the optimized IEnumerable variant. This is because:

1. Each `ReadByteAt` DIM default call boxes the source struct
2. Then calls `ReadAt(offset, 1-byte span)` on the box
3. `ReadAt` creates a `Span` slice and copies 1 byte
4. The box is discarded

The per-byte overhead of boxing + span creation + copy is ~4ns, which at 10,000 iterations
is ~40,000 ns — explaining the 77,161 ns total.

This shows that while DIMs are **correct** with the IEnumerable strategy, they are
**not performant** for hot-path per-byte operations. Types must still override
`ReadByteAt` for acceptable performance.

### Point 5: The IEnumerable Analogy Is Structurally Valid But Impractical

Unlike the ISpanOwner strategy where the IEnumerable analogy was "structurally invalid"
(mutable source → broken DIMs), the IEnumerable strategy IS structurally valid:

| Pattern | IEnumerable + LINQ | IStreamSource + DIMs |
|---|---|---|
| Source mutates? | No — collection is read-only | No — source is `readonly struct` |
| Cursor is separate? | Yes — enumerator is a new object | Yes — reader is a new object |
| DIMs/extensions work? | Yes — they create new enumerators | Yes — they call stateless ReadAt |
| Boxing hazard? | No — source is immutable | No — source is immutable |

However, the analogy breaks down in **practical value**:

- LINQ extensions create **rich new behaviors** (Where, Select, GroupBy, Join) — the
  extension surface is enormous and provides massive developer value.
- IStreamSource DIMs create **trivial delegations** (ReadByteAt → ReadAt, CopyTo → ReadAt loop) —
  the extension surface is small and the defaults are slow enough that serious implementations
  override them anyway.

## Trade-offs Summary

| # | Dimension | Dedicated | ISpanOwner | IEnumerable | Winner |
|---|---|---|---|---|---|
| 1 | **DIM correctness** | N/A | ❌ Fatal bug | ✅ Fixed | IEnumerable |
| 2 | **ReadByte perf** | 1.93x | 2.21x | 2.53x | Dedicated |
| 3 | **WriteByte perf** | 1.67x | 1.70x | 3.76x | Dedicated |
| 4 | **ReadSpan perf** | ~1.0x | ~1.0x | ~1.0x | Tie |
| 5 | **WriteSpan perf** | ~1.0x | ~1.0x | ~1.1x | Tie |
| 6 | **CopyTo perf** | ~1.0x | ~1.0x | ~1.1x | Tie |
| 7 | **Allocation** | 48B | 56B | 104B | Dedicated |
| 8 | **Alloc time** | 9.4 ns | 10.8 ns | 25.1 ns | Dedicated |
| 9 | **DIM defaults useful?** | N/A | ❌ Broken | ⚠️ Correct but slow | Dedicated |
| 10 | **Code reuse** | ~95 LOC dup/type | ~95 LOC saved | ~95 LOC saved | Tie |
| 11 | **Complexity** | Simple | Medium | High (3 types) | Dedicated |
| 12 | **Dispatch chain** | 1 virtual | 1 virtual + 1 direct | 1 virtual + 1 class + 1 VSD | Dedicated |

**Score**: Dedicated wins 7, IEnumerable wins 1 (DIM correctness), Ties 4.

## Conclusion

### Finding 1: The IEnumerable Strategy Solves the DIM Showstopper

Separating immutable data source from mutable cursor eliminates the boxing+mutation bug.
DIM defaults work correctly on `readonly struct` sources because boxing an immutable
value loses nothing. This is a genuine architectural improvement over the ISpanOwner strategy.

**Evidence**: All 11 correctness tests pass. The critical comparison test proves:
- ISpanOwner DIM-only: `1, 1, 1` (broken)
- IEnumerable DIM-only: `1, 2, 3, -1` (correct)

### Finding 2: Correctness Costs Performance

The IEnumerable strategy pays for correctness with:
- **2.5x slower** ReadByte vs MemoryStream (vs Dedicated's 1.9x)
- **3.8x slower** WriteByte vs MemoryStream (vs Dedicated's 1.7x)
- **2x more** allocation (104B vs 48B)
- **2x slower** allocation time (25ns vs 9ns)

The performance gap comes from the extra indirection layer (Reader → Source interface dispatch)
that the ISpanOwner strategy avoids via JIT generic specialization.

### Finding 3: DIM Defaults Are Correct But Not Useful

While DIM defaults now **work** (no infinite loops), they are too slow for production use:
- DIM-only ReadByte: 5.6x slower than MemoryStream
- Any serious implementation must override `ReadByteAt` and `CopyTo`
- With all hot-path DIMs overridden, the "free defaults" provide negligible value

This means the IEnumerable strategy gains correctness but still doesn't deliver the
"IEnumerable experience" of writing 2 methods and getting a full surface for free —
the performance-sensitive defaults must be overridden, just like the ISpanOwner strategy.

### Finding 4: The IEnumerable Analogy Is Structurally Valid But Practically Weak

The IEnumerable analogy holds architecturally (immutable source + separate cursor + safe DIMs).
But unlike LINQ (which provides ~100 rich extension methods creating massive value),
IStreamSource DIMs provide only 3 trivial delegations (`CreateReader`, `ReadByteAt`, `CopyTo`)
that serious implementations override. The analogy is valid but the value proposition is weak.

### Recommendation: Dedicated Stream Subclasses Remain the Right Design

Both the ISpanOwner and IEnumerable strategies for IStreamable fail to deliver meaningful
advantages over dedicated Stream subclasses:

| Strategy | DIM Correctness | Performance | Complexity | Verdict |
|---|---|---|---|---|
| Dedicated classes | N/A (no DIMs) | Best | Lowest | ✅ **Recommended** |
| ISpanOwner + DIMs | ❌ Fatal bug | Good (when DIMs overridden) | Medium | ❌ Broken |
| IEnumerable + DIMs | ✅ Correct | Worst (extra indirection) | Highest (3 types) | ❌ Too costly |
| ISpanOwner, no DIMs | ✅ Correct (all overridden) | Good | Medium | ⚠️ Viable but no advantage |

The dedicated approach wins because:
- **Simplest** — one class per type, no generic parameters, no struct mutation semantics
- **Fastest** — single virtual dispatch, direct field access
- **Smallest allocation** — 48B per instance
- **Equally expressive** — each class is independently optimizable

## References

- [ISpanOwner-Strategy Exploration (previous iteration)](./IStreamable-Exploration.md)
- [CommunityToolkit.HighPerformance — ISpanOwner](https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.HighPerformance/Streams/Sources/Interfaces/ISpanOwner.cs)
- [dotnet/runtime#82801 — Stream wrappers API proposal](https://github.com/dotnet/runtime/issues/82801)
- [ViveliDuCh/runtime PR #1 — Dedicated stream implementations](https://github.com/ViveliDuCh/runtime/pull/1)
- [ECMA-335 III.2.1 — constrained. callvirt](https://www.ecma-international.org/publications-and-standards/standards/ecma-335/)
- [Virtual Stub Dispatch design doc](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/virtual-stub-dispatch.md)

## Appendix: Prototype Code

All prototype code is in this branch under `exploration/benchmarks/`:

- **IStreamSource interface + StreamSourceReader**: `EnumerableStreamTypes.cs`
- **Source implementations (ReadOnlyMemory, Memory)**: `EnumerableStreamSources.cs`
- **Stream adapter**: `EnumerableSourceStream.cs`
- **Correctness tests**: `EnumerableCorrectnessTests.cs`
- **Benchmarks**: `Benchmarks.cs` (Enum* methods)
