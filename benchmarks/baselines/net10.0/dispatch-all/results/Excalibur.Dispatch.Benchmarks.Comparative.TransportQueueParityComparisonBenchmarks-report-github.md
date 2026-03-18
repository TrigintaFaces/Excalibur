```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=3  UnrollFactor=1  

```
| Method                                                          | Mean        | Error       | StdDev    | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |------------:|------------:|----------:|------------:|------:|--------:|----------:|------------:|
| &#39;Dispatch (remote): queued command end-to-end&#39;                  |    43.67 μs |   152.74 μs |  8.372 μs |    42.40 μs |  1.02 |    0.24 |   1.33 KB |        1.00 |
| &#39;Wolverine: queued command end-to-end (SendAsync)&#39;              |   160.07 μs |   255.22 μs | 13.989 μs |   152.70 μs |  3.75 |    0.67 |  12.14 KB |        9.14 |
| &#39;MassTransit: queued command end-to-end (Publish)&#39;              |   366.50 μs |   722.13 μs | 39.582 μs |   347.50 μs |  8.60 |    1.61 |  33.09 KB |       24.91 |
| &#39;Dispatch (remote): queued event fan-out end-to-end&#39;            |    68.63 μs |   117.06 μs |  6.417 μs |    68.10 μs |  1.61 |    0.29 |   8.48 KB |        6.38 |
| &#39;Wolverine: queued event fan-out end-to-end (PublishAsync)&#39;     |    99.67 μs |   150.54 μs |  8.252 μs |    98.60 μs |  2.34 |    0.41 |  12.13 KB |        9.14 |
| &#39;MassTransit: queued event fan-out end-to-end (Publish)&#39;        |   378.30 μs |   372.02 μs | 20.391 μs |   370.70 μs |  8.87 |    1.50 |  33.51 KB |       25.23 |
| &#39;Dispatch (remote): queued commands end-to-end (10 concurrent)&#39; |   125.97 μs | 1,462.39 μs | 80.158 μs |    92.10 μs |  2.95 |    1.72 |   14.8 KB |       11.14 |
| &#39;Wolverine: queued commands end-to-end (10 concurrent)&#39;         |   248.73 μs |   231.50 μs | 12.689 μs |   245.90 μs |  5.83 |    0.98 |  52.27 KB |       39.36 |
| &#39;MassTransit: queued commands end-to-end (10 concurrent)&#39;       | 1,269.73 μs |   357.20 μs | 19.579 μs | 1,277.30 μs | 29.79 |    4.84 | 225.16 KB |      169.54 |
