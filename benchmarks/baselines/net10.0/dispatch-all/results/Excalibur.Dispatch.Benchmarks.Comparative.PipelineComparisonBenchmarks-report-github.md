```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=3  UnrollFactor=1  

```
| Method                                    | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------------ |----------:|----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39;        |  28.50 μs |  50.00 μs |  2.740 μs |  27.50 μs |  1.01 |    0.12 |     704 B |        1.00 |
| &#39;MediatR: 3 pipeline behaviors&#39;           |  13.87 μs | 121.38 μs |  6.653 μs |  10.80 μs |  0.49 |    0.21 |    6456 B |        9.17 |
| &#39;Wolverine: 3 middleware&#39;                 |  44.83 μs | 153.83 μs |  8.432 μs |  47.10 μs |  1.58 |    0.29 |    3456 B |        4.91 |
| &#39;MassTransit: 3 consume filters&#39;          | 121.43 μs | 233.71 μs | 12.810 μs | 120.00 μs |  4.29 |    0.52 |   11032 B |       15.67 |
| &#39;Dispatch: 10 concurrent + 3 behaviors&#39;   |  61.90 μs |  72.88 μs |  3.995 μs |  63.90 μs |  2.18 |    0.21 |   10280 B |       14.60 |
| &#39;MediatR: 10 concurrent + 3 behaviors&#39;    |  41.43 μs | 132.77 μs |  7.278 μs |  43.00 μs |  1.46 |    0.25 |    7808 B |       11.09 |
| &#39;Wolverine: 10 concurrent + 3 middleware&#39; |  41.70 μs |  62.43 μs |  3.422 μs |  43.10 μs |  1.47 |    0.16 |   10576 B |       15.02 |
| &#39;MassTransit: 10 concurrent + 3 filters&#39;  | 192.37 μs | 378.69 μs | 20.757 μs | 183.40 μs |  6.79 |    0.84 |   52832 B |       75.05 |
