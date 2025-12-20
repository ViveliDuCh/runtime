// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.IO.Tests;
using System.Text;
using System.Threading.Tasks;

namespace System.IO.StreamExtensions.Tests;

/// <summary>
/// Conformance tests for ReadOnlyMemory{char} - a read-only, non-seekable stream
/// that encodes text on-the-fly.
/// </summary>
public class ROMCharStreamConformanceTests : StandaloneStreamConformanceTests
{
    // StreamConformanceTests flags to specify capabilities of ReadOnlyMemoryCharStream
    protected override bool CanSeek => true; // these have deafult values, just for clarity
    protected override bool CanSetLength => false; // Immutalble stream
    protected override bool NopFlushCompletesSynchronously => true;

    /// <summary>
    /// Creates a read-only ReadOnlyMemoryCharStream with provided initial data. 
    /// </summary>
    protected override Task<Stream?> CreateReadOnlyStreamCore(byte[]? initialData)
    {
        if (initialData == null || initialData.Length == 0)
        {
            // Empty string for null or empty data
            return Task.FromResult<Stream?>(new ReadOnlyMemoryCharStream(ReadOnlyMemory<char>.Empty, Encoding.UTF8));
        }

        // Convert byte array to string using UTF8
        string sourceString = Encoding.UTF8.GetString(initialData);

        // Validate that encoding: ensure round-trip fidelity.
        byte[] reencoded = Encoding.UTF8.GetBytes(sourceString);
        if (reencoded.Length != initialData.Length || !reencoded.AsSpan().SequenceEqual(initialData))
        {
            // The input bytes don't round-trip through UTF-8 encoding.
            return Task.FromResult<Stream?>(null);
        }

        // Creates a ReadOnlyMemoryCharStream just with the valid provided initial data. 
        return Task.FromResult<Stream?>(new ReadOnlyMemoryCharStream(sourceString.AsMemory(), Encoding.UTF8));
    }

    // Write only stream - no write support
    protected override Task<Stream?> CreateWriteOnlyStreamCore(byte[]? initialData) => Task.FromResult<Stream?>(null);

    // Read only stream - no read/write support
    protected override Task<Stream?> CreateReadWriteStreamCore(byte[]? initialData) => Task.FromResult<Stream?>(null);
}
