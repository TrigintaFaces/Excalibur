```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                                   | LoggingMode | FilterMode      | DelayMode     | Mean          | Error         | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------------------------- |------------ |---------------- |-------------- |--------------:|--------------:|------------:|------:|--------:|-------:|----------:|------------:|
| **&#39;Retry success after transient failures&#39;** | **Disabled**    | **None**            | **ZeroDelay**     |      **1.819 μs** |     **0.0420 μs** |   **0.0186 μs** |  **1.00** |    **0.01** | **0.0267** |     **520 B** |        **1.00** |
| &#39;Retry exhausted failure path&#39;           | Disabled    | None            | ZeroDelay     |      6.238 μs |     0.0375 μs |   0.0196 μs |  3.43 |    0.03 | 0.1144 |    2208 B |        4.25 |
|                                          |             |                 |               |               |               |             |       |         |        |           |             |
| **&#39;Retry success after transient failures&#39;** | **Disabled**    | **None**            | **FixedDelay1Ms** | **29,922.111 μs** |   **762.1864 μs** | **398.6382 μs** |  **1.00** |    **0.02** |      **-** |    **1248 B** |        **1.00** |
| &#39;Retry exhausted failure path&#39;           | Disabled    | None            | FixedDelay1Ms | 29,879.251 μs | 1,213.9238 μs | 634.9056 μs |  1.00 |    0.02 |      - |    2947 B |        2.36 |
|                                          |             |                 |               |               |               |             |       |         |        |           |             |
| **&#39;Retry success after transient failures&#39;** | **Disabled**    | **RetriableOnly**   | **ZeroDelay**     |      **1.839 μs** |     **0.0168 μs** |   **0.0088 μs** |  **1.00** |    **0.01** | **0.0267** |     **520 B** |        **1.00** |
| &#39;Retry exhausted failure path&#39;           | Disabled    | RetriableOnly   | ZeroDelay     |      6.332 μs |     0.0804 μs |   0.0421 μs |  3.44 |    0.03 | 0.1144 |    2208 B |        4.25 |
|                                          |             |                 |               |               |               |             |       |         |        |           |             |
| **&#39;Retry success after transient failures&#39;** | **Disabled**    | **RetriableOnly**   | **FixedDelay1Ms** | **30,224.848 μs** | **1,281.3738 μs** | **670.1833 μs** |  **1.00** |    **0.03** |      **-** |    **1323 B** |        **1.00** |
| &#39;Retry exhausted failure path&#39;           | Disabled    | RetriableOnly   | FixedDelay1Ms | 29,580.400 μs |   946.5988 μs | 495.0895 μs |  0.98 |    0.03 |      - |    2956 B |        2.23 |
|                                          |             |                 |               |               |               |             |       |         |        |           |             |
| **&#39;Retry success after transient failures&#39;** | **Disabled**    | **NonRetriableSet** | **ZeroDelay**     |      **1.866 μs** |     **0.0184 μs** |   **0.0096 μs** |  **1.00** |    **0.01** | **0.0267** |     **520 B** |        **1.00** |
| &#39;Retry exhausted failure path&#39;           | Disabled    | NonRetriableSet | ZeroDelay     |      6.259 μs |     0.0446 μs |   0.0233 μs |  3.35 |    0.02 | 0.1144 |    2208 B |        4.25 |
|                                          |             |                 |               |               |               |             |       |         |        |           |             |
| **&#39;Retry success after transient failures&#39;** | **Disabled**    | **NonRetriableSet** | **FixedDelay1Ms** | **30,039.008 μs** | **1,058.6054 μs** | **553.6711 μs** |  **1.00** |    **0.02** |      **-** |    **1272 B** |        **1.00** |
| &#39;Retry exhausted failure path&#39;           | Disabled    | NonRetriableSet | FixedDelay1Ms | 29,985.598 μs | 1,364.3150 μs | 713.5631 μs |  1.00 |    0.03 |      - |    2956 B |        2.32 |
|                                          |             |                 |               |               |               |             |       |         |        |           |             |
| **&#39;Retry success after transient failures&#39;** | **Enabled**     | **None**            | **ZeroDelay**     |      **1.834 μs** |     **0.0171 μs** |   **0.0089 μs** |  **1.00** |    **0.01** | **0.0267** |     **520 B** |        **1.00** |
| &#39;Retry exhausted failure path&#39;           | Enabled     | None            | ZeroDelay     |      6.365 μs |     0.0506 μs |   0.0225 μs |  3.47 |    0.02 | 0.1144 |    2208 B |        4.25 |
|                                          |             |                 |               |               |               |             |       |         |        |           |             |
| **&#39;Retry success after transient failures&#39;** | **Enabled**     | **None**            | **FixedDelay1Ms** | **30,079.196 μs** | **1,087.5859 μs** | **482.8949 μs** |  **1.00** |    **0.02** |      **-** |    **1332 B** |        **1.00** |
| &#39;Retry exhausted failure path&#39;           | Enabled     | None            | FixedDelay1Ms | 29,735.596 μs | 1,857.7381 μs | 971.6330 μs |  0.99 |    0.03 |      - |    2926 B |        2.20 |
|                                          |             |                 |               |               |               |             |       |         |        |           |             |
| **&#39;Retry success after transient failures&#39;** | **Enabled**     | **RetriableOnly**   | **ZeroDelay**     |      **1.857 μs** |     **0.0157 μs** |   **0.0070 μs** |  **1.00** |    **0.00** | **0.0267** |     **520 B** |        **1.00** |
| &#39;Retry exhausted failure path&#39;           | Enabled     | RetriableOnly   | ZeroDelay     |      6.399 μs |     0.1421 μs |   0.0743 μs |  3.45 |    0.04 | 0.1144 |    2208 B |        4.25 |
|                                          |             |                 |               |               |               |             |       |         |        |           |             |
| **&#39;Retry success after transient failures&#39;** | **Enabled**     | **RetriableOnly**   | **FixedDelay1Ms** | **29,610.028 μs** |   **937.8740 μs** | **490.5263 μs** |  **1.00** |    **0.02** |      **-** |    **1332 B** |        **1.00** |
| &#39;Retry exhausted failure path&#39;           | Enabled     | RetriableOnly   | FixedDelay1Ms | 29,635.229 μs | 1,886.6720 μs | 986.7659 μs |  1.00 |    0.04 |      - |    2956 B |        2.22 |
|                                          |             |                 |               |               |               |             |       |         |        |           |             |
| **&#39;Retry success after transient failures&#39;** | **Enabled**     | **NonRetriableSet** | **ZeroDelay**     |      **1.867 μs** |     **0.0323 μs** |   **0.0169 μs** |  **1.00** |    **0.01** | **0.0267** |     **520 B** |        **1.00** |
| &#39;Retry exhausted failure path&#39;           | Enabled     | NonRetriableSet | ZeroDelay     |      6.456 μs |     0.0670 μs |   0.0298 μs |  3.46 |    0.03 | 0.1144 |    2208 B |        4.25 |
|                                          |             |                 |               |               |               |             |       |         |        |           |             |
| **&#39;Retry success after transient failures&#39;** | **Enabled**     | **NonRetriableSet** | **FixedDelay1Ms** | **30,836.361 μs** |   **490.7114 μs** | **256.6515 μs** |  **1.00** |    **0.01** |      **-** |    **1240 B** |        **1.00** |
| &#39;Retry exhausted failure path&#39;           | Enabled     | NonRetriableSet | FixedDelay1Ms | 29,528.838 μs |   736.1261 μs | 326.8445 μs |  0.96 |    0.01 |      - |    2947 B |        2.38 |
