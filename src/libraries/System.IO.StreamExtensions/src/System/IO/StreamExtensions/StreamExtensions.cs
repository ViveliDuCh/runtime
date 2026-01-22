// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Buffers;
using System.Text;

namespace System.IO.StreamExtensions;

/// <summary>
/// Provides extension method for creating a stream from ReadOnlySequence<byte>.
/// </summary>
public static class StreamExtensions
{
    extension(Stream)
    {
        /// <summary>
        /// Creates a read-only stream from a sequence of bytes.
        /// </summary>
        public static Stream ReadOnlyData(ReadOnlySequence<byte> sequence) => new ReadOnlySequenceStream(sequence);
    }
}
