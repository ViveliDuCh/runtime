```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7985) (Hyper-V)
Unknown processor
.NET SDK 10.0.104
  [Host]     : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.4 (10.0.426.12010), X64 RyuJIT AVX2


```
| Type                    | Method                        | Distribution      | Mean      | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|------------------------ |------------------------------ |------------------ |----------:|----------:|----------:|------:|----------:|------------:|
| **CountDigitsULongBench**   | **Fmtlib_CountDigits**            | **Large_1e15_Max**    |  **1.274 μs** | **0.0024 μs** | **0.0021 μs** |  **1.00** |         **-** |          **NA** |
| CountDigitsULongBench   | Log10Plus1_ULong              | Large_1e15_Max    |  1.426 μs | 0.0022 μs | 0.0020 μs |  1.12 |         - |          NA |
|                         |                               |                   |           |           |           |       |           |             |
| **CountDigitsUInt128Bench** | **FormattingHelpers_CountDigits** | **Large_full_range**  | **33.065 μs** | **0.2291 μs** | **0.2143 μs** |  **1.00** |         **-** |          **NA** |
| CountDigitsUInt128Bench | Log10Plus1_UInt128            | Large_full_range  |  2.266 μs | 0.0218 μs | 0.0193 μs |  0.07 |         - |          NA |
|                         |                               |                   |           |           |           |       |           |             |
| **CountDigitsULongBench**   | **Fmtlib_CountDigits**            | **Medium_1M_1B**      |  **1.272 μs** | **0.0032 μs** | **0.0029 μs** |  **1.00** |         **-** |          **NA** |
| CountDigitsULongBench   | Log10Plus1_ULong              | Medium_1M_1B      |  1.447 μs | 0.0014 μs | 0.0011 μs |  1.14 |         - |          NA |
|                         |                               |                   |           |           |           |       |           |             |
| **CountDigitsULongBench**   | **Fmtlib_CountDigits**            | **Mixed**             |  **1.272 μs** | **0.0013 μs** | **0.0011 μs** |  **1.00** |         **-** |          **NA** |
| CountDigitsULongBench   | Log10Plus1_ULong              | Mixed             |  1.419 μs | 0.0021 μs | 0.0020 μs |  1.12 |         - |          NA |
|                         |                               |                   |           |           |           |       |           |             |
| **CountDigitsULongBench**   | **Fmtlib_CountDigits**            | **Small_1_999**       |  **1.276 μs** | **0.0062 μs** | **0.0055 μs** |  **1.00** |         **-** |          **NA** |
| CountDigitsULongBench   | Log10Plus1_ULong              | Small_1_999       |  1.540 μs | 0.0017 μs | 0.0015 μs |  1.21 |         - |          NA |
|                         |                               |                   |           |           |           |       |           |             |
| **CountDigitsUInt128Bench** | **FormattingHelpers_CountDigits** | **Small_ulong_range** |  **1.774 μs** | **0.0045 μs** | **0.0040 μs** |  **1.00** |         **-** |          **NA** |
| CountDigitsUInt128Bench | Log10Plus1_UInt128            | Small_ulong_range |  2.223 μs | 0.0212 μs | 0.0177 μs |  1.25 |         - |          NA |
