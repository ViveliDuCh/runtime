# IStreamable Interface Exploration: Technical Assessment

## Objective

Evaluate an `IStreamable` interface-based design (using struct implementations + generic
specialization) as an alternative approach for providing standardized `Stream` wrappers
over memory and text-based types in .NET. This explores whether a single generic
`StreamableStream<T>` adapter can replace the multiple dedicated `Stream` subclasses
currently proposed, achieving equivalent performance with better extensibility.

This exploration was prompted by the benchmarking work in
[dotnet/runtime#124990 review](https://github.com/dotnet/runtime/pull/124990#pullrequestreview-3878064715)
(MemoryStream Memory constructors PR), which used
[Jozkee/performance benchmark code](https://github.com/Jozkee/performance/blob/c6770508bf4f703f400c13e6fe1dd481094881d1/src/benchmarks/micro/libraries/System.IO/MemoryStreamTests.cs)
to validate the MemoryStream delegation pattern. That work demonstrated the viability of
different code organization patterns for memory-backed streams, raising the question of
whether a more generalized interface-based approach could unify them.

## Background

### Problem Statement

.NET lacks built-in `Stream` wrappers for common in-memory data types. Developers
repeatedly need to wrap `string`, `Memory<byte>`, `ReadOnlyMemory<byte>`,
`ReadOnlyMemory<char>`, and `ReadOnlySequence<byte>` as streams for interoperability
with APIs that only accept `Stream` (e.g., `XmlSerializer`, `DataContractSerializer`,
`HttpContent`).

This gap is currently filled by third-party libraries:

- [**CommunityToolkit.HighPerformance**](https://github.com/CommunityToolkit/dotnet):
  Provides `AsStream()` extension methods for `Memory<byte>`, `ReadOnlyMemory<byte>`,
  `IMemoryOwner<byte>`, `IBufferWriter<byte>`. Uses an internal `ISpanOwner` struct
  interface with `MemoryStream<TSource>` generic class — the closest existing precedent
  to the pattern explored here.
- [**Nerdbank.Streams**](https://github.com/dotnet/Nerdbank.Streams): Provides
  `AsStream()` extensions for `ReadOnlySequence<byte>`, `IBufferWriter<byte>`,
  `IDuplexPipe`, `PipeReader/PipeWriter`, `WebSocket`. Uses standalone dedicated classes
  per backing type.

### Current API Proposal Alternatives

The [API proposal](https://github.com/dotnet/runtime/issues/82801) describes several
design alternatives, all following a **"one custom Stream subclass per backing type"**
pattern:

- `ReadOnlyTextStream` for `string` / `ReadOnlyMemory<char>`
- `MemoryByteStream` (or ReadOnlyMemoryStream) for `Memory<byte>` / `ReadOnlyMemory<byte>`
- `ReadOnlySequenceStream` for `ReadOnlySequence<byte>`

These are exposed via factory methods (`Stream.FromText(...)`,
`Stream.FromReadOnlyData(...)`, etc.) or extension methods (`sequence.AsStream()`).
The proposal lists four alternative API shapes (static methods on Stream, extension
methods on the types, a StreamFactory class, or moving ReadOnlySequence to CoreLib),
but all share the same implementation architecture: separate dedicated Stream subclasses.

### This Exploration: IStreamable Alternative

Instead of N separate `Stream` subclasses, define a **single interface** (`IStreamable`)
that captures the core stream data-access contract, with **struct implementations** for
each backing type, adapted to `Stream` via a **single generic class**
`StreamableStream<TStreamable>`.

### Points Considered in This Assessment

The following dimensions were explicitly evaluated:

1. **Performance** — Does the generic approach match, exceed, or fall behind dedicated
   classes across all stream operations?
2. **Allocation / instance size** — What is the memory footprint difference?
3. **Code reuse** — How much duplicated code does the generic approach eliminate?
4. **Extensibility** — Can the interface be meaningfully extended by new types?
5. **Public API viability** — Can/should IStreamable be a public contract?
6. **Text stream fit** — Does the pattern work for encoding-based streams?
7. **Assembly layering** — How does the pattern interact with CoreLib / System.Memory boundaries?
8. **DIM (Default Interface Methods)** — Why DIM was considered and why the struct+generic
   approach was chosen instead.
9. **Debuggability / developer experience** — Stack traces, type names, IntelliSense.
10. **Mutable struct semantics** — Correctness risks from value-type position tracking.

## Methodology

The exploration followed a structured prototyping and benchmarking approach,
progressing through five steps:

### Step 1: Prior Art Analysis

Before designing anything, the existing implementations were studied:

**CommunityToolkit.HighPerformance** — The most relevant precedent. Its architecture:

```
ISpanOwner (internal interface)
├── Span<byte> Span { get; }
├── Memory<byte> Memory { get; }
└── int Length { get; }

Implementations (structs):
├── ArrayOwner         — wraps byte[] with offset/count
├── MemoryManagerOwner — wraps MemoryManager<byte>
└── ...

MemoryStream<TSource> : Stream where TSource : struct, ISpanOwner
└── Contains all stream logic (Read/Write/Seek), delegates to TSource for data access
```

Source: [`ISpanOwner.cs`](https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.HighPerformance/Streams/Sources/Interfaces/ISpanOwner.cs),
[`MemoryStream{TSource}.cs`](https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.HighPerformance/Streams/MemoryStream%7BTSource%7D.cs)

Key observation: `ISpanOwner` is a **thin data-access interface** — it only provides
`Span`, `Memory`, and `Length`. All stream semantics (position tracking, bounds checking,
seek logic) live in `MemoryStream<TSource>`. This works for homogeneous byte-buffer types
but cannot express types with fundamentally different read semantics (e.g., text encoding).

**Nerdbank.Streams** — Uses dedicated classes per type:
[`ReadOnlySequenceStream`](https://github.com/dotnet/Nerdbank.Streams/blob/main/src/Nerdbank.Streams/ReadOnlySequenceStream.cs)
is a standalone sealed class with `SequencePosition`-based tracking. No generic
specialization pattern.

**Existing dotnet/runtime prototype** — The fork's `stream-refactor-investigation` branch
has a
[`ReadOnlyTextStream`](https://github.com/ViveliDuCh/runtime/blob/stream-refactor-investigation/src/libraries/System.Private.CoreLib/src/System/IO/ReadOnlyTextStream.cs)
implementing on-the-fly encoding with `Encoder`, `byte[]` buffer, char position
tracking, and resync logic for seekability — a complex stateful type.

### Step 2: Interface Design

The `IStreamable` interface was designed as a **richer contract** than CommunityToolkit's
`ISpanOwner`, putting the stream logic (Read/Write/Seek) inside the struct itself rather
than in the generic adapter. This gives each backing type full control over its
implementation:

```csharp
internal interface IStreamable
{
    bool CanRead { get; }
    bool CanWrite { get; }
    bool CanSeek { get; }
    long Length { get; }
    long Position { get; set; }
    int Read(Span<byte> buffer);
    void Write(ReadOnlySpan<byte> buffer);
    long Seek(long offset, SeekOrigin origin);
    int ReadByte();
    void WriteByte(byte value);
    void CopyTo(Stream destination);
    void SetLength(long value);
}
```

Key design decisions and their rationale:

| Decision | Rationale |
|---|---|
| **Span-based Read/Write** | `Span<byte>` / `ReadOnlySpan<byte>` are the most efficient buffer types; avoids the `byte[]` + offset + count pattern |
| **No async members** | All backing types are in-memory — async operations are trivially synchronous. The `StreamableStream<T>` adapter wraps sync calls in `Task.FromResult` / `ValueTask`, identical to how `MemoryStream` handles async |
| **No Dispose on the interface** | Lifetime management belongs to the outer `StreamableStream<T>`. The struct holds data references but doesn't own resources |
| **ReadByte/WriteByte explicit** | These hot-path operations need per-type optimization (e.g., direct array indexing vs `Span` access), so they're interface members rather than default implementations |
| **CopyTo on the interface** | Each backing type can implement an optimized bulk copy (e.g., single `Span.CopyTo` for contiguous memory, segment-by-segment for sequences) |
| **Internal visibility** | Discussed in detail in the "Public API Viability" section below |

**Why not Default Interface Methods (DIM)?** DIM was explicitly considered and rejected
for the following reasons:

1. DIM methods dispatch through
   [virtual stub dispatch (VSD)](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/virtual-stub-dispatch.md),
   which is the slower interface dispatch path. VSD uses indirect stubs instead of
   direct vtable lookups.
2. The JIT cannot inline DIM calls through interface references. The whole performance
   benefit of the struct+generic pattern comes from the JIT specializing
   `StreamableStream<ConcreteStruct>`, enabling it to inline the struct's method
   implementations directly. DIM would negate this.
3. The JIT cannot devirtualize DIM calls on value types when called through the interface
   type, as documented in
   [CA1859](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1859).
   Only when the value type is known at compile time through a generic constraint does
   the JIT generate direct calls.

Reference: [Performance of direct virtual call vs interface call in C#](https://stackoverflow.com/questions/7225205/performance-of-direct-virtual-call-vs-interface-call-in-c-sharp)

### Step 3: Struct Implementations

Two struct implementations were prototyped to cover the read-only and read-write cases:

```csharp
// Read-only: wraps ReadOnlyMemory<byte>
internal struct ReadOnlyMemoryStreamable : IStreamable
{
    private readonly ReadOnlyMemory<byte> _memory;  // 16 bytes (object ref + int + int)
    private int _position;                           // 4 bytes
    // Total struct size: ~24 bytes (with padding)
}

// Read-write: wraps Memory<byte>
internal struct MemoryStreamable : IStreamable
{
    private readonly Memory<byte> _memory;   // 16 bytes
    private int _position;                    // 4 bytes
    private int _length;                      // 4 bytes
    private readonly bool _writable;          // 1 byte + padding
    // Total struct size: ~28 bytes (with padding)
}
```

Each struct is fully self-contained: it manages its own position, performs its own bounds
checking, and provides its own `ReadByte`/`WriteByte`/`CopyTo` implementations.

**Mutable struct concern**: The `_position` field is mutated on every read/write. This is
safe when the struct is stored as a field in `StreamableStream<T>` (class field access
does not copy), but would silently break if the struct were passed by value, stored in
a readonly field, or boxed to the `IStreamable` interface type. This is a known hazard
of mutable value types in .NET — see the discussion in Step 7 below.

### Step 4: Generic Stream Adapter

A single sealed class adapts any `IStreamable` struct to the `Stream` base class:

```csharp
internal sealed class StreamableStream<TStreamable> : Stream
    where TStreamable : struct, IStreamable
{
    private TStreamable _streamable;  // embedded struct — JIT specializes per type
    private bool _disposed;

    // All Stream overrides follow the same pattern:
    // 1. Validate arguments (using Stream.ValidateBufferArguments etc.)
    // 2. Check disposed state
    // 3. Delegate to _streamable.Method(...)
}
```

**Why the `struct` constraint is critical**: When the CLR encounters
`StreamableStream<ReadOnlyMemoryStreamable>`, the JIT generates machine code specialized
for `ReadOnlyMemoryStreamable`. All calls to `_streamable.Read(...)`,
`_streamable.ReadByte()`, etc. are resolved at JIT time to direct calls (no vtable
lookup, no interface dispatch). The JIT can then inline these calls if they're small
enough, producing code equivalent to what a hand-written dedicated class would generate.

This is the same mechanism that makes `List<int>` faster than `ArrayList` for value types
— generic specialization avoids boxing and enables direct field access.

**What this does NOT eliminate**: The outer `Stream` virtual dispatch. When a consumer
calls `stream.Read(buffer)` through a `Stream` reference, the CLR still performs a vtable
lookup to reach `StreamableStream<T>.Read`. This is unavoidable — any `Stream` subclass
pays this cost. The optimization is on the **inner** dispatch: from `StreamableStream<T>`
to the backing data source.

### Step 5: Benchmark Design

Three implementations were compared under identical conditions:

| Implementation | Description | Inner Dispatch |
|---|---|---|
| **MemoryStream** (baseline) | .NET's built-in `MemoryStream` with `byte[]` backing | Direct `byte[]` field access |
| **DedicatedStream** | A standalone `Stream` subclass per backing type (mirrors current proposal) | Direct `ReadOnlyMemory<byte>` field access |
| **StreamableStream&lt;T&gt;** | Generic adapter + IStreamable struct (this exploration) | JIT-specialized struct method call |

**Operations benchmarked**: Each operation was chosen to stress a different aspect of
the stream implementation:

| Operation | What It Tests | Hot Path Relevance |
|---|---|---|
| `ReadByte` | Per-byte overhead; the tightest inner loop | High — used by `BinaryReader`, byte-at-a-time parsing |
| `Read(Span<byte>)` | Bulk read with `Span` | High — the modern fast path for buffered I/O |
| `WriteByte` | Per-byte write overhead | Moderate — less common than bulk writes |
| `Write(ReadOnlySpan<byte>)` | Bulk write with `Span` | High — serialization, content generation |
| `CopyTo` | Optimized bulk transfer | Moderate — one-shot copies to output streams |
| Allocation | Constructor + instance size | Relevant for short-lived wrapper streams |

**Parameters**: Buffer sizes of 100 bytes (small, allocation-dominated) and 100,000 bytes
(large, throughput-dominated) were tested to capture both extremes.

**Environment**:
- BenchmarkDotNet v0.14.0
- Windows 11, x64
- .NET 9.0 runtime
- ShortRun job: 3 warmup iterations, 3 measured iterations
- `[MemoryDiagnoser]` enabled for allocation tracking (Gen0/Gen1/Gen2 + Allocated bytes)

## Benchmark Results

### Read Operations

| Operation | Size | MemoryStream | Dedicated | Streamable&lt;T&gt; | Streamable Ratio |
|---|---:|---:|---:|---:|---:|
| **ReadByte** | 100 | 84.8 ns | 180.7 ns | 177.9 ns | **0.98x** (vs Dedicated) |
| **ReadByte** | 100K | 64,676 ns | 155,491 ns | 157,285 ns | **1.01x** |
| **ReadSpan** | 100 | 10.0 ns | 10.7 ns | 10.7 ns | **1.00x** |
| **ReadSpan** | 100K | 1,981 ns | 2,180 ns | 1,992 ns | **0.91x** ✓ |
| **CopyTo** | 100 | 38.5 ns | 51.3 ns | 53.6 ns | **1.04x** |
| **CopyTo** | 100K | 46,363 ns | 41,599 ns | 40,770 ns | **0.98x** ✓ |

### Write Operations

| Operation | Size | MemoryStream | Dedicated | Streamable&lt;T&gt; | Streamable Ratio |
|---|---:|---:|---:|---:|---:|
| **WriteByte** | 100 | 154.9 ns | 223.2 ns | 216.1 ns | **0.97x** ✓ |
| **WriteByte** | 100K | 125,077 ns | 195,974 ns | 207,694 ns | **1.06x** |
| **WriteSpan** | 100 | 10.4 ns | 10.3 ns | 10.8 ns | **1.05x** |
| **WriteSpan** | 100K | 1,972 ns | 2,065 ns | 2,077 ns | **1.01x** |

> **Ratio column**: Streamable&lt;T&gt; vs Dedicated (values < 1.0 mean Streamable is
> faster; ✓ indicates Streamable matched or beat Dedicated).

### Allocation

| Implementation | Instance Size | Ratio vs MemoryStream |
|---|---:|---:|
| MemoryStream | 64 B | 1.00x |
| DedicatedStream | 48 B | 0.75x |
| StreamableStream&lt;T&gt; | 56 B | 0.88x |

## Analysis

### Point 1: Performance — Generic Specialization Achieves Parity

**StreamableStream&lt;T&gt; and dedicated Stream subclasses are performance-equivalent.**
Across all benchmarked operations, the two approaches show ratios between 0.91x and
1.06x — well within measurement noise for a ShortRun benchmark configuration.

**Why this result is expected**: The JIT generates code for
`StreamableStream<ReadOnlyMemoryStreamable>` that is structurally identical to what a
hand-written `DedicatedReadOnlyMemoryStream` would produce. Both end up as:

```
Stream.Read(Span) override → ObjectDisposedException check → ReadOnlyMemory.Span access → Slice + CopyTo
```

The generic constraint `where TStreamable : struct, IStreamable` ensures that calls to
`_streamable.Read(buffer)` are devirtualized at JIT time. No interface dispatch stub is
generated. The compiler emits a direct call (or inlines it entirely for small methods).

This confirms the pattern documented in CommunityToolkit.HighPerformance's
[`MemoryStream<TSource>`](https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.HighPerformance/Streams/MemoryStream%7BTSource%7D.cs),
which uses the same `where TSource : struct, ISpanOwner` constraint for the same reason.

**Rationale implication**: Performance cannot be used to argue for or against the
IStreamable approach. It is neutral. The decision must be based on other factors.

### Point 2: Why MemoryStream is Faster at Per-Byte Operations

Both Dedicated and Streamable implementations are ~2x slower than `MemoryStream` for
`ReadByte`/`WriteByte`. This is **not** due to generic/interface overhead — it's because:

1. **MemoryStream uses direct `byte[]` array access**: `_buffer[_position++]` compiles to
   a simple array bounds check + load, which the JIT optimizes aggressively (the array
   length is a direct field read, the bounds check is a single compare+branch).
2. **Memory-backed implementations use `ReadOnlyMemory<byte>.Span`**: This accessor goes
   through `MemoryMarshal` indirection to obtain a `Span<byte>`, then indexes into it.
   The indirection involves checking the `_object` field type and computing the span
   start, adding ~1-2ns per call that compounds in byte-at-a-time loops.

This is an inherent characteristic of `ReadOnlyMemory<byte>` vs `byte[]`, not of the
generic approach. Any `ReadOnlyMemory<byte>`-backed stream — whether dedicated or
generic — will exhibit this difference. The
[PR #124990](https://github.com/dotnet/runtime/pull/124990) for MemoryStream Memory
constructors addresses this by deferring the `_memoryData` null check past shared
early-return checks, minimizing IL size impact on the byte-array fast path.

For bulk operations (`Read(Span<byte>)`, `Write(ReadOnlySpan<byte>)`, `CopyTo`), this
overhead is amortized across the entire buffer and all three approaches converge to
equivalent performance (~10ns for 100B, ~2000ns for 100KB).

### Point 3: Allocation Analysis

| Implementation | Instance Size | Components |
|---|---:|---|
| MemoryStream | 64 B | Object header + vtable ptr + byte[] ref + many int fields (position, length, capacity, origin, etc.) + booleans |
| DedicatedStream | 48 B | Object header + vtable ptr + ReadOnlyMemory&lt;byte&gt; (16B) + int _position + bool _disposed |
| StreamableStream&lt;T&gt; | 56 B | Object header + vtable ptr + TStreamable struct (ReadOnlyMemory + int = ~20B) + bool _disposed + padding |

The 8-byte difference between StreamableStream (56B) and Dedicated (48B) is due to
struct alignment padding — the embedded `TStreamable` struct must be aligned to its
largest member's alignment requirement.

**Rationale implication**: The allocation difference is negligible. Stream instances are
typically long-lived relative to the data they process. Even under high-frequency
allocation (thousands of short-lived wrapper streams per second), the 8-byte difference
would not be observable in practice.

## Point 4: IStreamable as a Public Interface — Feasibility Assessment

### Could IStreamable be Exposed Publicly?

The interface could theoretically be made public, enabling third-party types to provide
stream-like behavior through a standardized contract. However, several significant
barriers exist, each examined below:

#### 4a. Mutable Struct Semantics

The IStreamable struct implementations are **mutable** (they update `_position` during
reads). When stored as a field in `StreamableStream<T>`, mutations are correctly
reflected because field access on a class does not copy the struct. However:

- **Passing by value copies the struct**, losing position state:
  ```csharp
  ReadOnlyMemoryStreamable s = new(data);
  s.Read(buffer);  // s._position is now 10
  DoSomething(s);  // passes a COPY — DoSomething sees position=10 but its reads don't affect s
  ```
- **Readonly fields/locals prevent mutation**, causing compile errors or silent bugs.
- **Interface boxing** (casting to `IStreamable`) copies the struct, breaking
  statefulness:
  ```csharp
  IStreamable boxed = new ReadOnlyMemoryStreamable(data);  // boxed copy
  boxed.Read(buffer);  // reads from the boxed copy — position state is on the heap
  // The original struct (if any) is unchanged
  ```

This is well-documented in the .NET design guidelines and is the reason
[CA1859](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1859)
recommends concrete types over interfaces for value types.

The CommunityToolkit works around this by keeping `ISpanOwner` as a **private internal
contract** — it's never exposed to consumers. This is the correct approach.

#### 4b. Stream is the Consumer Contract

The .NET ecosystem's consumer contract for I/O is `Stream`. APIs accept `Stream`
parameters, not `IStreamable`. Making `IStreamable` public wouldn't change how consumers
interact with streams — they'd still receive a `Stream` object. The interface only affects
the **provider** side (how backing types implement the data-access logic).

There is no practical benefit to exposing `IStreamable` publicly because:
- **Consumers can't use it directly** — they need `Stream` for `XmlSerializer`,
  `HttpContent`, `BinaryReader`, `CopyToAsync`, etc.
- **Providers can implement `Stream` directly** — as dedicated classes do, without any
  new abstraction.
- **The generic specialization benefit only materializes** when `TStreamable` is a
  compile-time known struct, which requires `StreamableStream<TStreamable>` — an
  implementation detail that cannot be hidden behind a non-generic factory method.

To clarify: `Stream.FromReadOnlyData(ReadOnlyMemory<byte> data)` would internally return
`new StreamableStream<ReadOnlyMemoryStreamable>(new ReadOnlyMemoryStreamable(data))`.
The consumer sees `Stream`, not `IStreamable` or `StreamableStream<T>`. The interface
is purely internal plumbing.

#### 4c. .NET Runtime Type System Constraints

- **`string`** cannot implement interfaces retroactively. `string : IStreamable` would
  require modifying `System.String` in CoreLib — a non-starter.
- **`ReadOnlyMemory<T>`** is a `readonly struct` that cannot be modified to implement
  new interfaces without changing CoreLib, and its readonly-ness conflicts with the
  mutable `_position` requirement of stream semantics.
- **`ReadOnlySequence<T>`** lives in `System.Memory.dll`, adding layering complexity
  (see Point 8 below).

For all these types, the IStreamable pattern requires **wrapper structs** (e.g.,
`ReadOnlyMemoryStreamable` wrapping `ReadOnlyMemory<byte>`), adding a layer of
indirection that provides no API surface benefit to the end user.

#### 4d. Default Interface Methods (DIM) Overhead

If DIM were used to provide shared logic in the interface (e.g., default `ReadByte`
implementation calling `Read` with a 1-byte span), the performance benefit would be lost:

1. DIM methods use
   [virtual stub dispatch (VSD)](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/virtual-stub-dispatch.md).
   VSD is currently used **only** for interface dispatch, not for virtual instance
   methods on classes. As documented in the VSD design doc: *"virtual instance method
   calls suffer an unacceptable speed degradation"* — VSD was disabled for class virtual
   methods for this reason, but interface dispatch still uses it.

2. When calling a DIM through a generic constraint, the JIT can potentially
   devirtualize, but this is not guaranteed for all DIM patterns. The struct+abstract
   interface member pattern (where each struct provides its own implementation) is
   reliably devirtualized.

3. Reference: [Andrew Lock: Using DIM for performance in IHeaderDictionary](https://andrewlock.net/using-default-interface-methods-for-performance-gains-in-iheaderdictionary/)
   documents a case where DIM was used successfully in ASP.NET Core, but notably in that
   case the DIM provided **higher-level** default behavior that delegated to
   **lower-level** members that each type overrides — not the other way around.

**Rationale**: DIM is useful for evolving interfaces and providing opt-in higher-level
methods. It is not suitable as the primary dispatch mechanism for hot-path stream
operations. The struct+generic approach achieves the same code sharing goal without
the dispatch overhead.

### Point 4 Verdict: Internal Contract Only

IStreamable should remain an **internal implementation detail**, not a public API surface.
This mirrors the pattern used by:
- CommunityToolkit.HighPerformance:
  [`ISpanOwner`](https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.HighPerformance/Streams/Sources/Interfaces/ISpanOwner.cs)
  — internal interface.
- .NET runtime itself: Many internal interfaces/structs exist for generic specialization
  (e.g., `ISpanFormattable` implementations in formatting code, `TArg` patterns in
  string formatting) without being publicly exposed as the primary API.

## Point 5: Comparison with Existing Approaches

### CommunityToolkit.HighPerformance

The CommunityToolkit uses an almost identical pattern:

```
ISpanOwner (interface)          ≈  IStreamable (this exploration)
├── ArrayOwner (struct)         ≈  ReadOnlyMemoryStreamable
├── MemoryManagerOwner (struct) ≈  MemoryStreamable
└── ...
MemoryStream<TSource> (class)   ≈  StreamableStream<TStreamable>
```

Key differences in design philosophy:

| Aspect | CommunityToolkit `ISpanOwner` | This Exploration `IStreamable` |
|---|---|---|
| **Interface scope** | Thin data accessor: `Span`, `Memory`, `Length` | Full stream contract: Read, Write, Seek, CopyTo |
| **Stream logic location** | In the generic class (`MemoryStream<TSource>`) | In the struct (each struct controls its own logic) |
| **Heterogeneous types** | Cannot express types with different read semantics | Can express encoding, multi-segment, etc. |
| **Code sharing** | More shared code (all logic in one generic class) | Less shared code (each struct has its own logic) |

The IStreamable approach is more flexible because `ReadOnlyTextStream` needs on-the-fly
encoding with `Encoder`, `byte[]` buffer, and char position tracking — this cannot be
expressed as just `Span` + `Length`. However, this flexibility comes at the cost of more
code per struct implementation.

### Nerdbank.Streams

Nerdbank.Streams uses the traditional one-class-per-type pattern:
[`ReadOnlySequenceStream`](https://github.com/dotnet/Nerdbank.Streams/blob/main/src/Nerdbank.Streams/ReadOnlySequenceStream.cs)
is a standalone sealed class. It also provides additional features not present in the
IStreamable prototype:
- `disposeAction` callback for buffer recycling
- Cached `Task<int>` reuse for repeated reads of the same byte count
- `SequencePosition`-based seeking (preserves multi-segment structure)

These features are type-specific optimizations that wouldn't naturally fit into a generic
`IStreamable` interface — they demonstrate the value of dedicated implementations.

## Point 6: Extensibility Analysis

### Coverage of Types from the API Proposal

| Source Type | IStreamable Struct | Feasibility | Notes |
|---|---|---|---|
| `ReadOnlyMemory<byte>` | `ReadOnlyMemoryStreamable` | ✅ Straightforward | Prototyped and benchmarked |
| `Memory<byte>` | `MemoryStreamable` | ✅ Straightforward | Prototyped and benchmarked |
| `string` | `StringStreamable` | ⚠️ Complex | Requires Encoder, byte[] buffer, resync logic |
| `ReadOnlyMemory<char>` | `ReadOnlyMemoryCharStreamable` | ⚠️ Complex | Same encoding complexity as string |
| `ReadOnlySequence<byte>` | `ReadOnlySequenceStreamable` | ⚠️ Layering | Lives in System.Memory.dll |

### Point 6a: Text Streams Don't Fit the Pattern

The `ReadOnlyTextStream` (for `string` / `ReadOnlyMemory<char>`) requires:

- An `Encoder` instance (reference type, stateful)
- A `byte[]` encoding buffer (reference type, 4KB default)
- A `_charPosition` integer for tracking encoder progress
- A `_byteBufferPosition` / `_byteBufferCount` for the encoding window
- A `_needsResync` flag for seek-then-read correctness
- A `_cachedLength` nullable for lazy byte-count computation

Fitting this into an `IStreamable` struct produces a struct with:
- 2+ reference-type fields (Encoder, byte[])
- 6+ value-type fields (positions, counts, flags)
- Total size: >64 bytes

This negates the value-type benefits:
- Large structs are expensive to copy (function calls, assignments).
- Reference-type fields in structs don't benefit from stack allocation.
- The JIT may not inline methods on large structs.
- The struct becomes too large to fit in registers.

The existing
[`ReadOnlyTextStream`](https://github.com/ViveliDuCh/runtime/blob/stream-refactor-investigation/src/libraries/System.Private.CoreLib/src/System/IO/ReadOnlyTextStream.cs)
prototype is implemented as a dedicated sealed class precisely because of this complexity.
Forcing it into the IStreamable mold would add indirection without benefit.

### Point 6b: ReadOnlySequence Layering

`ReadOnlySequence<byte>` lives in `System.Memory.dll`, while `IStreamable` and
`StreamableStream<T>` would need to be in `System.Private.CoreLib` (where `Stream` lives).
A `ReadOnlySequenceStreamable` struct would need to be in `System.Memory.dll`, and the
generic specialization of `StreamableStream<ReadOnlySequenceStreamable>` would need to
cross assembly boundaries.

This is technically solvable — the JIT handles cross-assembly generic instantiation —
but adds complexity to the layering story. The current proposal addresses this with
either a C#14 static extension on `Stream` or a classic extension method on
`ReadOnlySequence<byte>`, both of which are simpler and already under review.

## Point 7: Code Reuse Assessment

The main concrete advantage of the IStreamable approach is code reuse. Here's what's
shared vs duplicated in each approach:

### Shared in StreamableStream&lt;T&gt; (not duplicated per type):

| Logic | Lines Saved Per Type |
|---|---:|
| `ObjectDisposedException.ThrowIf` checks | ~20 |
| `ValidateBufferArguments` calls | ~10 |
| `CancellationToken` handling in async methods | ~30 |
| `Task.FromResult` / `ValueTask` wrapping | ~20 |
| `Dispose` / `DisposeAsync` | ~10 |
| `Flush` / `FlushAsync` (no-ops) | ~5 |
| **Total per additional backing type** | **~95** |

For 5 backing types, this saves ~380 lines of boilerplate. However, this boilerplate is
straightforward, well-understood code that is easy to write correctly and easy to review.
The complexity cost of the generic pattern (understanding struct mutation semantics,
generic specialization, the two-level dispatch architecture) may exceed the benefit of
saving ~95 lines per type.

### Dedicated approach duplication is manageable:

Each dedicated `Stream` subclass is ~120-150 lines. For 5 types, that's ~600-750 lines
total. With the IStreamable approach, it's ~5 structs of ~60 lines + ~150 lines for
`StreamableStream<T>` = ~450 lines. The net savings is ~150-300 lines across the entire
feature.

## Point 8: Trade-offs Summary

| # | Dimension | Dedicated Classes | IStreamable + Generic | Winner |
|---|---|---|---|---|
| 1 | **Performance** | Baseline | **Equivalent** (0.91-1.06x, benchmarked) | Tie |
| 2 | **Allocation** | 48 B per instance | 56 B per instance (+17%) | Dedicated (marginal) |
| 3 | **Code reuse** | ~95 lines duplicated per type | Shared in StreamableStream&lt;T&gt; | IStreamable |
| 4 | **Extensibility** | New Stream subclass per type | New struct per type | Tie |
| 5 | **Complexity** | Simple inheritance | Generic specialization + struct mutation | Dedicated |
| 6 | **Text streams** | ✅ Natural fit | ⚠️ Large struct, reduced benefit | Dedicated |
| 7 | **ReadOnlySequence** | Extension method (current proposal) | Cross-assembly generic instantiation | Dedicated |
| 8 | **Public API surface** | Factory methods on Stream | Same (IStreamable stays internal) | Tie |
| 9 | **Debuggability** | Direct class names in stack traces | `StreamableStream<ReadOnlyMemoryStreamable>` in stack traces | Dedicated |
| 10 | **Reviewability** | Each class self-contained, easy to review | Requires understanding generic specialization pattern | Dedicated |

**Score**: Dedicated wins 5 dimensions, IStreamable wins 1, and 4 are ties.

## Point 9: Limitations Encountered

1. **Struct mutation semantics** (correctness risk): Mutable structs implementing
   `IStreamable` require careful handling. The struct must be stored as a **field** (not a
   local, not boxed) to preserve position state across method calls. This is a footgun
   for any future developer modifying the code — incorrectly passing the struct by value
   or storing it in a readonly field would introduce silent bugs. Dedicated classes
   eliminate this entire class of bugs.

2. **No zero-cost outer dispatch**: While the inner dispatch (IStreamable → concrete
   struct) is eliminated by generic specialization, the outer dispatch (caller →
   StreamableStream → Stream) still goes through virtual method tables. This is
   unavoidable for any Stream subclass. It means the IStreamable pattern cannot provide
   a performance advantage over dedicated classes — only parity.

3. **Text encoding doesn't fit the pattern well**: `ReadOnlyTextStream` needs
   stateful encoding (Encoder, byte buffer, char position tracking), producing a
   struct >64 bytes with reference-type fields. This negates the value-type advantages
   (no stack allocation benefit, expensive copies, JIT may not inline). For 2 of the
   5 target types (string, ReadOnlyMemory&lt;char&gt;), the IStreamable pattern provides
   no benefit.

4. **Not publicly extensible in practice**: While the interface is technically
   implementable by external code, the struct + generic constraint pattern means
   consumers would need to instantiate `StreamableStream<TheirStruct>` explicitly,
   which is awkward vs the proposed factory method pattern. And since `Stream` remains
   the consumer contract, there's no scenario where a consumer would benefit from
   knowing about `IStreamable`.

5. **Allocation overhead**: 56 bytes vs 48 bytes (dedicated) per instance due to struct
   alignment padding in the generic class. Marginal in isolation, but the dedicated
   approach is strictly better on this dimension.

6. **Debuggability**: Stack traces show `StreamableStream<ReadOnlyMemoryStreamable>.Read`
   instead of `ReadOnlyMemoryStream.Read`. While not a blocking issue, it adds cognitive
   load when debugging. Exception messages referencing the generic type are also less
   readable.

7. **JIT code size**: The JIT generates separate code for each `StreamableStream<T>`
   instantiation. With 5 backing types, this means 5 copies of the adapter's validation
   and async wrapping logic in the JIT output. Dedicated classes share no code at the
   machine code level either (each class gets its own vtable entries), so this is roughly
   equivalent, but worth noting that the generic approach does not save JIT compilation
   time or code size.

## Conclusion

### Finding 1: Generic Specialization Achieves Performance Parity

The benchmarks definitively show that `StreamableStream<TStreamable>` and dedicated
`Stream` subclasses produce **equivalent performance** across all measured operations
(0.91x to 1.06x ratio range). The JIT's generic specialization for struct type parameters
successfully eliminates interface dispatch overhead, confirming the pattern used by
CommunityToolkit.HighPerformance.

**Evidence**: All 10 benchmark data points in the results section.
**Implication**: Performance is not a differentiating factor between the approaches.

### Finding 2: The Pattern Has Significant Practical Limitations

For the specific use case being addressed (wrapping `Memory<byte>`,
`ReadOnlyMemory<byte>`, `string`, `ReadOnlyMemory<char>`, `ReadOnlySequence<byte>`
as streams):

1. **Performance is equivalent, not better.** The generic approach doesn't outperform
   dedicated classes — it matches them. When two approaches have equal performance, the
   simpler one should be preferred (per the general engineering principle of choosing
   the simplest correct solution).

2. **Text streams don't fit the pattern.** The encoding pipeline required for `string` /
   `ReadOnlyMemory<char>` → `Stream` conversion produces structs too large and complex
   to benefit from value-type semantics. This means 2 of the 5 target types would still
   need dedicated classes, creating a hybrid architecture that's harder to understand
   than a uniform dedicated-class approach.

3. **Extensibility is theoretical, not practical.** The IStreamable interface cannot be
   meaningfully exposed as a public API due to:
   - Mutable struct semantics creating correctness hazards (Point 4a)
   - `Stream` being the established consumer contract (Point 4b)
   - Existing types (string, ReadOnlyMemory) not being retroactively extensible (Point 4c)
   - DIM overhead making shared default implementations slower (Point 4d)

4. **Code sharing is the main advantage, and it's modest.** The IStreamable approach
   saves ~95 lines of boilerplate per additional backing type (~150-300 lines total across
   the feature). This is real but modest, and the boilerplate being saved is
   straightforward validation/async-wrapping code that is easy to write correctly.

5. **The complexity cost outweighs the code savings.** Understanding the IStreamable
   architecture requires knowledge of:
   - Generic specialization and JIT behavior
   - Mutable struct semantics and their pitfalls
   - The two-level dispatch architecture (outer virtual + inner specialized)
   - Why the struct must be a field, not a local or readonly

   This complexity burden applies to every future developer who reads, modifies, or
   reviews the code. For a feature with 5 backing types (a small, fixed set), this
   overhead is not justified by the modest code savings.

### Recommendation

The **dedicated Stream subclass approach** (current proposal) is the right design for
the .NET runtime:

- **Simpler** — no generic type parameters, no struct mutation semantics to reason about.
  Each class is self-contained and independently understandable.
- **Equally performant** — as the benchmarks confirm across all operations.
- **Better fit for the full type range** — especially text streams with encoding, which
  need dedicated classes regardless.
- **Cleaner public API** — factory methods on `Stream` that return `Stream`, hiding all
  implementation details. The consumer never needs to know about the backing implementation.
- **More debuggable** — stack traces show descriptive class names like
  `ReadOnlyMemoryStream.Read` rather than `StreamableStream<ReadOnlyMemoryStreamable>.Read`.

### Where IStreamable Makes Sense

The IStreamable pattern **is** valuable in specific contexts:

- **Library code** (like CommunityToolkit) where a single generic implementation covers
  multiple backing types, reducing package size and maintenance burden. CommunityToolkit
  serves a different audience — multi-TFM NuGet package consumers — where shipping fewer
  compiled types matters.
- **High extensibility requirements** where new backing types are frequently added by
  different teams/contributors and code duplication becomes a real maintenance problem.
- **Cases where all backing types are homogeneous** (all provide `Span` + `Length` with
  identical read/write semantics), making the interface thin and the struct implementations
  trivial.

For the .NET runtime's stream wrapper feature, where the number of backing types is fixed
at 5, where 2 of those types require complex encoding logic, and where the implementations
need type-specific optimizations, individual dedicated classes provide the better
engineering trade-off.

## References

- [CommunityToolkit.HighPerformance — ISpanOwner interface](https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.HighPerformance/Streams/Sources/Interfaces/ISpanOwner.cs)
- [CommunityToolkit.HighPerformance — MemoryStream&lt;TSource&gt;](https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.HighPerformance/Streams/MemoryStream%7BTSource%7D.cs)
- [Nerdbank.Streams — ReadOnlySequenceStream](https://github.com/dotnet/Nerdbank.Streams/blob/main/src/Nerdbank.Streams/ReadOnlySequenceStream.cs)
- [Nerdbank.Streams — StreamExtensions.AsStream](https://github.com/dotnet/Nerdbank.Streams/blob/main/src/Nerdbank.Streams/StreamExtensions.cs)
- [.NET Runtime — Virtual Stub Dispatch design doc](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/virtual-stub-dispatch.md)
- [CA1859: Prefer concrete types for performance](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1859)
- [Default Interface Methods in C#](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/interface-implementation/default-interface-methods-versions)
- [Performance of direct virtual call vs interface call in C#](https://stackoverflow.com/questions/7225205/performance-of-direct-virtual-call-vs-interface-call-in-c-sharp)
- [Andrew Lock: Using DIM for performance in IHeaderDictionary](https://andrewlock.net/using-default-interface-methods-for-performance-gains-in-iheaderdictionary/)
- [How to avoid boxing structs that implement interfaces](https://giannisakritidis.com/blog/Avoid-Struct-Boxing/)
- [C# 13 ref struct interfaces and allows ref struct constraint](https://blog.ndepend.com/c-13-ref-struct-interfaces-and-the-allows-ref-struct-generic-anti-constraint/)

## Appendix: Prototype Code

The prototype code is in this branch:

- **Interface**: `src/libraries/System.Private.CoreLib/src/System/IO/Streamable/IStreamable.cs`
- **ReadOnlyMemory struct**: `src/libraries/System.Private.CoreLib/src/System/IO/Streamable/ReadOnlyMemoryStreamable.cs`
- **Memory struct**: `src/libraries/System.Private.CoreLib/src/System/IO/Streamable/MemoryStreamable.cs`
- **Generic adapter**: `src/libraries/System.Private.CoreLib/src/System/IO/Streamable/StreamableStream.cs`
- **Benchmarks**: `exploration/benchmarks/`

### Full Benchmark Results

```
BenchmarkDotNet v0.14.0, Windows
.NET SDK 10.0.104
  [Host]         : .NET 9.0
  ShortRun-.NET 9.0 : .NET 9.0, 3 iterations, 3 warmups

| Operation  | Size   | MemoryStream     | Dedicated       | Streamable<T>   | Alloc (MS/D/S)     |
|------------|--------|------------------|-----------------|-----------------|--------------------|
| ReadByte   | 100    | 84.8 ns          | 180.7 ns        | 177.9 ns        | 64B / 48B / 56B    |
| ReadByte   | 100K   | 64,676 ns        | 155,491 ns      | 157,285 ns      | 64B / 48B / 56B    |
| ReadSpan   | 100    | 10.0 ns          | 10.7 ns         | 10.7 ns         | 64B / 48B / 56B    |
| ReadSpan   | 100K   | 1,981 ns         | 2,180 ns        | 1,992 ns        | 64B / 48B / 56B    |
| CopyTo     | 100    | 38.5 ns          | 51.3 ns         | 53.6 ns         | 408B / 392B / 400B |
| CopyTo     | 100K   | 46,363 ns        | 41,599 ns       | 40,770 ns       | ~100KB (all same)  |
| WriteByte  | 100    | 154.9 ns         | 223.2 ns        | 216.1 ns        | 64B / 48B / 56B    |
| WriteByte  | 100K   | 125,077 ns       | 195,974 ns      | 207,694 ns      | 64B / 48B / 56B    |
| WriteSpan  | 100    | 10.4 ns          | 10.3 ns         | 10.8 ns         | 64B / 48B / 56B    |
| WriteSpan  | 100K   | 1,972 ns         | 2,065 ns        | 2,077 ns        | 64B / 48B / 56B    |
| Alloc      | 1000   | 7.0 ns           | 6.4 ns          | 8.0 ns          | 64B / 48B / 56B    |
```
