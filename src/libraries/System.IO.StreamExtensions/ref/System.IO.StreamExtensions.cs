// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// ------------------------------------------------------------------------------
// Changes to this file must follow the https://aka.ms/api-review process.
// ------------------------------------------------------------------------------

namespace System.IO
{
    public static partial class StreamFactory
    {
        public static System.IO.Stream StreamFromReadOnlyData(System.Buffers.ReadOnlySequence<byte> sequence) { throw null; }
        public static System.IO.Stream StreamFromReadOnlyData(System.ReadOnlyMemory<byte> data) { throw null; }
        public static System.IO.Stream StreamFromText(System.ReadOnlyMemory<char> text, System.Text.Encoding? encoding = null) { throw null; }
        public static System.IO.Stream StreamFromText(string text, System.Text.Encoding? encoding = null) { throw null; }
        public static System.IO.Stream StreamFromWritableData(System.Memory<byte> data) { throw null; }
        public static System.IO.Stream StreamFromWritableData(System.Memory<byte> data, bool writable) { throw null; }
    }
}
