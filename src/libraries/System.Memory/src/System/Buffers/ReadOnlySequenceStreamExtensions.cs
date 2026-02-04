// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Buffers;

namespace System.IO
{
    /// <summary>
    /// Provides extension methods for creating streams from ReadOnlySequence&lt;byte&gt;
    /// </summary>
    public static class ReadOnlySequenceStreamExtensions
    {
        /// <summary>
        /// Extends the <see cref="Stream"/> type with static factory methods.
        /// </summary>
        extension(Stream)
        {
            /// <summary>
            /// Creates a read-only, seekable stream from a ReadOnlySequence&lt;byte&gt;
            /// </summary>
            /// <param name="sequence">The byte sequence to wrap.</param>
            /// <returns>A read-only stream over the sequence.</returns>
            public static Stream FromReadOnlyData(ReadOnlySequence<byte> sequence) =>
                new ReadOnlySequenceStream(sequence);
        }
    }
}
