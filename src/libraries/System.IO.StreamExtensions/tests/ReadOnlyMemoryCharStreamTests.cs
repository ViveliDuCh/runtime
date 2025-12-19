// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license. 
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace System.IO.StreamExtensions.Tests;

/// <summary>
/// Additional specific tests for ReadOnlyMemoryCharStream beyond conformance tests.
/// </summary>
public class ReadOnlyMemoryCharStreamTests
{
    [Fact]
    public void Constructor_DefaultEncoding_UsesUTF8()
    {
        var chars = "test".AsMemory();
        var stream = new ReadOnlyMemoryCharStream(chars);

        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
        Assert.False(stream.CanWrite);
    }

    [Fact]
    public void Constructor_ExplicitEncoding_UsesSpecifiedEncoding()
    {
        var chars = "test".AsMemory();
        var stream = new ReadOnlyMemoryCharStream(chars, Encoding.UTF32);

        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Constructor_NullEncoding_ThrowsArgumentNullException()
    {
        var chars = "test".AsMemory();
        Assert.Throws<ArgumentNullException>(() => new ReadOnlyMemoryCharStream(chars, null!));
    }

    [Fact]
    public void Constructor_EmptyMemory_CreatesValidStream()
    {
        var emptyMemory = ReadOnlyMemory<char>.Empty;
        var stream = new ReadOnlyMemoryCharStream(emptyMemory);

        Assert.True(stream.CanRead);

        byte[] buffer = new byte[10];
        int bytesRead = stream.Read(buffer, 0, 10);
        Assert.Equal(0, bytesRead);  // EOF immediately
    }

    [Theory]
    [InlineData("ASCII text")]
    [InlineData("Ñoño español")]
    [InlineData("Emoji: 😀🎉")]
    public async Task ReadOnlyMemoryCharStream_WorksWithDifferentEncodings(string input)
    {
        // Test with different encodings
        var encodings = new[] { Encoding.UTF8, Encoding.Unicode, Encoding.UTF32 };

        foreach (var encoding in encodings)
        {
            byte[] expectedBytes = encoding.GetBytes(input);
            var chars = input.AsMemory();
            var stream = new ReadOnlyMemoryCharStream(chars, encoding);

            byte[] actualBytes = new byte[expectedBytes.Length * 2];
            int totalRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(actualBytes.AsMemory(totalRead))) > 0)
            {
                totalRead += bytesRead;
            }

            Assert.Equal(expectedBytes.Length, totalRead);
            Assert.Equal(expectedBytes, actualBytes.AsSpan(0, totalRead).ToArray());
        }
    }

    [Fact]
    public async Task ReadOnlyMemoryCharStream_WorksWithMemorySlice()
    {
        // Create a larger string and slice it
        string largeString = "0123456789ABCDEFGHIJ";
        var fullMemory = largeString.AsMemory();
        var slice = fullMemory.Slice(5, 10);  // "56789ABCDE"

        byte[] expectedBytes = Encoding.UTF8.GetBytes("56789ABCDE");
        var stream = new ReadOnlyMemoryCharStream(slice, Encoding.UTF8);

        byte[] actualBytes = new byte[expectedBytes.Length + 10];
        int totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(actualBytes.AsMemory(totalRead))) > 0)
        {
            totalRead += bytesRead;
        }

        Assert.Equal(expectedBytes.Length, totalRead);
        Assert.Equal(expectedBytes, actualBytes.AsSpan(0, totalRead).ToArray());
    }

    // char array backed ReadOnlyMemory. 
    [Fact]
    public async Task ReadOnlyMemoryCharStream_WorksWithCharArray()
    {
        // Create ReadOnlyMemory from char array
        char[] charArray = { 'H', 'e', 'l', 'l', 'o' };
        var memory = new ReadOnlyMemory<char>(charArray);

        byte[] expectedBytes = Encoding.UTF8.GetBytes("Hello");
        var stream = new ReadOnlyMemoryCharStream(memory, Encoding.UTF8);

        byte[] actualBytes = new byte[expectedBytes.Length + 10];
        int totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(actualBytes.AsMemory(totalRead))) > 0)
        {
            totalRead += bytesRead;
        }

        Assert.Equal(expectedBytes.Length, totalRead);
        Assert.Equal(expectedBytes, actualBytes.AsSpan(0, totalRead).ToArray());
    }

    [Fact]
    public async Task ReadOnlyMemoryCharStream_MultipleSlicesIndependent()
    {
        // Arrange
        string source = "ABCDEFGHIJKLMNOP";
        var slice1 = source.AsMemory(0, 5);   // "ABCDE"
        var slice2 = source.AsMemory(5, 5);   // "FGHIJ"
        var slice3 = source.AsMemory(10, 6);  // "KLMNOP"

        var stream1 = new ReadOnlyMemoryCharStream(slice1, Encoding.UTF8);
        var stream2 = new ReadOnlyMemoryCharStream(slice2, Encoding.UTF8);
        var stream3 = new ReadOnlyMemoryCharStream(slice3, Encoding.UTF8);

        // Act
        byte[] result1 = new byte[10];
        byte[] result2 = new byte[10];
        byte[] result3 = new byte[10];

        int read1 = await stream1.ReadAsync(result1);
        int read2 = await stream2.ReadAsync(result2);
        int read3 = await stream3.ReadAsync(result3);

        // Assert
        Assert.Equal("ABCDE", Encoding.UTF8.GetString(result1, 0, read1));
        Assert.Equal("FGHIJ", Encoding.UTF8.GetString(result2, 0, read2));
        Assert.Equal("KLMNOP", Encoding.UTF8.GetString(result3, 0, read3));
    }

    [Fact]
    public async Task ReadOnlyMemoryCharStream_HandlesSurrogatePairs()
    {
        // String with multiple emoji (surrogate pairs)
        string input = "😀😁😂🤣😃😄";
        var chars = input.AsMemory();
        byte[] expectedBytes = Encoding.UTF8.GetBytes(input);
        var stream = new ReadOnlyMemoryCharStream(chars, Encoding.UTF8);

        byte[] actualBytes = new byte[expectedBytes.Length];
        int totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(actualBytes.AsMemory(totalRead))) > 0)
        {
            totalRead += bytesRead;
        }

        Assert.Equal(expectedBytes.Length, totalRead);
        Assert.Equal(expectedBytes, actualBytes.AsSpan(0, totalRead).ToArray());
    }

    [Fact]
    public async Task ReadOnlyMemoryCharStream_MultiByteCharactersAcrossChunkBoundary()
    {
        string input = new string('A', 1023) + "你";
        var chars = input.AsMemory();
        byte[] expectedBytes = Encoding.UTF8.GetBytes(input);
        var stream = new ReadOnlyMemoryCharStream(chars, Encoding.UTF8);

        byte[] actualBytes = new byte[expectedBytes.Length];
        int totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(actualBytes.AsMemory(totalRead))) > 0)
        {
            totalRead += bytesRead;
        }

        Assert.Equal(expectedBytes.Length, totalRead);
        Assert.Equal(expectedBytes, actualBytes.AsSpan(0, totalRead).ToArray());
    }

    // Conformance tests already cover a lot of unsupported behaviors
    // with ValidateMisuseExceptionsAsync()
    [Fact]
    public void ReadOnlyMemoryCharStream_LengthSupported()
    {
        var chars = "test".AsMemory();
        var stream = new ReadOnlyMemoryCharStream(chars);

        Assert.Equal(chars.Length, stream.Length);
    }

    [Fact]
    public void ReadOnlyMemoryCharStream_PositionGetSupported()
    {
        var chars = "test".AsMemory();
        var stream = new ReadOnlyMemoryCharStream(chars);

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void ReadOnlyMemoryCharStream_PositionSetSupported()
    {
        var chars = "test".AsMemory();
        var stream = new ReadOnlyMemoryCharStream(chars);
        stream.Position = 0;
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void ReadOnlyMemoryCharStream_SeekSupported()
    {
        var chars = "test".AsMemory();
        var stream = new ReadOnlyMemoryCharStream(chars);

        Assert.Equal(0, stream.Seek(0, SeekOrigin.Begin));
    }

    [Fact]
    public void ReadOnlyMemoryCharStream_WriteThrowsNotSupportedException()
    {
        var chars = "test".AsMemory();
        var stream = new ReadOnlyMemoryCharStream(chars);

        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    [Fact]
    public void ReadOnlyMemoryCharStream_SetLengthThrowsNotSupportedException()
    {
        var chars = "test".AsMemory();
        var stream = new ReadOnlyMemoryCharStream(chars);

        Assert.Throws<NotSupportedException>(() => stream.SetLength(100));
    }

    // Conformance tests already cover Dispose behavior with ValidateDisposeExceptionAsync()
    [Fact]
    public void ReadOnlyMemoryCharStream_CanReadFalseAfterDispose()
    {
        var chars = "test".AsMemory();
        var stream = new ReadOnlyMemoryCharStream(chars);

        stream.Dispose();

        Assert.False(stream.CanRead);
    }

    [Fact]
    public void ReadOnlyMemoryCharStream_ReadAfterDispose_ThrowsObjectDisposedException()
    {
        var chars = "test".AsMemory();
        var stream = new ReadOnlyMemoryCharStream(chars);
        stream.Dispose();

        byte[] buffer = new byte[10];
        Assert.Throws<ObjectDisposedException>(() => stream.Read(buffer, 0, 10));
    }

    [Fact]
    public void ReadOnlyMemoryCharStream_MultipleDispose_DoesNotThrow()
    {
        var chars = "test".AsMemory();
        var stream = new ReadOnlyMemoryCharStream(chars);

        stream.Dispose();
        stream.Dispose();  // Should not throw
        stream.Dispose();  // Should not throw
    }

    // Unique
    [Theory]
    [InlineData("Hello")]
    [InlineData("Unicode:  你好")]
    [InlineData("Emoji: 😀")] // Cross-stream comparison with StringStream
    public async Task ReadOnlyMemoryCharStream_ProducesSameOutputAsStringStream(string input)
    {
        var memoryStream = new ReadOnlyMemoryCharStream(input.AsMemory(), Encoding.UTF8);
        var stringStream = new StringStream(input, Encoding.UTF8);

        byte[] memoryResult = new byte[1000];
        byte[] stringResult = new byte[1000];

        int memoryBytesRead = await memoryStream.ReadAsync(memoryResult);
        int stringBytesRead = await stringStream.ReadAsync(stringResult);

        Assert.Equal(stringBytesRead, memoryBytesRead);
        Assert.Equal(
            stringResult.AsSpan(0, stringBytesRead).ToArray(),
            memoryResult.AsSpan(0, memoryBytesRead).ToArray()
        );
    }
}
