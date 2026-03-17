// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO;

/// <summary>
/// MINIMAL implementation — only provides the 3 core IStreamable members.
/// Gets ReadByte, Seek, CopyTo, Write (throws), WriteByte (throws),
/// SetLength (throws) for FREE from DIM defaults.
/// </summary>
/// <remarks>
/// Compare with the full override version below: this is 25 lines vs ~95.
/// The DIM defaults work correctly but may be slower for per-byte ops
/// (ReadByte allocates a 1-byte span via the DIM instead of direct indexing).
/// </remarks>
internal struct ReadOnlyMemoryStreamableMinimal : IStreamable
{
    private readonly ReadOnlyMemory<byte> _memory;
    private int _position;

    public ReadOnlyMemoryStreamableMinimal(ReadOnlyMemory<byte> memory)
    {
        _memory = memory;
        _position = 0;
    }

    // === CORE MEMBERS (required) ===
    public readonly long Length => _memory.Length;

    public long Position
    {
        readonly get => _position;
        set => _position = (int)value;
    }

    public int Read(Span<byte> buffer)
    {
        int n = Math.Min(_memory.Length - _position, buffer.Length);
        if (n <= 0) return 0;
        _memory.Span.Slice(_position, n).CopyTo(buffer);
        _position += n;
        return n;
    }

    // === EVERYTHING ELSE comes from DIM defaults ===
    // ReadByte()    → DIM: calls Read() with 1-byte span
    // Seek()        → DIM: computes position from Length/Position
    // CopyTo()      → DIM: reads in loop with rented buffer
    // CanRead       → DIM: true
    // CanWrite      → DIM: false
    // CanSeek       → DIM: true
    // Write()       → DIM: throws NotSupportedException
    // WriteByte()   → DIM: throws NotSupportedException
    // SetLength()   → DIM: throws NotSupportedException
}

/// <summary>
/// FULL override version — provides optimized overrides for hot-path
/// operations while still relying on DIMs for write operations (throws).
/// </summary>
/// <remarks>
/// An implementer can selectively override only the DIMs where the default
/// isn't fast enough. Here we override ReadByte (direct indexing) and
/// CopyTo (single Span.CopyTo) but keep the DIM defaults for Seek,
/// Write, WriteByte, SetLength.
/// </remarks>
internal struct ReadOnlyMemoryStreamable : IStreamable
{
    private readonly ReadOnlyMemory<byte> _memory;
    private int _position;

    public ReadOnlyMemoryStreamable(ReadOnlyMemory<byte> memory)
    {
        _memory = memory;
        _position = 0;
    }

    // === CORE MEMBERS (required) ===
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

    // === SELECTIVE OVERRIDES for performance ===

    // Override ReadByte: direct Span indexing instead of DIM's 1-byte Read()
    public int ReadByte()
    {
        if (_position >= _memory.Length)
            return -1;

        return _memory.Span[_position++];
    }

    // Override CopyTo: single bulk copy instead of DIM's rented-buffer loop
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

    // Seek, CanRead, CanWrite, CanSeek, Write, WriteByte, SetLength
    // all use the DIM defaults — no code needed here
}
