```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.7781)
Unknown processor
.NET SDK 10.0.103
  [Host]   : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2
  ShortRun : .NET 9.0.13 (9.0.1326.6317), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                    | Mean     | Gen0   | Allocated |
|-------------------------- |---------:|-------:|----------:|
| &#39;MemoryStream (baseline)&#39; | 180.4 ns |      - |         - |
| Direct:Stream             | 149.5 ns | 0.0050 |      64 B |
| Derived:MemoryStream      | 148.1 ns | 0.0081 |     104 B |
