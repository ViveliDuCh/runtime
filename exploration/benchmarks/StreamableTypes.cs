// Self-contained IStreamable interface for standalone benchmarking.
// This mirrors what would live in System.Private.CoreLib.

using System;
using System.IO;

namespace IStreamableBenchmarks;

/// <summary>
/// Contract for types providing stream-like read/write/seek operations.
/// Designed for struct implementations + generic specialization.
/// </summary>
public interface IStreamable
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

/// <summary>
/// Read-only IStreamable backed by ReadOnlyMemory&lt;byte&gt;.
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

    public readonly bool CanRead => true;
    public readonly bool CanWrite => false;
    public readonly bool CanSeek => true;
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

    public readonly void Write(ReadOnlySpan<byte> buffer) =>
        throw new NotSupportedException();

    public long Seek(long offset, SeekOrigin origin)
    {
        long pos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _memory.Length + offset,
            _ => throw new ArgumentException()
        };
        _position = (int)pos;
        return _position;
    }

    public int ReadByte() =>
        _position >= _memory.Length ? -1 : _memory.Span[_position++];

    public readonly void WriteByte(byte value) =>
        throw new NotSupportedException();

    public void CopyTo(Stream destination)
    {
        var remaining = _memory.Span.Slice(_position);
        if (remaining.Length > 0)
        {
            destination.Write(remaining);
            _position = _memory.Length;
        }
    }

    public readonly void SetLength(long value) =>
        throw new NotSupportedException();
}

/// <summary>
/// Writable IStreamable backed by Memory&lt;byte&gt;.
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

    public readonly bool CanRead => true;
    public readonly bool CanWrite => true;
    public readonly bool CanSeek => true;
    public readonly long Length => _length;

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

    public void Write(ReadOnlySpan<byte> buffer)
    {
        buffer.CopyTo(_memory.Span.Slice(_position));
        _position += buffer.Length;
        if (_position > _length) _length = _position;
    }

    public long Seek(long offset, SeekOrigin origin)
    {
        long pos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentException()
        };
        _position = (int)pos;
        return _position;
    }

    public int ReadByte() =>
        _position >= _length ? -1 : _memory.Span[_position++];

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
