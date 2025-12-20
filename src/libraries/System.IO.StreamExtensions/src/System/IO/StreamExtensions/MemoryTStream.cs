// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.StreamExtensions;

/// <summary>
/// Provides a <see cref="Stream"/> implementation over a <see cref="Memory{T}"/> of bytes with optional write support.
/// </summary>
public class MemoryTStream : Stream
{
    private Memory<byte> _buffer;
    private ReadOnlyMemory<byte> _readOnlyBuffer;
    private bool _isReadOnlyBacking;
    private int _position;
    private int _length; // // Number of valid bytes within the buffer
    private bool _isOpen;
    private bool _writable; // For read-only support
    private readonly bool _exposable;
    private Task<int>? _lastReadTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryTStream"/> class over the specified <see cref="Memory{Byte}"/>.
    /// The stream is writable and publicly visible by default.
    /// </summary>
    /// <param name="buffer">The <see cref="Memory{Byte}"/> to wrap.</param>
    public MemoryTStream(Memory<byte> buffer)
    : this(buffer, writable: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryTStream"/> class over the specified <see cref="ReadOnlyMemory{Byte}"/>.
    /// The stream is read-only and publicly visible by default.
    /// </summary>
    /// <param name="buffer">The <see cref="ReadOnlyMemory{Byte}"/> to wrap.</param>
    public MemoryTStream(ReadOnlyMemory<byte> buffer)
        : this(buffer, publiclyVisible: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryTStream"/> class over the specified <see cref="ReadOnlyMemory{Byte}"/> with visibility control.
    /// Stream is always read-only.
    /// </summary>
    /// <param name="buffer">The <see cref="ReadOnlyMemory{Byte}"/> to wrap.</param>
    /// <param name="publiclyVisible">Indicates whether the underlying buffer can be accessed via TryGetBuffer.</param>
    public MemoryTStream(ReadOnlyMemory<byte> buffer, bool publiclyVisible)
    {
        _readOnlyBuffer = buffer;
        _isReadOnlyBacking = true;
        _length = buffer.Length;
        _writable = false;
        _exposable = publiclyVisible;
        _isOpen = true;
        _position = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryTStream"/> class over the specified <see cref="Memory{Byte}"/>.
    /// </summary>
    /// <param name="buffer">The <see cref="Memory{Byte}"/> to wrap.</param>
    /// <param name="writable">Indicates whether the stream supports writing.</param>
    public MemoryTStream(Memory<byte> buffer, bool writable)
    {
        _buffer = buffer;
        _length = buffer.Length;
        _isOpen = true;
        _writable = writable;
        _position = 0;
        _exposable = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryTStream"/> class over the specified <see cref="Memory{Byte}"/> with a specific initial length.
    /// </summary>
    /// <param name="buffer">The <see cref="Memory{Byte}"/> to wrap (provides the capacity).</param>
    /// <param name="length">The initial logical length of the stream (must be &lt;= buffer.Length).</param>
    /// <param name="writable">Indicates whether the stream supports writing.</param>
    /// <remarks>
    /// This constructor allows tracking logical length separately from capacity. Use <paramref name="length"/> = 0
    /// for an empty buffer that grows as data is written, or set it to the number of valid bytes already in the buffer.
    /// </remarks>
    public MemoryTStream(Memory<byte> buffer, int length, bool writable)
        : this(buffer, length, writable, publiclyVisible: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryTStream"/> class over the specified <see cref="Memory{Byte}"/> with optional write support.
    /// </summary>
    /// <param name="buffer">The <see cref="Memory{Byte}"/> to wrap.</param>
    /// <param name="writable">Indicates whether the stream supports writing.</param>
    /// <param name="publiclyVisible">Indicates whether the underlying buffer can be accessed via TryGetBuffer.</param>
    public MemoryTStream(Memory<byte> buffer, bool writable, bool publiclyVisible)
    {
        _buffer = buffer;
        _length = buffer.Length;
        _isOpen = true;
        _writable = writable;
        _position = 0;
        _exposable = publiclyVisible;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryTStream"/> class over the specified <see cref="Memory{Byte}"/> with a specific initial length.
    /// </summary>
    /// <param name="buffer">The <see cref="Memory{Byte}"/> to wrap (provides the capacity).</param>
    /// <param name="length">The initial logical length of the stream (must be &lt;= buffer.Length).</param>
    /// <param name="writable">Indicates whether the stream supports writing.</param>
    /// <param name="publiclyVisible">Indicates whether the underlying buffer can be accessed via TryGetBuffer.</param>
    /// <remarks>
    /// This constructor allows tracking logical length separately from capacity. Use <paramref name="length"/> = 0
    /// for an empty buffer that grows as data is written, or set it to the number of valid bytes already in the buffer.
    /// </remarks>
    public MemoryTStream(Memory<byte> buffer, int length, bool writable, bool publiclyVisible)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, buffer.Length);

        _buffer = buffer;
        _length = length;
        _writable = writable;
        _exposable = publiclyVisible;
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
            return _length;
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

    /// <summary>
    /// Attempts to get the underlying writable buffer, if present and exposable.
    /// </summary>
    /// <param name="buffer">When this method returns, contains the underlying <see cref="Memory{Byte}"/> if the buffer is writable and exposable; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if the buffer is writable and exposable and was retrieved; otherwise, <see langword="false"/>.</returns>
    public bool TryGetBuffer(out Memory<byte> buffer)
    {
        if (!_exposable || _isReadOnlyBacking)
        {
            buffer = default;
            return false;
        }

        buffer = _buffer;
        return true;
    }

    /// <summary>
    /// Attempts to get the underlying buffer as read-only memory.
    /// </summary>
    /// <param name="buffer">When this method returns, contains the underlying buffer as <see cref="ReadOnlyMemory{Byte}"/> if exposable; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if the buffer is exposable and was retrieved; otherwise, <see langword="false"/>.</returns>
    public bool TryGetBuffer(out ReadOnlyMemory<byte> buffer)
    {
        if (!_exposable)
        {
            buffer = default;
            return false;
        }

        buffer = InternalBuffer;
        return true;
    }

    /// <inheritdoc />
    public override int ReadByte()
    {
        EnsureNotClosed();

        if (_position >= _length)
            return -1;

        return InternalBuffer.Span[_position++];
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length - offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, buffer.Length);

        return Read(new Span<byte>(buffer, offset, count));
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        EnsureNotClosed();

        // If position is past the number of valid bytes written (_length), return 0 (EOF)
        if (_position >= _length)
        {
            return 0;
        }

        int bytesAvailable = _length - _position;
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

        try
        {
            int n = Read(buffer, offset, count);

            // Try to reuse the cached task if it has the same result
            Task<int>? lastReadTask = _lastReadTask;
            if (lastReadTask != null && lastReadTask.Result == n)
            {
                return lastReadTask;
            }

            // Create a new task and cache it
            Task<int> newTask = Task.FromResult(n);
            _lastReadTask = newTask;
            return newTask;
        }
        catch (OperationCanceledException oce)
        {
            return Task.FromCanceled<int>(oce.CancellationToken);
        }
        catch (Exception exception)
        {
            return Task.FromException<int>(exception);
        }
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        try
        {

            int bytesRead;
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> array))
            {
                // Fast path:  Memory<byte> wraps an array
                bytesRead = Read(array.Array!, array.Offset, array.Count);
            }
            else
            {
                // Slow path: rent a buffer, read, copy
                byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
                try
                {
                    bytesRead = Read(rentedBuffer, 0, buffer.Length);
                    rentedBuffer.AsSpan(0, bytesRead).CopyTo(buffer.Span);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }

            return new ValueTask<int>(bytesRead);
        }
        catch (OperationCanceledException oce)
        {
            return ValueTask.FromCanceled<int>(oce.CancellationToken);
        }
        catch (Exception exception)
        {
            return ValueTask.FromException<int>(exception);
        }
    }

    /// <inheritdoc />
    public override void WriteByte(byte value)
    {
        EnsureNotClosed();
        EnsureWriteable();

        if (_isReadOnlyBacking) // extra writable check
            throw new NotSupportedException("Cannot write: underlying buffer is read-only.");

        if (_position >= InternalBuffer.Length)
            throw new NotSupportedException("Cannot expand buffer. Write would exceed capacity.");

        _buffer.Span[_position++] = value;

        // Update number of valid bytes written if written past the current length
        if (_position > _length)
            _length = _position;
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) {
        ValidateBufferArguments(buffer, offset, count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length - offset);

        Write(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureNotClosed();
        EnsureWriteable();

        if (_isReadOnlyBacking)
            throw new NotSupportedException("Cannot write: underlying buffer is read-only.");

        if (_position + buffer.Length > _buffer.Length)
            throw new NotSupportedException("Cannot expand buffer.  Write would exceed capacity.");

        buffer.CopyTo(_buffer.Span.Slice(_position));
        _position += buffer.Length;

        // Update number of valid bytes written if written past the current length
        if (_position > _length)
            _length = _position;
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
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentException("Invalid seek origin.", nameof(origin))
        };

        if (newPosition < 0)
            throw new IOException("An attempt was made to move the position before the beginning of the stream.");

        // Allow seeking beyond logical length up to buffer capacity (for write scenarios)
        // and even beyond buffer capacity (reads will return 0, writes will throw)
        ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, int.MaxValue, nameof(offset));

        _position = (int)newPosition;
        return newPosition;
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotSupportedException("Cannot resize MemoryTStream.");
    }

    /// <inheritdoc />
    public override void Flush()
    {
        // No-op: MemoryTStream has no buffers to flush
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        // Return completed task synchronously for MemoryTStream (no actual flushing needed)
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
        if (!CanWrite)
            throw new NotSupportedException();
    }
}
