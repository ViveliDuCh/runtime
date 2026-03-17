// Self-contained IStreamable interface with DIMs for standalone benchmarking.
// The key idea: implement Read + Length + Position → get everything else for free.

using System;
using System.Buffers;
using System.IO;

namespace IStreamableBenchmarks;

/// <summary>
/// IStreamable with Default Interface Methods (DIMs).
/// Analogous to IEnumerable: implement a few core methods, get the rest free.
///
/// Core members (MUST implement):
///   - Read(Span&lt;byte&gt;)
///   - Length
///   - Position { get; set; }
///
/// DIM defaults (MAY override for performance):
///   - CanRead      → true
///   - CanWrite     → false
///   - CanSeek      → true
///   - ReadByte()   → calls Read() with 1-byte span
///   - Seek()       → computes from Position + Length
///   - CopyTo()     → reads in loop with rented buffer
///   - Write()      → throws NotSupportedException
///   - WriteByte()  → throws NotSupportedException
///   - SetLength()  → throws NotSupportedException
/// </summary>
public interface IStreamable
{
    // === CORE — must implement ===
    long Length { get; }
    long Position { get; set; }
    int Read(Span<byte> buffer);

    // === DIMs — free defaults ===
    bool CanRead => true;
    bool CanWrite => false;
    bool CanSeek => true;

    int ReadByte()
    {
        byte b = 0;
        return Read(new Span<byte>(ref b)) == 1 ? b : -1;
    }

    long Seek(long offset, SeekOrigin origin)
    {
        long pos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentException()
        };
        Position = pos;
        return pos;
    }

    void CopyTo(Stream destination)
    {
        byte[] buf = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            int n;
            while ((n = Read(buf)) > 0)
                destination.Write(buf.AsSpan(0, n));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();
    void WriteByte(byte value) => throw new NotSupportedException();
    void SetLength(long value) => throw new NotSupportedException();
}

/// <summary>
/// MINIMAL implementation — only 3 core members.
/// ReadByte, Seek, CopyTo all use DIM defaults.
/// Shows the "IEnumerable experience": minimal code, full capability.
/// </summary>
public struct ReadOnlyMemoryStreamableMinimal : IStreamable
{
    private readonly ReadOnlyMemory<byte> _memory;
    private int _position;

    public ReadOnlyMemoryStreamableMinimal(ReadOnlyMemory<byte> memory)
    {
        _memory = memory;
        _position = 0;
    }

    public readonly long Length => _memory.Length;

    public long Position
    {
        readonly get => _position;
        set => _position = (int)value;
    }

    public int Read(Span<byte> buffer)
    {
        int n = Math.Min(_memory.Length - _position, buffer.Length);
        if (n <= 0) return 0;
        _memory.Span.Slice(_position, n).CopyTo(buffer);
        _position += n;
        return n;
    }

    // Everything else: DIM defaults handle it
}

/// <summary>
/// OPTIMIZED implementation — overrides ReadByte and CopyTo for performance,
/// keeps DIM defaults for Seek, Write, WriteByte, SetLength.
/// Shows selective override: only override what matters.
/// </summary>
public struct ReadOnlyMemoryStreamable : IStreamable
{
    private readonly ReadOnlyMemory<byte> _memory;
    private int _position;

    public ReadOnlyMemoryStreamable(ReadOnlyMemory<byte> memory)
    {
        _memory = memory;
        _position = 0;
    }

    public readonly long Length => _memory.Length;

    public long Position
    {
        readonly get => _position;
        set => _position = (int)value;
    }

    public int Read(Span<byte> buffer)
    {
        int n = Math.Min(_memory.Length - _position, buffer.Length);
        if (n <= 0) return 0;
        _memory.Span.Slice(_position, n).CopyTo(buffer);
        _position += n;
        return n;
    }

    // Override: direct Span indexing instead of DIM's 1-byte Read()
    public int ReadByte() =>
        _position >= _memory.Length ? -1 : _memory.Span[_position++];

    // Override: single bulk copy instead of DIM's rented-buffer loop
    public void CopyTo(Stream destination)
    {
        var remaining = _memory.Span.Slice(_position);
        if (remaining.Length > 0)
        {
            destination.Write(remaining);
            _position = _memory.Length;
        }
    }
}

/// <summary>
/// Writable IStreamable backed by Memory&lt;byte&gt;.
/// Overrides CanWrite, Write, WriteByte to enable writing.
/// </summary>
public struct MemoryStreamable : IStreamable
{
    private readonly Memory<byte> _memory;
    private int _position;
    private int _length;

    public MemoryStreamable(Memory<byte> memory)
    {
        _memory = memory;
        _position = 0;
        _length = memory.Length;
    }

    public readonly long Length => _length;
    public readonly bool CanWrite => true; // Override DIM default (false)

    public long Position
    {
        readonly get => _position;
        set => _position = (int)value;
    }

    public int Read(Span<byte> buffer)
    {
        int n = Math.Min(_length - _position, buffer.Length);
        if (n <= 0) return 0;
        _memory.Span.Slice(_position, n).CopyTo(buffer);
        _position += n;
        return n;
    }

    // Override: direct indexing
    public int ReadByte() =>
        _position >= _length ? -1 : _memory.Span[_position++];

    // Override: enable writing
    public void Write(ReadOnlySpan<byte> buffer)
    {
        buffer.CopyTo(_memory.Span.Slice(_position));
        _position += buffer.Length;
        if (_position > _length) _length = _position;
    }

    // Override: enable single-byte writing
    public void WriteByte(byte value)
    {
        _memory.Span[_position++] = value;
        if (_position > _length) _length = _position;
    }

    public void CopyTo(Stream destination)
    {
        var remaining = _memory.Span.Slice(_position, _length - _position);
        if (remaining.Length > 0)
        {
            destination.Write(remaining);
            _position = _length;
        }
    }

    public void SetLength(long value)
    {
        _length = (int)value;
        if (_position > _length) _position = _length;
    }
}
