// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO;

/// <summary>
/// Provides a <see cref="Stream"/> implementation over a <see cref="Memory{T}"/> of bytes with optional write support.
/// </summary>
/// <remarks>
/// This type is not thread-safe. Synchronize access if the stream is used concurrently.
/// The stream supports positions up to <see cref="int.MaxValue"/>. Attempting to seek beyond this limit will throw an exception.
/// The stream cannot expand beyond the initial memory capacity.
/// </remarks>
internal sealed class MemoryByteStream : Stream
{
    private Memory<byte> _buffer;
    private ReadOnlyMemory<byte> _readOnlyBuffer;
    private bool _isReadOnlyBacking;
    private int _position;
    private bool _isOpen;
    private bool _writable; // For read-only support

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryByteStream"/> class over the specified <see cref="Memory{Byte}"/>.
    /// The stream is writable and publicly visible by default.
    /// </summary>
    /// <param name="buffer">The <see cref="Memory{Byte}"/> to wrap.</param>
    public MemoryByteStream(Memory<byte> buffer)
    : this(buffer, writable: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryByteStream"/> class over the specified <see cref="Memory{Byte}"/> with write control.
    /// </summary>
    /// <param name="buffer">The <see cref="Memory{Byte}"/> to wrap.</param>
    /// <param name="writable">Whether the stream supports writing.</param>
    public MemoryByteStream(Memory<byte> buffer, bool writable)
    {
        _buffer = buffer;
        _isReadOnlyBacking = false;
        _writable = writable;
        _isOpen = true;
        _position = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryByteStream"/> class over the specified <see cref="ReadOnlyMemory{Byte}"/> with visibility control.
    /// Stream is always read-only.
    /// </summary>
    /// <param name="buffer">The <see cref="ReadOnlyMemory{Byte}"/> to wrap.</param>
    public MemoryByteStream(ReadOnlyMemory<byte> buffer)
    {
        _readOnlyBuffer = buffer;
        _isReadOnlyBacking = true;
        _writable = false;
        _isOpen = true;
        _position = 0;
    }

    /// <inheritdoc />
    public override bool CanRead => _isOpen;

    /// <inheritdoc />
    public override bool CanSeek => _isOpen;

    /// <inheritdoc />
    public override bool CanWrite => _writable && _isOpen;

    /// <inheritdoc />
    public override long Length
    {
        get
        {
            EnsureNotClosed();
            return InternalBuffer.Length;
        }
    }

    private ReadOnlyMemory<byte> InternalBuffer
        => _isReadOnlyBacking ? _readOnlyBuffer : _buffer;

    /// <inheritdoc />
    public override long Position
    {
        get
        {
            EnsureNotClosed();
            return _position;
        }
        set
        {
            EnsureNotClosed();
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, int.MaxValue);
            _position = (int)value;
        }
    }

    /// <inheritdoc />
    public override int ReadByte()
    {
        EnsureNotClosed();

        if (_position >= InternalBuffer.Length)
            return -1;

        return InternalBuffer.Span[_position++];
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(new Span<byte>(buffer, offset, count));
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        EnsureNotClosed();

        int length = InternalBuffer.Length;

        // If position is past the end of the buffer, return 0 (EOF)
        if (_position >= length)
        {
            return 0;
        }

        int bytesAvailable = length - _position;
        int bytesToRead = Math.Min(bytesAvailable, buffer.Length);

        if (bytesToRead > 0)
        {
            InternalBuffer.Span.Slice(_position, bytesToRead).CopyTo(buffer);
            _position += bytesToRead;
        }

        return bytesToRead;
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);

        // If cancellation was requested, bail early
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<int>(cancellationToken);

        int n = Read(buffer, offset, count);
        return Task.FromResult(n);
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        int bytesRead = Read(buffer.Span);
        return new ValueTask<int>(bytesRead);
    }

    /// <inheritdoc />
    public override void WriteByte(byte value)
    {
        EnsureNotClosed();
        EnsureWriteable();

        if (_position >= InternalBuffer.Length)
            throw new NotSupportedException(SR.NotSupported_MemStreamNotExpandable);

        _buffer.Span[_position++] = value;
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        Write(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureNotClosed();
        EnsureWriteable();

        if (_position > _buffer.Length - buffer.Length)
            throw new NotSupportedException(SR.NotSupported_MemStreamNotExpandable);

        buffer.CopyTo(_buffer.Span.Slice(_position));
        _position += buffer.Length;
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);

        // If cancellation is already requested, bail early
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        try
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }
        catch (OperationCanceledException oce)
        {
            return Task.FromCanceled(oce.CancellationToken);
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        try
        {
            // See corresponding comment in ReadAsync for why we don't just always use Write(ReadOnlySpan<byte>).
            // Unlike ReadAsync, we could delegate to WriteAsync(byte[], ...) here, but we don't for consistency.
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> sourceArray))
            {
                Write(sourceArray.Array!, sourceArray.Offset, sourceArray.Count);
            }
            else
            {
                Write(buffer.Span);
            }
            return default;
        }
        catch (OperationCanceledException oce)
        {
            return new ValueTask(Task.FromCanceled(oce.CancellationToken));
        }
        catch (Exception exception)
        {
            return ValueTask.FromException(exception);
        }
    }

    /// <summary>
    /// Sets the position within the current stream.
    /// </summary>
    /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
    /// <param name="origin">A value of type <see cref="SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
    /// <returns>The new position within the stream.</returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        EnsureNotClosed();

        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => InternalBuffer.Length + offset,
            _ => throw new ArgumentException(SR.Argument_InvalidSeekOrigin)
        };

        if (newPosition < 0)
            throw new IOException(SR.IO_SeekBeforeBegin);

        // Allow seeking beyond logical length up to buffer capacity (for write scenarios)
        // and even beyond buffer capacity (reads will return 0, writes will throw)
        ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, int.MaxValue, nameof(offset));

        _position = (int)newPosition;
        return newPosition;
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotSupportedException(SR.NotSupported_MemStreamNotExpandable);
    }

    /// <inheritdoc />
    public override void Flush()
    {
        // No-op: MemoryByteStream has no buffers to flush
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        // Return completed task synchronously for MemoryByteStream (no actual flushing needed)
        return cancellationToken.IsCancellationRequested
            ? Task.FromCanceled(cancellationToken)
            : Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && _isOpen)
        {
            _isOpen = false;
            _writable = false;
            // Don't set buffer to null - allow TryGetBuffer, GetBuffer & ToArray to work.
            // That the stream should no longer be used for I/O
            // doesn't mean the underlying memory should be invalidated.
        }
        base.Dispose(disposing);
    }

    private void EnsureNotClosed()
    {
        ObjectDisposedException.ThrowIf(!_isOpen, this);
    }

    private void EnsureWriteable()
    {
        if (_isReadOnlyBacking || !_writable)
            ThrowHelper.ThrowNotSupportedException_UnwritableStream();

        ObjectDisposedException.ThrowIf(!_isOpen, this);
    }
}
