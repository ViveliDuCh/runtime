# Stream Wrapper Design Investigation: Deriving from `MemoryStream` vs. Deriving from `Stream`

> **Context**: [API Proposal dotnet/runtime#82801](https://github.com/dotnet/runtime/issues/82801) |
> [Prototype PR ViveliDuCh/runtime#1](https://github.com/ViveliDuCh/runtime/pull/1) |
> Benchmark branch: [`stream-investigation`](https://github.com/ViveliDuCh/runtime/tree/stream-investigation)

## 1. Problem Statement

The `MemoryByteStream` prototype wraps `Memory<byte>` / `ReadOnlyMemory<byte>` as a seekable `Stream`.
The current implementation [derives directly from `System.IO.Stream`](https://github.com/ViveliDuCh/runtime/blob/df12a999c4bf4b7224db51fcd66aa32a37fde3ac/src/libraries/System.Private.CoreLib/src/System/IO/MemoryByteStream.cs#L17).

This investigation explores whether it would be better to derive from
[`System.IO.MemoryStream`](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs)
instead, to reduce redundancy and reuse existing infrastructure.

---

## 2. Approach A — Derive directly from `Stream` (current prototype)

**Source**: [`MemoryByteStream.cs` on `stream-tests`](https://github.com/ViveliDuCh/runtime/blob/df12a999c4bf4b7224db51fcd66aa32a37fde3ac/src/libraries/System.Private.CoreLib/src/System/IO/MemoryByteStream.cs)

```csharp
// MemoryByteStream.cs — current prototype (lines 17-24)
// https://github.com/ViveliDuCh/runtime/blob/df12a999c4bf4b7224db51fcd66aa32a37fde3ac/src/libraries/System.Private.CoreLib/src/System/IO/MemoryByteStream.cs#L17-L24
internal sealed class MemoryByteStream : Stream
{
    private Memory<byte> _buffer;
    private ReadOnlyMemory<byte> _readOnlyBuffer;
    private readonly bool _isReadOnlyBacking;
    private int _position;
    private bool _isOpen;
    // ...
}
```

### Fields (5 fields)

| Field | Type | Size (x64) | Purpose |
|---|---|---|---|
| `_buffer` | `Memory<byte>` | 16 bytes | Writable buffer |
| `_readOnlyBuffer` | `ReadOnlyMemory<byte>` | 16 bytes | Read-only buffer |
| `_isReadOnlyBacking` | `bool` | 1 byte | Read-only mode flag |
| `_position` | `int` | 4 bytes | Current position |
| `_isOpen` | `bool` | 1 byte | Disposed flag |

**Total own-field overhead**: ~38 bytes (+ Stream base ~24 bytes = ~64 bytes object size, confirmed by benchmarks)

### Characteristics

- **Sealed** — no virtual dispatch overhead, JIT can devirtualize all calls
- **Zero dead fields** — every field is used
- **Works with any `Memory<byte>` backing** — arrays, native memory, custom `MemoryManager<byte>`, etc.
- **No `GetType()` checks** — unlike `MemoryStream` which [checks `GetType() != typeof(MemoryStream)` on every `Read(Span<byte>)`](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L142-L149)

---

## 3. Approach B — Derive from `MemoryStream`

### 3.1. Why `MemoryStream`'s fields are inaccessible

All of `MemoryStream`'s internal state is **private** — not `protected`:

```csharp
// MemoryStream.cs — private fields (lines 23-35)
// https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L23-L35
public class MemoryStream : Stream
{
    private byte[] _buffer;           // Either allocated internally or externally.
    private readonly int _origin;     // For user-provided arrays, start at this origin
    private int _position;            // read/write head.
    private int _length;              // Number of bytes within the memory stream
    private int _capacity;            // length of usable portion of buffer for stream
    private bool _expandable;         // User-provided buffers aren't expandable.
    private bool _writable;           // Can user write to this stream?
    private readonly bool _exposable; // Whether the array can be returned to the user.
    private bool _isOpen;             // Is this stream open or closed?
    private CachedCompletedInt32Task _lastReadTask;
    // ...
}
```

A derived class **cannot read or write** any of these fields. This means:

1. **Cannot redirect `_buffer`** to a `Memory<byte>` — `MemoryStream.Read()` directly accesses `_buffer` as `byte[]`
2. **Cannot share `_position`** — the base tracks its own position independently
3. **Cannot skip `_origin`/`_length`/`_capacity`** bookkeeping — those are integral to every operation

### 3.2. The only viable derived approach

The only way to make a `MemoryStream`-derived class work with `Memory<byte>` is:

1. Call `base(0)` — which allocates an **empty `byte[]`** and sets `_expandable=true`, `_writable=true`, `_exposable=true`, `_isOpen=true`
2. Store your own `Memory<byte>` / `ReadOnlyMemory<byte>` fields
3. **Override EVERY method** to use your fields instead of the base's inaccessible ones

```csharp
// MemoryStreamDerivedApproach.cs — the investigation prototype
// https://github.com/ViveliDuCh/runtime/blob/stream-investigation/benchmarks/StreamWrapperBenchmarks/MemoryStreamDerivedApproach.cs
public sealed class MemoryStreamDerivedApproach : MemoryStream
{
    // Our own fields (since base fields are ALL private, inaccessible)
    private Memory<byte> _memBuffer;
    private ReadOnlyMemory<byte> _readOnlyMemBuffer;
    private readonly bool _isReadOnlyBacking;
    private int _pos;
    private bool _open;

    // base(0) allocates: byte[] _buffer (empty), plus sets
    // _capacity=0, _expandable=true, _writable=true, _exposable=true, _isOpen=true
    // All of those become dead weight that we never use.

    public MemoryStreamDerivedApproach(Memory<byte> buffer) : base(0)
    {
        _memBuffer = buffer;
        _isReadOnlyBacking = false;
        _open = true;
    }

    public MemoryStreamDerivedApproach(ReadOnlyMemory<byte> buffer) : base(0)
    {
        _readOnlyMemBuffer = buffer;
        _isReadOnlyBacking = true;
        _open = true;
    }

    // Must override EVERYTHING because base uses its own inaccessible private fields
    public override int Read(Span<byte> buffer) { /* identical to Approach A */ }
    public override void Write(ReadOnlySpan<byte> buffer) { /* identical to Approach A */ }
    // ... every other method ...
}
```

### 3.3. Dead weight from `MemoryStream` base

When deriving from `MemoryStream`, the `base(0)` constructor creates these **unused** allocations and fields:

| Base field | Type | Size (x64) | Used by derived? |
|---|---|---|---|
| `_buffer` | `byte[]` (empty) | 8 bytes (ref) + 24 bytes (array header) | ❌ Dead |
| `_origin` | `int` | 4 bytes | ❌ Dead |
| `_position` | `int` | 4 bytes | ❌ Dead (we use our own) |
| `_length` | `int` | 4 bytes | ❌ Dead |
| `_capacity` | `int` | 4 bytes | ❌ Dead |
| `_expandable` | `bool` | 1 byte | ❌ Dead |
| `_writable` | `bool` | 1 byte | ❌ Dead |
| `_exposable` | `bool` | 1 byte | ❌ Dead |
| `_isOpen` | `bool` | 1 byte | ❌ Dead |
| `_lastReadTask` | `CachedCompletedInt32Task` | 8 bytes | ❌ Dead |

**Total dead weight from base**: ~36 bytes in the object + a separate `byte[]` allocation (~24 bytes on heap)

---

## 4. Benchmark Results

Benchmark project: [`benchmarks/StreamWrapperBenchmarks/`](https://github.com/ViveliDuCh/runtime/tree/stream-investigation/benchmarks/StreamWrapperBenchmarks)

**Environment**: .NET 9.0.13, X64 RyuJIT AVX2, Concurrent Workstation GC

### 4.1. Read Performance (Read byte[] overload)

| Method | DataSize | Mean | Allocated |
|---|---|---:|---:|
| MemoryStream (baseline) | 64 | 11.69 ns | 64 B |
| **Direct:Stream (ROM\<byte\>)** | 64 | **11.94 ns** | **64 B** |
| Derived:MemoryStream (ROM\<byte\>) | 64 | 13.89 ns | 104 B |
| MemoryStream (baseline) | 1024 | 21.40 ns | 64 B |
| **Direct:Stream (ROM\<byte\>)** | 1024 | **17.98 ns** | **64 B** |
| Derived:MemoryStream (ROM\<byte\>) | 1024 | 22.47 ns | 104 B |
| MemoryStream (baseline) | 65536 | 1,207 ns | 64 B |
| **Direct:Stream (ROM\<byte\>)** | 65536 | **1,233 ns** | **64 B** |
| Derived:MemoryStream (ROM\<byte\>) | 65536 | 1,164 ns | 104 B |

### 4.2. Read Performance (Read Span\<byte\> overload)

| Method | DataSize | Mean | Allocated |
|---|---|---:|---:|
| MemoryStream ReadSpan | 64 | 8.77 ns | 64 B |
| **Direct:Stream ReadSpan** | 64 | **12.07 ns** | **64 B** |
| Derived:MemoryStream ReadSpan | 64 | 13.71 ns | 104 B |
| MemoryStream ReadSpan | 1024 | 16.37 ns | 64 B |
| **Direct:Stream ReadSpan** | 1024 | **18.55 ns** | **64 B** |
| Derived:MemoryStream ReadSpan | 1024 | 23.16 ns | 104 B |
| MemoryStream ReadSpan | 65536 | 1,190 ns | 64 B |
| **Direct:Stream ReadSpan** | 65536 | **1,153 ns** | **64 B** |
| Derived:MemoryStream ReadSpan | 65536 | 1,163 ns | 104 B |

### 4.3. Write Performance

| Method | DataSize | Mean | Allocated |
|---|---|---:|---:|
| MemoryStream (baseline) | 64 | 12.27 ns | 64 B |
| **Direct:Stream (Memory\<byte\>)** | 64 | **9.67 ns** | **64 B** |
| Derived:MemoryStream (Memory\<byte\>) | 64 | 12.69 ns | 104 B |
| MemoryStream WriteSpan | 64 | 8.39 ns | 64 B |
| **Direct:Stream WriteSpan** | 64 | **9.68 ns** | **64 B** |
| Derived:MemoryStream WriteSpan | 64 | 14.38 ns | 104 B |

### 4.4. ReadByte (per-byte) Performance

| Method | DataSize | Mean | Allocated |
|---|---|---:|---:|
| MemoryStream (baseline) | 64 | 58.17 ns | 0 B |
| **Direct:Stream** | 64 | **135.73 ns** | **64 B** |
| Derived:MemoryStream | 64 | 124.24 ns | 104 B |
| MemoryStream (baseline) | 1024 | 894.42 ns | 0 B |
| **Direct:Stream** | 1024 | **1,826.61 ns** | **64 B** |
| Derived:MemoryStream | 1024 | 1,833.09 ns | 104 B |

> **Note**: MemoryStream's `ReadByte()` is faster because it directly indexes `byte[] _buffer[_position++]` — a simple array indexing operation. Both `Memory<byte>`-based approaches must call `.Span[_position++]` which involves a bounds check through the Memory abstraction layer. This is an inherent cost of wrapping `Memory<byte>` instead of `byte[]`, regardless of inheritance strategy.

### 4.5. Seek Performance

| Method | Mean | Allocated |
|---|---:|---:|
| MemoryStream (baseline) | 180.4 ns | 0 B |
| **Direct:Stream** | **149.5 ns** | **64 B** |
| Derived:MemoryStream | 148.1 ns | 104 B |

### 4.6. Object Allocation Size

| Method | Mean | Allocated |
|---|---:|---:|
| MemoryStream alloc | 41.61 ns | **1,090 B** (1.09 KB) |
| **Direct:Stream alloc** | **39.76 ns** | **1,090 B** (1.09 KB) |
| Derived:MemoryStream alloc | 44.38 ns | **1,157 B** (1.13 KB) |

> The 1 KB bulk is the `new byte[1024]` in Setup. The _object overhead difference_ is:
> - **Direct:Stream**: 64 B (object header + Stream base + 5 own fields)
> - **Derived:MemoryStream**: 104 B (object header + Stream base + MemoryStream dead fields + 5 own fields)
> - **Delta: +40 bytes (+62.5%)** per instance for the derived approach, all wasted on dead `MemoryStream` fields

### 4.7. Type Check (`is MemoryStream`)

| Method | Mean |
|---|---:|
| MemoryStream `is MemoryStream` | 0.25 ns |
| Direct:Stream `is MemoryStream` | 0.20 ns (returns `false`) |
| Derived:MemoryStream `is MemoryStream` | 0.19 ns (returns `true`) |

> Type checks are near-free (~0.2 ns). The `is MemoryStream` check returning `true` for the derived approach is the **only tangible benefit** of deriving from `MemoryStream`.

---

## 5. Code Reuse Analysis

### 5.1. What could be reused from `MemoryStream`?

| Feature | Reusable? | Why / Why not |
|---|---|---|
| `Read(byte[], int, int)` | ❌ | Uses private `_buffer`, `_position`, `_length` directly ([line 320](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L320)) |
| `Read(Span<byte>)` | ❌ | Has `GetType() != typeof(MemoryStream)` guard ([line 348](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L348-L354)); uses private `_buffer` |
| `Write(byte[], int, int)` | ❌ | Uses private `_buffer`, `EnsureCapacity` ([line 579](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L579)) |
| `Write(ReadOnlySpan<byte>)` | ❌ | Same `GetType()` guard and private `_buffer` access ([line 622](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L622-L631)) |
| `ReadByte()` | ❌ | Directly indexes `_buffer[_position++]` ([line 427](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L427-L434)) |
| `Seek()` | ❌ | Uses private `_origin`, `_position`, `_length` ([line 512](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L512)) |
| `Flush()` / `FlushAsync()` | ✅ | No-op, trivial to implement either way |
| `CopyTo()` / `CopyToAsync()` | ❌ | Uses private `_buffer`, `_position`, `InternalEmulateRead` ([line 437](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L437-L465)) |
| `Capacity` property | ❌ | Uses private `_capacity`, `_origin`, `_expandable` ([line 253](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L253-L291)) |
| `GetBuffer()` / `TryGetBuffer()` | ❌ | Returns private `_buffer` directly ([line 180](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L180-L197)) |
| `ToArray()` | ❌ | Copies from private `_buffer` ([line 569](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L569-L577)) |
| Internal perf helpers | ❌ | `InternalGetBuffer()`, `InternalReadSpan()`, `InternalEmulateRead()` are `internal` to the same assembly ([lines 199-247](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L199-L247)) |

**Result: Zero methods can be reused.** The derived approach must override **every single method**, making it functionally identical to the direct-from-Stream approach but with extra dead weight.

### 5.2. The `GetType()` check problem

`MemoryStream` uses `GetType() != typeof(MemoryStream)` guards in performance-critical paths:

```csharp
// MemoryStream.cs lines 348-354
// https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L348-L354
public override int Read(Span<byte> buffer)
{
    if (GetType() != typeof(MemoryStream))
    {
        // Derived type may have overridden Read(byte[], int, int)
        // so we must use that slower path
        return base.Read(buffer);
    }
    // ... fast path using _buffer directly
}
```

If `MemoryByteStream` derived from `MemoryStream`, it would trigger this guard (`GetType()` would return `typeof(MemoryByteStream)`, not `typeof(MemoryStream)`), causing:
- `Read(Span<byte>)` to fall back to `base.Read(buffer)` in `Stream`, which allocates a temp `byte[]` from `ArrayPool`
- `Write(ReadOnlySpan<byte>)` to fall back similarly

This means the base methods we'd want to reuse would actually perform **worse** for a derived class.

---

## 6. Existing `MemoryStream` Subclass Precedent: `PinnedBufferMemoryStream`

The runtime already has a `MemoryStream`-derived class:
[`PinnedBufferMemoryStream`](https://github.com/ViveliDuCh/runtime/blob/df12a999c4bf4b7224db51fcd66aa32a37fde3ac/src/libraries/System.Private.CoreLib/src/System/IO/PinnedBufferMemoryStream.cs)

```csharp
// PinnedBufferMemoryStream.cs
// https://github.com/ViveliDuCh/runtime/blob/df12a999c4bf4b7224db51fcd66aa32a37fde3ac/src/libraries/System.Private.CoreLib/src/System/IO/PinnedBufferMemoryStream.cs#L12-L28
internal sealed unsafe class PinnedBufferMemoryStream : UnmanagedMemoryStream
{
    private readonly byte[] _array;
    private GCHandle _pinningHandle;
    // ...
}
```

Note: It actually derives from `UnmanagedMemoryStream`, **not** `MemoryStream`. This is because it needs pointer-based access. This precedent confirms that even within the runtime, new stream implementations derive from the most appropriate base — not necessarily `MemoryStream`.

### 6.1. Comprehensive Base Class Suitability Analysis

Every direct `Stream` subclass in `System.Private.CoreLib/src/System/IO/` was evaluated as a potential base for a `Memory<byte>` wrapper:

| Candidate Base | Sealed? | Field Visibility | Backing Store | Suitable? | Why |
|---|---|---|---|---|---|
| **`Stream`** (abstract) | N/A | N/A (abstract) | N/A | ✅ **Best fit** | Clean slate, no baggage, sealed derivative devirtualizes |
| [`MemoryStream`](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L22) | Open | **All private** | `byte[]` | ❌ | 0 methods reusable, +40B dead weight, GetType() guards penalize (see §3–5) |
| [`UnmanagedMemoryStream`](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/UnmanagedMemoryStream.cs#L34) | Open | **All private** (despite protected `Initialize()`) | `byte*` / `SafeBuffer` | ❌ | Requires `unsafe byte*` or `SafeBuffer`; can't hold managed `Memory<byte>` |
| [`BufferedStream`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/IO/BufferedStream.cs) | **Sealed** | All private | Wraps another `Stream` | ❌ | Sealed — cannot inherit. Also wrong semantics (adds buffering layer over existing stream) |
| [`FileStream`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/IO/FileStream.cs) | Open | Private | `FileStreamStrategy` | ❌ | File I/O semantics, completely unrelated |

#### Why `UnmanagedMemoryStream` is a near-miss but still wrong

`UnmanagedMemoryStream` is the **only** Stream subclass designed for external subclassing — it has:
- A `protected UnmanagedMemoryStream()` empty constructor ([line 50](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/UnmanagedMemoryStream.cs#L50))
- `protected void Initialize(SafeBuffer, ...)` ([line 80](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/UnmanagedMemoryStream.cs#L80))
- `protected unsafe void Initialize(byte*, ...)` ([line 151](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/UnmanagedMemoryStream.cs#L151))

However, its fields are **still all private** (`_buffer`, `_mem`, `_capacity`, `_length`, `_position`, `_access`, `_isOpen`) — [lines 36-44](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/UnmanagedMemoryStream.cs#L36-L44). A derived class can call `Initialize()` but cannot afterwards interact with the stored state except through the public/protected virtual methods. More critically:

1. **Backing store mismatch**: `UnmanagedMemoryStream` stores `unsafe byte*` pointers. `Memory<byte>` is a managed abstraction that may or may not be pinnable. Pinning `Memory<byte>` to get a pointer would require keeping a `GCHandle` alive — exactly what `PinnedBufferMemoryStream` does, but that approach carries GC-pinning costs and finalizer overhead.

2. **Semantic mismatch**: `UnmanagedMemoryStream` is designed for memory that _outlives_ the stream and is _externally managed_. `Memory<byte>` from a `MemoryManager<byte>` may have lifetime semantics tied to `IDisposable` — the stream wrapper should not assume the memory is permanently valid.

3. **The same override-everything problem**: Even if we could `Initialize()` with a pinned pointer, all the read/write methods access `_mem` (private), so we'd still need to override everything — the same situation as with `MemoryStream`.

#### Conclusion

**No existing Stream subclass in the runtime provides a useful base for wrapping `Memory<byte>`.** The `Stream` abstract class itself is the correct base — it provides the public API contract with zero baggage, and a `sealed` derivative enables full devirtualization by the JIT.

---

## 7. The `is MemoryStream` Compatibility Question

The only argument _for_ deriving from `MemoryStream` is that `is MemoryStream` checks would return `true`.

### Where does `is MemoryStream` appear in the runtime?

A search in the upstream runtime shows `is MemoryStream` or `as MemoryStream` is used in a few places:

- [`MemoryStream.CopyToAsync()`](https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L497) — optimizes MemoryStream-to-MemoryStream copy by calling `Write()` synchronously instead of `WriteAsync`. A derived class wouldn't benefit because the fast path accesses `_buffer` directly.

- **Some `HttpContent` paths** — e.g., `ReadOnlyMemoryContent` already creates a `MemoryStream` for array-backed data. The factory method `Stream.FromReadOnlyData()` [already fast-paths to `MemoryStream` when the data wraps an array](https://github.com/ViveliDuCh/runtime/blob/df12a999c4bf4b7224db51fcd66aa32a37fde3ac/src/libraries/System.Private.CoreLib/src/System/IO/Stream.cs#L1342-L1351):

```csharp
// Stream.cs — factory method (lines 1342-1351)
// https://github.com/ViveliDuCh/runtime/blob/df12a999c4bf4b7224db51fcd66aa32a37fde3ac/src/libraries/System.Private.CoreLib/src/System/IO/Stream.cs#L1342-L1351
public static Stream FromReadOnlyData(ReadOnlyMemory<byte> data)
{
    if (MemoryMarshal.TryGetArray(data, out ArraySegment<byte> dataBacking))
    {
        // Fast path: ReadOnlyMemory<byte> wraps an array → use actual MemoryStream
        return new MemoryStream(dataBacking.Array!, dataBacking.Offset, dataBacking.Count, writable: false);
    }
    // Non-array backing (native memory, IMemoryOwner, etc.) → use MemoryByteStream
    return new MemoryByteStream(data);
}
```

This means `MemoryByteStream` is **only ever used when the `Memory<byte>` does NOT wrap a `byte[]`**.
In those cases (native memory, custom `MemoryManager<byte>`, etc.), there is no `byte[]` to give to `MemoryStream`'s constructor anyway.

---

## 8. Comparative Summary

| Criterion | Direct from `Stream` (A) | Derived from `MemoryStream` (B) |
|---|---|---|
| **Code reuse from base** | N/A (abstract) | **0 methods reusable** |
| **Object size** | **64 B** | 104 B (+62.5%) |
| **Dead fields** | **0** | ~36 bytes (9 unused base fields) |
| **Extra allocations** | **None** | `byte[0]` from `base(0)` (~24 B) |
| **Read perf (64B)** | **~12 ns** | ~14 ns |
| **Write perf (64B)** | **~10 ns** | ~13 ns |
| **ReadByte perf** | ~136 ns / 64 bytes | ~124 ns / 64 bytes |
| **Seek perf** | **~150 ns** | ~148 ns |
| **`is MemoryStream`** | ❌ `false` | ✅ `true` |
| **Works with non-array Memory** | ✅ Always | ✅ But carries dead `byte[]` |
| **Sealed / devirtualization** | ✅ Clean | ⚠️ Must override MemoryStream virtuals |
| **`GetType()` guard bypass** | ✅ Not affected | ❌ Triggers slow path fallback |
| **Maintenance burden** | **Minimal** — single clear class | **Higher** — must track MemoryStream changes + override all |

---

## 9. Recommendation

**Derive directly from `Stream`** (Approach A, the current prototype) is the correct design for the following reasons:

1. **Zero code reuse is achievable from `MemoryStream`**: All fields are private, all methods operate on those private fields. Deriving and overriding everything is no different from implementing from scratch — except with extra dead weight.

2. **+62.5% memory overhead per instance**: The derived approach wastes 40 extra bytes per object on unused `MemoryStream` fields, plus allocates a dead `byte[0]`. For high-throughput scenarios (HTTP pipelines, serialization), this adds up.

3. **The factory methods already handle the fast path**: `Stream.FromReadOnlyData()` and `Stream.FromWritableData()` [already return a real `MemoryStream`](https://github.com/ViveliDuCh/runtime/blob/df12a999c4bf4b7224db51fcd66aa32a37fde3ac/src/libraries/System.Private.CoreLib/src/System/IO/Stream.cs#L1342-L1367) when the `Memory<byte>` wraps an array. `MemoryByteStream` is only created for non-array Memory, where `MemoryStream` inheritance provides zero benefit anyway.

4. **`GetType()` guards in MemoryStream would penalize the derived approach**: The Span-based overloads in `MemoryStream` explicitly fall back to slower paths for derived types. A `MemoryByteStream : MemoryStream` would trigger these fallbacks unless it overrides everything (which we do, but the code becomes confusing — "why derive if you override everything?").

5. **Semantic correctness**: `MemoryByteStream` is fundamentally a **different kind of stream** than `MemoryStream`. `MemoryStream` is an expandable, byte[]-backed, self-contained buffer. `MemoryByteStream` is a fixed-size view over externally-owned `Memory<byte>`. The `is MemoryStream` type relationship would be misleading — code that casts to `MemoryStream` and calls `.GetBuffer()` or `.ToArray()` or `.SetLength()` would get exceptions, violating Liskov substitution.

6. **Runtime precedent**: Even within the runtime itself, `PinnedBufferMemoryStream` derives from `UnmanagedMemoryStream` (not `MemoryStream`) because it has different backing semantics. The pattern of choosing the right base class based on actual semantics is well-established.

### Potential Improvement

The one observation from benchmarks is that `ReadByte()` is ~2x slower on `Memory<byte>`-based streams vs `MemoryStream` due to the `.Span` accessor overhead. If this becomes a hot path, consider caching the span at construction (or on first access) — though this is a micro-optimization that applies equally to both approaches.

---

## 10. Benchmark Reproduction

```bash
cd benchmarks/StreamWrapperBenchmarks
dotnet run -c Release
```

Full benchmark source:
- [`DirectFromStreamApproach.cs`](https://github.com/ViveliDuCh/runtime/blob/stream-investigation/benchmarks/StreamWrapperBenchmarks/DirectFromStreamApproach.cs)
- [`MemoryStreamDerivedApproach.cs`](https://github.com/ViveliDuCh/runtime/blob/stream-investigation/benchmarks/StreamWrapperBenchmarks/MemoryStreamDerivedApproach.cs)
- [`Program.cs`](https://github.com/ViveliDuCh/runtime/blob/stream-investigation/benchmarks/StreamWrapperBenchmarks/Program.cs) (benchmark definitions)

---

## 11. Related Investigation: Abstract Base Class Refactoring

A follow-up investigation explores a **third approach**: refactoring `MemoryStream` into an abstract base class with two sealed concrete implementations (`ArrayMemoryStream` and `MemoryMemoryStream`).

**Key finding**: `ArrayMemoryStream` (the `MemoryStream` equivalent) shows **zero regression** — many operations are 10–34% faster due to `sealed` devirtualization. This avoids the problems that sank [dotnet/runtime PR #84103](https://github.com/dotnet/runtime/pull/84103).

Full details: [`stream-refactor-investigation.md`](https://github.com/ViveliDuCh/runtime/blob/stream-refactor-investigation/docs/stream-refactor-investigation.md) on the [`stream-refactor-investigation`](https://github.com/ViveliDuCh/runtime/tree/stream-refactor-investigation) branch.
