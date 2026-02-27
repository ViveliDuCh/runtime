```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.7781)
Unknown processor
.NET SDK 10.0.103
  [Host]   : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                             | DataSize | Mean         | Gen0   | Allocated |
|----------------------------------- |--------- |-------------:|-------:|----------:|
| **&#39;MemoryStream (baseline)&#39;**          | **64**       |    **11.687 ns** | **0.0051** |      **64 B** |
| &#39;Direct:Stream (ROM&lt;byte&gt;)&#39;        | 64       |    11.943 ns | 0.0051 |      64 B |
| &#39;Derived:MemoryStream (ROM&lt;byte&gt;)&#39; | 64       |    13.890 ns | 0.0083 |     104 B |
| &#39;MemoryStream ReadSpan&#39;            | 64       |     8.770 ns | 0.0051 |      64 B |
| &#39;Direct:Stream ReadSpan&#39;           | 64       |    12.068 ns | 0.0051 |      64 B |
| &#39;Derived:MemoryStream ReadSpan&#39;    | 64       |    13.708 ns | 0.0083 |     104 B |
| **&#39;MemoryStream (baseline)&#39;**          | **1024**     |    **21.395 ns** | **0.0051** |      **64 B** |
| &#39;Direct:Stream (ROM&lt;byte&gt;)&#39;        | 1024     |    17.976 ns | 0.0051 |      64 B |
| &#39;Derived:MemoryStream (ROM&lt;byte&gt;)&#39; | 1024     |    22.472 ns | 0.0083 |     104 B |
| &#39;MemoryStream ReadSpan&#39;            | 1024     |    16.372 ns | 0.0051 |      64 B |
| &#39;Direct:Stream ReadSpan&#39;           | 1024     |    18.550 ns | 0.0051 |      64 B |
| &#39;Derived:MemoryStream ReadSpan&#39;    | 1024     |    23.162 ns | 0.0083 |     104 B |
| **&#39;MemoryStream (baseline)&#39;**          | **65536**    | **1,207.125 ns** | **0.0038** |      **64 B** |
| &#39;Direct:Stream (ROM&lt;byte&gt;)&#39;        | 65536    | 1,233.177 ns | 0.0038 |      64 B |
| &#39;Derived:MemoryStream (ROM&lt;byte&gt;)&#39; | 65536    | 1,163.825 ns | 0.0076 |     104 B |
| &#39;MemoryStream ReadSpan&#39;            | 65536    | 1,190.245 ns | 0.0038 |      64 B |
| &#39;Direct:Stream ReadSpan&#39;           | 65536    | 1,152.567 ns | 0.0038 |      64 B |
| &#39;Derived:MemoryStream ReadSpan&#39;    | 65536    | 1,162.800 ns | 0.0076 |     104 B |
