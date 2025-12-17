using System.ComponentModel;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace System.IO.StreamExtensions;

public static class StreamExtensions
{

    // Extension members for Stream type
    // To create Stream instances from different data types
    extension(Stream)
    {
        public static Stream StreamFromString(string text, Encoding? encoding = null) => new StringStream(text, encoding ?? Encoding.UTF8);
        public static Stream StreamFromText(ReadOnlyMemory<char> text, Encoding? encoding = null) => new ReadOnlyMemoryCharStream(text, encoding ?? Encoding.UTF8);
        public static Stream StreamFromReadOnlySequence(ReadOnlySequence<byte> sequence) => new ReadOnlySequenceStream(sequence);
        public static Stream StreamFromData(Memory<byte> data) => new MemoryTStream(data);
        public static Stream StreamFromROData(ReadOnlyMemory<byte> data) => new ReadOnlyMemoryStream(data);
    }
}
