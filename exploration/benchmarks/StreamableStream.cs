// Self-contained StreamableStream<T> for standalone benchmarking.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IStreamableBenchmarks;

/// <summary>
/// Generic Stream adapter that delegates to an IStreamable struct.
/// The JIT specializes per TStreamable, enabling inlining of inner calls.
/// </summary>
public sealed class StreamableStream<TStreamable> : Stream
    where TStreamable : struct, IStreamable
{
    private TStreamable _streamable;
    private bool _disposed;

    public StreamableStream(TStreamable streamable) => _streamable = streamable;

    public override bool CanRead => !_disposed && _streamable.CanRead;
    public override bool CanWrite => !_disposed && _streamable.CanWrite;
    public override bool CanSeek => !_disposed && _streamable.CanSeek;

    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _streamable.Length;
        }
    }

    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _streamable.Position;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _streamable.Position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _streamable.Read(new Span<byte>(buffer, offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _streamable.Read(buffer);
    }

    public override int ReadByte()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _streamable.ReadByte();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _streamable.Write(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _streamable.Write(buffer);
    }

    public override void WriteByte(byte value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _streamable.WriteByte(value);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _streamable.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _streamable.SetLength(value);
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _streamable.CopyTo(destination);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        ValidateBufferArguments(buffer, offset, count);
        if (ct.IsCancellationRequested) return Task.FromCanceled<int>(ct);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Task.FromResult(_streamable.Read(new Span<byte>(buffer, offset, count)));
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<int>(ct);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new ValueTask<int>(_streamable.Read(buffer.Span));
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        ValidateBufferArguments(buffer, offset, count);
        if (ct.IsCancellationRequested) return Task.FromCanceled(ct);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _streamable.Write(new ReadOnlySpan<byte>(buffer, offset, count));
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _streamable.Write(buffer.Span);
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
