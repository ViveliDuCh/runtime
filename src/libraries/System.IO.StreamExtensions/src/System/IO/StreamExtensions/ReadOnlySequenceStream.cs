// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;

namespace System.IO.StreamExtensions;

/// <summary>
/// Provides a seekable, read-only <see cref="Stream"/> implementation over a <see cref="ReadOnlySequence{T}"/> of bytes.
/// </summary>
// Seekable Stream from ReadOnlySequence<byte>
public sealed class ReadOnlySequenceStream : Stream
{
    private ReadOnlySequence<byte> sequence;
    private SequencePosition position;
    private long _positionPastEnd; // -1 if within bounds, or the actual position if past end
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlySequenceStream"/> class over the specified <see cref="ReadOnlySequence{Byte}"/>.
    /// </summary>
    /// <param name="sequence">The <see cref="ReadOnlySequence{Byte}"/> to wrap.</param>
    public ReadOnlySequenceStream(ReadOnlySequence<byte> sequence)
    {
        this.sequence = sequence;
        this.position = sequence.Start;
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
            return sequence.Length;
        }
    }

    /// <inheritdoc />
    public override long Position
    {
        get
        {
            EnsureNotDisposed();
            return _positionPastEnd >= 0 ? _positionPastEnd : sequence.Slice(sequence.Start, position).Length;
        }
        set
        {
            EnsureNotDisposed();
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            // Allow seeking past the end
            if (value >= Length)
            {
                position = sequence.End;
                _positionPastEnd = value;
            }
            else
            {
                position = sequence.GetPosition(value, sequence.Start);
                _positionPastEnd = -1;
            }
        }
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        EnsureNotDisposed();

        if (_positionPastEnd >= 0)
        {
            return 0;
        }

        ReadOnlySequence<byte> remaining = sequence.Slice(position);
        int n = (int)Math.Min(remaining.Length, buffer.Length);
        if (n <= 0)
        {
            return 0;
        }

        remaining.Slice(0, n).CopyTo(buffer);
        position = sequence.GetPosition(n, position);
        return n;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        EnsureNotDisposed();

        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if ((ulong)(uint)offset + (uint)count > (uint)buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        return Read(buffer.AsSpan(offset, count));
    }

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
        long currentPosition = _positionPastEnd >= 0 ? _positionPastEnd : sequence.Slice(sequence.Start, position).Length;
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
            position = sequence.End;
            _positionPastEnd = absolutePosition;
        }
        else
        {
            position = sequence.GetPosition(absolutePosition, sequence.Start);
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
    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureNotDisposed();
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _isDisposed = true;
        base.Dispose(disposing);
    }
}
