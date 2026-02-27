```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.7781)
Unknown processor
.NET SDK 10.0.103
  [Host]   : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                    | DataSize | Mean        | Gen0   | Allocated |
|-------------------------- |--------- |------------:|-------:|----------:|
| **&#39;MemoryStream (baseline)&#39;** | **64**       |    **58.17 ns** |      **-** |         **-** |
| Direct:Stream             | 64       |   135.73 ns | 0.0050 |      64 B |
| Derived:MemoryStream      | 64       |   124.24 ns | 0.0081 |     104 B |
| **&#39;MemoryStream (baseline)&#39;** | **1024**     |   **894.42 ns** |      **-** |         **-** |
| Direct:Stream             | 1024     | 1,826.61 ns | 0.0038 |      64 B |
| Derived:MemoryStream      | 1024     | 1,833.09 ns | 0.0076 |     104 B |
