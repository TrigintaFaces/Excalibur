```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                        | HandlerLifetime | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------ |---------------- |----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **&#39;Resolve action handler&#39;**      | **Transient**       |  **6.897 ns** | **0.1518 ns** | **0.0794 ns** |  **1.00** |    **0.02** | **0.0013** |      **24 B** |        **1.00** |
| &#39;Dispatch command&#39;            | Transient       | 54.444 ns | 4.7806 ns | 2.5003 ns |  7.90 |    0.35 | 0.0127 |     240 B |       10.00 |
| &#39;Registry lookup (warm hit)&#39;  | Transient       |  3.759 ns | 0.0507 ns | 0.0265 ns |  0.55 |    0.01 |      - |         - |        0.00 |
| &#39;Registry lookup (cold miss)&#39; | Transient       |  7.075 ns | 0.1108 ns | 0.0579 ns |  1.03 |    0.01 |      - |         - |        0.00 |
|                               |                 |           |           |           |       |         |        |           |             |
| **&#39;Resolve action handler&#39;**      | **Scoped**          | **68.934 ns** | **1.7337 ns** | **0.7698 ns** |  **1.00** |    **0.01** | **0.0178** |     **336 B** |        **1.00** |
| &#39;Dispatch command&#39;            | Scoped          | 53.581 ns | 3.8773 ns | 2.0279 ns |  0.78 |    0.03 | 0.0127 |     240 B |        0.71 |
| &#39;Registry lookup (warm hit)&#39;  | Scoped          |  3.766 ns | 0.1214 ns | 0.0539 ns |  0.05 |    0.00 |      - |         - |        0.00 |
| &#39;Registry lookup (cold miss)&#39; | Scoped          |  7.012 ns | 0.0976 ns | 0.0433 ns |  0.10 |    0.00 |      - |         - |        0.00 |
|                               |                 |           |           |           |       |         |        |           |             |
| **&#39;Resolve action handler&#39;**      | **Singleton**       |  **5.659 ns** | **0.1654 ns** | **0.0865 ns** |  **1.00** |    **0.02** |      **-** |         **-** |          **NA** |
| &#39;Dispatch command&#39;            | Singleton       | 52.924 ns | 0.9015 ns | 0.4003 ns |  9.35 |    0.15 | 0.0127 |     240 B |          NA |
| &#39;Registry lookup (warm hit)&#39;  | Singleton       |  3.882 ns | 0.0829 ns | 0.0434 ns |  0.69 |    0.01 |      - |         - |          NA |
| &#39;Registry lookup (cold miss)&#39; | Singleton       |  7.091 ns | 0.0732 ns | 0.0383 ns |  1.25 |    0.02 |      - |         - |          NA |
