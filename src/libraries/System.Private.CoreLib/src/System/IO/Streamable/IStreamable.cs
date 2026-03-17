// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO;

/// <summary>
/// Defines a contract for types that can provide stream-like read/write/seek
/// operations over an underlying data source. Implementations should be
/// value types (structs) to allow generic specialization and avoid virtual
/// dispatch overhead when used with <see cref="StreamableStream{TStreamable}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This interface is designed to be implemented by lightweight struct wrappers
/// around memory-based data sources such as <see cref="Memory{T}"/>,
/// <see cref="ReadOnlyMemory{T}"/>, <see cref="string"/>, and
/// <see cref="System.Buffers.ReadOnlySequence{T}"/>.
/// </para>
/// <para>
/// By constraining <typeparamref name="TStreamable"/> as a struct in
/// <see cref="StreamableStream{TStreamable}"/>, the JIT generates specialized
/// code for each value type, enabling inlining and eliminating interface
/// dispatch overhead.
/// </para>
/// </remarks>
internal interface IStreamable
{
    /// <summary>Gets whether the streamable supports reading.</summary>
    bool CanRead { get; }

    /// <summary>Gets whether the streamable supports writing.</summary>
    bool CanWrite { get; }

    /// <summary>Gets whether the streamable supports seeking.</summary>
    bool CanSeek { get; }

    /// <summary>Gets the length of the underlying data in bytes.</summary>
    long Length { get; }

    /// <summary>Gets or sets the current position within the data.</summary>
    long Position { get; set; }

    /// <summary>
    /// Reads bytes from the current position into the provided buffer.
    /// </summary>
    /// <param name="buffer">The buffer to read into.</param>
    /// <returns>The number of bytes read.</returns>
    int Read(Span<byte> buffer);

    /// <summary>
    /// Writes bytes from the provided buffer at the current position.
    /// </summary>
    /// <param name="buffer">The buffer to write from.</param>
    void Write(ReadOnlySpan<byte> buffer);

    /// <summary>
    /// Sets the position within the data.
    /// </summary>
    /// <param name="offset">The byte offset relative to <paramref name="origin"/>.</param>
    /// <param name="origin">The reference point for the offset.</param>
    /// <returns>The new position.</returns>
    long Seek(long offset, SeekOrigin origin);

    /// <summary>
    /// Reads a single byte from the current position.
    /// </summary>
    /// <returns>The byte value, or -1 if at the end.</returns>
    int ReadByte();

    /// <summary>
    /// Writes a single byte at the current position.
    /// </summary>
    /// <param name="value">The byte to write.</param>
    void WriteByte(byte value);

    /// <summary>
    /// Copies the remaining data to the destination stream.
    /// </summary>
    /// <param name="destination">The stream to copy to.</param>
    void CopyTo(Stream destination);

    /// <summary>
    /// Sets the length of the data. May throw if not supported.
    /// </summary>
    /// <param name="value">The new length.</param>
    void SetLength(long value);
}
