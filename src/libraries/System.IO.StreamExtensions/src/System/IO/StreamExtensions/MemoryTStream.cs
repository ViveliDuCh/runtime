// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Collections.Generic;
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
    private int _position;
    private int _length; // // Number of valid bytes within the buffer
    private bool _isOpen;
    private bool _writable; // For read-only support
    private readonly bool _exposable;

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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryTStream"/> class over the specified <see cref="Memory{Byte}"/>.
    /// </summary>
    /// <param name="buffer">The <see cref="Memory{Byte}"/> to wrap.</param>
    /// <param name="publiclyVisible">Indicates whether the underlying buffer can be accessed via <see cref="TryGetBuffer"/>.</param>
    /// <param name="writable">Indicates whether the stream supports writing.</param>
    public MemoryTStream(Memory<byte> buffer, bool writable, bool publiclyVisible)
        : this(buffer, buffer.Length, writable, publiclyVisible)
    { // Since the length is buffer.Length and the internal buffer's length shouldn't change
      // we can just always use buffer length. **Check to change the length parameter or if to keep it
      // we can just always use buffer length. **Check to change the length parameter or if to keep it
      // If kept, then maybe the logical length is needed, or maybe just _position
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryTStream"/> class over the specified <see cref="Memory{Byte}"/> with a specific initial length.
    /// </summary>
    /// <param name="buffer">The <see cref="Memory{Byte}"/> to wrap (provides the capacity).</param>
    /// <param name="length">The initial logical length of the stream (must be &lt;= buffer.Length).</param>
    /// <param name="writable">Indicates whether the stream supports writing.</param>
    /// <param name="publiclyVisible">Indicates whether the underlying buffer can be accessed via <see cref="TryGetBuffer"/>.</param>
    public MemoryTStream(Memory<byte> buffer, int length, bool writable, bool publiclyVisible)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, buffer.Length);

        _buffer = buffer;
        _length = length; // Mem<byte> can represent a buffer maybe not completely fully used
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
    /// Attempts to get the underlying buffer.
    /// </summary>
    /// <param name="buffer">When this method returns, contains the underlying <see cref="Memory{Byte}"/> if the buffer is exposable; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if the buffer is exposable and was retrieved; otherwise, <see langword="false"/>.</returns>
    public bool TryGetBuffer(out Memory<byte> buffer)
    {
        if (!_exposable)
        {
            buffer = default;
            return false;
        }

        buffer = _buffer;
        return true;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        // Validate count before offset to ensure proper parameter name in exception
        if (offset > buffer.Length || count > buffer.Length - offset)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length - offset);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, buffer.Length);
        }

        EnsureNotClosed();

        // If position is past the number of valid bytes written (_length), return 0 (EOF)
        if (_position >= _length)
        {
            return 0;
        }

        int bytesAvailable = _length - _position;
        int bytesToRead = Math.Min(bytesAvailable, count);

        if (bytesToRead > 0)
        {
            _buffer.Span.Slice(_position, bytesToRead).CopyTo(buffer.AsSpan(offset));
            _position += bytesToRead;
        }

        return bytesToRead;
    }

    /// <inheritdoc />
    public override int ReadByte()
    {
        EnsureNotClosed();

        if (_position >= _length)
            return -1;

        return _buffer.Span[_position++];
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        // Validate count before offset to ensure proper parameter name in exception
        if (offset > buffer.Length || count > buffer.Length - offset)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length - offset);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, buffer.Length);
        }

        EnsureNotClosed();
        EnsureWriteable();

        if (_position + count > _buffer.Length)
            throw new NotSupportedException("Cannot expand buffer.  Write would exceed capacity.");

        buffer.AsSpan(offset, count).CopyTo(_buffer.Span.Slice(_position));
        _position += count;

        // Update number of valid bytes written if written past the current length
        if (_position > _length)
            _length = _position;
    }

    /// <inheritdoc />
    public override void WriteByte(byte value)
    {
        EnsureNotClosed();
        EnsureWriteable();

        if (_position >= _buffer.Length)
            throw new NotSupportedException("Cannot expand buffer. Write would exceed capacity.");

        _buffer.Span[_position++] = value;

        // Update number of valid bytes written if written past the current length
        if (_position > _length)
            _length = _position;
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
