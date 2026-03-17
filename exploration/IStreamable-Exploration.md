# IStreamable Interface Exploration: Technical Assessment

## Objective

Explore an `IStreamable` interface using **Default Interface Methods (DIMs)** as a
standardized extensibility contract — analogous to how `IEnumerable<T>` standardizes
iteration: a type implements one core method (`GetEnumerator()`) and gets the entire
LINQ surface for free. The goal is to evaluate whether `IStreamable` could let types
like `string`, `Memory<byte>`, `ReadOnlyMemory<byte>`, `ReadOnlySequence<byte>`,
`ReadOnlyMemory<char>` implement a few core members (`Read`, `Length`, `Position`)
and automatically get `ReadByte()`, `Seek()`, `CopyTo()`, etc. via DIM defaults —
standardizing a common developer need currently served only by third-party libraries
([CommunityToolkit.HighPerformance](https://github.com/CommunityToolkit/dotnet),
[Nerdbank.Streams](https://github.com/dotnet/Nerdbank.Streams)).

This exploration was prompted by the benchmarking work in
[dotnet/runtime#124990 review](https://github.com/dotnet/runtime/pull/124990#pullrequestreview-3878064715)
and the stream wrapper prototypes in [ViveliDuCh/runtime PR #1](https://github.com/ViveliDuCh/runtime/pull/1).

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

### This Exploration: IStreamable with DIMs

The core idea: define an `IStreamable` interface where implementers provide only
**3 core members** and get everything else from **DIM defaults**:

```csharp
interface IStreamable
{
    // Core — MUST implement (like IEnumerable.GetEnumerator)
    long Length { get; }
    long Position { get; set; }
    int Read(Span<byte> buffer);

    // DIM defaults — get these for FREE (like LINQ on IEnumerable)
    int ReadByte() { /* calls Read() with 1-byte span */ }
    long Seek(long offset, SeekOrigin origin) { /* uses Position + Length */ }
    void CopyTo(Stream destination) { /* reads in loop */ }
    bool CanRead => true;
    bool CanWrite => false;
    bool CanSeek => true;
    void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();
    void WriteByte(byte value) => throw new NotSupportedException();
    void SetLength(long value) => throw new NotSupportedException();
}
```

A minimal read-only implementation would be just ~15 lines:

```csharp
struct ReadOnlyMemoryStreamableMinimal : IStreamable
{
    private readonly ReadOnlyMemory<byte> _memory;
    private int _position;
    public long Length => _memory.Length;
    public long Position { get => _position; set => _position = (int)value; }
    public int Read(Span<byte> buffer) { /* copy from _memory */ }
    // ReadByte, Seek, CopyTo, etc. — all from DIMs, zero code needed
}
```

The adapter `StreamableStream<T> where T : struct, IStreamable` wraps any IStreamable
struct as a `Stream`, combining DIM defaults with JIT generic specialization.

## Critical Finding: DIM + Mutable Struct = Broken Semantics

> **⚠️ SHOWSTOPPER**: Default Interface Methods on mutable structs through generic
> constraints cause **silent infinite loops** due to CLR boxing behavior.

### The Problem

When `StreamableStream<T>` calls `_streamable.ReadByte()` on a struct `T` that doesn't
override `ReadByte()` (relying on the DIM default), the CLR emits a `constrained callvirt`
instruction. For DIM methods not implemented by the struct, the runtime **boxes the
struct** to dispatch to the interface's default method body. The DIM's `ReadByte()`
then calls `this.Read(...)`, which mutates `_position` on the **boxed copy**. The box
is discarded after the call. The original struct's `_position` never advances.

### Proof

```
Test: StreamableStream<ReadOnlyMemoryStreamableMinimal> over [1, 2, 3]
  stream.ReadByte() → 1  (reads position 0, box advances to 1, box discarded)
  stream.ReadByte() → 1  (reads position 0 again, same result)
  stream.ReadByte() → 1  (infinite loop — position never advances)
  stream.ReadByte() → 1  ...

Control: StreamableStream<ReadOnlyMemoryStreamable> (with explicit ReadByte override)
  stream.ReadByte() → 1  (direct call, no boxing, position advances)
  stream.ReadByte() → 2
  stream.ReadByte() → 3
  stream.ReadByte() → -1 (end of data)
```

### Root Cause

The CLR specification for `constrained. callvirt` ([ECMA-335 III.2.1](https://www.ecma-international.org/publications-and-standards/standards/ecma-335/)):

> If `thisType` is a value type and `thisType` does not implement `method` then
> ptr is dereferenced, boxed, and passed as the 'this' pointer to the callvirt
> method instruction.

This means for any DIM method that a value type doesn't explicitly implement:
1. The value is boxed (copied to the heap)
2. The DIM body runs on the boxed copy
3. Any state mutations (`_position++`) happen on the boxed copy
4. The boxed copy is discarded — original struct is unchanged
5. Next call: same state as before → infinite loop for stateful operations

### Implications

This makes the "IEnumerable analogy" fundamentally impossible for mutable value types:

| Pattern | IEnumerable + LINQ | IStreamable + DIMs |
|---|---|---|
| Core method | `GetEnumerator()` → returns new enumerator | `Read(Span<byte>)` → mutates position |
| Default methods | Extension methods on `IEnumerable<T>` | DIMs on interface |
| State mutation | Enumerator is a separate object — caller doesn't mutate | Stream IS the state — every call mutates position |
| Boxing issue | Not relevant — enumerator is created fresh | **Fatal** — boxing copies position state |

The key difference: `IEnumerable<T>` extension methods (LINQ) create **new objects**
(enumerators, lazy sequences). They don't mutate the source. `IStreamable` DIMs must
**mutate** the implementer's position on every read — which breaks when boxing occurs.

### Workarounds Considered

| Workaround | Feasibility |
|---|---|
| Use class types instead of structs | ✅ Works but loses generic specialization perf benefit |
| Require implementers to override ALL DIMs | ✅ Works but defeats the "free defaults" purpose |
| Make IStreamable a class (abstract base) | ✅ Works but then it's just `Stream` with different methods |
| Use `ref` parameters for state | ❌ Not possible in interface DIMs |
| Use C# 13 `allows ref struct` constraint | ❌ `ref struct` still has boxing issues with DIMs |

**None of the workarounds preserve both goals** (free DIM defaults + mutable state
correctness + performance).

### Points Considered in This Assessment

Despite the showstopper above, the following dimensions were fully evaluated:

1. **DIM default behavior** — Does the DIM pattern work correctly for mutable types?
2. **DIM dispatch performance** — What is the overhead of DIM calls through generic constraints?
3. **Override performance** — When implementers override DIMs, is it equivalent to dedicated classes?
4. **Code reuse** — How much boilerplate do DIMs actually save?
5. **Public API viability** — Could IStreamable be a public extensibility contract?
6. **Text stream fit** — Does the pattern work for encoding-based streams?
7. **Comparison with IEnumerable pattern** — Why the analogy breaks down.

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

| # | Dimension | Dedicated Classes | IStreamable + DIMs | Winner |
|---|---|---|---|---|
| 1 | **DIM correctness** | N/A — no DIMs | ❌ **Fatal**: DIM + mutable struct = infinite loop | Dedicated |
| 2 | **Performance (overrides)** | Baseline | Equivalent (0.91-1.06x, benchmarked) | Tie |
| 3 | **Performance (DIMs)** | N/A | ❌ Non-functional (infinite loop) | Dedicated |
| 4 | **Allocation** | 48 B per instance | 56 B per instance (+17%) | Dedicated (marginal) |
| 5 | **Code reuse** | ~95 lines duplicated per type | Shared in StreamableStream&lt;T&gt; | IStreamable |
| 6 | **"Free defaults" (IEnumerable analogy)** | Not applicable | ❌ Broken by boxing | Dedicated |
| 7 | **Text streams** | ✅ Natural fit | ⚠️ Large struct, reduced benefit | Dedicated |
| 8 | **ReadOnlySequence** | Extension method (current proposal) | Cross-assembly generic | Dedicated |
| 9 | **Public extensibility** | Factory methods on Stream | Struct constraint prevents practical public use | Tie |
| 10 | **Complexity** | Simple inheritance | Generic specialization + boxing pitfalls | Dedicated |

**Score**: Dedicated wins 7 dimensions, IStreamable wins 1 (code reuse), and 2 are ties.

## Point 9: Limitations Encountered

1. **⚠️ SHOWSTOPPER — DIM + mutable struct boxing** (correctness bug): The core promise
   of the DIM approach — "implement Read, get ReadByte for free" — is fundamentally
   broken. The CLR boxes value types when dispatching to DIM methods through constrained
   generic calls, causing position mutations to be lost. This produces silent infinite
   loops. This was proven experimentally (see "Critical Finding" section above).

2. **When implementers override all DIMs, the pattern works** but defeats the purpose.
   If every struct must provide `ReadByte()`, `Seek()`, `CopyTo()`, `Write()` (throws),
   `WriteByte()` (throws), `SetLength()` (throws), then the DIMs provide zero value —
   the interface is just an abstract contract, not a source of free behavior.

3. **The IEnumerable analogy is structurally invalid**: LINQ extension methods on
   `IEnumerable<T>` create new objects (enumerators, lazy sequences) — they don't mutate
   the source. IStreamable DIMs must mutate the implementer's position on every read,
   which breaks under value-type boxing.

4. **Text encoding doesn't fit**: `ReadOnlyTextStream` needs stateful encoding
   (Encoder, byte buffer, char position tracking, resync logic), producing a struct
   >64 bytes with reference-type fields. The DIM defaults (which assume simple
   `Read` → `ReadByte` delegation) can't express the encoding pipeline.

5. **Allocation overhead**: 56 bytes vs 48 bytes (dedicated) per instance.

6. **Debuggability**: Generic type names in stack traces add cognitive load.

7. **Not publicly extensible**: The struct constraint + boxing pitfalls make this
   unsuitable as a public API. External implementers would hit the DIM boxing bug
   unless extensively documented and warned against.

## Conclusion

### Finding 1: The DIM "IEnumerable Experience" Is Impossible for Stream-Like Types

The central goal of this exploration — providing free default behavior via DIMs so
implementers write only `Read` + `Length` + `Position` — **does not work** for mutable
value types. The CLR's boxing behavior for DIM dispatch through generic constraints
causes silent infinite loops when DIM methods mutate state.

**Evidence**: The `StreamableStream<ReadOnlyMemoryStreamableMinimal>` test returns
`1, 1, 1, 1...` (position never advances) while the override version correctly returns
`1, 2, 3, -1`. See "Critical Finding" section.

**Root cause**: ECMA-335 III.2.1 specifies that `constrained. callvirt` on a value type
that does not implement the method boxes the value. DIM method bodies then operate on
the boxed copy, not the original.

### Finding 2: With All DIMs Overridden, It Reduces to the Previous Assessment

When implementers override all performance-sensitive DIMs (ReadByte, CopyTo, etc.),
the pattern works correctly and achieves performance parity with dedicated classes
(0.91-1.06x ratio). But at that point:
- The DIMs provide zero value (all overridden)
- The code savings are negligible (~95 lines of boilerplate per type)
- The complexity cost remains (generic specialization, struct mutation semantics)

This reduces to the same conclusion as the
[struct+generic specialization assessment](https://github.com/ViveliDuCh/runtime/blob/dev/vivianad/istreamable-exploration/exploration/IStreamable-Exploration.md)
from the previous iteration.

### Finding 3: Dedicated Stream Subclasses Remain the Right Design

The **dedicated Stream subclass approach** (current proposal) is the right design for
the .NET runtime:

- **Correct** — no boxing pitfalls, no DIM dispatch bugs
- **Simple** — each class is self-contained and independently understandable
- **Equally performant** — as the benchmarks confirm across all operations
- **Better fit for all target types** — text streams with encoding, sequences with
  multi-segment access, all work naturally as dedicated classes
- **Cleaner public API** — factory methods on `Stream` that return `Stream`

### Where the DIM Pattern Could Work

The DIM pattern for stream-like interfaces **could** work if:
- The interface is implemented by **reference types** (classes, not structs) — boxing
  doesn't occur, DIM methods operate on the original object
- The interface does not require **mutable state** — e.g., metadata-only interfaces
  where DIMs compute values from immutable properties
- The interface is used directly (not through a generic `T : struct` constraint)

For the .NET stream wrapper use case, none of these conditions hold.
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
