```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                              | Mean        | Error      | StdDev    | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------------------- |------------:|-----------:|----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;                  |    62.49 ns |   0.366 ns |  0.306 ns |   1.00 |    0.01 | 0.0050 |      96 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;      |    62.91 ns |   0.752 ns |  0.667 ns |   1.01 |    0.01 | 0.0050 |      96 B |        1.00 |
| &#39;Dispatch: Single command (context-less 2-arg)&#39;     |    69.56 ns |   0.659 ns |  0.514 ns |   1.11 |    0.01 | 0.0050 |      96 B |        1.00 |
| &#39;MediatR: Single command handler&#39;                   |    43.77 ns |   0.845 ns |  0.868 ns |   0.70 |    0.01 | 0.0080 |     152 B |        1.58 |
| &#39;Dispatch: Notification to 3 handlers&#39;              |   159.14 ns |   1.688 ns |  1.579 ns |   2.55 |    0.03 | 0.0050 |      96 B |        1.00 |
| &#39;MediatR: Notification to 3 handlers&#39;               |   102.94 ns |   2.043 ns |  4.127 ns |   1.65 |    0.07 | 0.0327 |     616 B |        6.42 |
| &#39;Dispatch: Query with return value&#39;                 |    85.50 ns |   1.721 ns |  1.691 ns |   1.37 |    0.03 | 0.0101 |     192 B |        2.00 |
| &#39;Dispatch: Query strict direct-local&#39;               |    83.55 ns |   1.163 ns |  1.031 ns |   1.34 |    0.02 | 0.0101 |     192 B |        2.00 |
| &#39;Dispatch: Query with return value (typed API)&#39;     |    75.62 ns |   1.303 ns |  1.219 ns |   1.21 |    0.02 | 0.0153 |     288 B |        3.00 |
| &#39;Dispatch: Query (context-less 2-arg)&#39;              |    82.05 ns |   1.029 ns |  0.963 ns |   1.31 |    0.02 | 0.0153 |     288 B |        3.00 |
| &#39;MediatR: Query with return value&#39;                  |    47.34 ns |   0.971 ns |  2.070 ns |   0.76 |    0.03 | 0.0119 |     224 B |        2.33 |
| &#39;Dispatch: Singleton-promoted (context-less 2-arg)&#39; |    68.70 ns |   0.428 ns |  0.380 ns |   1.10 |    0.01 | 0.0050 |      96 B |        1.00 |
| &#39;Dispatch: Query singleton-promoted&#39;                |    83.59 ns |   1.205 ns |  1.127 ns |   1.34 |    0.02 | 0.0153 |     288 B |        3.00 |
| &#39;Dispatch: 10 concurrent commands&#39;                  |   727.32 ns |   6.633 ns |  6.204 ns |  11.64 |    0.11 | 0.0715 |    1360 B |       14.17 |
| &#39;MediatR: 10 concurrent commands&#39;                   |   549.84 ns |  10.970 ns | 16.080 ns |   8.80 |    0.26 | 0.0982 |    1856 B |       19.33 |
| &#39;Dispatch: 100 concurrent commands&#39;                 | 6,886.86 ns |  67.849 ns | 60.147 ns | 110.22 |    1.07 | 0.6409 |   12160 B |      126.67 |
| &#39;MediatR: 100 concurrent commands&#39;                  | 5,286.81 ns | 100.199 ns | 93.726 ns |  84.61 |    1.51 | 0.9003 |   17064 B |      177.75 |
