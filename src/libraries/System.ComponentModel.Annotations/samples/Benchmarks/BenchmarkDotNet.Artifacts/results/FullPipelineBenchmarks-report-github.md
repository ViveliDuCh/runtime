```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.8390) (Hyper-V)
Unknown processor
.NET SDK 11.0.100-preview.5.26227.104
  [Host]     : .NET 11.0.0 (11.0.26.22804), X64 RyuJIT AVX2
  DefaultJob : .NET 11.0.0 (11.0.26.22804), X64 RyuJIT AVX2


```
| Method              | Mean      | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------------- |----------:|------:|-------:|----------:|------------:|
| FullPipeline_Batch  |  8.406 μs |  1.00 | 0.1221 |   2.34 KB |        1.00 |
| FullPipeline_Stream | 12.065 μs |  1.44 | 0.1373 |   2.38 KB |        1.02 |
