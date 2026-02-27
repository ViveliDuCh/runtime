// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StreamWrapperBenchmarks;

[ShortRunJob]
[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.Median, Column.RatioSD)]
public class StreamReadBenchmarks
{
    private byte[] _data = null!;
    private byte[] _readBuffer = null!;

    [Params(64, 1024, 65536)]
    public int DataSize;

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[DataSize];
        Random.Shared.NextBytes(_data);
        _readBuffer = new byte[DataSize];
    }

    // --- Read(Span<byte>) ---

    [Benchmark(Description = "MemoryStream (baseline)")]
    public int MemoryStream_Read()
    {
        var ms = new MemoryStream(_data, writable: false);
        return ms.Read(_readBuffer, 0, _readBuffer.Length);
    }

    [Benchmark(Description = "Direct:Stream (ROM<byte>)")]
    public int DirectFromStream_Read()
    {
        var ms = new DirectFromStreamApproach(new ReadOnlyMemory<byte>(_data));
        return ms.Read(_readBuffer, 0, _readBuffer.Length);
    }

    [Benchmark(Description = "Derived:MemoryStream (ROM<byte>)")]
    public int MemoryStreamDerived_Read()
    {
        var ms = new MemoryStreamDerivedApproach(new ReadOnlyMemory<byte>(_data));
        return ms.Read(_readBuffer, 0, _readBuffer.Length);
    }

    // --- Read(Span<byte>) with Span overload ---

    [Benchmark(Description = "MemoryStream ReadSpan")]
    public int MemoryStream_ReadSpan()
    {
        var ms = new MemoryStream(_data, writable: false);
        return ms.Read(_readBuffer.AsSpan());
    }

    [Benchmark(Description = "Direct:Stream ReadSpan")]
    public int DirectFromStream_ReadSpan()
    {
        var ms = new DirectFromStreamApproach(new ReadOnlyMemory<byte>(_data));
        return ms.Read(_readBuffer.AsSpan());
    }

    [Benchmark(Description = "Derived:MemoryStream ReadSpan")]
    public int MemoryStreamDerived_ReadSpan()
    {
        var ms = new MemoryStreamDerivedApproach(new ReadOnlyMemory<byte>(_data));
        return ms.Read(_readBuffer.AsSpan());
    }
}

[ShortRunJob]
[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.Median, Column.RatioSD)]
public class StreamWriteBenchmarks
{
    private byte[] _data = null!;
    private byte[] _target = null!;

    [Params(64, 1024, 65536)]
    public int DataSize;

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[DataSize];
        Random.Shared.NextBytes(_data);
        _target = new byte[DataSize];
    }

    [Benchmark(Description = "MemoryStream (baseline)")]
    public void MemoryStream_Write()
    {
        var ms = new MemoryStream(_target, writable: true);
        ms.Write(_data, 0, _data.Length);
    }

    [Benchmark(Description = "Direct:Stream (Memory<byte>)")]
    public void DirectFromStream_Write()
    {
        var ms = new DirectFromStreamApproach(new Memory<byte>(_target));
        ms.Write(_data, 0, _data.Length);
    }

    [Benchmark(Description = "Derived:MemoryStream (Memory<byte>)")]
    public void MemoryStreamDerived_Write()
    {
        var ms = new MemoryStreamDerivedApproach(new Memory<byte>(_target));
        ms.Write(_data, 0, _data.Length);
    }

    // Span overloads
    [Benchmark(Description = "MemoryStream WriteSpan")]
    public void MemoryStream_WriteSpan()
    {
        var ms = new MemoryStream(_target, writable: true);
        ms.Write(_data.AsSpan());
    }

    [Benchmark(Description = "Direct:Stream WriteSpan")]
    public void DirectFromStream_WriteSpan()
    {
        var ms = new DirectFromStreamApproach(new Memory<byte>(_target));
        ms.Write(_data.AsSpan());
    }

    [Benchmark(Description = "Derived:MemoryStream WriteSpan")]
    public void MemoryStreamDerived_WriteSpan()
    {
        var ms = new MemoryStreamDerivedApproach(new Memory<byte>(_target));
        ms.Write(_data.AsSpan());
    }
}

[ShortRunJob]
[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.Median, Column.RatioSD)]
public class StreamSeekBenchmarks
{
    private byte[] _data = null!;

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[4096];
        Random.Shared.NextBytes(_data);
    }

    [Benchmark(Description = "MemoryStream (baseline)")]
    public long MemoryStream_Seek()
    {
        var ms = new MemoryStream(_data, writable: false);
        long sum = 0;
        for (int i = 0; i < 100; i++)
        {
            sum += ms.Seek(i % _data.Length, SeekOrigin.Begin);
        }
        return sum;
    }

    [Benchmark(Description = "Direct:Stream")]
    public long DirectFromStream_Seek()
    {
        var ms = new DirectFromStreamApproach(new ReadOnlyMemory<byte>(_data));
        long sum = 0;
        for (int i = 0; i < 100; i++)
        {
            sum += ms.Seek(i % _data.Length, SeekOrigin.Begin);
        }
        return sum;
    }

    [Benchmark(Description = "Derived:MemoryStream")]
    public long MemoryStreamDerived_Seek()
    {
        var ms = new MemoryStreamDerivedApproach(new ReadOnlyMemory<byte>(_data));
        long sum = 0;
        for (int i = 0; i < 100; i++)
        {
            sum += ms.Seek(i % _data.Length, SeekOrigin.Begin);
        }
        return sum;
    }
}

[ShortRunJob]
[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.Median, Column.RatioSD)]
public class StreamReadByteBenchmarks
{
    private byte[] _data = null!;

    [Params(64, 1024)]
    public int DataSize;

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[DataSize];
        Random.Shared.NextBytes(_data);
    }

    [Benchmark(Description = "MemoryStream (baseline)")]
    public int MemoryStream_ReadByte()
    {
        var ms = new MemoryStream(_data, writable: false);
        int sum = 0;
        for (int i = 0; i < _data.Length; i++)
            sum += ms.ReadByte();
        return sum;
    }

    [Benchmark(Description = "Direct:Stream")]
    public int DirectFromStream_ReadByte()
    {
        var ms = new DirectFromStreamApproach(new ReadOnlyMemory<byte>(_data));
        int sum = 0;
        for (int i = 0; i < _data.Length; i++)
            sum += ms.ReadByte();
        return sum;
    }

    [Benchmark(Description = "Derived:MemoryStream")]
    public int MemoryStreamDerived_ReadByte()
    {
        var ms = new MemoryStreamDerivedApproach(new ReadOnlyMemory<byte>(_data));
        int sum = 0;
        for (int i = 0; i < _data.Length; i++)
            sum += ms.ReadByte();
        return sum;
    }
}

[ShortRunJob]
[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.Median, Column.RatioSD)]
public class ObjectSizeBenchmarks
{
    [Benchmark(Description = "MemoryStream alloc")]
    public MemoryStream AllocMemoryStream()
    {
        return new MemoryStream(new byte[1024], writable: false);
    }

    [Benchmark(Description = "Direct:Stream alloc")]
    public DirectFromStreamApproach AllocDirectFromStream()
    {
        return new DirectFromStreamApproach(new ReadOnlyMemory<byte>(new byte[1024]));
    }

    [Benchmark(Description = "Derived:MemoryStream alloc")]
    public MemoryStreamDerivedApproach AllocMemoryStreamDerived()
    {
        return new MemoryStreamDerivedApproach(new ReadOnlyMemory<byte>(new byte[1024]));
    }
}

[ShortRunJob]
[MemoryDiagnoser]
[HideColumns(Column.Error, Column.StdDev, Column.Median, Column.RatioSD)]
public class TypeCheckBenchmarks
{
    private Stream _memoryStream = null!;
    private Stream _directStream = null!;
    private Stream _derivedStream = null!;

    [GlobalSetup]
    public void Setup()
    {
        var data = new byte[64];
        _memoryStream = new MemoryStream(data, writable: false);
        _directStream = new DirectFromStreamApproach(new ReadOnlyMemory<byte>(data));
        _derivedStream = new MemoryStreamDerivedApproach(new ReadOnlyMemory<byte>(data));
    }

    [Benchmark(Description = "MemoryStream 'is MemoryStream'")]
    public bool MemoryStream_TypeCheck() => _memoryStream is MemoryStream;

    [Benchmark(Description = "Direct:Stream 'is MemoryStream'")]
    public bool DirectFromStream_TypeCheck() => _directStream is MemoryStream;

    [Benchmark(Description = "Derived:MemoryStream 'is MemoryStream'")]
    public bool DerivedMemoryStream_TypeCheck() => _derivedStream is MemoryStream;
}

public class Program
{
    public static void Main(string[] args)
    {
        var config = DefaultConfig.Instance;
        BenchmarkRunner.Run(new[]
        {
            BenchmarkConverter.TypeToBenchmarks(typeof(StreamReadBenchmarks), config),
            BenchmarkConverter.TypeToBenchmarks(typeof(StreamWriteBenchmarks), config),
            BenchmarkConverter.TypeToBenchmarks(typeof(StreamSeekBenchmarks), config),
            BenchmarkConverter.TypeToBenchmarks(typeof(StreamReadByteBenchmarks), config),
            BenchmarkConverter.TypeToBenchmarks(typeof(ObjectSizeBenchmarks), config),
            BenchmarkConverter.TypeToBenchmarks(typeof(TypeCheckBenchmarks), config),
        });
    }
}
