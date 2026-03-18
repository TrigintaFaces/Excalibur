```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-cold  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=20  IterationTime=100ms  LaunchCount=1  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=0  

```
| Method                           | EventHandlerCount | Mean       | Error        | StdDev       | Median   | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------------------- |------------------ |-----------:|-------------:|-------------:|---------:|------:|--------:|----------:|------------:|
| **&#39;Cold: fixture build only&#39;**       | **1**                 | **3,560.0 μs** | **12,062.46 μs** | **13,891.15 μs** | **296.9 μs** | **10.71** |   **43.10** | **177.34 KB** |        **1.00** |
| &#39;Cold: first handler lookup&#39;     | 1                 |   329.8 μs |    107.42 μs |    123.70 μs | 297.6 μs |  0.99 |    0.50 |  182.6 KB |        1.03 |
| &#39;Cold: first handler activation&#39; | 1                 |   400.3 μs |    373.94 μs |    430.63 μs | 296.0 μs |  1.20 |    1.39 | 184.71 KB |        1.04 |
| &#39;Cold: first handler invoke&#39;     | 1                 |   391.2 μs |    323.52 μs |    372.57 μs | 297.7 μs |  1.18 |    1.22 | 184.49 KB |        1.04 |
|                                  |                   |            |              |              |          |       |         |           |             |
| **&#39;Cold: fixture build only&#39;**       | **10**                |   **580.0 μs** |    **763.43 μs** |    **879.17 μs** | **356.8 μs** |  **1.50** |    **2.33** | **204.24 KB** |        **1.00** |
| &#39;Cold: first handler lookup&#39;     | 10                |   348.6 μs |     66.95 μs |     77.10 μs | 331.6 μs |  0.90 |    0.31 | 204.94 KB |        1.00 |
| &#39;Cold: first handler activation&#39; | 10                |   348.3 μs |     28.47 μs |     32.79 μs | 337.9 μs |  0.90 |    0.26 | 206.74 KB |        1.01 |
| &#39;Cold: first handler invoke&#39;     | 10                |   398.5 μs |    109.33 μs |    125.90 μs | 377.9 μs |  1.03 |    0.43 | 206.55 KB |        1.01 |
|                                  |                   |            |              |              |          |       |         |           |             |
| **&#39;Cold: fixture build only&#39;**       | **50**                |   **924.0 μs** |  **1,660.89 μs** |  **1,912.68 μs** | **468.1 μs** |  **1.84** |    **3.86** | **313.88 KB** |        **1.00** |
| &#39;Cold: first handler lookup&#39;     | 50                |   499.0 μs |     41.61 μs |     47.92 μs | 488.1 μs |  0.99 |    0.27 | 314.06 KB |        1.00 |
| &#39;Cold: first handler activation&#39; | 50                |   543.3 μs |     80.65 μs |     92.88 μs | 528.3 μs |  1.08 |    0.33 | 315.56 KB |        1.01 |
| &#39;Cold: first handler invoke&#39;     | 50                |   545.4 μs |    126.54 μs |    145.72 μs | 513.7 μs |  1.08 |    0.40 | 316.49 KB |        1.01 |
