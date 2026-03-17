// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO;

/// <summary>
/// A struct implementing <see cref="IStreamable"/> that wraps a
/// <see cref="Memory{T}"/> of <see cref="byte"/> as a writable,
/// seekable data source with fixed capacity.
/// </summary>
internal struct MemoryStreamable : IStreamable
{
    private readonly Memory<byte> _memory;
    private int _position;
    private int _length;
    private readonly bool _writable;

    public MemoryStreamable(Memory<byte> memory, bool writable = true)
    {
        _memory = memory;
        _position = 0;
        _length = memory.Length;
        _writable = writable;
    }

    public readonly bool CanRead => true;
    public readonly bool CanWrite => _writable;
    public readonly bool CanSeek => true;
    public readonly long Length => _length;

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
        int bytesAvailable = _length - _position;
        int bytesToCopy = Math.Min(bytesAvailable, buffer.Length);

        if (bytesToCopy <= 0)
            return 0;

        _memory.Span.Slice(_position, bytesToCopy).CopyTo(buffer);
        _position += bytesToCopy;
        return bytesToCopy;
    }

    public void Write(ReadOnlySpan<byte> buffer)
    {
        if (!_writable)
            ThrowHelper.ThrowNotSupportedException_UnwritableStream();

        if (_position + buffer.Length > _memory.Length)
            throw new NotSupportedException(SR.NotSupported_MemStreamNotExpandable);

        buffer.CopyTo(_memory.Span.Slice(_position));
        _position += buffer.Length;

        if (_position > _length)
            _length = _position;
    }

    public long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentException(SR.Argument_InvalidSeekOrigin)
        };

        ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, int.MaxValue);

        _position = (int)newPosition;
        return _position;
    }

    public int ReadByte()
    {
        if (_position >= _length)
            return -1;

        return _memory.Span[_position++];
    }

    public void WriteByte(byte value)
    {
        if (!_writable)
            ThrowHelper.ThrowNotSupportedException_UnwritableStream();

        if (_position >= _memory.Length)
            throw new NotSupportedException(SR.NotSupported_MemStreamNotExpandable);

        _memory.Span[_position++] = value;

        if (_position > _length)
            _length = _position;
    }

    public void CopyTo(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        ReadOnlySpan<byte> remaining = _memory.Span.Slice(_position, _length - _position);
        if (remaining.Length > 0)
        {
            destination.Write(remaining);
            _position = _length;
        }
    }

    public void SetLength(long value)
    {
        if (!_writable)
            ThrowHelper.ThrowNotSupportedException_UnwritableStream();

        ArgumentOutOfRangeException.ThrowIfNegative(value);

        if (value > _memory.Length)
            throw new NotSupportedException(SR.NotSupported_MemStreamNotExpandable);

        _length = (int)value;

        if (_position > _length)
            _position = _length;
    }
}
