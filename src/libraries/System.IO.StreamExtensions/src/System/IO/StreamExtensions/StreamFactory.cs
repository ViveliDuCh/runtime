using System;
using System.Collections.Generic;
using System.Text;

namespace System.IO.StreamExtensions;

public static class StreamFactory
{
    public static Stream StreamFromString(string text, Encoding? encoding = null) => new StringStream(text, encoding ?? Encoding.UTF8);
    public static Stream StreamFromReadOnlySequence(ReadOnlySequence<byte> sequence) => new ReadOnlySequenceStream(sequence);

    public static Stream StreamFromStringCopy(string text, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        return new MemoryStream(encoding.GetBytes(text));
    }


    public static Stream StreamFromReadOnlySequenceCopy(ReadOnlySequence<byte> sequence)
    {
        var ms = new MemoryStream();
        foreach (var segment in sequence)
        {
            ms.Write(segment.Span);
        }
        ms.Position = 0;
        return ms; // Copies all data into underlying buffer
    }
}
