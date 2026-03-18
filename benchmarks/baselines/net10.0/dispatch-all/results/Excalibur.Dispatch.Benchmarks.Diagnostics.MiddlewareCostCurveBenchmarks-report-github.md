```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                            | MiddlewareCount | Scenario | CacheHit | Mean        | Error      | StdDev     | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------------------- |---------------- |--------- |--------- |------------:|-----------:|-----------:|------:|--------:|-------:|-------:|----------:|------------:|
| **Dispatch_WithConfiguredMiddleware** | **0**               | **Command**  | **False**    |    **61.22 ns** |   **7.905 ns** |   **3.510 ns** |  **1.00** |    **0.07** | **0.0178** |      **-** |     **336 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **0**               | **Command**  | **True**     |   **101.46 ns** |  **17.447 ns** |   **7.746 ns** |  **1.00** |    **0.10** | **0.0305** |      **-** |     **576 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **0**               | **Query**    | **False**    |    **85.45 ns** |   **7.166 ns** |   **3.748 ns** |  **1.00** |    **0.06** | **0.0280** |      **-** |     **528 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **0**               | **Query**    | **True**     |   **130.04 ns** |  **14.689 ns** |   **6.522 ns** |  **1.00** |    **0.06** | **0.0408** |      **-** |     **768 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **0**               | **Event**    | **False**    |   **151.09 ns** |  **21.703 ns** |  **11.351 ns** |  **1.00** |    **0.10** | **0.0191** |      **-** |     **360 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **0**               | **Event**    | **True**     |   **134.14 ns** |   **3.976 ns** |   **1.418 ns** |  **1.00** |    **0.01** | **0.0191** |      **-** |     **360 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **1**               | **Command**  | **False**    |   **368.06 ns** | **157.227 ns** |  **82.233 ns** |  **1.04** |    **0.31** | **0.0410** |      **-** |     **776 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **1**               | **Command**  | **True**     |   **417.32 ns** | **182.165 ns** |  **95.276 ns** |  **1.05** |    **0.34** | **0.0558** |      **-** |    **1056 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **1**               | **Query**    | **False**    |   **476.73 ns** | **251.318 ns** | **131.444 ns** |  **1.07** |    **0.40** | **0.0548** |      **-** |    **1040 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **1**               | **Query**    | **True**     |   **359.45 ns** | **114.031 ns** |  **59.641 ns** |  **1.02** |    **0.22** | **0.0558** |      **-** |    **1056 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **1**               | **Event**    | **False**    |   **351.47 ns** | **172.850 ns** |  **76.747 ns** |  **1.03** |    **0.27** | **0.0391** |      **-** |     **736 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **1**               | **Event**    | **True**     |   **603.36 ns** |  **27.087 ns** |  **14.167 ns** |  **1.00** |    **0.03** | **0.0391** |      **-** |     **736 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **3**               | **Command**  | **False**    |   **620.02 ns** | **188.082 ns** |  **98.370 ns** |  **1.02** |    **0.22** | **0.0639** |      **-** |    **1216 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **3**               | **Command**  | **True**     |   **415.49 ns** |  **24.456 ns** |  **12.791 ns** |  **1.00** |    **0.04** | **0.0663** |      **-** |    **1248 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **3**               | **Query**    | **False**    |   **542.05 ns** |  **35.499 ns** |  **18.567 ns** |  **1.00** |    **0.05** | **0.0782** |      **-** |    **1480 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **3**               | **Query**    | **True**     |   **395.22 ns** |  **46.239 ns** |  **20.531 ns** |  **1.00** |    **0.07** | **0.0663** |      **-** |    **1248 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **3**               | **Event**    | **False**    |   **423.95 ns** |  **42.629 ns** |  **22.296 ns** |  **1.00** |    **0.07** | **0.0625** |      **-** |    **1176 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **3**               | **Event**    | **True**     |   **525.61 ns** | **188.993 ns** |  **83.914 ns** |  **1.02** |    **0.20** | **0.0625** |      **-** |    **1176 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **5**               | **Command**  | **False**    |   **508.31 ns** |  **43.579 ns** |  **22.793 ns** |  **1.00** |    **0.06** | **0.0744** |      **-** |    **1408 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **5**               | **Command**  | **True**     |   **540.14 ns** |  **33.876 ns** |  **17.718 ns** |  **1.00** |    **0.04** | **0.1040** |      **-** |    **1968 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **5**               | **Query**    | **False**    | **1,056.50 ns** |  **99.724 ns** |  **52.158 ns** |  **1.00** |    **0.07** | **0.0877** |      **-** |    **1672 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **5**               | **Query**    | **True**     |   **902.80 ns** |  **73.139 ns** |  **38.253 ns** |  **1.00** |    **0.06** | **0.1040** |      **-** |    **1968 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **5**               | **Event**    | **False**    |   **889.62 ns** |  **21.878 ns** |  **11.443 ns** |  **1.00** |    **0.02** | **0.0725** |      **-** |    **1368 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **5**               | **Event**    | **True**     |   **827.39 ns** |  **19.196 ns** |   **8.523 ns** |  **1.00** |    **0.01** | **0.0725** |      **-** |    **1368 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **10**              | **Command**  | **False**    | **1,168.76 ns** | **118.705 ns** |  **62.085 ns** |  **1.00** |    **0.07** | **0.1278** |      **-** |    **2416 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **10**              | **Command**  | **True**     |   **706.78 ns** |  **27.765 ns** |  **12.328 ns** |  **1.00** |    **0.02** | **0.1297** | **0.0010** |    **2448 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **10**              | **Query**    | **False**    | **1,027.15 ns** | **604.848 ns** | **316.347 ns** |  **1.08** |    **0.42** | **0.1421** | **0.0010** |    **2680 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **10**              | **Query**    | **True**     |   **973.34 ns** | **442.746 ns** | **231.565 ns** |  **1.05** |    **0.32** | **0.1297** |      **-** |    **2448 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **10**              | **Event**    | **False**    |   **724.67 ns** | **123.013 ns** |  **64.338 ns** |  **1.01** |    **0.12** | **0.1259** | **0.0010** |    **2376 B** |        **1.00** |
|                                   |                 |          |          |             |            |            |       |         |        |        |           |             |
| **Dispatch_WithConfiguredMiddleware** | **10**              | **Event**    | **True**     |   **802.14 ns** |  **43.025 ns** |  **22.503 ns** |  **1.00** |    **0.04** | **0.1259** | **0.0010** |    **2376 B** |        **1.00** |
