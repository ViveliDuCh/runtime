// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Buffers;
using System.Text;

namespace System.IO.StreamExtensions;

/// <summary>
/// Provides extension methods for creating streams from various data sources.
/// </summary>
public static class StreamExtensions
{

    // Extension members for Stream type
    // To create Stream instances from different data types
    extension(Stream)
    {
        /// <summary>
        /// Creates a stream from a string.
        /// </summary>
        public static Stream FromText(string text, Encoding? encoding = null) => new StringStream(text, encoding ?? Encoding.UTF8);

        /// <summary>
        /// Creates a stream from read-only character memory.
        /// </summary>
        public static Stream FromText(ReadOnlyMemory<char> text, Encoding? encoding = null) => new ReadOnlyMemoryCharStream(text, encoding ?? Encoding.UTF8);

        /// <summary>
        /// Creates a read-only stream from byte memory.
        /// </summary>
        public static Stream FromReadOnlyData(ReadOnlyMemory<byte> data) => new MemoryTStream(data);

        /// <summary>
        /// Creates a read-only stream from a sequence of bytes.
        /// </summary>
        public static Stream ReadOnlyData(ReadOnlySequence<byte> sequence) => new ReadOnlySequenceStream(sequence);

        /// <summary>
        /// Creates a writable stream from byte memory.
        /// </summary>
        public static Stream FromWritableData(Memory<byte> data) => new MemoryTStream(data);
    }
}
