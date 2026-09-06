```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                              | Mean        | Error     | StdDev    | Median      | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------------------- |------------:|----------:|----------:|------------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;                  |    59.91 ns |  0.578 ns |  0.512 ns |    59.80 ns |   1.00 |    0.01 | 0.0050 |      96 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;      |    60.63 ns |  0.308 ns |  0.288 ns |    60.66 ns |   1.01 |    0.01 | 0.0050 |      96 B |        1.00 |
| &#39;Dispatch: Single command (context-less 2-arg)&#39;     |    67.52 ns |  0.497 ns |  0.440 ns |    67.46 ns |   1.13 |    0.01 | 0.0050 |      96 B |        1.00 |
| &#39;MediatR: Single command handler&#39;                   |    41.78 ns |  0.704 ns |  0.811 ns |    41.69 ns |   0.70 |    0.01 | 0.0080 |     152 B |        1.58 |
| &#39;Dispatch: Notification to 3 handlers&#39;              |   154.79 ns |  0.412 ns |  0.365 ns |   154.72 ns |   2.58 |    0.02 | 0.0050 |      96 B |        1.00 |
| &#39;MediatR: Notification to 3 handlers&#39;               |    95.10 ns |  1.903 ns |  4.176 ns |    93.55 ns |   1.59 |    0.07 | 0.0327 |     616 B |        6.42 |
| &#39;Dispatch: Query with return value&#39;                 |    86.16 ns |  0.933 ns |  0.873 ns |    86.31 ns |   1.44 |    0.02 | 0.0101 |     192 B |        2.00 |
| &#39;Dispatch: Query strict direct-local&#39;               |    86.36 ns |  0.693 ns |  0.614 ns |    86.40 ns |   1.44 |    0.02 | 0.0101 |     192 B |        2.00 |
| &#39;Dispatch: Query with return value (typed API)&#39;     |    72.24 ns |  0.741 ns |  0.694 ns |    72.18 ns |   1.21 |    0.01 | 0.0153 |     288 B |        3.00 |
| &#39;Dispatch: Query (context-less 2-arg)&#39;              |    79.20 ns |  1.569 ns |  1.467 ns |    80.12 ns |   1.32 |    0.03 | 0.0153 |     288 B |        3.00 |
| &#39;MediatR: Query with return value&#39;                  |    47.41 ns |  0.937 ns |  1.079 ns |    47.48 ns |   0.79 |    0.02 | 0.0119 |     224 B |        2.33 |
| &#39;Dispatch: Singleton-promoted (context-less 2-arg)&#39; |    68.08 ns |  0.392 ns |  0.348 ns |    68.01 ns |   1.14 |    0.01 | 0.0050 |      96 B |        1.00 |
| &#39;Dispatch: Query singleton-promoted&#39;                |    80.14 ns |  0.858 ns |  0.803 ns |    80.34 ns |   1.34 |    0.02 | 0.0153 |     288 B |        3.00 |
| &#39;Dispatch: 10 concurrent commands&#39;                  |   715.33 ns |  7.259 ns |  6.790 ns |   715.09 ns |  11.94 |    0.15 | 0.0715 |    1360 B |       14.17 |
| &#39;MediatR: 10 concurrent commands&#39;                   |   524.60 ns | 10.518 ns | 12.521 ns |   521.00 ns |   8.76 |    0.22 | 0.0982 |    1856 B |       19.33 |
| &#39;Dispatch: 100 concurrent commands&#39;                 | 7,090.18 ns | 48.452 ns | 45.322 ns | 7,084.23 ns | 118.35 |    1.22 | 0.6409 |   12160 B |      126.67 |
| &#39;MediatR: 100 concurrent commands&#39;                  | 5,084.92 ns | 62.445 ns | 58.411 ns | 5,085.34 ns |  84.88 |    1.17 | 0.9003 |   17064 B |      177.75 |
