```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.7781)
Unknown processor
.NET SDK 10.0.103
  [Host]   : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                       | Mean     | Gen0   | Gen1   | Allocated |
|----------------------------- |---------:|-------:|-------:|----------:|
| &#39;MemoryStream alloc&#39;         | 41.61 ns | 0.0886 |      - |   1.09 KB |
| &#39;Direct:Stream alloc&#39;        | 39.76 ns | 0.0886 | 0.0003 |   1.09 KB |
| &#39;Derived:MemoryStream alloc&#39; | 44.38 ns | 0.0918 | 0.0004 |   1.13 KB |
