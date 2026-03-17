```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.7984)
Unknown processor
.NET SDK 11.0.100-preview.3.26160.119
  [Host]            : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX2
  ShortRun-.NET 9.0 : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX2

Job=ShortRun-.NET 9.0  Runtime=.NET 9.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Type                 | Method                 | Categories | Size   | Mean           | Error           | StdDev        | Ratio | RatioSD | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|--------------------- |----------------------- |----------- |------- |---------------:|----------------:|--------------:|------:|--------:|--------:|--------:|--------:|----------:|------------:|
| **AllocationBenchmarks** | **MemoryStream_Alloc**     | **Allocation** | **1000**   |       **6.973 ns** |       **6.9593 ns** |     **0.3815 ns** |  **1.00** |    **0.07** |  **0.0051** |       **-** |       **-** |      **64 B** |        **1.00** |
| AllocationBenchmarks | Dedicated_Alloc        | Allocation | 1000   |       6.395 ns |       5.1160 ns |     0.2804 ns |  0.92 |    0.06 |  0.0038 |       - |       - |      48 B |        0.75 |
| AllocationBenchmarks | Streamable_Alloc       | Allocation | 1000   |       8.031 ns |       5.7087 ns |     0.3129 ns |  1.15 |    0.07 |  0.0045 |       - |       - |      56 B |        0.88 |
|                      |                        |            |        |                |                 |               |       |         |         |         |         |           |             |
| **ReadBenchmarks**       | **MemoryStream_CopyTo**    | **CopyTo**     | **100**    |      **38.496 ns** |      **36.6400 ns** |     **2.0084 ns** |  **1.00** |    **0.06** |  **0.0325** |       **-** |       **-** |     **408 B** |        **1.00** |
| ReadBenchmarks       | Dedicated_CopyTo       | CopyTo     | 100    |      51.325 ns |      32.5426 ns |     1.7838 ns |  1.34 |    0.07 |  0.0312 |       - |       - |     392 B |        0.96 |
| ReadBenchmarks       | Streamable_CopyTo      | CopyTo     | 100    |      53.605 ns |      46.7618 ns |     2.5632 ns |  1.40 |    0.09 |  0.0318 |       - |       - |     400 B |        0.98 |
|                      |                        |            |        |                |                 |               |       |         |         |         |         |           |             |
| **ReadBenchmarks**       | **MemoryStream_CopyTo**    | **CopyTo**     | **100000** |  **46,362.974 ns** |  **33,206.4454 ns** | **1,820.1573 ns** |  **1.00** |    **0.05** | **31.1890** | **31.1890** | **31.1890** |  **100163 B** |        **1.00** |
| ReadBenchmarks       | Dedicated_CopyTo       | CopyTo     | 100000 |  41,599.041 ns |  13,618.5538 ns |   746.4789 ns |  0.90 |    0.03 | 31.1890 | 31.1890 | 31.1890 |  100147 B |        1.00 |
| ReadBenchmarks       | Streamable_CopyTo      | CopyTo     | 100000 |  40,770.292 ns |  12,425.0920 ns |   681.0612 ns |  0.88 |    0.03 | 31.1890 | 31.1890 | 31.1890 |  100155 B |        1.00 |
|                      |                        |            |        |                |                 |               |       |         |         |         |         |           |             |
| **ReadBenchmarks**       | **MemoryStream_ReadByte**  | **ReadByte**   | **100**    |      **84.801 ns** |       **6.1903 ns** |     **0.3393 ns** |  **1.00** |    **0.00** |  **0.0050** |       **-** |       **-** |      **64 B** |        **1.00** |
| ReadBenchmarks       | Dedicated_ReadByte     | ReadByte   | 100    |     180.716 ns |     208.0684 ns |    11.4049 ns |  2.13 |    0.12 |  0.0038 |       - |       - |      48 B |        0.75 |
| ReadBenchmarks       | Streamable_ReadByte    | ReadByte   | 100    |     177.852 ns |      64.9488 ns |     3.5601 ns |  2.10 |    0.04 |  0.0043 |       - |       - |      56 B |        0.88 |
|                      |                        |            |        |                |                 |               |       |         |         |         |         |           |             |
| **ReadBenchmarks**       | **MemoryStream_ReadByte**  | **ReadByte**   | **100000** |  **64,676.042 ns** |  **51,597.1233 ns** | **2,828.2124 ns** |  **1.00** |    **0.05** |       **-** |       **-** |       **-** |      **64 B** |        **1.00** |
| ReadBenchmarks       | Dedicated_ReadByte     | ReadByte   | 100000 | 155,491.260 ns | 117,541.7493 ns | 6,442.8599 ns |  2.41 |    0.13 |       - |       - |       - |      48 B |        0.75 |
| ReadBenchmarks       | Streamable_ReadByte    | ReadByte   | 100000 | 157,285.286 ns |  62,395.8805 ns | 3,420.1288 ns |  2.44 |    0.10 |       - |       - |       - |      56 B |        0.88 |
|                      |                        |            |        |                |                 |               |       |         |         |         |         |           |             |
| **ReadBenchmarks**       | **MemoryStream_ReadSpan**  | **ReadSpan**   | **100**    |       **9.999 ns** |       **5.8546 ns** |     **0.3209 ns** |  **1.00** |    **0.04** |  **0.0051** |       **-** |       **-** |      **64 B** |        **1.00** |
| ReadBenchmarks       | Dedicated_ReadSpan     | ReadSpan   | 100    |      10.681 ns |       0.7646 ns |     0.0419 ns |  1.07 |    0.03 |  0.0038 |       - |       - |      48 B |        0.75 |
| ReadBenchmarks       | Streamable_ReadSpan    | ReadSpan   | 100    |      10.680 ns |       6.7115 ns |     0.3679 ns |  1.07 |    0.04 |  0.0045 |       - |       - |      56 B |        0.88 |
|                      |                        |            |        |                |                 |               |       |         |         |         |         |           |             |
| **ReadBenchmarks**       | **MemoryStream_ReadSpan**  | **ReadSpan**   | **100000** |   **1,981.349 ns** |     **709.1657 ns** |    **38.8718 ns** |  **1.00** |    **0.02** |  **0.0038** |       **-** |       **-** |      **64 B** |        **1.00** |
| ReadBenchmarks       | Dedicated_ReadSpan     | ReadSpan   | 100000 |   2,179.592 ns |     540.6225 ns |    29.6333 ns |  1.10 |    0.02 |  0.0038 |       - |       - |      48 B |        0.75 |
| ReadBenchmarks       | Streamable_ReadSpan    | ReadSpan   | 100000 |   1,991.650 ns |     860.5586 ns |    47.1701 ns |  1.01 |    0.03 |  0.0038 |       - |       - |      56 B |        0.88 |
|                      |                        |            |        |                |                 |               |       |         |         |         |         |           |             |
| **WriteBenchmarks**      | **MemoryStream_WriteByte** | **WriteByte**  | **100**    |     **154.891 ns** |      **59.1102 ns** |     **3.2400 ns** |  **1.00** |    **0.03** |  **0.0050** |       **-** |       **-** |      **64 B** |        **1.00** |
| WriteBenchmarks      | Dedicated_WriteByte    | WriteByte  | 100    |     223.166 ns |     262.5945 ns |    14.3937 ns |  1.44 |    0.08 |  0.0043 |       - |       - |      56 B |        0.88 |
| WriteBenchmarks      | Streamable_WriteByte   | WriteByte  | 100    |     216.105 ns |      82.9365 ns |     4.5460 ns |  1.40 |    0.04 |  0.0043 |       - |       - |      56 B |        0.88 |
|                      |                        |            |        |                |                 |               |       |         |         |         |         |           |             |
| **WriteBenchmarks**      | **MemoryStream_WriteByte** | **WriteByte**  | **100000** | **125,077.238 ns** |  **42,089.1777 ns** | **2,307.0499 ns** |  **1.00** |    **0.02** |       **-** |       **-** |       **-** |      **64 B** |        **1.00** |
| WriteBenchmarks      | Dedicated_WriteByte    | WriteByte  | 100000 | 195,974.455 ns |  77,127.7157 ns | 4,227.6304 ns |  1.57 |    0.04 |       - |       - |       - |      56 B |        0.88 |
| WriteBenchmarks      | Streamable_WriteByte   | WriteByte  | 100000 | 207,694.157 ns |  56,748.1482 ns | 3,110.5575 ns |  1.66 |    0.03 |       - |       - |       - |      56 B |        0.88 |
|                      |                        |            |        |                |                 |               |       |         |         |         |         |           |             |
| **WriteBenchmarks**      | **MemoryStream_WriteSpan** | **WriteSpan**  | **100**    |      **10.369 ns** |       **4.1183 ns** |     **0.2257 ns** |  **1.00** |    **0.03** |  **0.0051** |       **-** |       **-** |      **64 B** |        **1.00** |
| WriteBenchmarks      | Dedicated_WriteSpan    | WriteSpan  | 100    |      10.258 ns |       7.5021 ns |     0.4112 ns |  0.99 |    0.04 |  0.0045 |       - |       - |      56 B |        0.88 |
| WriteBenchmarks      | Streamable_WriteSpan   | WriteSpan  | 100    |      10.775 ns |      10.4578 ns |     0.5732 ns |  1.04 |    0.05 |  0.0045 |       - |       - |      56 B |        0.88 |
|                      |                        |            |        |                |                 |               |       |         |         |         |         |           |             |
| **WriteBenchmarks**      | **MemoryStream_WriteSpan** | **WriteSpan**  | **100000** |   **1,972.440 ns** |     **880.6022 ns** |    **48.2688 ns** |  **1.00** |    **0.03** |  **0.0038** |       **-** |       **-** |      **64 B** |        **1.00** |
| WriteBenchmarks      | Dedicated_WriteSpan    | WriteSpan  | 100000 |   2,065.425 ns |   1,499.0781 ns |    82.1695 ns |  1.05 |    0.04 |  0.0038 |       - |       - |      56 B |        0.88 |
| WriteBenchmarks      | Streamable_WriteSpan   | WriteSpan  | 100000 |   2,076.786 ns |     357.0527 ns |    19.5713 ns |  1.05 |    0.02 |  0.0038 |       - |       - |      56 B |        0.88 |
