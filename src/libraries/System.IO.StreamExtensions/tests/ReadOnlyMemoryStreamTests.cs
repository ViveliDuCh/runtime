// Licensed to the .NET Foundation under one or more agreements. 
// The .NET Foundation licenses this file to you under the MIT license. 
using Xunit;
using System.Threading.Tasks;

namespace System.IO.StreamExtensions.Tests;

/// <summary>
/// Additional specific tests for ReadOnlyMemoryStream beyond conformance tests.
/// </summary>
public class ReadOnlyMemoryStreamTests
{
    [Fact]
    public void Constructor_DefaultParameters_CreatesPubliclyVisibleStream()
    {
        var buffer = new byte[100];
        var stream = new MemoryTStream(new ReadOnlyMemory<byte>(buffer));

        Assert.True(stream.CanRead);
        Assert.False(stream.CanWrite);
        Assert.True(stream.CanSeek);
        Assert.Equal(100, stream.Length);
        Assert.Equal(0, stream.Position);

        // Should be publicly visible by default
        Assert.True(stream.TryGetBuffer(out ReadOnlyMemory<byte> bufferMemory));
    }

    [Theory]
    [InlineData(true)]   // Publicly visible
    [InlineData(false)]  // Hidden
    public void Constructor_PubliclyVisibleParameter_ControlsBufferExposure(bool publiclyVisible)
    {
        var buffer = new byte[100];
        var stream = new MemoryTStream(new ReadOnlyMemory<byte>(buffer), publiclyVisible);

        Assert.Equal(publiclyVisible, stream.TryGetBuffer(out ReadOnlyMemory<byte> bufferMemory));
    }

    // Empty ReadOnlyMemory<byte> creates valid zero-length stream.
    [Fact]
    public void Constructor_EmptyMemory_CreatesZeroLengthStream()
    {
        var emptyMemory = ReadOnlyMemory<byte>.Empty;
        var stream = new MemoryTStream(emptyMemory);

        Assert.Equal(0, stream.Length);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
        Assert.False(stream.CanWrite);
    }

    [Fact]
    public void Constructor_FromMemory_WorksCorrectly()
    {
        var buffer = new byte[] { 1, 2, 3, 4, 5 };
        Memory<byte> memory = buffer;
        var stream = new MemoryTStream(memory);  // Implicit conversion

        Assert.Equal(5, stream.Length);
        Assert.True(stream.CanRead);
    }

    // Not covered in conformance tests: TryGetBuffer behavior
    [Fact]
    public void TryGetBuffer_PubliclyVisible_ReturnsTrue()
    {
        var originalBuffer = new byte[] { 1, 2, 3, 4, 5 };
        var stream = new MemoryTStream(originalBuffer, publiclyVisible: true);

        bool success = stream.TryGetBuffer(out ReadOnlyMemory<byte> retrievedBuffer);

        Assert.True(success);
        Assert.Equal(originalBuffer.Length, retrievedBuffer.Length);
        // Verify it's the same underlying data
        Assert.True(retrievedBuffer.Span.SequenceEqual(originalBuffer));
    }
    
    [Fact]
    public void TryGetBuffer_NotPubliclyVisible_ReturnsFalse()
    {
        var buffer = new byte[10];
        var stream = new MemoryTStream(buffer, publiclyVisible: false);

        bool success = stream.TryGetBuffer(out ReadOnlyMemory<byte> retrievedBuffer);

        Assert.False(success);
        Assert.Equal(default, retrievedBuffer);
    }

    [Fact]
    public void TryGetBuffer_AfterDispose_StillWorks()
    {
        var buffer = new byte[] { 1, 2, 3 };
        var stream = new MemoryTStream(buffer, publiclyVisible: true);

        stream.Dispose();
        bool success = stream.TryGetBuffer(out ReadOnlyMemory<byte> retrievedBuffer);

        Assert.True(success);
        Assert.Equal(3, retrievedBuffer.Length);
    }

    [Fact]
    public void TryGetBuffer_ReturnsSameUnderlyingMemory()
    {
        var originalBuffer = new byte[] { 10, 20, 30, 40, 50 };
        var stream = new MemoryTStream(originalBuffer, publiclyVisible: true);

        stream.TryGetBuffer(out ReadOnlyMemory<byte> retrievedBuffer);

        // Should be the same underlying memory
        Assert.True(retrievedBuffer.Span.SequenceEqual(originalBuffer));

        // Verify by checking specific values
        for (int i = 0; i < originalBuffer.Length; i++)
        {
            Assert.Equal(originalBuffer[i], retrievedBuffer.Span[i]);
        }
    }

    // Not covered in conformance tests: ReadOnlyMemory slices stream handling
    [Fact]
    public void Stream_WorksWithSlicedMemory()
    {
        var largeBuffer = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var slice = largeBuffer.AsMemory(3, 4);  // [3, 4, 5, 6]
        var stream = new MemoryTStream(slice);

        Assert.Equal(4, stream.Length);

        byte[] result = new byte[4];
        int bytesRead = stream.Read(result, 0, 4);

        Assert.Equal(4, bytesRead);
        Assert.Equal(new byte[] { 3, 4, 5, 6 }, result);
    }

    // Conformance tests repetitive:

    // Conformance tests for ReadOnlyMemoryStream validate 'position' extensively
    [Fact]
    public void Position_AdvancesDuringRead()
    {
        var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var stream = new MemoryTStream(buffer);
        byte[] readBuffer = new byte[3];

        Assert.Equal(0, stream.Position);

        stream.Read(readBuffer, 0, 3);
        Assert.Equal(3, stream.Position);

        stream.Read(readBuffer, 0, 3);
        Assert.Equal(6, stream.Position);

        stream.Read(readBuffer, 0, 3);
        Assert.Equal(9, stream.Position);
    }

    // Conformance tests validate seeking extensively
    [Fact]
    public void Seek_FromCurrent_RelativeOffset()
    {
        var stream = new MemoryTStream(new byte[100]);
        stream.Position = 50;

        // Seek forward 10 bytes
        long newPosition = stream.Seek(10, SeekOrigin.Current);
        Assert.Equal(60, newPosition);

        // Seek backward 20 bytes
        newPosition = stream.Seek(-20, SeekOrigin.Current);
        Assert.Equal(40, newPosition);
    }

    [Fact]
    public void Seek_InvalidOrigin_ThrowsArgumentException()
    {
        var stream = new MemoryTStream(new byte[100]);

        Assert.Throws<ArgumentException>(() => stream.Seek(0, (SeekOrigin)999));
    }

    // Conformance tests validate reads extensively
    [Fact]
    public void Read_ReturnsCorrectData()
    {
        var data = new byte[] { 10, 20, 30, 40, 50 };
        var stream = new MemoryTStream(data);
        byte[] buffer = new byte[3];

        int bytesRead = stream.Read(buffer, 0, 3);

        Assert.Equal(3, bytesRead);
        Assert.Equal(new byte[] { 10, 20, 30 }, buffer);
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public void Read_LargerThanAvailable_ReturnsPartialData()
    {
        var data = new byte[] { 1, 2, 3 };
        var stream = new MemoryTStream(data);
        byte[] buffer = new byte[10];

        int bytesRead = stream.Read(buffer, 0, 10);

        Assert.Equal(3, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer[..3]);
    }

    [Fact]
    public void Read_AfterSeek_ReturnsCorrectData()
    {
        var data = new byte[] { 10, 20, 30, 40, 50 };
        var stream = new MemoryTStream(data);

        stream.Seek(2, SeekOrigin.Begin);
        byte[] buffer = new byte[2];
        int bytesRead = stream.Read(buffer, 0, 2);

        Assert.Equal(2, bytesRead);
        Assert.Equal(new byte[] { 30, 40 }, buffer);
    }

    [Fact]
    public void Read_DoesNotModifyUnderlyingMemory()
    {
        var originalData = new byte[] { 1, 2, 3, 4, 5 };
        var dataCopy = (byte[])originalData.Clone();
        var stream = new MemoryTStream(originalData);

        byte[] buffer = new byte[5];
        stream.Read(buffer, 0, 5);

        // Original data should be unchanged
        Assert.Equal(dataCopy, originalData);
    }

    // Conformance tests validate throwing with ValidateMisuseExceptionsAsync()
    [Fact]
    public void Write_ThrowsNotSupportedException()
    {
        var stream = new MemoryTStream(new ReadOnlyMemory<byte>(new byte[10]));
        byte[] data = new byte[] { 1, 2, 3 };

        Assert.Throws<NotSupportedException>(() => stream.Write(data, 0, 3));
    }

    [Fact]
    public void SetLength_ThrowsNotSupportedException()
    {
        var stream = new MemoryTStream(new byte[10]);
        Assert.Throws<NotSupportedException>(() => stream.SetLength(20));
    }

    // Conformance validates disposal with ValidateDisposedExceptionsAsync()
    [Fact]
    public void Dispose_SetsCanPropertiesToFalse()
    {
        var stream = new MemoryTStream(new byte[10]);

        stream.Dispose();

        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
    }

    [Fact]
    public void Operations_AfterDispose_ThrowObjectDisposedException()
    {
        var buffer = new byte[10];
        var stream = new MemoryTStream(buffer);
        stream.Dispose();

        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[5], 0, 5));
        Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<ObjectDisposedException>(() => _ = stream.Position);
        Assert.Throws<ObjectDisposedException>(() => stream.Position = 0);
        Assert.Throws<ObjectDisposedException>(() => _ = stream.Length);
    }

    // Standard IDisposable pattern - Dispose() should be idempotent.
    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        var stream = new MemoryTStream(new byte[10]);

        stream.Dispose();
        stream.Dispose();  // Should not throw
        stream.Dispose();  // Should not throw
    }

    // Conformance tests extensively validate argument validation
    [Fact]
    public void Read_NullBuffer_ThrowsArgumentNullException()
    {
        var stream = new MemoryTStream(new byte[10]);

        Assert.Throws<ArgumentNullException>(() => stream.Read(null!, 0, 5));
    }

    // Edge Case
    [Fact]
    public void EmptyBuffer_BehavesCorrectly()
    {
        var stream = new MemoryTStream(ReadOnlyMemory<byte>.Empty);

        Assert.Equal(0, stream.Length);
        Assert.Equal(0, stream.Position);

        byte[] buffer = new byte[10];
        Assert.Equal(0, stream.Read(buffer, 0, 10));

        stream.Seek(0, SeekOrigin.Begin);
        Assert.Equal(0, stream.Position);

        // Seeking beyond empty buffer is allowed
        long newPosition = stream.Seek(1, SeekOrigin.Begin);
        Assert.Equal(1, newPosition);
        Assert.Equal(1, stream.Position);
    }

    [Fact]
    public async Task ReadAsync_SameResultSize_ReusesCachedTask()
    {
        var data = new byte[20];
        for (int i = 0; i < 20; i++) data[i] = (byte)i;
        var stream = new MemoryTStream(data);

        byte[] buffer1 = new byte[5];
        byte[] buffer2 = new byte[5];
        byte[] buffer3 = new byte[5];

        Task<int> task1 = stream.ReadAsync(buffer1, 0, 5);
        Task<int> task2 = stream.ReadAsync(buffer2, 0, 5);
        Task<int> task3 = stream.ReadAsync(buffer3, 0, 5);

        await task1;
        await task2;
        await task3;

        Assert.Same(task1, task2);
        Assert.Same(task2, task3);

        Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, buffer1);
        Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, buffer2);
        Assert.Equal(new byte[] { 10, 11, 12, 13, 14 }, buffer3);
    }

    [Fact]
    public async Task ReadAsync_DifferentResultSize_CreatesNewTask()
    {
        var data = new byte[10];
        for (int i = 0; i < 10; i++) data[i] = (byte)i;
        var stream = new MemoryTStream(data);

        byte[] buffer1 = new byte[5];
        byte[] buffer2 = new byte[3];
        byte[] buffer3 = new byte[2];

        Task<int> task1 = stream.ReadAsync(buffer1, 0, 5);  // Returns 5
        Task<int> task2 = stream.ReadAsync(buffer2, 0, 3);  // Returns 3
        Task<int> task3 = stream.ReadAsync(buffer3, 0, 2);  // Returns 2

        await task1;
        await task2;
        await task3;

        Assert.NotSame(task1, task2);
        Assert.NotSame(task2, task3);
    }

    [Fact]
    public async Task ReadAsync_ArrayBackedMemory_UsesFastPath()
    {
        var data = new byte[] { 10, 20, 30, 40, 50 };
        var stream = new MemoryTStream(data);

        byte[] arrayBuffer = new byte[3];
        Memory<byte> memory = arrayBuffer.AsMemory();

        int bytesRead = await stream.ReadAsync(memory);

        Assert.Equal(3, bytesRead);
        Assert.Equal(new byte[] { 10, 20, 30 }, arrayBuffer);
    }
}
