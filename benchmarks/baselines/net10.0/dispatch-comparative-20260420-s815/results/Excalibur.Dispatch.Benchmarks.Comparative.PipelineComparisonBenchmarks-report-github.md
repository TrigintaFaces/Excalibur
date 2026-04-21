```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                    | Mean       | Error     | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------------ |-----------:|----------:|---------:|------:|--------:|----------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39;        |   8.833 μs |  1.689 μs | 1.005 μs |  1.01 |    0.15 |    4896 B |        1.00 |
| &#39;MediatR: 3 pipeline behaviors&#39;           |  22.994 μs |  6.336 μs | 3.771 μs |  2.63 |    0.49 |    8088 B |        1.65 |
| &#39;Wolverine: 3 middleware&#39;                 |  33.044 μs |  7.324 μs | 4.358 μs |  3.78 |    0.61 |     680 B |        0.14 |
| &#39;MassTransit: 3 consume filters&#39;          |  76.600 μs | 13.170 μs | 8.711 μs |  8.77 |    1.31 |    4632 B |        0.95 |
| &#39;Dispatch: 10 concurrent + 3 behaviors&#39;   |  15.967 μs |  1.693 μs | 1.007 μs |  1.83 |    0.22 |    2112 B |        0.43 |
| &#39;MediatR: 10 concurrent + 3 behaviors&#39;    |  25.980 μs |  1.849 μs | 1.223 μs |  2.97 |    0.33 |    9776 B |        2.00 |
| &#39;Wolverine: 10 concurrent + 3 middleware&#39; |  40.560 μs |  7.043 μs | 4.659 μs |  4.64 |    0.69 |   17088 B |        3.49 |
| &#39;MassTransit: 10 concurrent + 3 filters&#39;  | 108.044 μs |  6.762 μs | 4.024 μs | 12.36 |    1.33 |   56560 B |       11.55 |
