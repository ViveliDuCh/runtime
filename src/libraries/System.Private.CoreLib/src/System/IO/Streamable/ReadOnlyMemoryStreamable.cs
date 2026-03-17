// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO;

/// <summary>
/// A struct implementing <see cref="IStreamable"/> that wraps a
/// <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/> as a read-only,
/// seekable data source.
/// </summary>
internal struct ReadOnlyMemoryStreamable : IStreamable
{
    private readonly ReadOnlyMemory<byte> _memory;
    private int _position;

    public ReadOnlyMemoryStreamable(ReadOnlyMemory<byte> memory)
    {
        _memory = memory;
        _position = 0;
    }

    public readonly bool CanRead => true;
    public readonly bool CanWrite => false;
    public readonly bool CanSeek => true;
    public readonly long Length => _memory.Length;

    public long Position
    {
        readonly get => _position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, int.MaxValue);
            _position = (int)value;
        }
    }

    public int Read(Span<byte> buffer)
    {
        int bytesAvailable = _memory.Length - _position;
        int bytesToCopy = Math.Min(bytesAvailable, buffer.Length);

        if (bytesToCopy <= 0)
            return 0;

        _memory.Span.Slice(_position, bytesToCopy).CopyTo(buffer);
        _position += bytesToCopy;
        return bytesToCopy;
    }

    public readonly void Write(ReadOnlySpan<byte> buffer) =>
        ThrowHelper.ThrowNotSupportedException_UnwritableStream();

    public long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _memory.Length + offset,
            _ => throw new ArgumentException(SR.Argument_InvalidSeekOrigin)
        };

        ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, int.MaxValue);

        _position = (int)newPosition;
        return _position;
    }

    public int ReadByte()
    {
        if (_position >= _memory.Length)
            return -1;

        return _memory.Span[_position++];
    }

    public readonly void WriteByte(byte value) =>
        ThrowHelper.ThrowNotSupportedException_UnwritableStream();

    public void CopyTo(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        ReadOnlySpan<byte> remaining = _memory.Span.Slice(_position);
        if (remaining.Length > 0)
        {
            destination.Write(remaining);
            _position = _memory.Length;
        }
    }

    public readonly void SetLength(long value) =>
        ThrowHelper.ThrowNotSupportedException_UnwritableStream();
}
