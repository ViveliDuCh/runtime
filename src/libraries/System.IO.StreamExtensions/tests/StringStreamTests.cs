// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace System.IO.StreamExtensions.Tests;

/// <summary>
/// Additional specific tests for StringStream beyond conformance tests.
/// </summary>
public class StringStreamTests
{
    // Different inputs, same encoding
    [Theory]
    [InlineData("Hello, World! ")]
    [InlineData("Unicode: 你好世界 🌍")]
    [InlineData("Multi\nLine\r\nText")]
    public async Task StringStream_ReadsCorrectBytesForDifferentStrings(string input)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(input);
        var stream = new StringStream(input, Encoding.UTF8);

        byte[] actualBytes = new byte[expectedBytes.Length + 100]; // Extra space
        int totalRead = 0;
        int bytesRead;
        // Since ReadAsync() hasn't been implemented yet, falls back to Stream's basic synchronous Read that's wrapped in a Task.
        while ((bytesRead = await stream.ReadAsync(actualBytes.AsMemory(totalRead))) > 0)
        {
            totalRead += bytesRead;
        }

        Assert.Equal(expectedBytes.Length, totalRead);
        Assert.Equal(expectedBytes, actualBytes.AsSpan(0, totalRead).ToArray());
    }

    // Same input, different encodings
    [Theory]
    [InlineData("ASCII text")]
    [InlineData("Ñoño español")]
    public async Task StringStream_WorksWithDifferentEncodings(string input)
    {
        // Test with different encodings
        var encodings = new[] { Encoding.UTF8, Encoding.Unicode, Encoding.UTF32 };

        foreach (var encoding in encodings)
        {
            byte[] expectedBytes = encoding.GetBytes(input);
            var stream = new StringStream(input, encoding);

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
    public void StringStream_ThrowsOnNullString()
    {
        Assert.Throws<ArgumentNullException>(() => new StringStream(null!));
    }

    [Fact]
    public void StringStream_CanReadPropertyReturnsTrue()
    {
        var stream = new StringStream("test");
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void StringStream_CanSeekPropertyReturnsFalse()
    {
        var stream = new StringStream("test");
        Assert.False(stream.CanSeek);
    }

    [Fact]
    public void StringStream_CanWritePropertyReturnsFalse()
    {
        var stream = new StringStream("test");
        Assert.False(stream.CanWrite);
    }

    [Fact]
    public void StringStream_LengthThrowsNotSupportedException()
    {
        var stream = new StringStream("test");
        Assert.Throws<NotSupportedException>(() => stream.Length);
    }

    [Fact]
    public void StringStream_PositionGetThrowsNotSupportedException()
    {
        var stream = new StringStream("test");
        Assert.Throws<NotSupportedException>(() => stream.Position);
    }

    [Fact]
    public void StringStream_SeekThrowsNotSupportedException()
    {
        var stream = new StringStream("test");
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    [Fact]
    public void StringStream_WriteThrowsNotSupportedException()
    {
        var stream = new StringStream("test");
        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    [Fact]
    public void StringStream_SetLengthThrowsNotSupportedException()
    {
        var stream = new StringStream("test");
        Assert.Throws<NotSupportedException>(() => stream.SetLength(100));
    }

    // Edge case: Test chunked reading (important for 4KB buffer design)
    [Fact]
    public async Task StringStream_HandlesChunkedReading()
    {
        // Create a string larger than internal buffer(4KB)
        string largeString = new string('A', 10000); // 10KB of 'A's
        byte[] expectedBytes = Encoding.UTF8.GetBytes(largeString);
        var stream = new StringStream(largeString, Encoding.UTF8);

        byte[] actualBytes = new byte[expectedBytes.Length];
        int totalRead = 0;
        int chunkSize = 512; // Read 512 bytes at a time
        // Read in  chunks smaller than internal buffer size
        while (totalRead < expectedBytes.Length)
        {
            int bytesRead = await stream.ReadAsync(
                actualBytes.AsMemory(totalRead, Math.Min(chunkSize, expectedBytes.Length - totalRead))
            );

            if (bytesRead == 0) break;
            totalRead += bytesRead;
        }

        Assert.Equal(expectedBytes.Length, totalRead);
        Assert.Equal(expectedBytes, actualBytes);
    }

    // Edge case: Test read behavior with exact buffer size match
    [Fact]
    public async Task StringStream_ReadsWithExactBufferSizeMatch()
    {
        // String that encodes to exactly 4096 bytes(internal buffer size)
        string input = new string('A', 4096);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(input);
        var stream = new StringStream(input, Encoding.UTF8);

        byte[] buffer = new byte[4096];
        int bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal(4096, bytesRead);
        Assert.Equal(expectedBytes, buffer);
    }

    [Fact]
    public async Task StringStream_MultipleReadsEventuallyReturnZero()
    {
        var stream = new StringStream("small", Encoding.UTF8);
        byte[] buffer = new byte[100];

        int totalRead = 0;
        int bytesRead;
        int readCount = 0;

        // Read until EOF or 10 reads
        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead))) > 0 && readCount < 10)
        {
            totalRead += bytesRead;
            readCount++;
        }

        // Additional read should return 0
        int finalRead = await stream.ReadAsync(buffer.AsMemory(0));

        Assert.Equal(5, totalRead); // "small" = 5 bytes in UTF8
        Assert.Equal(0, finalRead);
    }
}
