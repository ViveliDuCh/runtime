// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace System.IO;

/// <summary>
/// A <see cref="Stream"/> implementation that delegates to an <see cref="IStreamable"/>
/// struct, using generic specialization to eliminate virtual dispatch overhead on the
/// inner data-access calls.
/// </summary>
/// <typeparam name="TStreamable">
/// A struct implementing <see cref="IStreamable"/>. The JIT specializes
/// all method calls on this type parameter, enabling inlining and
/// devirtualization.
/// </typeparam>
/// <remarks>
/// <para>
/// This design follows the same pattern as CommunityToolkit.HighPerformance's
/// <c>MemoryStream&lt;TSource&gt;</c> with its <c>ISpanOwner</c> interface.
/// By constraining <typeparamref name="TStreamable"/> to <c>struct</c>, the
/// JIT generates a dedicated code path for each backing type, avoiding the
/// overhead of interface dispatch that would occur with a non-generic approach.
/// </para>
/// <para>
/// Outer virtual dispatch (from callers using the <see cref="Stream"/> base class)
/// still occurs as with any Stream subclass. The optimization is on the inner
/// dispatch to the backing data source.
/// </para>
/// </remarks>
internal sealed class StreamableStream<TStreamable> : Stream
    where TStreamable : struct, IStreamable
{
    private TStreamable _streamable;
    private bool _disposed;

    public StreamableStream(TStreamable streamable)
    {
        _streamable = streamable;
    }

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

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        ValidateCopyToArguments(destination, bufferSize);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        ObjectDisposedException.ThrowIf(_disposed, this);

        _streamable.CopyTo(destination);
        return Task.CompletedTask;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<int>(cancellationToken);

        ObjectDisposedException.ThrowIf(_disposed, this);

        return Task.FromResult(_streamable.Read(new Span<byte>(buffer, offset, count)));
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled<int>(cancellationToken);

        ObjectDisposedException.ThrowIf(_disposed, this);

        return new ValueTask<int>(_streamable.Read(buffer.Span));
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        ObjectDisposedException.ThrowIf(_disposed, this);

        _streamable.Write(new ReadOnlySpan<byte>(buffer, offset, count));
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);

        ObjectDisposedException.ThrowIf(_disposed, this);

        _streamable.Write(buffer.Span);
        return default;
    }

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}
