```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.7781)
Unknown processor
.NET SDK 10.0.103
  [Host]   : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                | DataSize | Mean         | Gen0   | Allocated |
|-------------------------------------- |--------- |-------------:|-------:|----------:|
| **&#39;MemoryStream (baseline)&#39;**             | **64**       |    **12.272 ns** | **0.0051** |      **64 B** |
| &#39;Direct:Stream (Memory&lt;byte&gt;)&#39;        | 64       |     9.670 ns | 0.0051 |      64 B |
| &#39;Derived:MemoryStream (Memory&lt;byte&gt;)&#39; | 64       |    12.692 ns | 0.0083 |     104 B |
| &#39;MemoryStream WriteSpan&#39;              | 64       |     8.387 ns | 0.0051 |      64 B |
| &#39;Direct:Stream WriteSpan&#39;             | 64       |     9.680 ns | 0.0051 |      64 B |
| &#39;Derived:MemoryStream WriteSpan&#39;      | 64       |    14.377 ns | 0.0083 |     104 B |
| **&#39;MemoryStream (baseline)&#39;**             | **1024**     |    **19.869 ns** | **0.0051** |      **64 B** |
| &#39;Direct:Stream (Memory&lt;byte&gt;)&#39;        | 1024     |    22.244 ns | 0.0051 |      64 B |
| &#39;Derived:MemoryStream (Memory&lt;byte&gt;)&#39; | 1024     |    21.617 ns | 0.0083 |     104 B |
| &#39;MemoryStream WriteSpan&#39;              | 1024     |    16.204 ns | 0.0051 |      64 B |
| &#39;Direct:Stream WriteSpan&#39;             | 1024     |    17.569 ns | 0.0051 |      64 B |
| &#39;Derived:MemoryStream WriteSpan&#39;      | 1024     |    22.409 ns | 0.0083 |     104 B |
| **&#39;MemoryStream (baseline)&#39;**             | **65536**    | **1,169.950 ns** | **0.0038** |      **64 B** |
| &#39;Direct:Stream (Memory&lt;byte&gt;)&#39;        | 65536    | 1,182.464 ns | 0.0038 |      64 B |
| &#39;Derived:MemoryStream (Memory&lt;byte&gt;)&#39; | 65536    | 1,218.817 ns | 0.0076 |     104 B |
| &#39;MemoryStream WriteSpan&#39;              | 65536    | 1,176.510 ns | 0.0038 |      64 B |
| &#39;Direct:Stream WriteSpan&#39;             | 65536    | 1,308.655 ns | 0.0038 |      64 B |
| &#39;Derived:MemoryStream WriteSpan&#39;      | 65536    | 1,203.409 ns | 0.0076 |     104 B |
