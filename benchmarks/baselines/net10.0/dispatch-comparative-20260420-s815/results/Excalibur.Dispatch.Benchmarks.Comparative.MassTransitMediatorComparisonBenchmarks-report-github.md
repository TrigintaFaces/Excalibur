```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                                           | Mean        | Error        | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------------------------------------------------------------- |------------:|-------------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                               |    26.25 μs |     7.916 μs |   5.236 μs |  1.04 |    0.31 |   6.86 KB |        1.00 |
| &#39;MassTransit Mediator (in-process): Single command&#39;              |    87.06 μs |    16.250 μs |   8.499 μs |  3.46 |    0.83 |  11.74 KB |        1.71 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;                   |    31.56 μs |    14.164 μs |   9.368 μs |  1.25 |    0.46 |   6.88 KB |        1.00 |
| &#39;MassTransit Mediator (in-process): Notification to 2 consumers&#39; |    90.88 μs |    19.904 μs |  13.165 μs |  3.61 |    0.95 |  12.69 KB |        1.85 |
| &#39;Dispatch (local): Query with return&#39;                            |    18.41 μs |     4.350 μs |   2.878 μs |  0.73 |    0.20 |   7.98 KB |        1.16 |
| &#39;MassTransit Mediator (in-process): Query with return&#39;           |   162.67 μs |    35.661 μs |  21.221 μs |  6.46 |    1.65 |   19.9 KB |        2.90 |
| &#39;Dispatch (local): 10 concurrent commands&#39;                       |    23.16 μs |     4.139 μs |   2.463 μs |  0.92 |    0.22 |   9.64 KB |        1.41 |
| &#39;MassTransit Mediator (in-process): 10 concurrent commands&#39;      |   121.70 μs |    21.099 μs |  13.955 μs |  4.83 |    1.20 |  44.27 KB |        6.45 |
| &#39;Dispatch (local): 100 concurrent commands&#39;                      |    58.57 μs |     3.020 μs |   1.997 μs |  2.33 |    0.52 |  38.33 KB |        5.59 |
| &#39;MassTransit Mediator (in-process): 100 concurrent commands&#39;     | 1,333.75 μs | 1,130.411 μs | 747.697 μs | 52.98 |   31.27 | 367.98 KB |       53.65 |
