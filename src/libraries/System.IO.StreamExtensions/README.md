# System.IO.StreamExtensions

This project provides stream wrappers and factory methods for common memory and text-based types, specifically: `string`, `ReadOnlyMemory<char>`, `ReadOnlyMemory<byte>`, `Memory<byte>`, and `ReadOnlySequence<byte>`. This serves as an initial prototype for the API proposal in [dotnet/runtime#82801](https://github.com/dotnet/runtime/issues/82801), addressing the core variants that achieved consensus during the first API review as a logical starting point.

## Project Structure & Provided Types

The following stream wrappers are implemented, each providing high correctness test coverage and conformance/complementary behavioral tests:

- **StringStream**: Wraps a `string` as a non-seekable read-only stream, encoding its content on demand.
- **ReadOnlyMemoryCharStream**: Wraps `ReadOnlyMemory<char>` as a non-seekable read-only stream, encoding on demand (ideal for efficient slicing and non-allocating substring scenarios).
- **ReadOnlyMemoryStream**: Wraps `ReadOnlyMemory<byte>` as a read-only stream.
- **ReadOnlySequenceStream**: Wraps `ReadOnlySequence<byte>` as a read-only stream.
- **MemoryTStream**: Wraps `Memory<byte>` as a writable stream with limited capabilities (see below).

The project implements **factory methods** for these types, matching the initial API prototype and providing a standard means of creating streams from memory and text data.

## Technical and Design Notes

- Streams that wrap data like `ReadOnlyMemory<T>` or `Memory<T>` do **not** "own" the underlying buffer. This differs from [CommunityToolkit.HighPerformance](https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.HighPerformance/Streams/MemoryStream%7BTSource%7D.cs), where memory ownership and expandability are sometimes supported.
  - **Buffer management**: For wrappers like `MemoryTStream` (over `Memory<byte>`), the stream acts only as a view. The buffer is _not_ expandable. Dispose() on the stream does **not** free or alter the original buffer, which is expected to remain valid after stream disposal.
  - **Capacity logic**: Attempts to write beyond a fixed buffer's capacity will throw an exception; attempting to read beyond the buffer returns 0 bytes read, matching .NET Stream convention for fixed-size buffers.

- **Encoding on-the-fly**: Both `StringStream` and `ReadOnlyMemoryCharStream` encode their data as needed. Neither is seekable. While `string` and `ReadOnlyMemory<char>` currently have dedicated wrappers, future benchmarking may suggest merging them or further specializing them, especially as `ReadOnlyMemory<char>` proves efficient for slicing and non-allocated substrings.

- The current implementation aligns with the consensus established in the [dotnet/runtime#82801](https://github.com/dotnet/runtime/issues/82801) proposal and presents a logical API baseline. Further variants and potential performance improvements will be explored in subsequent iterations, rather than at this prototype stage, via benchmarks.

## Usage Example

```csharp
using System.IO;
using System.Text;

// Create a stream from a string for HTTP content
Stream stream = StreamFactory.StreamFromText("Hello world", Encoding.UTF8);
// Use with HttpClient, File I/O, etc.

// Create a read/write stream over a Memory<byte> buffer
Memory<byte> buffer = new byte[4096];
using Stream writableStream = StreamFactory.StreamFromData(buffer);
// ... perform stream operations
```

## Implementation Goals

- High fidelity to .NET conventions and expectations around stream ownership and buffer lifetime.
- High correctness through exhaustive test coverage for all implemented wrappers and API behaviors.
- Agility to extend/adjust API and implementation in response to future dotnet/runtime API review and benchmarking.
