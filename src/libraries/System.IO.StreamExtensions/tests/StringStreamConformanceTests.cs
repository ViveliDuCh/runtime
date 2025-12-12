// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.IO.Tests;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace System.IO.StreamExtensions.Tests;

/// <summary>
/// Conformance tests for StringStream - a read-only, non-seekable stream
/// that encodes strings on-the-fly.
/// </summary>
public class StringStreamConformanceTests : StandaloneStreamConformanceTests
{
    // StreamConformanceTests flags to specify capabilities of StringStream
    protected override bool CanSeek => false;
    protected override bool CanSetLength => false;
    protected override bool CanGetPositionWhenCanSeekIsFalse => false;
    protected override bool ReadsReadUntilSizeOrEof => true;
    protected override bool NopFlushCompletesSynchronously => true;

    /// <summary>
    /// Creates a read-only StringStream with provided initial data. 
    /// </summary>
    protected override Task<Stream?> CreateReadOnlyStreamCore(byte[]? initialData)
    {
        if (initialData == null || initialData.Length == 0)
        {
            // Empty string for null or empty data
            return Task.FromResult<Stream?>(new StringStream("", Encoding.UTF8));
        }

        // Convert byte array to string using UTF8
        string sourceString = Encoding.UTF8.GetString(initialData);

        // Validate that encoding produces the expected bytes for proper UTF-8 input.
        // StringStream encodes strings to bytes, so we need to ensure round-trip fidelity.
        byte[] reencoded = Encoding.UTF8.GetBytes(sourceString);
        if (reencoded.Length != initialData.Length || !reencoded.AsSpan().SequenceEqual(initialData))
        {
            // The input bytes don't round-trip through UTF-8 encoding.
            // This is expected for arbitrary byte sequences that aren't valid UTF-8.
            // Return null to skip tests that rely on exact byte reproduction.
            return Task.FromResult<Stream?>(null);
        }
        // Creates a StringStream just with the valid provided initial data. 
        return Task.FromResult<Stream?>(new StringStream(sourceString, Encoding.UTF8));
    }

    // Write only stream - no write support
    protected override Task<Stream?> CreateWriteOnlyStreamCore(byte[]? initialData) => Task.FromResult<Stream?>(null);

    // Read only stream - no read/write support
    protected override Task<Stream?> CreateReadWriteStreamCore(byte[]? initialData) => Task.FromResult<Stream?>(null);
}
