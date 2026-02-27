// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace StreamWrapperBenchmarks;

/// <summary>
/// Approach B: Derives from MemoryStream, with new constructors for Memory&lt;byte&gt;
/// and ReadOnlyMemory&lt;byte&gt;.
///
/// The goal is to reuse MemoryStream's infrastructure while wrapping Memory types directly.
///
/// PROBLEM: MemoryStream's fields (_buffer, _position, _length, _capacity, _origin,
/// _expandable, _exposable, _isOpen, _lastReadTask) are ALL private — not protected.
/// A derived class has NO access to them.
///
/// Therefore, we must:
///   1. Call `base(0)` to satisfy the constructor requirement (allocates a dummy empty array)
///   2. Store our own Memory&lt;byte&gt; / ReadOnlyMemory&lt;byte&gt; fields
///   3. Override EVERY method to use our fields instead of the inaccessible base fields
///
/// This means we carry the weight of MemoryStream's unused private fields
/// (56+ bytes of dead state) while gaining zero code reuse from the base class.
/// The only "benefit" is `is MemoryStream` type checks return true.
/// </summary>
public sealed class MemoryStreamDerivedApproach : MemoryStream
{
    // Our own fields (since base fields are inaccessible)
    private Memory<byte> _memBuffer;
    private ReadOnlyMemory<byte> _readOnlyMemBuffer;
    private readonly bool _isReadOnlyBacking;
    private int _pos;
    private bool _open;

    // MemoryStream base(0) allocates: byte[] _buffer (empty), plus sets
    // _capacity=0, _expandable=true, _writable=true, _exposable=true, _isOpen=true
    // All of those are now dead weight that we never use.

    /// <summary>
    /// Wraps a writable Memory&lt;byte&gt; as a seekable, non-expandable stream.
    /// </summary>
    public MemoryStreamDerivedApproach(Memory<byte> buffer) : base(0)
    {
        _memBuffer = buffer;
        _isReadOnlyBacking = false;
        _open = true;
    }

    /// <summary>
    /// Wraps a ReadOnlyMemory&lt;byte&gt; as a seekable, read-only stream.
    /// </summary>
    public MemoryStreamDerivedApproach(ReadOnlyMemory<byte> buffer) : base(0)
    {
        _readOnlyMemBuffer = buffer;
        _isReadOnlyBacking = true;
        _open = true;
    }

    private ReadOnlyMemory<byte> InternalBuffer
        => _isReadOnlyBacking ? _readOnlyMemBuffer : _memBuffer;

    // --- Must override EVERY property/method because base uses inaccessible private fields ---

    public override bool CanRead => _open;
    public override bool CanSeek => _open;
    public override bool CanWrite => !_isReadOnlyBacking && _open;

    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(!_open, this);
            return InternalBuffer.Length;
        }
    }

    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(!_open, this);
            return _pos;
        }
        set
        {
            ObjectDisposedException.ThrowIf(!_open, this);
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, int.MaxValue);
            _pos = (int)value;
        }
    }

    // Override Capacity to prevent base from trying to resize its own dummy array
    public override int Capacity
    {
        get
        {
            ObjectDisposedException.ThrowIf(!_open, this);
            return InternalBuffer.Length;
        }
        set => throw new NotSupportedException("Cannot resize a Memory-backed stream.");
    }

    public override int ReadByte()
    {
        ObjectDisposedException.ThrowIf(!_open, this);
        if (_pos >= InternalBuffer.Length) return -1;
        return InternalBuffer.Span[_pos++];
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(new Span<byte>(buffer, offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(!_open, this);
        int length = InternalBuffer.Length;
        if (_pos >= length) return 0;

        int bytesToRead = Math.Min(length - _pos, buffer.Length);
        if (bytesToRead > 0)
        {
            InternalBuffer.Span.Slice(_pos, bytesToRead).CopyTo(buffer);
            _pos += bytesToRead;
        }
        return bytesToRead;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<int>(cancellationToken);
        return Task.FromResult(Read(buffer, offset, count));
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
        ObjectDisposedException.ThrowIf(!_open, this);
        if (_isReadOnlyBacking) throw new NotSupportedException();
        if (_pos > _memBuffer.Length - buffer.Length) throw new NotSupportedException();
        buffer.CopyTo(_memBuffer.Span.Slice(_pos));
        _pos += buffer.Length;
    }

    public override void WriteByte(byte value)
    {
        ObjectDisposedException.ThrowIf(!_open, this);
        if (_isReadOnlyBacking) throw new NotSupportedException();
        if (_pos >= _memBuffer.Length) throw new NotSupportedException();
        _memBuffer.Span[_pos++] = value;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled(cancellationToken);
        Write(buffer.Span);
        return default;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(!_open, this);
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _pos + offset,
            SeekOrigin.End => InternalBuffer.Length + offset,
            _ => throw new ArgumentException("Invalid seek origin")
        };
        if (newPosition < 0) throw new IOException("Seek before begin");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, int.MaxValue, nameof(offset));
        _pos = (int)newPosition;
        return newPosition;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;

    // Must override these MemoryStream-specific virtuals to prevent base from using its dead fields
    public override byte[] GetBuffer() => throw new NotSupportedException("Use Memory<byte> directly.");

    public override bool TryGetBuffer(out ArraySegment<byte> buffer)
    {
        if (MemoryMarshal.TryGetArray(InternalBuffer, out var segment))
        {
            buffer = segment;
            return true;
        }
        buffer = default;
        return false;
    }

    public override byte[] ToArray() => InternalBuffer.ToArray();

    public override void WriteTo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ObjectDisposedException.ThrowIf(!_open, this);
        var buf = InternalBuffer;
        if (MemoryMarshal.TryGetArray(buf, out var segment))
        {
            stream.Write(segment.Array!, segment.Offset, segment.Count);
        }
        else
        {
            stream.Write(buf.Span);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _open = false;
        base.Dispose(disposing);
    }
}
