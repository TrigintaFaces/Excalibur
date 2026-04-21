```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                    | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------------ |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39;        |  10.07 μs |  1.109 μs |  0.580 μs |  1.00 |    0.08 |    3216 B |        1.00 |
| &#39;MediatR: 3 pipeline behaviors&#39;           |  21.13 μs |  5.587 μs |  3.695 μs |  2.10 |    0.37 |     744 B |        0.23 |
| &#39;Wolverine: 3 middleware&#39;                 |  36.18 μs |  4.893 μs |  3.237 μs |  3.60 |    0.37 |     768 B |        0.24 |
| &#39;MassTransit: 3 consume filters&#39;          |  99.48 μs | 16.319 μs | 10.794 μs |  9.90 |    1.16 |   14056 B |        4.37 |
| &#39;Dispatch: 10 concurrent + 3 behaviors&#39;   |  18.67 μs |  2.523 μs |  1.669 μs |  1.86 |    0.19 |    8400 B |        2.61 |
| &#39;MediatR: 10 concurrent + 3 behaviors&#39;    |  30.64 μs |  3.034 μs |  1.806 μs |  3.05 |    0.24 |    9488 B |        2.95 |
| &#39;Wolverine: 10 concurrent + 3 middleware&#39; |  41.84 μs |  8.074 μs |  5.341 μs |  4.16 |    0.56 |   16960 B |        5.27 |
| &#39;MassTransit: 10 concurrent + 3 filters&#39;  | 182.10 μs | 20.242 μs | 13.389 μs | 18.13 |    1.61 |   56528 B |       17.58 |
