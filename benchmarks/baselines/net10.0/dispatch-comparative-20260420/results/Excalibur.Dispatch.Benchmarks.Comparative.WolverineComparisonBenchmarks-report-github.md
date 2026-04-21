```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                    | Mean      | Error     | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------------ |----------:|----------:|---------:|------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;                |  18.25 μs |  2.820 μs | 1.865 μs |  1.01 |    0.14 |     552 B |        1.00 |
| &#39;Dispatch: Single command (ultra-local)&#39;  |  10.20 μs |  1.832 μs | 1.212 μs |  0.56 |    0.09 |      24 B |        0.04 |
| &#39;Wolverine: Single command (InvokeAsync)&#39; |  31.41 μs |  4.944 μs | 3.270 μs |  1.74 |    0.24 |   10048 B |       18.20 |
| &#39;Wolverine: Single command (SendAsync)&#39;   | 110.85 μs | 14.279 μs | 9.445 μs |  6.13 |    0.79 |   15576 B |       28.22 |
| &#39;Dispatch: Event to 2 handlers&#39;           |  23.00 μs |  3.665 μs | 2.424 μs |  1.27 |    0.18 |     960 B |        1.74 |
| &#39;Wolverine: Event publish&#39;                | 112.21 μs | 14.220 μs | 9.406 μs |  6.21 |    0.79 |   15544 B |       28.16 |
| &#39;Dispatch: 10 concurrent commands&#39;        |  27.12 μs |  2.749 μs | 1.438 μs |  1.50 |    0.17 |    3952 B |        7.16 |
| &#39;Wolverine: 10 concurrent commands&#39;       |  44.49 μs |  8.615 μs | 5.698 μs |  2.46 |    0.39 |    8432 B |       15.28 |
| &#39;Dispatch: Query with return value&#39;       |  21.36 μs |  3.286 μs | 1.955 μs |  1.18 |    0.16 |     744 B |        1.35 |
| &#39;Wolverine: Query with return value&#39;      |  35.46 μs |  4.487 μs | 2.670 μs |  1.96 |    0.24 |    3576 B |        6.48 |
| &#39;Dispatch: 100 concurrent commands&#39;       |  53.04 μs |  3.041 μs | 2.012 μs |  2.93 |    0.31 |   26752 B |       48.46 |
| &#39;Wolverine: 100 concurrent commands&#39;      | 108.68 μs |  5.388 μs | 3.206 μs |  6.01 |    0.62 |   79088 B |      143.28 |
| &#39;Dispatch: Batch queries (10)&#39;            |  28.08 μs |  3.918 μs | 2.332 μs |  1.55 |    0.20 |    5896 B |       10.68 |
| &#39;Wolverine: Batch queries (10)&#39;           |  48.32 μs |  8.170 μs | 4.862 μs |  2.67 |    0.37 |   17672 B |       32.01 |
