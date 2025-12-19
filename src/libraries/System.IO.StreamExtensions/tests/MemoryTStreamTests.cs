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
    // TECHNICAL: Tests the distinction between capacity and logical length
    [Fact]
    public void Constructor_ExplicitLength_SetsLogicalLength()
    {
        var buffer = new byte[100];
        var stream = new MemoryTStream(buffer, length: 50, writable: true, publiclyVisible: true);

        Assert.Equal(50, stream.Length);  // Logical length
        Assert.Equal(0, stream.Position);

        // Can write up to capacity (100), not just logical length (50)
        stream.Position = 75;
        stream.WriteByte(42);
        Assert.Equal(76, stream.Length);  // Length grows as we write past it
    }

    [Fact]
    public void Constructor_InvalidLength_Throws()
    {
        var buffer = new byte[100];

        // Negative length
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MemoryTStream(buffer, length: -1, writable: true, publiclyVisible: true));

        // Length exceeds capacity
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MemoryTStream(buffer, length: 101, writable: true, publiclyVisible: true));
    }

    [Fact]
    public void Constructor_EmptyMemory_CreatesZeroCapacityStream()
    {
        var emptyMemory = Memory<byte>.Empty;
        var stream = new MemoryTStream(emptyMemory, writable: true);

        Assert.Equal(0, stream.Length);
        Assert.Equal(0, stream.Position);

        // Cannot write to zero-capacity stream
        Assert.Throws<NotSupportedException>(() => stream.WriteByte(42));
    }

    [Fact]
    public void Write_BeyondCapacity_ThrowsNotSupportedException()
    {
        var buffer = new byte[10];
        var stream = new MemoryTStream(buffer, writable: true);

        byte[] data = new byte[15];  // More than capacity

        var exception = Assert.Throws<NotSupportedException>(() =>
            stream.Write(data, 0, data.Length));

        Assert.Contains("Cannot expand buffer", exception.Message);
        Assert.Contains("exceed capacity", exception.Message);
    }

    [Fact]
    public void WriteByte_BeyondCapacity_ThrowsNotSupportedException()
    {
        var buffer = new byte[3];
        var stream = new MemoryTStream(buffer, writable: true);

        stream.WriteByte(1);
        stream.WriteByte(2);
        stream.WriteByte(3);

        var exception = Assert.Throws<NotSupportedException>(() => stream.WriteByte(4));
        Assert.Contains("Cannot expand buffer", exception.Message);
    }

    [Fact]
    public void Write_UpToExactCapacity_Succeeds()
    {
        var buffer = new byte[10];
        var stream = new MemoryTStream(buffer, writable: true);

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
        var stream = new MemoryTStream(buffer, writable: true);

        stream.Write(new byte[8], 0, 8);  // 8 bytes used, 2 remaining
        Assert.Equal(8, stream.Position);

        // Try to write 5 bytes (only 2 fit)
        byte[] data = new byte[5];
        Assert.Throws<NotSupportedException>(() => stream.Write(data, 0, 5));

        // Position should be unchanged after failed write
        Assert.Equal(8, stream.Position);
    }

    [Fact]
    public void Write_ExtendsLength_WhenWritingPastCurrentLength()
    {
        var buffer = new byte[100];
        var stream = new MemoryTStream(buffer, length: 10, writable: true, publiclyVisible: true);

        Assert.Equal(10, stream.Length);

        // Write at position 20 (past current length of 10)
        stream.Position = 20;
        stream.WriteByte(42);

        // Length should now be 21 (position 20 + 1 byte)
        Assert.Equal(21, stream.Length);
    }

    [Fact]
    public void Read_PastLogicalLength_ReturnsZero()
    {
        var buffer = new byte[100];  // Capacity:  100
        var stream = new MemoryTStream(buffer, length: 10, writable: true, publiclyVisible: true);  // Length: 10

        stream.Position = 10;  // At end of logical length

        byte[] readBuffer = new byte[10];
        int bytesRead = stream.Read(readBuffer, 0, 10);

        Assert.Equal(0, bytesRead);  // EOF at logical length, not capacity
    }

    [Fact]
    public void Seek_PastLogicalLength_ThenWrite_CreatesZeroGap()
    {
        var buffer = new byte[100];
        var stream = new MemoryTStream(buffer, length: 10, writable: true, publiclyVisible: true);

        // Seek 5 bytes past logical length
        stream.Seek(15, SeekOrigin.Begin);
        stream.WriteByte(42);

        Assert.Equal(16, stream.Length);  // Length extended to position + 1
        Assert.Equal(16, stream.Position);

        // Verify the gap (positions 10-14) contains zeros
        stream.Position = 10;
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(0, stream.ReadByte());
        }

        // Verify the written byte
        Assert.Equal(42, stream.ReadByte());
    }

    //seeking beyond capacity is allowed.
    //Write will fail, but seek succeeds.
    [Fact]
    public void Seek_PastCapacity_Succeeds()
    {
        var buffer = new byte[10];
        var stream = new MemoryTStream(buffer, writable: true);

        // Seek beyond capacity
        stream.Seek(100, SeekOrigin.Begin);
        Assert.Equal(100, stream.Position);

        // Read returns 0 (beyond logical length)
        Assert.Equal(-1, stream.ReadByte());

        // Write throws (beyond capacity)
        Assert.Throws<NotSupportedException>(() => stream.WriteByte(42));
    }

    [Fact]
    public void Seek_FromEndNegativeOffset_PositionsCorrectly()
    {
        var buffer = new byte[100];
        var stream = new MemoryTStream(buffer, length: 50, writable: true, publiclyVisible: true);

        // Seek to 10 bytes before end
        long newPosition = stream.Seek(-10, SeekOrigin.End);

        Assert.Equal(40, newPosition);  // 50 - 10 = 40
        Assert.Equal(40, stream.Position);
    }

    [Fact]
    public void ReadOnlyStream_WriteOperations_ThrowNotSupportedException()
    {
        var buffer = new byte[100];
        var stream = new MemoryTStream(buffer, writable: false);

        Assert.False(stream.CanWrite);
        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[5], 0, 5));
        Assert.Throws<NotSupportedException>(() => stream.WriteByte(42));
    }

    // VALIDATES: Read-only stream allows read and seek operations.
    [Fact]
    public void ReadOnlyStream_ReadAndSeekOperations_Succeed()
    {
        var buffer = new byte[] { 1, 2, 3, 4, 5 };
        var stream = new MemoryTStream(buffer, writable: false);

        // Read
        byte[] readBuffer = new byte[3];
        int bytesRead = stream.Read(readBuffer, 0, 3);
        Assert.Equal(3, bytesRead);
        Assert.Equal(new byte[] { 1, 2, 3 }, readBuffer);

        // Seek
        stream.Seek(0, SeekOrigin.Begin);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void TryGetBuffer_PubliclyVisible_ReturnsBuffer()
    {
        var originalBuffer = new byte[] { 1, 2, 3, 4, 5 };
        var stream = new MemoryTStream(originalBuffer, writable: true, publiclyVisible: true);

        bool success = stream.TryGetBuffer(out Memory<byte> retrievedBuffer);

        Assert.True(success);
        Assert.Equal(originalBuffer.Length, retrievedBuffer.Length);
        Assert.True(retrievedBuffer.Span.SequenceEqual(originalBuffer));
    }

    [Fact]
    public void TryGetBuffer_NotPubliclyVisible_ReturnsFalse()
    {
        var buffer = new byte[10];
        var stream = new MemoryTStream(buffer, writable: true, publiclyVisible: false);

        bool success = stream.TryGetBuffer(out Memory<byte> retrievedBuffer);

        Assert.False(success);
        Assert.Equal(default, retrievedBuffer);
    }

    //buffer remains accessible after dispose.
    [Fact]
    public void TryGetBuffer_AfterDispose_StillWorks()
    {
        var buffer = new byte[] { 1, 2, 3 };
        var stream = new MemoryTStream(buffer, writable: true, publiclyVisible: true);

        stream.Dispose();

        bool success = stream.TryGetBuffer(out Memory<byte> retrievedBuffer);
        Assert.True(success);
        Assert.Equal(3, retrievedBuffer.Length);
    }

    // Modifications through TryGetBuffer reflect in stream.
    [Fact]
    public void TryGetBuffer_ModificationsThroughBuffer_VisibleInStream()
    {
        var buffer = new byte[10];
        var stream = new MemoryTStream(buffer, writable: true, publiclyVisible: true);

        stream.TryGetBuffer(out Memory<byte> exposedBuffer);
        exposedBuffer.Span[5] = 42;

        // Read through stream should see the modification
        stream.Position = 5;
        Assert.Equal(42, stream.ReadByte());
    }

    [Fact]
    public void Write_OverExistingData_ReplacesData()
    {
        var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var stream = new MemoryTStream(buffer, writable: true);

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
        var stream = new MemoryTStream(buffer, writable: true);

        // Should not throw even though it's way beyond capacity
        stream.Position = int.MaxValue;
        Assert.Equal(int.MaxValue, stream.Position);
    }

    [Fact]
    public void Position_SetNegative_ThrowsArgumentOutOfRangeException()
    {
        var stream = new MemoryTStream(new byte[100], writable: true);
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = -1);
    }

    [Fact]
    public void Position_SetBeyondLongMaxValue_ThrowsArgumentOutOfRangeException()
    {
        var stream = new MemoryTStream(new byte[100], writable: true);

        // Position property accepts long, but internally casts to int
        // Setting to value > int.MaxValue should throw
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = (long)int.MaxValue + 1);
    }

    [Fact]
    public void Dispose_SetsCanPropertiesToFalse()
    {
        var stream = new MemoryTStream(new byte[10], writable: true);

        stream.Dispose();

        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
    }

    [Fact]
    public void Operations_AfterDispose_ThrowObjectDisposedException()
    {
        var buffer = new byte[10];
        var stream = new MemoryTStream(buffer, writable: true);
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
        var stream = new MemoryTStream(new byte[10], writable: true);

        stream.Write(new byte[0], 0, 0);

        Assert.Equal(0, stream.Position);
        Assert.Equal(10, stream.Length);  // Length from initial buffer
    }

    [Fact]
    public void Read_ZeroBytes_ReturnsZero()
    {
        var stream = new MemoryTStream(new byte[10], writable: false);

        int bytesRead = stream.Read(new byte[10], 0, 0);

        Assert.Equal(0, bytesRead);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void SetLength_ThrowsNotSupportedException()
    {
        var stream = new MemoryTStream(new byte[10], writable: true);

        Assert.Throws<NotSupportedException>(() => stream.SetLength(20));
    }

    [Fact]
    public void ComplexScenario_WriteSeekOverwriteRead()
    {
        var buffer = new byte[20]; // Length = 0, start with empty buffer.
        var stream = new MemoryTStream(buffer, length: 0, writable: true, publiclyVisible: false);

        // 1. Write initial data
        stream.Write(new byte[] { 1, 2, 3, 4, 5 }, 0, 5);
        Assert.Equal(5, stream.Position);
        Assert.Equal(5, stream.Length);

        // 2. Seek to position 2
        stream.Seek(2, SeekOrigin.Begin);
        Assert.Equal(2, stream.Position);

        // 3. Overwrite with new data
        stream.Write(new byte[] { 100, 101 }, 0, 2);
        Assert.Equal(4, stream.Position);

        // 4. Seek to end and append
        stream.Seek(0, SeekOrigin.End);
        stream.Write(new byte[] { 6, 7 }, 0, 2);
        Assert.Equal(7, stream.Length);

        // 5. Read all and verify
        stream.Position = 0;
        byte[] result = new byte[7];
        stream.Read(result, 0, 7);

        Assert.Equal(new byte[] { 1, 2, 100, 101, 5, 6, 7 }, result);
    }

    [Fact]
    public async Task ReadAsync_SameResultSize_ReusesCachedTask()
    {
        var data = new byte[20];
        for (int i = 0; i < 20; i++) data[i] = (byte)i;
        var stream = new MemoryTStream(data, writable: false);

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
        var stream = new MemoryTStream(data, writable: false);

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
        var stream = new MemoryTStream(data, writable: false);

        byte[] arrayBuffer = new byte[3];
        Memory<byte> memory = arrayBuffer.AsMemory();
        int bytesRead = await stream.ReadAsync(memory);

        Assert.Equal(3, bytesRead);
        Assert.Equal(new byte[] { 10, 20, 30 }, arrayBuffer);
    }

    [Fact]
    public async Task WriteAsync_ArrayBackedMemory_UsesFastPath()
    {
        var buffer = new byte[10];
        var stream = new MemoryTStream(buffer, length: 0, writable: true);

        byte[] sourceArray = new byte[] { 10, 20, 30 };
        ReadOnlyMemory<byte> memory = sourceArray.AsMemory();

        await stream.WriteAsync(memory);

        Assert.Equal(3, stream.Position);
        Assert.Equal(3, stream.Length);

        stream.Position = 0;
        byte[] readBack = new byte[3];
        stream.Read(readBack, 0, 3);
        Assert.Equal(sourceArray, readBack);
    }
}
