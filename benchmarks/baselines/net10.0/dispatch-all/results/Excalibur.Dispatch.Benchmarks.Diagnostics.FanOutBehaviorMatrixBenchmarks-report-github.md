```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                                 | HandlerCount | BehaviorMode  | Mean             | Error          | StdDev         | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------------------------- |------------- |-------------- |-----------------:|---------------:|---------------:|------:|--------:|-------:|----------:|------------:|
| **&#39;Fan-out dispatch by handler behavior&#39;** | **1**            | **CompletedTask** |         **56.05 ns** |       **1.063 ns** |       **0.472 ns** |  **1.00** |    **0.01** | **0.0126** |     **240 B** |        **1.00** |
|                                        |              |               |                  |                |                |       |         |        |           |             |
| **&#39;Fan-out dispatch by handler behavior&#39;** | **1**            | **TaskYield**     |        **758.77 ns** |       **6.994 ns** |       **3.658 ns** |  **1.00** |    **0.01** | **0.0324** |     **621 B** |        **1.00** |
|                                        |              |               |                  |                |                |       |         |        |           |             |
| **&#39;Fan-out dispatch by handler behavior&#39;** | **1**            | **ShortIoDelay**  | **14,870,298.21 ns** | **252,061.580 ns** | **111,916.904 ns** |  **1.00** |    **0.01** |      **-** |     **942 B** |        **1.00** |
|                                        |              |               |                  |                |                |       |         |        |           |             |
| **&#39;Fan-out dispatch by handler behavior&#39;** | **10**           | **CompletedTask** |        **317.25 ns** |       **2.442 ns** |       **1.277 ns** |  **1.00** |    **0.01** | **0.0238** |     **456 B** |        **1.00** |
|                                        |              |               |                  |                |                |       |         |        |           |             |
| **&#39;Fan-out dispatch by handler behavior&#39;** | **10**           | **TaskYield**     |      **3,799.99 ns** |      **27.114 ns** |      **14.181 ns** |  **1.00** |    **0.00** | **0.0916** |    **1751 B** |        **1.00** |
|                                        |              |               |                  |                |                |       |         |        |           |             |
| **&#39;Fan-out dispatch by handler behavior&#39;** | **10**           | **ShortIoDelay**  | **14,810,165.36 ns** | **153,721.879 ns** |  **54,818.703 ns** |  **1.00** |    **0.00** |      **-** |    **3735 B** |        **1.00** |
|                                        |              |               |                  |                |                |       |         |        |           |             |
| **&#39;Fan-out dispatch by handler behavior&#39;** | **50**           | **CompletedTask** |      **1,823.58 ns** |      **38.096 ns** |      **19.925 ns** |  **1.00** |    **0.01** | **0.0744** |    **1416 B** |        **1.00** |
|                                        |              |               |                  |                |                |       |         |        |           |             |
| **&#39;Fan-out dispatch by handler behavior&#39;** | **50**           | **TaskYield**     |     **18,450.08 ns** |     **420.613 ns** |     **219.989 ns** |  **1.00** |    **0.02** | **0.3662** |    **7075 B** |        **1.00** |
|                                        |              |               |                  |                |                |       |         |        |           |             |
| **&#39;Fan-out dispatch by handler behavior&#39;** | **50**           | **ShortIoDelay**  | **15,312,026.76 ns** | **353,232.826 ns** | **184,747.598 ns** |  **1.00** |    **0.02** |      **-** |   **16214 B** |        **1.00** |
