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

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlySequenceStream"/> class over the specified <see cref="ReadOnlySequence{Byte}"/>.
    /// </summary>
    /// <param name="sequence">The <see cref="ReadOnlySequence{Byte}"/> to wrap.</param>
    public ReadOnlySequenceStream(ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsEmpty)
        {
            throw new ArgumentException("The sequence cannot be empty.", nameof(sequence));
        }
        this.sequence = sequence;
        this.position = sequence.Start;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => sequence.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => sequence.Slice(0, position).Length;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            position = sequence.GetPosition(value, sequence.Start);
        }
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        ReadOnlySequence<byte> remaining = sequence.Slice(position);
        ReadOnlySequence<byte> toCopy = remaining.Slice(0, Math.Min(buffer.Length, remaining.Length));
        position = toCopy.End;
        toCopy.CopyTo(buffer);
        return (int)toCopy.Length;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (buffer.Length - offset < count) throw new ArgumentException("Invalid offset and length.");

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
        SequencePosition relativeTo = origin switch
        {
            SeekOrigin.Begin => sequence.Start,
            SeekOrigin.Current => position,
            SeekOrigin.End => sequence.Start,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        // SeekOrigin.End always converts to absolute
        // by adding Length and using Start as reference
        if (origin == SeekOrigin.End)
        {
            offset += Length;
            relativeTo = sequence.Start;
        }
        else if (origin == SeekOrigin.Current && offset < 0)
        {
            offset += Position;
            relativeTo = sequence.Start;
        }

        position = sequence.GetPosition(offset, relativeTo);
        return Position;
    }

    /// <inheritdoc />
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
