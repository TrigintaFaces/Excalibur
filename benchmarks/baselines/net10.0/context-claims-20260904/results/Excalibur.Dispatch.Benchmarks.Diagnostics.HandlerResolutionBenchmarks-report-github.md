```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                        | HandlerLifetime | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------ |---------------- |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **&#39;Resolve action handler&#39;**      | **Transient**       |   **7.045 ns** | **0.2076 ns** | **0.1086 ns** |  **1.00** |    **0.02** | **0.0013** |      **24 B** |        **1.00** |
| &#39;Dispatch command&#39;            | Transient       |  39.428 ns | 1.8542 ns | 0.9698 ns |  5.60 |    0.15 | 0.0114 |     216 B |        9.00 |
| &#39;Registry lookup (warm hit)&#39;  | Transient       |   4.116 ns | 0.1921 ns | 0.0853 ns |  0.58 |    0.01 |      - |         - |        0.00 |
| &#39;Registry lookup (cold miss)&#39; | Transient       |   6.347 ns | 0.0650 ns | 0.0340 ns |  0.90 |    0.01 |      - |         - |        0.00 |
|                               |                 |            |           |           |       |         |        |           |             |
| **&#39;Resolve action handler&#39;**      | **Scoped**          |  **69.761 ns** | **7.2416 ns** | **3.7875 ns** |  **1.00** |    **0.07** | **0.0178** |     **336 B** |        **1.00** |
| &#39;Dispatch command&#39;            | Scoped          | 144.023 ns | 1.3071 ns | 0.5804 ns |  2.07 |    0.10 | 0.0234 |     440 B |        1.31 |
| &#39;Registry lookup (warm hit)&#39;  | Scoped          |   4.089 ns | 0.0405 ns | 0.0212 ns |  0.06 |    0.00 |      - |         - |        0.00 |
| &#39;Registry lookup (cold miss)&#39; | Scoped          |   6.393 ns | 0.1388 ns | 0.0726 ns |  0.09 |    0.00 |      - |         - |        0.00 |
|                               |                 |            |           |           |       |         |        |           |             |
| **&#39;Resolve action handler&#39;**      | **Singleton**       |   **5.501 ns** | **0.0444 ns** | **0.0197 ns** |  **1.00** |    **0.00** |      **-** |         **-** |          **NA** |
| &#39;Dispatch command&#39;            | Singleton       |  54.108 ns | 1.8527 ns | 0.9690 ns |  9.84 |    0.17 | 0.0127 |     240 B |          NA |
| &#39;Registry lookup (warm hit)&#39;  | Singleton       |   4.088 ns | 0.0422 ns | 0.0221 ns |  0.74 |    0.00 |      - |         - |          NA |
| &#39;Registry lookup (cold miss)&#39; | Singleton       |   6.405 ns | 0.1847 ns | 0.0820 ns |  1.16 |    0.01 |      - |         - |          NA |
