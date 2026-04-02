```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7985) (Hyper-V)
Unknown processor
.NET SDK 11.0.100-preview.3.26170.106
  [Host]     : .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
  DefaultJob : .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2


```
| Method                 | Distribution    | Mean        | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------------------- |---------------- |------------:|---------:|---------:|------:|--------:|----------:|------------:|
| **Lemire_CountDigits**     | **Large_1M_1B**     |    **889.6 ns** |  **1.62 ns** |  **1.43 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Log10Plus1_UInt        | Large_1M_1B     |  1,230.9 ns |  1.02 ns |  0.86 ns |  1.38 |    0.00 |         - |          NA |
| DivideLoop_CountDigits | Large_1M_1B     |  9,499.0 ns | 12.24 ns | 11.45 ns | 10.68 |    0.02 |         - |          NA |
| Log10Plus1_Int         | Large_1M_1B     |  1,231.1 ns |  1.59 ns |  1.49 ns |  1.38 |    0.00 |         - |          NA |
|                        |                 |             |          |          |       |         |           |             |
| **Lemire_CountDigits**     | **Medium_100_9999** |    **884.4 ns** |  **0.60 ns** |  **0.56 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Log10Plus1_UInt        | Medium_100_9999 |  1,195.1 ns |  1.52 ns |  1.34 ns |  1.35 |    0.00 |         - |          NA |
| DivideLoop_CountDigits | Medium_100_9999 |  3,544.4 ns |  7.49 ns |  7.01 ns |  4.01 |    0.01 |         - |          NA |
| Log10Plus1_Int         | Medium_100_9999 |  1,194.6 ns |  1.31 ns |  1.09 ns |  1.35 |    0.00 |         - |          NA |
|                        |                 |             |          |          |       |         |           |             |
| **Lemire_CountDigits**     | **Mixed**           |    **884.0 ns** |  **0.68 ns** |  **0.61 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Log10Plus1_UInt        | Mixed           |  1,194.9 ns |  0.83 ns |  0.73 ns |  1.35 |    0.00 |         - |          NA |
| DivideLoop_CountDigits | Mixed           | 13,547.8 ns | 40.05 ns | 37.46 ns | 15.33 |    0.04 |         - |          NA |
| Log10Plus1_Int         | Mixed           |  1,179.7 ns |  1.27 ns |  1.19 ns |  1.33 |    0.00 |         - |          NA |
|                        |                 |             |          |          |       |         |           |             |
| **Lemire_CountDigits**     | **Small_1_9**       |    **884.2 ns** |  **1.15 ns** |  **1.08 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| Log10Plus1_UInt        | Small_1_9       |  1,194.6 ns |  1.06 ns |  0.82 ns |  1.35 |    0.00 |         - |          NA |
| DivideLoop_CountDigits | Small_1_9       |  1,024.2 ns |  0.60 ns |  0.50 ns |  1.16 |    0.00 |         - |          NA |
| Log10Plus1_Int         | Small_1_9       |  1,180.0 ns |  2.56 ns |  2.27 ns |  1.33 |    0.00 |         - |          NA |
