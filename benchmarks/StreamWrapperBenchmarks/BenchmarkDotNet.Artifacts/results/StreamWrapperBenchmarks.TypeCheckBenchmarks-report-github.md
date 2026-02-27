```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.7781)
Unknown processor
.NET SDK 10.0.103
  [Host]   : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                   | Mean      | Allocated |
|----------------------------------------- |----------:|----------:|
| &#39;MemoryStream &#39;is MemoryStream&#39;&#39;         | 0.2511 ns |         - |
| &#39;Direct:Stream &#39;is MemoryStream&#39;&#39;        | 0.1981 ns |         - |
| &#39;Derived:MemoryStream &#39;is MemoryStream&#39;&#39; | 0.1893 ns |         - |
