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

    // NOTE: DIMOnly_ReadByte benchmark REMOVED — it causes an infinite loop
    // due to the ISpanOwner DIM+boxing showstopper. Position never advances,
    // so `while (ReadByte() != -1)` never terminates.
    // This bug is proven in EnumerableCorrectnessTests.Compare_DIM_Behavior.

    [Benchmark, BenchmarkCategory("ReadByte")]
    public int EnumOptimized_ReadByte()
    {
        IStreamSource source = new ReadOnlyMemorySource(_data);
        using var s = new EnumerableSourceStream(source);
        int last = 0;
        while ((last = s.ReadByte()) != -1) { }
        return last;
    }

    [Benchmark, BenchmarkCategory("ReadByte")]
    public int EnumDIMOnly_ReadByte()
    {
        IStreamSource source = new ReadOnlyMemorySourceMinimal(_data);
        using var s = new EnumerableSourceStream(source);
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

    [Benchmark, BenchmarkCategory("ReadSpan")]
    public int EnumOptimized_ReadSpan()
    {
        IStreamSource source = new ReadOnlyMemorySource(_data);
        using var s = new EnumerableSourceStream(source);
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

    [Benchmark, BenchmarkCategory("CopyTo")]
    public void EnumOptimized_CopyTo()
    {
        IStreamSource source = new ReadOnlyMemorySource(_data);
        using var src = new EnumerableSourceStream(source);
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

    [Benchmark, BenchmarkCategory("WriteByte")]
    public void EnumOptimized_WriteByte()
    {
        IStreamSource source = new MemorySource((Memory<byte>)_buffer);
        using var s = new EnumerableSourceStream(source);
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

    [Benchmark, BenchmarkCategory("WriteSpan")]
    public void EnumOptimized_WriteSpan()
    {
        IStreamSource source = new MemorySource((Memory<byte>)_buffer);
        using var s = new EnumerableSourceStream(source);
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

    [Benchmark, BenchmarkCategory("Allocation")]
    public Stream EnumOptimized_Alloc()
    {
        IStreamSource source = new ReadOnlyMemorySource(_data);
        return new EnumerableSourceStream(source);
    }
}
