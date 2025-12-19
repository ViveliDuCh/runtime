// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.StreamExtensions;

/// <summary>
/// Provides a seekable, read-only <see cref="Stream"/> implementation over a <see cref="ReadOnlySequence{T}"/> of bytes.
/// </summary>
// Seekable Stream from ReadOnlySequence<byte>
public sealed class ReadOnlySequenceStream : Stream
{
    private ReadOnlySequence<byte> _sequence;
    private SequencePosition _position;
    private long _positionPastEnd; // -1 if within bounds, or the actual position if past end
    private bool _isDisposed;
    private Task<int>? _lastReadTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlySequenceStream"/> class over the specified <see cref="ReadOnlySequence{Byte}"/>.
    /// </summary>
    /// <param name="sequence">The <see cref="ReadOnlySequence{Byte}"/> to wrap.</param>
    public ReadOnlySequenceStream(ReadOnlySequence<byte> sequence)
    {
        _sequence = sequence;
        _position = sequence.Start;
        _positionPastEnd = -1;
        _isDisposed = false;
    }

    /// <inheritdoc />
    public override bool CanRead => !_isDisposed;

    /// <inheritdoc />
    public override bool CanSeek => !_isDisposed;

    /// <inheritdoc />
    public override bool CanWrite => false;

    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    /// <inheritdoc />
    public override long Length
    {
        get
        {
            EnsureNotDisposed();
            return _sequence.Length;
        }
    }

    /// <inheritdoc />
    public override long Position
    {
        get
        {
            EnsureNotDisposed();
            return _positionPastEnd >= 0 ? _positionPastEnd : _sequence.Slice(_sequence.Start, _position).Length;
        }
        set
        {
            EnsureNotDisposed();
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            // Allow seeking past the end
            if (value >= Length)
            {
                _position = _sequence.End;
                _positionPastEnd = value;
            }
            else
            {
                _position = _sequence.GetPosition(value, _sequence.Start);
                _positionPastEnd = -1;
            }
        }
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        EnsureNotDisposed();

        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if ((ulong)(uint)offset + (uint)count > (uint)buffer.Length) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        EnsureNotDisposed();

        if (_positionPastEnd >= 0)
        {
            return 0;
        }

        ReadOnlySequence<byte> remaining = _sequence.Slice(_position);
        int n = (int)Math.Min(remaining.Length, buffer.Length);
        if (n <= 0)
        {
            return 0;
        }

        remaining.Slice(0, n).CopyTo(buffer);
        _position = _sequence.GetPosition(n, _position);
        return n;
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
    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureNotDisposed();
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <summary>
    /// Sets the position within the current stream.
    /// </summary>
    /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
    /// <param name="origin">A value of type <see cref="SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
    /// <returns>The new position within the stream.</returns>
    public override long Seek(long offset, SeekOrigin origin)
    {
        EnsureNotDisposed();

        // Calculate absolute position
        long currentPosition = _positionPastEnd >= 0 ? _positionPastEnd : _sequence.Slice(_sequence.Start, _position).Length;
        long absolutePosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => currentPosition + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        // Negative positions are invalid
        if (absolutePosition < 0)
        {
            throw new IOException("An attempt was made to move the position before the beginning of the stream.");
        }

        // Update position - seeking past end is allowed
        if (absolutePosition >= Length)
        {
            _position = _sequence.End;
            _positionPastEnd = absolutePosition;
        }
        else
        {
            _position = _sequence.GetPosition(absolutePosition, _sequence.Start);
            _positionPastEnd = -1;
        }

        return absolutePosition;
    }

    /// <inheritdoc />
    public override void Flush(){ }

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        EnsureNotDisposed();
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _lastReadTask = null;
        _isDisposed = true;
        base.Dispose(disposing);
    }
}
