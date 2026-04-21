```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                                           | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------------------------------------------------------------- |----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                               |  12.68 μs |   1.587 μs |  1.050 μs |  1.01 |    0.12 |     640 B |        1.00 |
| &#39;MassTransit Mediator (in-process): Single command&#39;              |  95.31 μs |  14.054 μs |  8.363 μs |  7.57 |    0.89 |   11736 B |       18.34 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;                   |  16.87 μs |   3.170 μs |  2.097 μs |  1.34 |    0.19 |     664 B |        1.04 |
| &#39;MassTransit Mediator (in-process): Notification to 2 consumers&#39; |  88.28 μs |  16.328 μs |  9.717 μs |  7.01 |    0.93 |   12656 B |       19.77 |
| &#39;Dispatch (local): Query with return&#39;                            |  12.80 μs |   2.562 μs |  1.525 μs |  1.02 |    0.14 |    6928 B |       10.82 |
| &#39;MassTransit Mediator (in-process): Query with return&#39;           | 278.10 μs | 110.567 μs | 57.828 μs | 22.07 |    4.70 |   20040 B |       31.31 |
| &#39;Dispatch (local): 10 concurrent commands&#39;                       |  16.08 μs |   5.803 μs |  3.838 μs |  1.28 |    0.31 |    9200 B |       14.38 |
| &#39;MassTransit Mediator (in-process): 10 concurrent commands&#39;      | 133.57 μs |  16.065 μs | 10.626 μs | 10.60 |    1.19 |   44608 B |       69.70 |
| &#39;Dispatch (local): 100 concurrent commands&#39;                      |  25.67 μs |   3.571 μs |  2.125 μs |  2.04 |    0.23 |   37232 B |       58.17 |
| &#39;MassTransit Mediator (in-process): 100 concurrent commands&#39;     | 557.91 μs |  37.669 μs | 24.916 μs | 44.28 |    4.12 |  376480 B |      588.25 |
