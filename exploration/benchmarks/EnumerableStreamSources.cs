// IStreamSource implementations for ReadOnlyMemory<byte> and Memory<byte>.
//
// These are the "collections" in the IEnumerable analogy — immutable data providers.
// The mutable cursor (position) lives in StreamSourceReader (a class).

using System;
using System.IO;

namespace IStreamableBenchmarks;

/// <summary>
/// MINIMAL IStreamSource for ReadOnlyMemory&lt;byte&gt;.
/// Only implements the 2 required members: Length and ReadAt.
/// Everything else (CreateReader, ReadByteAt, CopyTo) comes from DIM defaults.
///
/// This is the IEnumerable analogy in action:
///   - Implement GetEnumerator() → get LINQ for free
///   - Implement ReadAt() + Length → get CreateReader/ReadByteAt/CopyTo for free
/// </summary>
public readonly struct ReadOnlyMemorySourceMinimal : IStreamSource
{
    private readonly ReadOnlyMemory<byte> _memory;

    public ReadOnlyMemorySourceMinimal(ReadOnlyMemory<byte> memory) => _memory = memory;

    public long Length => _memory.Length;

    public int ReadAt(long offset, Span<byte> buffer)
    {
        int pos = (int)offset;
        int n = Math.Min(_memory.Length - pos, buffer.Length);
        if (n <= 0) return 0;
        _memory.Span.Slice(pos, n).CopyTo(buffer);
        return n;
    }

    // CreateReader, ReadByteAt, CopyTo → all DIM defaults, zero code needed
    // Because this struct is IMMUTABLE (readonly), boxing is harmless.
}

/// <summary>
/// OPTIMIZED IStreamSource for ReadOnlyMemory&lt;byte&gt;.
/// Overrides ReadByteAt (direct Span indexing) and CopyTo (single bulk copy)
/// for better hot-path performance. Still uses DIM default for CreateReader.
/// </summary>
public readonly struct ReadOnlyMemorySource : IStreamSource
{
    private readonly ReadOnlyMemory<byte> _memory;

    public ReadOnlyMemorySource(ReadOnlyMemory<byte> memory) => _memory = memory;

    public long Length => _memory.Length;

    public int ReadAt(long offset, Span<byte> buffer)
    {
        int pos = (int)offset;
        int n = Math.Min(_memory.Length - pos, buffer.Length);
        if (n <= 0) return 0;
        _memory.Span.Slice(pos, n).CopyTo(buffer);
        return n;
    }

    // Override: direct Span indexing instead of DIM's 1-byte ReadAt
    public int ReadByteAt(long offset)
    {
        int pos = (int)offset;
        return pos >= _memory.Length ? -1 : _memory.Span[pos];
    }

    // Override: single bulk copy instead of DIM's rented-buffer loop
    public void CopyTo(long offset, Stream destination)
    {
        int pos = (int)offset;
        var remaining = _memory.Span.Slice(pos);
        if (remaining.Length > 0)
            destination.Write(remaining);
    }
}

/// <summary>
/// OPTIMIZED writable IStreamSource for Memory&lt;byte&gt;.
/// Overrides CanWrite, WriteAt, ReadByteAt, CopyTo.
/// </summary>
public struct MemorySource : IStreamSource
{
    private readonly Memory<byte> _memory;
    private int _length;

    public MemorySource(Memory<byte> memory)
    {
        _memory = memory;
        _length = memory.Length;
    }

    public long Length => _length;
    public bool CanWrite => true;

    public int ReadAt(long offset, Span<byte> buffer)
    {
        int pos = (int)offset;
        int n = Math.Min(_length - pos, buffer.Length);
        if (n <= 0) return 0;
        _memory.Span.Slice(pos, n).CopyTo(buffer);
        return n;
    }

    public int ReadByteAt(long offset)
    {
        int pos = (int)offset;
        return pos >= _length ? -1 : _memory.Span[pos];
    }

    public void WriteAt(long offset, ReadOnlySpan<byte> buffer)
    {
        int pos = (int)offset;
        buffer.CopyTo(_memory.Span.Slice(pos));
        int end = pos + buffer.Length;
        if (end > _length) _length = end;
    }

    public void CopyTo(long offset, Stream destination)
    {
        int pos = (int)offset;
        var remaining = _memory.Span.Slice(pos, _length - pos);
        if (remaining.Length > 0)
            destination.Write(remaining);
    }

    public void SetLength(long value)
    {
        _length = (int)value;
    }
}
