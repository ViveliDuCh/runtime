using System;
using System.Collections.Generic;
using System.Text;

namespace System.IO.StreamExtensions;

public static class StreamFactory
{
    public static Stream StreamFromString(string text, Encoding? encoding = null) => new StringStream(text, encoding ?? Encoding.UTF8);
    public static Stream StreamFromText(ReadOnlyMemory<char> text, Encoding? encoding = null) => new ReadOnlyMemoryCharStream(text, encoding ?? Encoding.UTF8);
    public static Stream StreamFromReadOnlySequence(ReadOnlySequence<byte> sequence) => new ReadOnlySequenceStream(sequence);
    public static Stream StreamFromData(Memory<byte> data) => new MemoryTStream(data);
    public static Stream StreamFromROData(ReadOnlyMemory<byte> data) => new ReadOnlyMemoryStream(data);
}
