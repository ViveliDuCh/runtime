```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7985) (Hyper-V)
Unknown processor
.NET SDK 11.0.100-preview.3.26170.106
  [Host]     : .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2
  DefaultJob : .NET 11.0.0 (11.0.26.17106), X64 RyuJIT AVX2


```
| Method               | Distribution  | Mean       | Error   | StdDev  | Ratio | Allocated | Alloc Ratio |
|--------------------- |-------------- |-----------:|--------:|--------:|------:|----------:|------------:|
| **Current_UIntLog10**    | **Large_100_255** | **1,190.0 ns** | **2.00 ns** | **1.87 ns** |  **1.00** |         **-** |          **NA** |
| Proposed_IfElseChain | Large_100_255 |   649.4 ns | 0.93 ns | 0.87 ns |  0.55 |         - |          NA |
|                      |               |            |         |         |       |           |             |
| **Current_UIntLog10**    | **Medium_10_99**  | **1,213.8 ns** | **1.80 ns** | **1.50 ns** |  **1.00** |         **-** |          **NA** |
| Proposed_IfElseChain | Medium_10_99  |   648.5 ns | 0.89 ns | 0.84 ns |  0.53 |         - |          NA |
|                      |               |            |         |         |       |           |             |
| **Current_UIntLog10**    | **Mixed**         | **1,191.4 ns** | **1.19 ns** | **1.00 ns** |  **1.00** |         **-** |          **NA** |
| Proposed_IfElseChain | Mixed         | 1,357.9 ns | 3.83 ns | 3.59 ns |  1.14 |         - |          NA |
|                      |               |            |         |         |       |           |             |
| **Current_UIntLog10**    | **Small_0_9**     | **1,198.8 ns** | **1.97 ns** | **1.84 ns** |  **1.00** |         **-** |          **NA** |
| Proposed_IfElseChain | Small_0_9     |   330.0 ns | 0.18 ns | 0.14 ns |  0.28 |         - |          NA |
