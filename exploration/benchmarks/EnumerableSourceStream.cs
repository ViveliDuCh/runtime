// Stream adapter for the IEnumerable-strategy (IStreamSource + StreamSourceReader).
//
// This wraps a StreamSourceReader as a System.IO.Stream, providing the standard
// Stream interface that consumers expect.
//
// Architecture comparison:
//   ISpanOwner strategy:   StreamableStream<T> ──embeds──> T (struct, JIT specialization)
//   IEnumerable strategy:  EnumerableSourceStream ──owns──> StreamSourceReader ──refs──> IStreamSource
//
// The IEnumerable strategy has an extra level of indirection (reader → source)
// but avoids the DIM+boxing showstopper entirely.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IStreamableBenchmarks;

/// <summary>
/// Stream adapter that wraps a StreamSourceReader.
/// Unlike StreamableStream&lt;T&gt;, this is NOT generic — the reader is a class reference.
/// This means no JIT specialization, but also no boxing hazards.
/// </summary>
public sealed class EnumerableSourceStream : Stream
{
    private readonly StreamSourceReader _reader;
    private bool _disposed;

    public EnumerableSourceStream(IStreamSource source)
    {
        _reader = source.CreateReader();
    }

    public EnumerableSourceStream(StreamSourceReader reader)
    {
        _reader = reader;
    }

    public override bool CanRead => !_disposed && _reader.CanRead;
    public override bool CanWrite => !_disposed && _reader.CanWrite;
    public override bool CanSeek => !_disposed && _reader.CanSeek;

    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _reader.Length;
        }
    }

    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _reader.Position;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _reader.Position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _reader.Read(new Span<byte>(buffer, offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _reader.Read(buffer);
    }

    public override int ReadByte()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _reader.ReadByte();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _reader.Write(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _reader.Write(buffer);
    }

    public override void WriteByte(byte value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _reader.WriteByte(value);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _reader.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _reader.SetLength(value);
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _reader.CopyTo(destination);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        ValidateBufferArguments(buffer, offset, count);
        if (ct.IsCancellationRequested) return Task.FromCanceled<int>(ct);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Task.FromResult(_reader.Read(new Span<byte>(buffer, offset, count)));
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<int>(ct);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new ValueTask<int>(_reader.Read(buffer.Span));
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
    {
        ValidateBufferArguments(buffer, offset, count);
        if (ct.IsCancellationRequested) return Task.FromCanceled(ct);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _reader.Write(new ReadOnlySpan<byte>(buffer, offset, count));
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _reader.Write(buffer.Span);
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
