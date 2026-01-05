// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Buffers;
using System.Text;

namespace System.IO;

/// <summary>
/// Provides factory methods for creating streams from various data sources.
/// </summary>
public static class StreamFactory
{
    /// <summary>
    /// Creates a stream from a string.
    /// </summary>
    public static Stream StreamFromText(string text, Encoding? encoding = null) => new StringStream(text, encoding ?? Encoding.UTF8);

    /// <summary>
    /// Creates a stream from read-only character memory.
    /// </summary>
    public static Stream StreamFromText(ReadOnlyMemory<char> text, Encoding? encoding = null) => new ReadOnlyMemoryCharStream(text, encoding ?? Encoding.UTF8);

    /// <summary>
    /// Creates a read-only stream from immutable data/byte memory.
    /// </summary>
    public static Stream StreamFromReadOnlyData(ReadOnlyMemory<byte> data) => new MemoryTStream(data);

    /// <summary>
    /// Creates a read-only stream from a sequence of bytes.
    /// </summary>
    public static Stream StreamFromReadOnlyData(ReadOnlySequence<byte> sequence) => new ReadOnlySequenceStream(sequence);

    /// <summary>
    /// Creates a writable stream from a mutable byte memory.
    /// </summary>
    public static Stream StreamFromWritableData(Memory<byte> data) => new MemoryTStream(data);

    /// <summary>
    /// Creates a non/writable stream from mutable data/byte memory.
    /// </summary>
    public static Stream StreamFromWritableData(Memory<byte> data, bool writable) => new MemoryTStream(data, writable);
}
