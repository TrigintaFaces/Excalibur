```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                            | Mean           | Error         | StdDev         | Ratio     | RatioSD   | Allocated | Alloc Ratio |
|-------------------------------------------------- |---------------:|--------------:|---------------:|----------:|----------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;                |      16.511 μs |      6.445 μs |      3.8355 μs |      1.05 |      0.34 |    1920 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;    |      16.320 μs |      3.114 μs |      2.0596 μs |      1.04 |      0.28 |    4320 B |        2.25 |
| &#39;Dispatch: Single command ultra-local API&#39;        |      10.270 μs |      2.894 μs |      1.9143 μs |      0.65 |      0.19 |      24 B |        0.01 |
| &#39;NServiceBus: Single command handler (SendLocal)&#39; |  10,407.580 μs |  6,508.150 μs |  4,304.7383 μs |    663.28 |    310.70 |   88544 B |       46.12 |
| &#39;Dispatch: Notification to 3 handlers&#39;            |      18.630 μs |      2.351 μs |      1.5550 μs |      1.19 |      0.30 |     240 B |        0.12 |
| &#39;NServiceBus: Publish to 3 handlers&#39;              |   9,044.078 μs |  2,166.679 μs |  1,289.3559 μs |    576.39 |    157.40 |   95944 B |       49.97 |
| &#39;Dispatch: Query with return value&#39;               |      13.800 μs |      4.241 μs |      2.8052 μs |      0.88 |      0.27 |     576 B |        0.30 |
| &#39;Dispatch: Query strict direct-local&#39;             |       9.090 μs |      4.139 μs |      2.7380 μs |      0.58 |      0.22 |    6768 B |        3.52 |
| &#39;Dispatch: Query ultra-local API&#39;                 |      12.463 μs |      1.397 μs |      0.7308 μs |      0.79 |      0.19 |    5760 B |        3.00 |
| &#39;Dispatch: Ultra-local singleton-promoted&#39;        |       7.989 μs |      2.504 μs |      1.4903 μs |      0.51 |      0.15 |      24 B |        0.01 |
| &#39;Dispatch: Query singleton-promoted&#39;              |      10.870 μs |      2.758 μs |      1.8246 μs |      0.69 |      0.20 |    5520 B |        2.88 |
| &#39;Dispatch: 10 concurrent commands&#39;                |      11.188 μs |      1.044 μs |      0.5463 μs |      0.71 |      0.17 |    8464 B |        4.41 |
| &#39;NServiceBus: 10 concurrent commands&#39;             |  95,102.720 μs | 18,025.271 μs | 11,922.6006 μs |  6,060.98 |  1,607.04 |  566616 B |      295.11 |
| &#39;Dispatch: 100 concurrent commands&#39;               |      24.750 μs |      7.769 μs |      5.1388 μs |      1.58 |      0.49 |   19360 B |       10.08 |
| &#39;NServiceBus: 100 concurrent commands&#39;            | 950,038.800 μs | 55,450.682 μs | 36,677.1927 μs | 60,546.85 | 14,410.59 | 5437432 B |    2,832.00 |
