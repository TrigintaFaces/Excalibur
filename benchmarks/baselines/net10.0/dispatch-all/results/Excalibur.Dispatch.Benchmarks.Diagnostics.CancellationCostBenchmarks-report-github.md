```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                                | CallbackCount | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------------------------------- |-------------- |------------:|-----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| **&#39;Pre-canceled dispatch (throw path)&#39;**  | **1**             | **2,157.51 ns** |  **21.699 ns** | **11.349 ns** |  **1.00** |    **0.01** | **0.0381** |      **-** |     **760 B** |        **1.00** |
| &#39;Pre-canceled dispatch (result path)&#39; | 1             |    86.57 ns |   3.700 ns |  1.643 ns |  0.04 |    0.00 | 0.0267 |      - |     504 B |        0.66 |
| &#39;In-flight dispatch cancellation&#39;     | 1             | 4,367.42 ns |  39.457 ns | 20.637 ns |  2.02 |    0.01 | 0.1221 |      - |    2561 B |        3.37 |
| &#39;CTS cancel callback fan-out&#39;         | 1             |    76.85 ns |   1.135 ns |  0.594 ns |  0.04 |    0.00 | 0.0174 |      - |     328 B |        0.43 |
|                                       |               |             |            |           |       |         |        |        |           |             |
| **&#39;Pre-canceled dispatch (throw path)&#39;**  | **8**             | **2,165.46 ns** |  **19.977 ns** |  **8.870 ns** |  **1.00** |    **0.01** | **0.0381** |      **-** |     **760 B** |        **1.00** |
| &#39;Pre-canceled dispatch (result path)&#39; | 8             |    81.40 ns |   2.622 ns |  1.164 ns |  0.04 |    0.00 | 0.0267 |      - |     504 B |        0.66 |
| &#39;In-flight dispatch cancellation&#39;     | 8             | 4,391.65 ns | 184.905 ns | 96.709 ns |  2.03 |    0.04 | 0.1297 |      - |    2560 B |        3.37 |
| &#39;CTS cancel callback fan-out&#39;         | 8             |   315.63 ns |  17.860 ns |  9.341 ns |  0.15 |    0.00 | 0.0529 |      - |    1000 B |        1.32 |
|                                       |               |             |            |           |       |         |        |        |           |             |
| **&#39;Pre-canceled dispatch (throw path)&#39;**  | **32**            | **2,115.07 ns** |  **48.268 ns** | **21.431 ns** |  **1.00** |    **0.01** | **0.0381** |      **-** |     **760 B** |        **1.00** |
| &#39;Pre-canceled dispatch (result path)&#39; | 32            |    78.85 ns |   6.128 ns |  3.205 ns |  0.04 |    0.00 | 0.0267 |      - |     504 B |        0.66 |
| &#39;In-flight dispatch cancellation&#39;     | 32            | 4,385.91 ns |  38.056 ns | 19.904 ns |  2.07 |    0.02 | 0.1297 |      - |    2560 B |        3.37 |
| &#39;CTS cancel callback fan-out&#39;         | 32            | 1,107.74 ns |  21.165 ns | 11.070 ns |  0.52 |    0.01 | 0.1755 | 0.0019 |    3304 B |        4.35 |
