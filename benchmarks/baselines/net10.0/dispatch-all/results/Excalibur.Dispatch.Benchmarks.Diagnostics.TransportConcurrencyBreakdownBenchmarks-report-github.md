```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                                          | PayloadSizeBytes | Concurrency | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|------------------------------------------------ |----------------- |------------ |------------:|------------:|----------:|------:|--------:|-------:|-------:|-------:|----------:|------------:|
| **&#39;Transport publish path concurrent&#39;**             | **256**              | **1**           |  **9,190.9 ns** |    **71.81 ns** |  **37.56 ns** |  **1.00** |    **0.01** | **0.0153** |      **-** |      **-** |     **349 B** |        **1.00** |
| &#39;Transport receive path concurrent&#39;             | 256              | 1           |    289.1 ns |    21.08 ns |  11.03 ns |  0.03 |    0.00 | 0.0458 |      - |      - |     864 B |        2.48 |
| &#39;Transport publish+receive combined concurrent&#39; | 256              | 1           |  1,272.6 ns |    55.57 ns |  29.06 ns |  0.14 |    0.00 | 0.0610 | 0.0248 | 0.0019 |    1143 B |        3.28 |
|                                                 |                  |             |             |             |           |       |         |        |        |        |           |             |
| **&#39;Transport publish path concurrent&#39;**             | **256**              | **2**           | **10,075.4 ns** |   **205.29 ns** | **107.37 ns** |  **1.00** |    **0.01** | **0.0305** | **0.0153** |      **-** |     **702 B** |        **1.00** |
| &#39;Transport receive path concurrent&#39;             | 256              | 2           |    529.7 ns |     7.10 ns |   3.71 ns |  0.05 |    0.00 | 0.0830 |      - |      - |    1568 B |        2.23 |
| &#39;Transport publish+receive combined concurrent&#39; | 256              | 2           |  2,809.1 ns |   554.70 ns | 290.12 ns |  0.28 |    0.03 | 0.1144 | 0.0458 | 0.0038 |    2126 B |        3.03 |
|                                                 |                  |             |             |             |           |       |         |        |        |        |           |             |
| **&#39;Transport publish path concurrent&#39;**             | **256**              | **4**           | **10,944.3 ns** |   **235.40 ns** | **123.12 ns** |  **1.00** |    **0.01** | **0.0458** | **0.0305** |      **-** |    **1004 B** |        **1.00** |
| &#39;Transport receive path concurrent&#39;             | 256              | 4           |  1,066.4 ns |    22.40 ns |   9.95 ns |  0.10 |    0.00 | 0.1564 |      - |      - |    2976 B |        2.96 |
| &#39;Transport publish+receive combined concurrent&#39; | 256              | 4           |  5,272.8 ns |   112.97 ns |  50.16 ns |  0.48 |    0.01 | 0.2136 | 0.0839 | 0.0076 |    4085 B |        4.07 |
|                                                 |                  |             |             |             |           |       |         |        |        |        |           |             |
| **&#39;Transport publish path concurrent&#39;**             | **256**              | **8**           | **14,035.6 ns** |   **468.97 ns** | **245.28 ns** |  **1.00** |    **0.02** | **0.1068** | **0.0916** | **0.0153** |    **1889 B** |        **1.00** |
| &#39;Transport receive path concurrent&#39;             | 256              | 8           |  1,970.9 ns |    23.91 ns |  10.62 ns |  0.14 |    0.00 | 0.3052 |      - |      - |    5792 B |        3.07 |
| &#39;Transport publish+receive combined concurrent&#39; | 256              | 8           | 10,577.2 ns |   843.07 ns | 440.94 ns |  0.75 |    0.03 | 0.4272 | 0.1831 | 0.0153 |    8022 B |        4.25 |
|                                                 |                  |             |             |             |           |       |         |        |        |        |           |             |
| **&#39;Transport publish path concurrent&#39;**             | **256**              | **16**          | **19,089.4 ns** |   **498.42 ns** | **260.68 ns** |  **1.00** |    **0.02** | **0.1831** | **0.1526** | **0.0305** |    **3623 B** |        **1.00** |
| &#39;Transport receive path concurrent&#39;             | 256              | 16          |  4,128.9 ns |   121.53 ns |  63.56 ns |  0.22 |    0.00 | 0.6104 |      - |      - |   11496 B |        3.17 |
| &#39;Transport publish+receive combined concurrent&#39; | 256              | 16          | 20,134.7 ns |   590.12 ns | 262.02 ns |  1.05 |    0.02 | 0.8545 | 0.3357 | 0.0305 |   15971 B |        4.41 |
|                                                 |                  |             |             |             |           |       |         |        |        |        |           |             |
| **&#39;Transport publish path concurrent&#39;**             | **4096**             | **1**           |  **9,297.5 ns** |   **259.33 ns** | **135.63 ns** |  **1.00** |    **0.02** | **0.0153** |      **-** |      **-** |     **343 B** |        **1.00** |
| &#39;Transport receive path concurrent&#39;             | 4096             | 1           |    275.4 ns |     3.68 ns |   1.92 ns |  0.03 |    0.00 | 0.0458 |      - |      - |     864 B |        2.52 |
| &#39;Transport publish+receive combined concurrent&#39; | 4096             | 1           |  1,297.7 ns |   114.92 ns |  51.03 ns |  0.14 |    0.01 | 0.0610 | 0.0248 | 0.0019 |    1143 B |        3.33 |
|                                                 |                  |             |             |             |           |       |         |        |        |        |           |             |
| **&#39;Transport publish path concurrent&#39;**             | **4096**             | **2**           |  **9,882.9 ns** |    **61.28 ns** |  **32.05 ns** |  **1.00** |    **0.00** | **0.0305** | **0.0153** |      **-** |     **697 B** |        **1.00** |
| &#39;Transport receive path concurrent&#39;             | 4096             | 2           |    525.8 ns |     6.64 ns |   3.47 ns |  0.05 |    0.00 | 0.0830 |      - |      - |    1568 B |        2.25 |
| &#39;Transport publish+receive combined concurrent&#39; | 4096             | 2           |  2,568.4 ns |    86.50 ns |  38.41 ns |  0.26 |    0.00 | 0.1144 | 0.0458 | 0.0038 |    2129 B |        3.05 |
|                                                 |                  |             |             |             |           |       |         |        |        |        |           |             |
| **&#39;Transport publish path concurrent&#39;**             | **4096**             | **4**           | **10,882.4 ns** |    **86.18 ns** |  **30.73 ns** |  **1.00** |    **0.00** | **0.0458** | **0.0305** |      **-** |    **1001 B** |        **1.00** |
| &#39;Transport receive path concurrent&#39;             | 4096             | 4           |  1,037.8 ns |    15.77 ns |   7.00 ns |  0.10 |    0.00 | 0.1564 |      - |      - |    2976 B |        2.97 |
| &#39;Transport publish+receive combined concurrent&#39; | 4096             | 4           |  4,919.7 ns |   167.70 ns |  87.71 ns |  0.45 |    0.01 | 0.2136 | 0.0839 | 0.0076 |    4093 B |        4.09 |
|                                                 |                  |             |             |             |           |       |         |        |        |        |           |             |
| **&#39;Transport publish path concurrent&#39;**             | **4096**             | **8**           | **12,683.1 ns** |   **200.02 ns** |  **88.81 ns** |  **1.00** |    **0.01** | **0.0610** | **0.0305** |      **-** |    **1843 B** |        **1.00** |
| &#39;Transport receive path concurrent&#39;             | 4096             | 8           |  2,000.3 ns |    34.51 ns |  18.05 ns |  0.16 |    0.00 | 0.3052 |      - |      - |    5792 B |        3.14 |
| &#39;Transport publish+receive combined concurrent&#39; | 4096             | 8           | 10,388.4 ns | 1,027.98 ns | 537.65 ns |  0.82 |    0.04 | 0.4272 | 0.1678 | 0.0153 |    8026 B |        4.35 |
|                                                 |                  |             |             |             |           |       |         |        |        |        |           |             |
| **&#39;Transport publish path concurrent&#39;**             | **4096**             | **16**          | **18,847.8 ns** |   **344.39 ns** | **152.91 ns** |  **1.00** |    **0.01** | **0.1831** | **0.1526** | **0.0305** |    **3595 B** |        **1.00** |
| &#39;Transport receive path concurrent&#39;             | 4096             | 16          |  3,973.8 ns |   160.52 ns |  83.96 ns |  0.21 |    0.00 | 0.6104 |      - |      - |   11496 B |        3.20 |
| &#39;Transport publish+receive combined concurrent&#39; | 4096             | 16          | 20,056.3 ns |   519.63 ns | 271.78 ns |  1.06 |    0.02 | 0.8545 | 0.3357 | 0.0305 |   15972 B |        4.44 |
