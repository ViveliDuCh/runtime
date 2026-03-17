// Benchmarks comparing four approaches:
// 1. MemoryStream (baseline) - existing .NET MemoryStream
// 2. DedicatedStream - one custom Stream class per backing type (current proposal)
// 3. StreamableStream<T> (optimized) - IStreamable with selective overrides
// 4. StreamableStream<T> (minimal/DIM-only) - IStreamable with ONLY DIM defaults
//
// The DIM-only variant shows what you get "for free" without overriding anything.

using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace IStreamableBenchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[ShortRunJob(RuntimeMoniker.Net90)]
public class ReadBenchmarks
{
    private byte[] _data = null!;
    private byte[] _readBuffer = null!;

    [Params(100, 10_000)]
    public int Size;

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[Size];
        Random.Shared.NextBytes(_data);
        _readBuffer = new byte[Size];
    }

    // === ReadByte ===

    [Benchmark(Baseline = true), BenchmarkCategory("ReadByte")]
    public int MemoryStream_ReadByte()
    {
        using var s = new MemoryStream(_data, writable: false);
        int last = 0;
        while ((last = s.ReadByte()) != -1) { }
        return last;
    }

    [Benchmark, BenchmarkCategory("ReadByte")]
    public int Dedicated_ReadByte()
    {
        using var s = new DedicatedReadOnlyMemoryStream(_data);
        int last = 0;
        while ((last = s.ReadByte()) != -1) { }
        return last;
    }

    [Benchmark, BenchmarkCategory("ReadByte")]
    public int Streamable_ReadByte()
    {
        using var s = new StreamableStream<ReadOnlyMemoryStreamable>(
            new ReadOnlyMemoryStreamable(_data));
        int last = 0;
        while ((last = s.ReadByte()) != -1) { }
        return last;
    }

    [Benchmark, BenchmarkCategory("ReadByte")]
    public int DIMOnly_ReadByte()
    {
        using var s = new StreamableStream<ReadOnlyMemoryStreamableMinimal>(
            new ReadOnlyMemoryStreamableMinimal(_data));
        int last = 0;
        while ((last = s.ReadByte()) != -1) { }
        return last;
    }

    // === ReadSpan (bulk read) ===

    [Benchmark(Baseline = true), BenchmarkCategory("ReadSpan")]
    public int MemoryStream_ReadSpan()
    {
        using var s = new MemoryStream(_data, writable: false);
        return s.Read(_readBuffer);
    }

    [Benchmark, BenchmarkCategory("ReadSpan")]
    public int Dedicated_ReadSpan()
    {
        using var s = new DedicatedReadOnlyMemoryStream(_data);
        return s.Read(_readBuffer);
    }

    [Benchmark, BenchmarkCategory("ReadSpan")]
    public int Streamable_ReadSpan()
    {
        using var s = new StreamableStream<ReadOnlyMemoryStreamable>(
            new ReadOnlyMemoryStreamable(_data));
        return s.Read(_readBuffer);
    }

    // === CopyTo ===

    [Benchmark(Baseline = true), BenchmarkCategory("CopyTo")]
    public void MemoryStream_CopyTo()
    {
        using var src = new MemoryStream(_data, writable: false);
        using var dst = new MemoryStream();
        src.CopyTo(dst);
    }

    [Benchmark, BenchmarkCategory("CopyTo")]
    public void Dedicated_CopyTo()
    {
        using var src = new DedicatedReadOnlyMemoryStream(_data);
        using var dst = new MemoryStream();
        src.CopyTo(dst);
    }

    [Benchmark, BenchmarkCategory("CopyTo")]
    public void Streamable_CopyTo()
    {
        using var src = new StreamableStream<ReadOnlyMemoryStreamable>(
            new ReadOnlyMemoryStreamable(_data));
        using var dst = new MemoryStream();
        src.CopyTo(dst);
    }
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[ShortRunJob(RuntimeMoniker.Net90)]
public class WriteBenchmarks
{
    private byte[] _data = null!;
    private byte[] _buffer = null!;

    [Params(100, 10_000)]
    public int Size;

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[Size];
        Random.Shared.NextBytes(_data);
        _buffer = new byte[Size];
    }

    // === WriteByte ===

    [Benchmark(Baseline = true), BenchmarkCategory("WriteByte")]
    public void MemoryStream_WriteByte()
    {
        using var s = new MemoryStream(_buffer, writable: true);
        for (int i = 0; i < _data.Length; i++)
            s.WriteByte(_data[i]);
    }

    [Benchmark, BenchmarkCategory("WriteByte")]
    public void Dedicated_WriteByte()
    {
        using var s = new DedicatedMemoryByteStream((Memory<byte>)_buffer);
        for (int i = 0; i < _data.Length; i++)
            s.WriteByte(_data[i]);
    }

    [Benchmark, BenchmarkCategory("WriteByte")]
    public void Streamable_WriteByte()
    {
        using var s = new StreamableStream<MemoryStreamable>(
            new MemoryStreamable((Memory<byte>)_buffer));
        for (int i = 0; i < _data.Length; i++)
            s.WriteByte(_data[i]);
    }

    // === WriteSpan (bulk write) ===

    [Benchmark(Baseline = true), BenchmarkCategory("WriteSpan")]
    public void MemoryStream_WriteSpan()
    {
        using var s = new MemoryStream(_buffer, writable: true);
        s.Write(_data);
    }

    [Benchmark, BenchmarkCategory("WriteSpan")]
    public void Dedicated_WriteSpan()
    {
        using var s = new DedicatedMemoryByteStream((Memory<byte>)_buffer);
        s.Write(_data);
    }

    [Benchmark, BenchmarkCategory("WriteSpan")]
    public void Streamable_WriteSpan()
    {
        using var s = new StreamableStream<MemoryStreamable>(
            new MemoryStreamable((Memory<byte>)_buffer));
        s.Write(_data);
    }
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[ShortRunJob(RuntimeMoniker.Net90)]
public class AllocationBenchmarks
{
    private byte[] _data = null!;

    [Params(1000)]
    public int Size;

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[Size];
        Random.Shared.NextBytes(_data);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Allocation")]
    public Stream MemoryStream_Alloc() => new MemoryStream(_data, writable: false);

    [Benchmark, BenchmarkCategory("Allocation")]
    public Stream Dedicated_Alloc() => new DedicatedReadOnlyMemoryStream(_data);

    [Benchmark, BenchmarkCategory("Allocation")]
    public Stream Streamable_Alloc() =>
        new StreamableStream<ReadOnlyMemoryStreamable>(new ReadOnlyMemoryStreamable(_data));
}
