// Dedicated ReadOnlyMemory stream (non-generic) - simulates the "custom class per type"
// approach from the current issue #82801 proposal.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IStreamableBenchmarks;

/// <summary>
/// A standalone, non-generic read-only stream wrapping ReadOnlyMemory&lt;byte&gt;.
/// This represents the "one custom Stream subclass per backing type" design
/// from the current API proposal.
/// </summary>
public sealed class DedicatedReadOnlyMemoryStream : Stream
{
    private readonly ReadOnlyMemory<byte> _memory;
    private int _position;
    private bool _disposed;

    public DedicatedReadOnlyMemoryStream(ReadOnlyMemory<byte> memory)
    {
        _memory = memory;
    }

    public override bool CanRead => !_disposed;
    public override bool CanWrite => false;
    public override bool CanSeek => !_disposed;
    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _memory.Length;
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
        int n = Math.Min(_memory.Length - _position, buffer.Length);
        if (n <= 0) return 0;
        _memory.Span.Slice(_position, n).CopyTo(buffer);
        _position += n;
        return n;
    }

    public override int ReadByte()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _position >= _memory.Length ? -1 : _memory.Span[_position++];
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override void Write(ReadOnlySpan<byte> buffer) =>
        throw new NotSupportedException();

    public override void WriteByte(byte value) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var remaining = _memory.Span.Slice(_position);
        if (remaining.Length > 0)
        {
            destination.Write(remaining);
            _position = _memory.Length;
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

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken ct) =>
        ct.IsCancellationRequested ? Task.FromCanceled(ct) : Task.CompletedTask;

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        base.Dispose(disposing);
    }
}
