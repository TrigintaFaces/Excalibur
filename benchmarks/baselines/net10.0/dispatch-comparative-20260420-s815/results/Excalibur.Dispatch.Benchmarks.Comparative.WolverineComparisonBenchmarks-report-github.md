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
| &#39;Dispatch: Single command&#39;                |  11.44 μs |  2.141 μs | 1.416 μs |  1.01 |    0.16 |     552 B |        1.00 |
| &#39;Dispatch: Single command (ultra-local)&#39;  |  11.16 μs |  1.726 μs | 1.027 μs |  0.99 |    0.14 |    5400 B |        9.78 |
| &#39;Wolverine: Single command (InvokeAsync)&#39; |  26.85 μs |  4.542 μs | 3.004 μs |  2.38 |    0.37 |    8904 B |       16.13 |
| &#39;Wolverine: Single command (SendAsync)&#39;   |  93.83 μs | 10.819 μs | 7.156 μs |  8.31 |    1.10 |   15344 B |       27.80 |
| &#39;Dispatch: Event to 2 handlers&#39;           |  16.14 μs |  2.356 μs | 1.559 μs |  1.43 |    0.21 |   10656 B |       19.30 |
| &#39;Wolverine: Event publish&#39;                |  97.07 μs |  9.691 μs | 5.767 μs |  8.59 |    1.06 |   15336 B |       27.78 |
| &#39;Dispatch: 10 concurrent commands&#39;        |  15.40 μs |  3.284 μs | 1.954 μs |  1.36 |    0.22 |    5680 B |       10.29 |
| &#39;Wolverine: 10 concurrent commands&#39;       |  38.10 μs |  5.933 μs | 3.925 μs |  3.37 |    0.50 |    9184 B |       16.64 |
| &#39;Dispatch: Query with return value&#39;       |  17.33 μs |  1.545 μs | 0.919 μs |  1.53 |    0.19 |     456 B |        0.83 |
| &#39;Wolverine: Query with return value&#39;      |  32.13 μs |  3.863 μs | 2.555 μs |  2.84 |    0.38 |   11552 B |       20.93 |
| &#39;Dispatch: 100 concurrent commands&#39;       |  24.42 μs |  2.972 μs | 1.768 μs |  2.16 |    0.28 |   22048 B |       39.94 |
| &#39;Wolverine: 100 concurrent commands&#39;      | 114.49 μs |  5.206 μs | 2.723 μs | 10.14 |    1.14 |   71632 B |      129.77 |
| &#39;Dispatch: Batch queries (10)&#39;            |  21.10 μs |  1.927 μs | 1.275 μs |  1.87 |    0.23 |    3880 B |        7.03 |
| &#39;Wolverine: Batch queries (10)&#39;           |  46.16 μs |  7.653 μs | 5.062 μs |  4.09 |    0.62 |   14488 B |       26.25 |
