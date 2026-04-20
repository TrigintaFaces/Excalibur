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
| &#39;Dispatch: Single command handler&#39;              | 14.400 μs | 1.971 μs | 1.3038 μs |  1.01 |    0.12 |    1584 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;  | 13.233 μs | 3.449 μs | 2.0524 μs |  0.93 |    0.16 |    4320 B |        2.73 |
| &#39;Dispatch: Single command ultra-local API&#39;      | 11.520 μs | 2.728 μs | 1.8042 μs |  0.81 |    0.14 |    1080 B |        0.68 |
| &#39;MediatR: Single command handler&#39;               | 16.140 μs | 2.858 μs | 1.8906 μs |  1.13 |    0.16 |    5256 B |        3.32 |
| &#39;Dispatch: Notification to 3 handlers&#39;          | 16.922 μs | 3.679 μs | 2.1891 μs |  1.18 |    0.18 |    5280 B |        3.33 |
| &#39;MediatR: Notification to 3 handlers&#39;           | 14.994 μs | 7.874 μs | 4.6859 μs |  1.05 |    0.33 |    3352 B |        2.12 |
| &#39;Dispatch: Query with return value&#39;             | 17.070 μs | 2.036 μs | 1.3466 μs |  1.19 |    0.14 |     624 B |        0.39 |
| &#39;Dispatch: Query strict direct-local&#39;           | 15.440 μs | 2.553 μs | 1.6887 μs |  1.08 |    0.15 |    6000 B |        3.79 |
| &#39;Dispatch: Query with return value (typed API)&#39; | 10.570 μs | 3.566 μs | 2.3585 μs |  0.74 |    0.17 |    4848 B |        3.06 |
| &#39;Dispatch: Query ultra-local API&#39;               | 10.120 μs | 2.130 μs | 1.4087 μs |  0.71 |    0.11 |     192 B |        0.12 |
| &#39;MediatR: Query with return value&#39;              | 15.022 μs | 2.551 μs | 1.5180 μs |  1.05 |    0.14 |    6120 B |        3.86 |
| &#39;Dispatch: Ultra-local singleton-promoted&#39;      |  6.900 μs | 1.697 μs | 1.0100 μs |  0.48 |    0.08 |    1416 B |        0.89 |
| &#39;Dispatch: Query singleton-promoted&#39;            | 11.030 μs | 1.431 μs | 0.9464 μs |  0.77 |    0.09 |    3552 B |        2.24 |
| &#39;Dispatch: 10 concurrent commands&#39;              | 20.300 μs | 2.265 μs | 1.4981 μs |  1.42 |    0.16 |   10432 B |        6.59 |
| &#39;MediatR: 10 concurrent commands&#39;               | 25.280 μs | 8.349 μs | 5.5226 μs |  1.77 |    0.40 |   11184 B |        7.06 |
| &#39;Dispatch: 100 concurrent commands&#39;             | 47.720 μs | 5.200 μs | 3.4396 μs |  3.34 |    0.37 |   20656 B |       13.04 |
| &#39;MediatR: 100 concurrent commands&#39;              | 54.587 μs | 5.748 μs | 3.0062 μs |  3.82 |    0.39 |   24808 B |       15.66 |
