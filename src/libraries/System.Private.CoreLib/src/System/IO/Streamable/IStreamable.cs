// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO;

/// <summary>
/// Defines a contract for types that can provide stream-like byte-level
/// read access over an underlying data source, analogous to how
/// <see cref="System.Collections.Generic.IEnumerable{T}"/> standardizes
/// iteration (implement <c>GetEnumerator()</c> → get LINQ for free).
/// </summary>
/// <remarks>
/// <para>
/// <b>Minimal implementation</b>: A type only needs to provide three core
/// members — <see cref="Read(Span{byte})"/>, <see cref="Length"/>, and
/// <see cref="Position"/> — to get a fully functional streamable type.
/// All other members (<see cref="ReadByte"/>, <see cref="Seek"/>,
/// <see cref="CopyTo"/>, <see cref="CanWrite"/>, <see cref="Write"/>,
/// <see cref="WriteByte"/>, <see cref="SetLength"/>) have default
/// implementations (DIMs) that either delegate to the core members or
/// throw <see cref="NotSupportedException"/> for write operations.
/// </para>
/// <para>
/// Implementers <b>may override</b> any DIM for better performance.
/// For example, a <c>ReadOnlyMemory&lt;byte&gt;</c>-backed type can
/// override <see cref="ReadByte"/> to use direct <c>Span</c> indexing
/// instead of allocating a 1-byte buffer, or override <see cref="CopyTo"/>
/// to use a single <c>Span.CopyTo</c> call.
/// </para>
/// </remarks>
internal interface IStreamable
{
    // ──────────────────────────────────────────────────────────────────
    // CORE MEMBERS — implementers MUST provide these
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Gets the length of the underlying data in bytes.</summary>
    long Length { get; }

    /// <summary>Gets or sets the current byte position within the data.</summary>
    long Position { get; set; }

    /// <summary>
    /// Reads bytes from the current position into the provided buffer.
    /// This is the single core read operation that all DIMs build upon.
    /// </summary>
    /// <param name="buffer">The buffer to read into.</param>
    /// <returns>The number of bytes read, or 0 if at end.</returns>
    int Read(Span<byte> buffer);

    // ──────────────────────────────────────────────────────────────────
    // DIM DEFAULTS — implementers get these for free, MAY override
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Gets whether the streamable supports reading. Default: <c>true</c>.</summary>
    bool CanRead => true;

    /// <summary>Gets whether the streamable supports writing. Default: <c>false</c>.</summary>
    bool CanWrite => false;

    /// <summary>Gets whether the streamable supports seeking. Default: <c>true</c>.</summary>
    bool CanSeek => true;

    /// <summary>
    /// Reads a single byte from the current position.
    /// Default: delegates to <see cref="Read(Span{byte})"/> with a 1-byte buffer.
    /// Override for direct indexing performance.
    /// </summary>
    /// <returns>The byte value, or -1 if at the end.</returns>
    int ReadByte()
    {
        byte b = 0;
        return Read(new Span<byte>(ref b)) == 1 ? b : -1;
    }

    /// <summary>
    /// Sets the position within the data.
    /// Default: computes new position from <see cref="Position"/> and
    /// <see cref="Length"/>, then sets <see cref="Position"/>.
    /// </summary>
    long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentException(SR.Argument_InvalidSeekOrigin)
        };

        ArgumentOutOfRangeException.ThrowIfNegative(newPosition);
        Position = newPosition;
        return newPosition;
    }

    /// <summary>
    /// Copies remaining data to the destination stream.
    /// Default: reads in a loop using <see cref="Read(Span{byte})"/>
    /// with a rented buffer. Override for zero-copy bulk transfer.
    /// </summary>
    void CopyTo(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        byte[] buffer = Buffers.ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            int bytesRead;
            while ((bytesRead = Read(buffer)) > 0)
            {
                destination.Write(buffer.AsSpan(0, bytesRead));
            }
        }
        finally
        {
            Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Writes bytes from the provided buffer. Default: throws <see cref="NotSupportedException"/>.
    /// Override to support writing.
    /// </summary>
    void Write(ReadOnlySpan<byte> buffer) =>
        throw new NotSupportedException(SR.NotSupported_UnwritableStream);

    /// <summary>
    /// Writes a single byte. Default: throws <see cref="NotSupportedException"/>.
    /// Override to support writing.
    /// </summary>
    void WriteByte(byte value) =>
        throw new NotSupportedException(SR.NotSupported_UnwritableStream);

    /// <summary>
    /// Sets the length of the data. Default: throws <see cref="NotSupportedException"/>.
    /// </summary>
    void SetLength(long value) =>
        throw new NotSupportedException(SR.NotSupported_UnwritableStream);
}

