```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=3  UnrollFactor=1  

```
| Method                                                           | Mean       | Error      | StdDev     | Ratio  | RatioSD | Allocated | Alloc Ratio |
|----------------------------------------------------------------- |-----------:|-----------:|-----------:|-------:|--------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                               |   4.167 μs |   2.787 μs |  0.1528 μs |   1.00 |    0.05 |     192 B |        1.00 |
| &#39;MassTransit Mediator (in-process): Single command&#39;              |  39.133 μs |  51.126 μs |  2.8024 μs |   9.40 |    0.66 |    8712 B |       45.38 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;                   |   8.583 μs |  15.939 μs |  0.8737 μs |   2.06 |    0.19 |    4744 B |       24.71 |
| &#39;MassTransit Mediator (in-process): Notification to 2 consumers&#39; |  33.033 μs |  30.107 μs |  1.6503 μs |   7.94 |    0.43 |    9344 B |       48.67 |
| &#39;Dispatch (local): Query with return&#39;                            |   4.533 μs |   2.107 μs |  0.1155 μs |   1.09 |    0.04 |    2112 B |       11.00 |
| &#39;MassTransit Mediator (in-process): Query with return&#39;           | 121.633 μs | 420.637 μs | 23.0565 μs |  29.22 |    4.89 |   17016 B |       88.62 |
| &#39;Dispatch (local): 10 concurrent commands&#39;                       |   8.100 μs |   3.160 μs |  0.1732 μs |   1.95 |    0.07 |    6544 B |       34.08 |
| &#39;MassTransit Mediator (in-process): 10 concurrent commands&#39;      |  87.467 μs | 121.075 μs |  6.6365 μs |  21.01 |    1.54 |   41584 B |      216.58 |
| &#39;Dispatch (local): 100 concurrent commands&#39;                      |  30.200 μs |  25.865 μs |  1.4177 μs |   7.25 |    0.38 |   19552 B |      101.83 |
| &#39;MassTransit Mediator (in-process): 100 concurrent commands&#39;     | 490.450 μs | 422.329 μs | 23.1493 μs | 117.81 |    6.13 |  373120 B |    1,943.33 |
