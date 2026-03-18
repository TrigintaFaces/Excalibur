```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                  | EventHandlerCount | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------ |------------------ |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **&#39;Event dispatch (warm)&#39;** | **1**                 |    **55.40 ns** |  **0.329 ns** |  **0.172 ns** |  **1.00** |    **0.00** | **0.0127** |     **240 B** |        **1.00** |
| &#39;Event dispatch (cold)&#39; | 1                 |    57.14 ns |  2.240 ns |  1.172 ns |  1.03 |    0.02 | 0.0127 |     240 B |        1.00 |
|                         |                   |             |           |           |       |         |        |           |             |
| **&#39;Event dispatch (warm)&#39;** | **3**                 |   **125.20 ns** |  **1.006 ns** |  **0.526 ns** |  **1.00** |    **0.01** | **0.0153** |     **288 B** |        **1.00** |
| &#39;Event dispatch (cold)&#39; | 3                 |   130.52 ns |  5.887 ns |  3.079 ns |  1.04 |    0.02 | 0.0153 |     288 B |        1.00 |
|                         |                   |             |           |           |       |         |        |           |             |
| **&#39;Event dispatch (warm)&#39;** | **10**                |   **319.39 ns** |  **3.774 ns** |  **1.974 ns** |  **1.00** |    **0.01** | **0.0238** |     **456 B** |        **1.00** |
| &#39;Event dispatch (cold)&#39; | 10                |   329.53 ns | 12.778 ns |  6.683 ns |  1.03 |    0.02 | 0.0238 |     456 B |        1.00 |
|                         |                   |             |           |           |       |         |        |           |             |
| **&#39;Event dispatch (warm)&#39;** | **50**                | **1,838.12 ns** | **20.412 ns** | **10.676 ns** |  **1.00** |    **0.01** | **0.0744** |    **1416 B** |        **1.00** |
| &#39;Event dispatch (cold)&#39; | 50                | 1,856.19 ns | 48.436 ns | 25.333 ns |  1.01 |    0.01 | 0.0744 |    1416 B |        1.00 |
