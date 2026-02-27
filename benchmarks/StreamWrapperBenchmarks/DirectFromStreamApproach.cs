// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace StreamWrapperBenchmarks;

/// <summary>
/// Approach A: Derives directly from Stream (like the current MemoryByteStream prototype).
/// Minimal fields, sealed, no virtual dispatch overhead.
/// </summary>
public sealed class DirectFromStreamApproach : Stream
{
    private Memory<byte> _buffer;
    private ReadOnlyMemory<byte> _readOnlyBuffer;
    private readonly bool _isReadOnlyBacking;
    private int _position;
    private bool _isOpen;

    public DirectFromStreamApproach(Memory<byte> buffer)
    {
        _buffer = buffer;
        _isReadOnlyBacking = false;
        _isOpen = true;
    }

    public DirectFromStreamApproach(ReadOnlyMemory<byte> buffer)
    {
        _readOnlyBuffer = buffer;
        _isReadOnlyBacking = true;
        _isOpen = true;
    }

    public override bool CanRead => _isOpen;
    public override bool CanSeek => _isOpen;
    public override bool CanWrite => !_isReadOnlyBacking && _isOpen;

    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(!_isOpen, this);
            return InternalBuffer.Length;
        }
    }

    private ReadOnlyMemory<byte> InternalBuffer
        => _isReadOnlyBacking ? _readOnlyBuffer : _buffer;

    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(!_isOpen, this);
            return _position;
        }
        set
        {
            ObjectDisposedException.ThrowIf(!_isOpen, this);
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, int.MaxValue);
            _position = (int)value;
        }
    }

    public override int ReadByte()
    {
        ObjectDisposedException.ThrowIf(!_isOpen, this);
        if (_position >= InternalBuffer.Length) return -1;
        return InternalBuffer.Span[_position++];
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(new Span<byte>(buffer, offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(!_isOpen, this);
        int length = InternalBuffer.Length;
        if (_position >= length) return 0;

        int bytesToRead = Math.Min(length - _position, buffer.Length);
        if (bytesToRead > 0)
        {
            InternalBuffer.Span.Slice(_position, bytesToRead).CopyTo(buffer);
            _position += bytesToRead;
        }
        return bytesToRead;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled<int>(cancellationToken);
        return new ValueTask<int>(Read(buffer.Span));
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        Write(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(!_isOpen, this);
        if (_isReadOnlyBacking) throw new NotSupportedException();
        if (_position > _buffer.Length - buffer.Length) throw new NotSupportedException();
        buffer.CopyTo(_buffer.Span.Slice(_position));
        _position += buffer.Length;
    }

    public override void WriteByte(byte value)
    {
        ObjectDisposedException.ThrowIf(!_isOpen, this);
        if (_isReadOnlyBacking) throw new NotSupportedException();
        if (_position >= _buffer.Length) throw new NotSupportedException();
        _buffer.Span[_position++] = value;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled(cancellationToken);
        Write(buffer.Span);
        return default;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(!_isOpen, this);
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => InternalBuffer.Length + offset,
            _ => throw new ArgumentException("Invalid seek origin")
        };
        if (newPosition < 0) throw new IOException("Seek before begin");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, int.MaxValue, nameof(offset));
        _position = (int)newPosition;
        return newPosition;
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;

    protected override void Dispose(bool disposing)
    {
        if (disposing) _isOpen = false;
        base.Dispose(disposing);
    }
}
