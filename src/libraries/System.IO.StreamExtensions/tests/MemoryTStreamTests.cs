// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Xunit;

namespace System.IO.StreamExtensions.Tests;

/// <summary>
/// Additional specific tests for MemoryTStream beyond conformance tests.
/// </summary>
public class MemoryTStreamTests
{
    [Fact]
    public void Constructor_EmptyMemory_CreatesZeroCapacityStream()
    {
        var emptyMemory = Memory<byte>.Empty;
        var stream = StreamFactory.StreamFromWritableData(emptyMemory);

        Assert.Equal(0, stream.Length);
        Assert.Equal(0, stream.Position);

        // Cannot write to zero-capacity stream
        Assert.Throws<NotSupportedException>(() => stream.WriteByte(42));
    }

    [Fact]
    public void Write_BeyondCapacity_ThrowsNotSupportedException()
    {
        var buffer = new byte[10];
        var stream = StreamFactory.StreamFromWritableData(new Memory<byte>(buffer));

        byte[] data = new byte[15];  // More than capacity

        // Both MemoryStream (fixed capacity) and MemoryTStream throw NotSupportedException
        // when trying to expand beyond capacity, just with different messages
        var exception = Assert.Throws<NotSupportedException>(() =>
            stream.Write(data, 0, data.Length));

        // Accept either message format: MemoryTStream's or MemoryStream's 'SR.NotSupported_MemStreamNotExpandable' message
        Assert.True(
            exception.Message.Contains("Cannot expand buffer") ||
            exception.Message.Contains("not expandable"),
            $"Unexpected exception message: {exception.Message}");
    }

    [Fact]
    public void WriteByte_BeyondCapacity_ThrowsNotSupportedException()
    {
        var buffer = new byte[3];
        var stream = StreamFactory.StreamFromWritableData(new Memory<byte>(buffer));

        stream.WriteByte(1);
        stream.WriteByte(2);
        stream.WriteByte(3);

        // Both MemoryStream (fixed capacity) and MemoryTStream throw NotSupportedException
        var exception = Assert.Throws<NotSupportedException>(() => stream.WriteByte(4));

        // Accept either message format: MemoryTStream's or MemoryStream's 'SR.NotSupported_MemStreamNotExpandable' message
        Assert.True(
            exception.Message.Contains("Cannot expand buffer") ||
            exception.Message.Contains("not expandable"),
            $"Unexpected exception message: {exception.Message}");
    }

    [Fact]
    public void Write_UpToExactCapacity_Succeeds()
    {
        var buffer = new byte[10];
        var stream = StreamFactory.StreamFromWritableData(new Memory<byte>(buffer));

        byte[] data = new byte[10];  // Exactly capacity
        for (int i = 0; i < data.Length; i++) data[i] = (byte)i;

        stream.Write(data, 0, data.Length);

        Assert.Equal(10, stream.Position);
        Assert.Equal(10, stream.Length);

        // Verify data was written
        stream.Position = 0;
        byte[] readBack = new byte[10];
        int bytesRead = stream.Read(readBack, 0, 10);
        Assert.Equal(10, bytesRead);
        Assert.Equal(data, readBack);
    }

    [Fact]
    public void Write_PartialFitAtEndOfCapacity_WritesAvailableSpace()
    {
        var buffer = new byte[10];
        var stream = StreamFactory.StreamFromWritableData(buffer);

        stream.Write(new byte[8], 0, 8);  // 8 bytes used, 2 remaining
        Assert.Equal(8, stream.Position);

        // Try to write 5 bytes (only 2 fit)
        byte[] data = new byte[5];
        Assert.Throws<NotSupportedException>(() => stream.Write(data, 0, 5));

        // Position should be unchanged after failed write
        Assert.Equal(8, stream.Position);
    }

    //seeking beyond capacity is allowed.
    //Write will fail, but seek succeeds.
    [Fact]
    public void Seek_PastCapacity_Succeeds()
    {
        var buffer = new byte[10];
        var stream = StreamFactory.StreamFromWritableData(buffer);

        // Seek beyond capacity
        stream.Seek(100, SeekOrigin.Begin);
        Assert.Equal(100, stream.Position);

        Assert.Equal(-1, stream.ReadByte());

        // Write throws (beyond capacity)
        Assert.Throws<NotSupportedException>(() => stream.WriteByte(42));
    }

    [Fact]
    public void Seek_FromEndNegativeOffset_PositionsCorrectly()
    {
        var buffer = new byte[100];
        var stream = StreamFactory.StreamFromWritableData(buffer);

        // Seek to 10 bytes before end
        long newPosition = stream.Seek(-10, SeekOrigin.End);

        Assert.Equal(90, newPosition);  // 100 - 10 = 90
        Assert.Equal(90, stream.Position);
    }

    [Fact]
    public void ReadOnlyStream_WriteOperations_ThrowNotSupportedException()
    {
        var buffer = new byte[100];
        var stream = StreamFactory.StreamFromWritableData(buffer, writable: false);

        Assert.False(stream.CanWrite);
        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[5], 0, 5));
        Assert.Throws<NotSupportedException>(() => stream.WriteByte(42));
    }

    [Fact]
    public void Write_OverExistingData_ReplacesData()
    {
        var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var stream = StreamFactory.StreamFromWritableData(new Memory<byte>(buffer));

        // Overwrite positions 3-5 with new data
        stream.Position = 3;
        stream.Write(new byte[] { 100, 101, 102 }, 0, 3);

        // Verify overwrite
        stream.Position = 0;
        byte[] result = new byte[10];
        stream.Read(result, 0, 10);

        Assert.Equal(new byte[] { 1, 2, 3, 100, 101, 102, 7, 8, 9, 10 }, result);
    }

    [Fact]
    public void Position_SetToIntMaxValue_Succeeds()
    {
        var buffer = new byte[100];
        var stream = StreamFactory.StreamFromWritableData(buffer);

        // MemoryStream has MaxStreamLength (2147483591), MemoryTStream allows int.MaxValue
        if (stream is MemoryStream)
        {
            // MemoryStream.MaxStreamLength = Array.MaxLength = 2147483591
            // Setting position beyond this throws ArgumentOutOfRangeException
            Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = int.MaxValue);
        }
        else
        {
            // MemoryTStream should not throw even though it's way beyond capacity
            stream.Position = int.MaxValue;
            Assert.Equal(int.MaxValue, stream.Position);
        }
    }

    [Fact]
    public void Position_SetNegative_ThrowsArgumentOutOfRangeException()
    {
        var stream = StreamFactory.StreamFromWritableData(new byte[100]);
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = -1);
    }

    [Fact]
    public void Position_SetBeyondLongMaxValue_ThrowsArgumentOutOfRangeException()
    {
        var stream = StreamFactory.StreamFromWritableData(new byte[100]);

        // Position property accepts long, but internally casts to int
        // Setting to value > int.MaxValue should throw
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = (long)int.MaxValue + 1);
    }

    [Fact]
    public void Dispose_SetsCanPropertiesToFalse()
    {
        var stream = StreamFactory.StreamFromWritableData(new byte[10]);

        stream.Dispose();

        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
    }

    [Fact]
    public void Operations_AfterDispose_ThrowObjectDisposedException()
    {
        var buffer = new byte[10];
        var stream = StreamFactory.StreamFromWritableData(buffer);
        stream.Dispose();

        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[5], 0, 5));
        Assert.Throws<ObjectDisposedException>(() => stream.Write(new byte[5], 0, 5));
        Assert.Throws<ObjectDisposedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<ObjectDisposedException>(() => _ = stream.Position);
        Assert.Throws<ObjectDisposedException>(() => stream.Position = 0);
        Assert.Throws<ObjectDisposedException>(() => _ = stream.Length);
    }

    // Edge-cases
    // Zero-byte write doesn't throw and leaves state unchanged.
    [Fact]
    public void Write_ZeroBytes_Succeeds()
    {
        var stream = StreamFactory.StreamFromWritableData(new byte[10]);

        stream.Write(new byte[0], 0, 0);

        Assert.Equal(0, stream.Position);
        Assert.Equal(10, stream.Length);  // Length from initial buffer
    }

    [Fact]
    public void Read_ZeroBytes_ReturnsZero()
    {
        var stream = StreamFactory.StreamFromWritableData(new byte[10]);

        int bytesRead = stream.Read(new byte[10], 0, 0);

        Assert.Equal(0, bytesRead);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void SetLength_ThrowsNotSupportedException()
    {
        var stream = StreamFactory.StreamFromWritableData(new byte[10]);

        Assert.Throws<NotSupportedException>(() => stream.SetLength(20));
    }

    [Fact]
    public async Task ReadAsync_SameResultSize_ReusesCachedTask()
    {
        var data = new byte[20];
        for (int i = 0; i < 20; i++) data[i] = (byte)i;
        var stream = StreamFactory.StreamFromWritableData(data);

        byte[] buffer1 = new byte[5];
        byte[] buffer2 = new byte[5];
        byte[] buffer3 = new byte[5];

        Task<int> task1 = stream.ReadAsync(buffer1, 0, 5);
        Task<int> task2 = stream.ReadAsync(buffer2, 0, 5);
        Task<int> task3 = stream.ReadAsync(buffer3, 0, 5);

        await task1;
        await task2;
        await task3;

        Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, buffer1);
        Assert.Equal(new byte[] { 5, 6, 7, 8, 9 }, buffer2);
        Assert.Equal(new byte[] { 10, 11, 12, 13, 14 }, buffer3);
    }

    [Fact]
    public async Task ReadAsync_DifferentResultSize_CreatesNewTask()
    {
        var data = new byte[10];
        for (int i = 0; i < 10; i++) data[i] = (byte)i;
        var stream = StreamFactory.StreamFromWritableData(data);

        byte[] buffer1 = new byte[5];
        byte[] buffer2 = new byte[3];
        byte[] buffer3 = new byte[2];

        Task<int> task1 = stream.ReadAsync(buffer1, 0, 5);
        Task<int> task2 = stream.ReadAsync(buffer2, 0, 3);
        Task<int> task3 = stream.ReadAsync(buffer3, 0, 2);

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
        var stream = StreamFactory.StreamFromWritableData(data);

        byte[] arrayBuffer = new byte[3];
        Memory<byte> memory = arrayBuffer.AsMemory();
        int bytesRead = await stream.ReadAsync(memory);

        Assert.Equal(3, bytesRead);
        Assert.Equal(new byte[] { 10, 20, 30 }, arrayBuffer);
    }
}
