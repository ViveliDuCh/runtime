// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Tests;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace System.IO.StreamExtensions.Tests;

public class MemoryTStreamConformanceTests : StandaloneStreamConformanceTests
{
    protected override bool CanSeek => true;
    protected override bool CanSetLength => false;
    protected override bool NopFlushCompletesSynchronously => true;
    // This stream can't grow beyond initial capacity
    protected override bool CanSetLengthGreaterThanCapacity => false;

    protected override Task<Stream?> CreateReadOnlyStreamCore(byte[]? initialData)
    {
        if (initialData == null || initialData.Length == 0)
        {
            // Create empty memory for null or empty data
            var emptyMemory = Memory<byte>.Empty;
            return Task.FromResult<Stream?>(StreamFactory.StreamFromWritableData(emptyMemory,false));
        }

        // Create read-only stream (writable:  false) for a mutable Memory<byte>
        return Task.FromResult<Stream?>(StreamFactory.StreamFromWritableData(new Memory<byte>(initialData), writable:false));
    }

    protected override Task<Stream?> CreateWriteOnlyStreamCore(byte[]? initialData) => Task.FromResult<Stream?>(null);

    protected override Task<Stream?> CreateReadWriteStreamCore(byte[]? initialData)
    {
        // MemoryTStream wraps a fixed-capacity Memory<byte> buffer where Length == capacity.
        // Unlike MemoryStream, there's no concept of "logical length" separate from capacity.
        // This means MemoryTStream doesn't support the common pattern of creating an empty stream
        // and writing to it to grow it. Many conformance tests rely on this pattern.
        //
        // Returning null here skips tests that require creating an initially-empty writable stream,
        // as those tests fundamentally conflict with MemoryTStream's buffer-wrapping semantics.
        if (initialData == null || initialData.Length == 0)
        {
            return Task.FromResult<Stream?>(null);
        }

        var memory = new Memory<byte>(initialData);
        return Task.FromResult<Stream?>(StreamFactory.StreamFromWritableData(memory));
    }
}
