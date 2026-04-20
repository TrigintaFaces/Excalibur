```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                          | Mean      | Error    | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------------------ |----------:|---------:|----------:|------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;              |  8.760 μs | 2.112 μs | 1.3972 μs |  1.02 |    0.22 |    1920 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;  |  9.270 μs | 1.983 μs | 1.3115 μs |  1.08 |    0.22 |     240 B |        0.12 |
| &#39;Dispatch: Single command ultra-local API&#39;      |  9.780 μs | 2.261 μs | 1.4958 μs |  1.14 |    0.24 |      24 B |        0.01 |
| &#39;MediatR: Single command handler&#39;               | 14.722 μs | 1.349 μs | 0.8028 μs |  1.72 |    0.28 |    8280 B |        4.31 |
| &#39;Dispatch: Notification to 3 handlers&#39;          | 12.444 μs | 5.912 μs | 3.5182 μs |  1.45 |    0.45 |    4320 B |        2.25 |
| &#39;MediatR: Notification to 3 handlers&#39;           |  9.870 μs | 4.278 μs | 2.8296 μs |  1.15 |    0.37 |     616 B |        0.32 |
| &#39;Dispatch: Query with return value&#39;             | 11.810 μs | 2.059 μs | 1.3617 μs |  1.38 |    0.26 |     336 B |        0.17 |
| &#39;Dispatch: Query strict direct-local&#39;           | 13.717 μs | 5.591 μs | 3.3272 μs |  1.60 |    0.45 |     336 B |        0.17 |
| &#39;Dispatch: Query with return value (typed API)&#39; | 12.860 μs | 1.679 μs | 1.1108 μs |  1.50 |    0.26 |    4128 B |        2.15 |
| &#39;Dispatch: Query ultra-local API&#39;               |  9.078 μs | 1.984 μs | 1.1809 μs |  1.06 |    0.21 |     192 B |        0.10 |
| &#39;MediatR: Query with return value&#39;              | 13.040 μs | 5.831 μs | 3.8569 μs |  1.52 |    0.49 |    7416 B |        3.86 |
| &#39;Dispatch: Ultra-local singleton-promoted&#39;      |  5.790 μs | 2.960 μs | 1.9576 μs |  0.68 |    0.24 |      24 B |        0.01 |
| &#39;Dispatch: Query singleton-promoted&#39;            | 10.680 μs | 2.985 μs | 1.9743 μs |  1.25 |    0.29 |     480 B |        0.25 |
| &#39;Dispatch: 10 concurrent commands&#39;              | 13.590 μs | 4.632 μs | 3.0636 μs |  1.59 |    0.42 |    2080 B |        1.08 |
| &#39;MediatR: 10 concurrent commands&#39;               | 23.644 μs | 3.078 μs | 1.8317 μs |  2.76 |    0.47 |    3456 B |        1.80 |
| &#39;Dispatch: 100 concurrent commands&#39;             | 25.590 μs | 2.993 μs | 1.9796 μs |  2.99 |    0.51 |   20656 B |       10.76 |
| &#39;MediatR: 100 concurrent commands&#39;              | 55.190 μs | 5.237 μs | 3.4642 μs |  6.45 |    1.07 |   31816 B |       16.57 |
