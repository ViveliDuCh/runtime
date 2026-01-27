// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace System.IO.StreamExtensions.Tests;

/// <summary>
/// Additional specific tests for ReadOnlyTextStream with string beyond conformance tests.
/// </summary>
public class ReadOnlyTextStreamTests_String
{
    [Fact]
    public async Task SeekAndRead_WithMultiByteCharacters()
    {
        string input = "AB你好CD";
        var stream = Stream.FromText(input, Encoding.UTF8);

        byte[] expectedBytes = Encoding.UTF8.GetBytes(input);

        stream.Position = 2;
        byte[] buffer = new byte[3];
        int bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal(3, bytesRead);
        Assert.Equal(expectedBytes.AsSpan(2, 3).ToArray(), buffer);

        stream.Position = 0;
        buffer = new byte[2];
        bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal(2, bytesRead);
        Assert.Equal(expectedBytes.AsSpan(0, 2).ToArray(), buffer);
    }

    [Fact]
    public async Task PositionUpdatesCorrectlyAfterPartialReads()
    {
        string input = new string('X', 1000);
        var stream = Stream.FromText(input, Encoding.UTF8);

        Assert.Equal(0, stream.Position);

        byte[] buffer = new byte[100];
        await stream.ReadAsync(buffer);
        Assert.Equal(100, stream.Position);

        await stream.ReadAsync(buffer.AsMemory(0, 50));
        Assert.Equal(150, stream.Position);

        stream.Position = 75;
        Assert.Equal(75, stream.Position);

        await stream.ReadAsync(buffer);
        Assert.Equal(175, stream.Position);
    }

    [Fact]
    public async Task SeekBeyondInternalBufferBoundary()
    {
        string input = new string('A', 5000);
        var stream = Stream.FromText(input, Encoding.UTF8);

        stream.Position = 4500;
        Assert.Equal(4500, stream.Position);

        byte[] buffer = new byte[100];
        int bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal(100, bytesRead);
        Assert.All(buffer, b => Assert.Equal((byte)'A', b));
    }

    [Theory]
    [InlineData("Hello, World! ")]
    [InlineData("Unicode: 你好世界 🌍")]
    [InlineData("Multi\nLine\r\nText")]
    public async Task ReadsCorrectBytesForDifferentStrings(string input)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(input);
        var stream = Stream.FromText(input, Encoding.UTF8);

        byte[] actualBytes = new byte[expectedBytes.Length + 100];
        int totalRead = 0;
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(actualBytes.AsMemory(totalRead))) > 0)
        {
            totalRead += bytesRead;
        }

        Assert.Equal(expectedBytes.Length, totalRead);
        Assert.Equal(expectedBytes, actualBytes.AsSpan(0, totalRead).ToArray());
    }

    [Theory]
    [InlineData("ASCII text")]
    [InlineData("Ñoño español")]
    public async Task WorksWithDifferentEncodings(string input)
    {
        var encodings = new[] { Encoding.UTF8, Encoding.Unicode, Encoding.UTF32 };

        foreach (var encoding in encodings)
        {
            byte[] expectedBytes = encoding.GetBytes(input);
            var stream = Stream.FromText(input, encoding);

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
    public void ThrowsOnNullString()
    {
        Assert.Throws<ArgumentNullException>(() => Stream.FromText((string)null!));
    }

    [Fact]
    public void CanReadPropertyReturnsTrue()
    {
        var stream = Stream.FromText("test");
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void CanSeekPropertyReturnsTrue()
    {
        var stream = Stream.FromText("test");
        Assert.True(stream.CanSeek);
    }

    [Fact]
    public void CanWritePropertyReturnsFalse()
    {
        var stream = Stream.FromText("test");
        Assert.False(stream.CanWrite);
    }

    [Fact]
    public void LengthReturnsCorrectValue()
    {
        var testString = "test";
        var stream = Stream.FromText(testString);
        var expectedLength = Encoding.UTF8.GetByteCount(testString);
        Assert.Equal(expectedLength, stream.Length);
    }

    [Fact]
    public void WriteThrowsNotSupportedException()
    {
        var stream = Stream.FromText("test");
        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }

    [Fact]
    public void SetLengthThrowsNotSupportedException()
    {
        var stream = Stream.FromText("test");
        Assert.Throws<NotSupportedException>(() => stream.SetLength(100));
    }

    [Fact]
    public async Task HandlesChunkedReading()
    {
        string largeString = new string('A', 10000);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(largeString);
        var stream = Stream.FromText(largeString, Encoding.UTF8);

        byte[] actualBytes = new byte[expectedBytes.Length];
        int totalRead = 0;
        int chunkSize = 512;
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

    [Fact]
    public async Task ReadsWithExactBufferSizeMatch()
    {
        string input = new string('A', 4096);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(input);
        var stream = Stream.FromText(input, Encoding.UTF8);

        byte[] buffer = new byte[4096];
        int bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal(4096, bytesRead);
        Assert.Equal(expectedBytes, buffer);
    }

    [Fact]
    public async Task MultipleReadsEventuallyReturnZero()
    {
        var stream = Stream.FromText("small", Encoding.UTF8);
        byte[] buffer = new byte[100];

        int totalRead = 0;
        int bytesRead;
        int readCount = 0;

        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead))) > 0 && readCount < 10)
        {
            totalRead += bytesRead;
            readCount++;
        }

        int finalRead = await stream.ReadAsync(buffer.AsMemory(0));

        Assert.Equal(5, totalRead);
        Assert.Equal(0, finalRead);
    }

    [Fact]
    public async Task SequentialReadAsync_PositionUpdatesAfterEachRead()
    {
        string input = "ABCDEFGHIJKLMNOP";
        var stream = Stream.FromText(input, Encoding.UTF8);
        byte[] buffer = new byte[4];

        Assert.Equal(0, stream.Position);

        await stream.ReadAsync(buffer);
        Assert.Equal(4, stream.Position);

        await stream.ReadAsync(buffer);
        Assert.Equal(8, stream.Position);

        await stream.ReadAsync(buffer);
        Assert.Equal(12, stream.Position);

        await stream.ReadAsync(buffer);
        Assert.Equal(16, stream.Position);

        int eofRead = await stream.ReadAsync(buffer);
        Assert.Equal(0, eofRead);
        Assert.Equal(16, stream.Position);
    }

    [Fact]
    public async Task SequentialReadAsync_WithSmallChunks_ReadsEntireStream()
    {
        string input = new string('A', 5000);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(input);
        var stream = Stream.FromText(input, Encoding.UTF8);

        byte[] actualBytes = new byte[expectedBytes.Length];
        int totalBytesRead = 0;
        int chunkSize = 128;

        while (totalBytesRead < expectedBytes.Length)
        {
            int toRead = Math.Min(chunkSize, expectedBytes.Length - totalBytesRead);
            int bytesRead = await stream.ReadAsync(actualBytes.AsMemory(totalBytesRead, toRead));

            if (bytesRead == 0) break;

            totalBytesRead += bytesRead;
        }

        Assert.Equal(expectedBytes.Length, totalBytesRead);
        Assert.Equal(expectedBytes, actualBytes);
        Assert.Equal(expectedBytes.Length, stream.Position);
    }
}
