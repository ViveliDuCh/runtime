// Dedicated writable memory stream (non-generic) for benchmarking.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IStreamableBenchmarks;

/// <summary>
/// A standalone, non-generic writable stream wrapping Memory&lt;byte&gt;.
/// Represents the "one custom Stream subclass per backing type" design.
/// </summary>
public sealed class DedicatedMemoryByteStream : Stream
{
    private readonly Memory<byte> _memory;
    private int _position;
    private int _length;
    private bool _disposed;

    public DedicatedMemoryByteStream(Memory<byte> memory)
    {
        _memory = memory;
        _length = memory.Length;
    }

    public override bool CanRead => !_disposed;
    public override bool CanWrite => !_disposed;
    public override bool CanSeek => !_disposed;
    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _length;
        }
    }

    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _position;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _position = (int)value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ReadCore(new Span<byte>(buffer, offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ReadCore(buffer);
    }

    private int ReadCore(Span<byte> buffer)
    {
        int n = Math.Min(_length - _position, buffer.Length);
        if (n <= 0) return 0;
        _memory.Span.Slice(_position, n).CopyTo(buffer);
        _position += n;
        return n;
    }

    public override int ReadByte()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _position >= _length ? -1 : _memory.Span[_position++];
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        ObjectDisposedException.ThrowIf(_disposed, this);
        WriteCore(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WriteCore(buffer);
    }

    private void WriteCore(ReadOnlySpan<byte> buffer)
    {
        buffer.CopyTo(_memory.Span.Slice(_position));
        _position += buffer.Length;
        if (_position > _length) _length = _position;
    }

    public override void WriteByte(byte value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _memory.Span[_position++] = value;
        if (_position > _length) _length = _position;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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

    public override void SetLength(long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _length = (int)value;
        if (_position > _length) _position = _length;
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var remaining = _memory.Span.Slice(_position, _length - _position);
        if (remaining.Length > 0)
        {
            destination.Write(remaining);
            _position = _length;
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        ValidateBufferArguments(buffer, offset, count);
        if (ct.IsCancellationRequested) return Task.FromCanceled<int>(ct);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Task.FromResult(ReadCore(new Span<byte>(buffer, offset, count)));
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<int>(ct);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new ValueTask<int>(ReadCore(buffer.Span));
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        ValidateBufferArguments(buffer, offset, count);
        if (ct.IsCancellationRequested) return Task.FromCanceled(ct);
        ObjectDisposedException.ThrowIf(_disposed, this);
        WriteCore(new ReadOnlySpan<byte>(buffer, offset, count));
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        ObjectDisposedException.ThrowIf(_disposed, this);
        WriteCore(buffer.Span);
        return default;
    }

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken ct) =>
        ct.IsCancellationRequested ? Task.FromCanceled(ct) : Task.CompletedTask;

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        base.Dispose(disposing);
    }
}
