// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace System.IO;

/// <summary>
/// Provides factory methods for creating streams from various data sources.
/// </summary>
/// <remarks>
/// This type is not thread-safe. The streams created by these methods are also not thread-safe.
/// Synchronize access if a stream is used concurrently.
/// </remarks>
public static class StreamFactory
{
    /// <summary>
    /// Creates a read-only stream from a string.
    /// </summary>
    /// <param name="text">The string to read from.</param>
    /// <param name="encoding">The encoding to use when converting the string to bytes. If <see langword="null"/>, UTF-8 encoding is used.</param>
    /// <returns>A read-only <see cref="Stream"/> that encodes the string on-the-fly.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The stream supports seeking but is limited to positions within the range of <see cref="int.MaxValue"/>.
    /// </remarks>
    public static Stream StreamFromText(string text, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new StringStream(text, encoding ?? Encoding.UTF8);
    }

    /// <summary>
    /// Creates a read-only stream from read-only character memory.
    /// </summary>
    /// <param name="text">The character memory to read from.</param>
    /// <param name="encoding">The encoding to use when converting the characters to bytes. If <see langword="null"/>, UTF-8 encoding is used.</param>
    /// <returns>A read-only <see cref="Stream"/> that encodes the characters on-the-fly.</returns>
    /// <remarks>
    /// The stream supports seeking but is limited to positions within the range of <see cref="int.MaxValue"/>.
    /// </remarks>
    public static Stream StreamFromText(ReadOnlyMemory<char> text, Encoding? encoding = null) =>
        new ReadOnlyMemoryCharStream(text, encoding ?? Encoding.UTF8);

    /// <summary>
    /// Creates a read-only stream from immutable byte memory.
    /// </summary>
    /// <param name="data">The byte memory to wrap.</param>
    /// <returns>A read-only <see cref="Stream"/> over the byte memory.</returns>
    /// <remarks>
    /// The stream supports seeking but is limited to positions within the range of <see cref="int.MaxValue"/>.
    /// </remarks>
    public static Stream StreamFromReadOnlyData(ReadOnlyMemory<byte> data)
    {
        if (MemoryMarshal.TryGetArray(data, out ArraySegment<byte> dataBacking))
        {
            // Fast path:  ReadOnlyMemory<byte> wraps an array
            return new MemoryStream(dataBacking.Array!, dataBacking.Offset, dataBacking.Count, writable: false);
        }

        return new MemoryTStream(data);
    }

    /// <summary>
    /// Creates a read-only stream from a sequence of bytes.
    /// </summary>
    /// <param name="sequence">The byte sequence to wrap.</param>
    /// <returns>A read-only <see cref="Stream"/> over the byte sequence.</returns>
    public static Stream StreamFromReadOnlyData(ReadOnlySequence<byte> sequence) => new ReadOnlySequenceStream(sequence);

    /// <summary>
    /// Creates a writable stream from mutable byte memory.
    /// </summary>
    /// <param name="data">The byte memory to wrap.</param>
    /// <returns>A writable <see cref="Stream"/> over the byte memory.</returns>
    /// <remarks>
    /// The stream supports seeking but is limited to positions within the range of <see cref="int.MaxValue"/>.
    /// The stream cannot expand beyond the initial memory capacity.
    /// </remarks>
    public static Stream StreamFromWritableData(Memory<byte> data)
    {
        if (MemoryMarshal.TryGetArray(data, out ArraySegment<byte> dataBacking))
        {
            // Fast path:  Memory<byte> wraps an array
            return new MemoryStream(dataBacking.Array!, dataBacking.Offset, dataBacking.Count);
        }

        return new MemoryTStream(data);
    }

    /// <summary>
    /// Creates a stream from mutable byte memory with configurable write support.
    /// </summary>
    /// <param name="data">The byte memory to wrap.</param>
    /// <param name="writable">Whether the stream supports writing.</param>
    /// <returns>A <see cref="Stream"/> over the byte memory.</returns>
    /// <remarks>
    /// The stream supports seeking but is limited to positions within the range of <see cref="int.MaxValue"/>.
    /// The stream cannot expand beyond the initial memory capacity.
    /// </remarks>
    public static Stream StreamFromWritableData(Memory<byte> data, bool writable)
    {
        if (MemoryMarshal.TryGetArray(data, out ArraySegment<byte> dataBacking))
        {
            // Fast path:  Memory<byte> wraps an array
            return new MemoryStream(dataBacking.Array!, dataBacking.Offset, dataBacking.Count, writable);
        }

        return new MemoryTStream(data, writable);
    }
}
