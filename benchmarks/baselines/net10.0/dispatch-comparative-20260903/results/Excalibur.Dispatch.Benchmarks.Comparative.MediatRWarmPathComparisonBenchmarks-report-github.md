```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                          | Mean        | Error     | StdDev    | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------ |------------:|----------:|----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;              |    50.57 ns |  0.503 ns |  0.470 ns |   1.00 |    0.01 | 0.0051 |      96 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;  |    50.75 ns |  0.421 ns |  0.351 ns |   1.00 |    0.01 | 0.0051 |      96 B |        1.00 |
| &#39;Dispatch: Single command ultra-local API&#39;      |    33.39 ns |  0.299 ns |  0.250 ns |   0.66 |    0.01 | 0.0013 |      24 B |        0.25 |
| &#39;MediatR: Single command handler&#39;               |    43.01 ns |  0.411 ns |  0.343 ns |   0.85 |    0.01 | 0.0080 |     152 B |        1.58 |
| &#39;Dispatch: Notification to 3 handlers&#39;          |   135.13 ns |  2.309 ns |  2.660 ns |   2.67 |    0.06 | 0.0050 |      96 B |        1.00 |
| &#39;MediatR: Notification to 3 handlers&#39;           |   105.72 ns |  1.852 ns |  1.641 ns |   2.09 |    0.04 | 0.0327 |     616 B |        6.42 |
| &#39;Dispatch: Query with return value&#39;             |    62.15 ns |  0.391 ns |  0.305 ns |   1.23 |    0.01 | 0.0101 |     192 B |        2.00 |
| &#39;Dispatch: Query strict direct-local&#39;           |    60.93 ns |  0.383 ns |  0.320 ns |   1.20 |    0.01 | 0.0101 |     192 B |        2.00 |
| &#39;Dispatch: Query with return value (typed API)&#39; |    66.19 ns |  0.681 ns |  0.637 ns |   1.31 |    0.02 | 0.0153 |     288 B |        3.00 |
| &#39;Dispatch: Query ultra-local API&#39;               |    46.02 ns |  0.384 ns |  0.360 ns |   0.91 |    0.01 | 0.0063 |     120 B |        1.25 |
| &#39;MediatR: Query with return value&#39;              |    46.66 ns |  0.927 ns |  1.104 ns |   0.92 |    0.02 | 0.0119 |     224 B |        2.33 |
| &#39;Dispatch: Ultra-local singleton-promoted&#39;      |    33.28 ns |  0.065 ns |  0.051 ns |   0.66 |    0.01 | 0.0013 |      24 B |        0.25 |
| &#39;Dispatch: Query singleton-promoted&#39;            |    45.78 ns |  0.382 ns |  0.357 ns |   0.91 |    0.01 | 0.0063 |     120 B |        1.25 |
| &#39;Dispatch: 10 concurrent commands&#39;              |   625.83 ns | 11.531 ns | 15.783 ns |  12.38 |    0.33 | 0.0715 |    1360 B |       14.17 |
| &#39;MediatR: 10 concurrent commands&#39;               |   543.72 ns |  5.415 ns |  4.800 ns |  10.75 |    0.13 | 0.0982 |    1856 B |       19.33 |
| &#39;Dispatch: 100 concurrent commands&#39;             | 5,975.19 ns | 75.662 ns | 70.774 ns | 118.17 |    1.72 | 0.6409 |   12160 B |      126.67 |
| &#39;MediatR: 100 concurrent commands&#39;              | 5,135.47 ns | 41.620 ns | 34.755 ns | 101.56 |    1.13 | 0.9003 |   17064 B |      177.75 |
