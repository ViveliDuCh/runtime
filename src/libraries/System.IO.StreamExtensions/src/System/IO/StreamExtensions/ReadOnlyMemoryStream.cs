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
/// Provides a read-only <see cref="Stream"/> implementation over a <see cref="ReadOnlyMemory{T}"/> of bytes.
/// </summary>
public class ReadOnlyMemoryStream : Stream //ReadOnlyBufferStream from usecasesExtension project
{
    private ReadOnlyMemory<byte> _buffer;
    private int _position;
    private bool _isOpen;
    private readonly bool _publiclyVisible;
    private Task<int>? _lastReadTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyMemoryStream"/> class over the specified <see cref="ReadOnlyMemory{Byte}"/>.
    /// The underlying buffer is publicly visible by default.
    /// </summary>
    /// <param name="buffer">The <see cref="ReadOnlyMemory{Byte}"/> to wrap.</param>
    public ReadOnlyMemoryStream(ReadOnlyMemory<byte> buffer)
        : this(buffer, publiclyVisible: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyMemoryStream"/> class over the specified <see cref="ReadOnlyMemory{Byte}"/>.
    /// </summary>
    /// <param name="buffer">The <see cref="ReadOnlyMemory{Byte}"/> to wrap.</param>
    /// <param name="publiclyVisible">Indicates whether the underlying buffer can be accessed via <see cref="TryGetBuffer"/>.</param>
    public ReadOnlyMemoryStream(ReadOnlyMemory<byte> buffer, bool publiclyVisible)
    {
        _buffer = buffer;
        _publiclyVisible = publiclyVisible;
        _isOpen = true;
        _position = 0;
    }

    /// <inheritdoc />
    public override bool CanRead => _isOpen;

    /// <inheritdoc />
    public override bool CanSeek => _isOpen;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length
    {
        get
        {
            EnsureNotClosed();
            return _buffer.Length;
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
            _position = (int)Math.Min(value, int.MaxValue);
        }
    }

    /// <summary>
    /// Attempts to get the underlying buffer.
    /// </summary>
    /// <param name="buffer">When this method returns, contains the underlying <see cref="ReadOnlyMemory{Byte}"/> if the buffer is exposable; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if the buffer is exposable and was retrieved; otherwise, <see langword="false"/>.</returns>
    public bool TryGetBuffer(out ReadOnlyMemory<byte> buffer)
    {
        if (!_publiclyVisible)
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
        ValidateBufferArguments(buffer, offset, count);
        return Read(new Span<byte>(buffer, offset, count));
    }

    /// <inheritdoc />
    public override int Read(Span<byte> user_buffer)
    {
        EnsureNotClosed();

        int bytesAvailable = Math.Max(0, _buffer.Length - _position);
        int bytesToRead = Math.Min(bytesAvailable, user_buffer.Length);

        if (bytesToRead > 0)
        {
            _buffer.Span.Slice(_position, bytesToRead).CopyTo(user_buffer);
            _position += bytesToRead;
        }

        return bytesToRead;
    }

    /// <inheritdoc />
    public override int ReadByte()
    {
        EnsureNotClosed();

        if (_position >= _buffer.Length)
            return -1;

        return _buffer.Span[_position++];
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

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void WriteByte(byte value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        EnsureNotClosed();

        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _buffer.Length + offset,
            _ => throw new ArgumentException("Invalid seek origin.", nameof(origin))
        };

        if (newPosition < 0)
            throw new IOException("Seek position out of range.");

        _position = (int)Math.Min(newPosition, int.MaxValue);
        return _position;
    }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        throw new NotSupportedException("Cannot resize ReadOnlyBufferStream.");
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && _isOpen)
        {
            _lastReadTask = null;
            _isOpen = false;
            // Don't set buffer to null - allow TryGetBuffer, GetBuffer & ToArray to work.
            // That the stream should no longer be used for I/O
            // doesn’t mean the underlying memory should be invalidated.
        }
        base.Dispose(disposing);
    }

    private void EnsureNotClosed()
    {
        ObjectDisposedException.ThrowIf(!_isOpen, this);
    }
}
