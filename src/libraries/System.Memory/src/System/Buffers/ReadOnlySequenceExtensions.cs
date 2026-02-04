// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.IO;

namespace System.Buffers
{
    /// <summary>
    /// Provides extension method for creating a stream from <see cref="ReadOnlySequence{T}"/>.
    /// </summary>
    public static class ReadOnlySequenceExtensions
    {
        /// <summary>
        /// Creates a read-only stream from a sequence of bytes.
        /// </summary>
        public static Stream AsStream(this ReadOnlySequence<byte> sequence)
        {
            return new ReadOnlySequenceStream(sequence);
        }
    }
}
