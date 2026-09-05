```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                              | Mean        | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------------------- |------------:|-----------:|-----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;                  |    45.58 ns |   0.481 ns |   0.450 ns |   1.00 |    0.01 | 0.0051 |      96 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;      |    46.00 ns |   0.334 ns |   0.312 ns |   1.01 |    0.01 | 0.0051 |      96 B |        1.00 |
| &#39;Dispatch: Single command (context-less 2-arg)&#39;     |    53.00 ns |   0.457 ns |   0.405 ns |   1.16 |    0.01 | 0.0051 |      96 B |        1.00 |
| &#39;MediatR: Single command handler&#39;                   |    41.32 ns |   0.790 ns |   0.775 ns |   0.91 |    0.02 | 0.0080 |     152 B |        1.58 |
| &#39;Dispatch: Notification to 3 handlers&#39;              |   134.99 ns |   0.675 ns |   0.631 ns |   2.96 |    0.03 | 0.0050 |      96 B |        1.00 |
| &#39;MediatR: Notification to 3 handlers&#39;               |    95.01 ns |   1.924 ns |   5.428 ns |   2.08 |    0.12 | 0.0327 |     616 B |        6.42 |
| &#39;Dispatch: Query with return value&#39;                 |    63.67 ns |   0.975 ns |   0.912 ns |   1.40 |    0.02 | 0.0101 |     192 B |        2.00 |
| &#39;Dispatch: Query strict direct-local&#39;               |    63.02 ns |   0.974 ns |   0.911 ns |   1.38 |    0.02 | 0.0101 |     192 B |        2.00 |
| &#39;Dispatch: Query with return value (typed API)&#39;     |    63.76 ns |   1.318 ns |   1.569 ns |   1.40 |    0.04 | 0.0153 |     288 B |        3.00 |
| &#39;Dispatch: Query (context-less 2-arg)&#39;              |    69.87 ns |   1.439 ns |   1.477 ns |   1.53 |    0.03 | 0.0153 |     288 B |        3.00 |
| &#39;MediatR: Query with return value&#39;                  |    47.55 ns |   0.962 ns |   1.710 ns |   1.04 |    0.04 | 0.0119 |     224 B |        2.33 |
| &#39;Dispatch: Singleton-promoted (context-less 2-arg)&#39; |    53.85 ns |   0.273 ns |   0.228 ns |   1.18 |    0.01 | 0.0051 |      96 B |        1.00 |
| &#39;Dispatch: Query singleton-promoted&#39;                |    68.99 ns |   1.412 ns |   1.681 ns |   1.51 |    0.04 | 0.0153 |     288 B |        3.00 |
| &#39;Dispatch: 10 concurrent commands&#39;                  |   596.06 ns |  11.183 ns |   9.339 ns |  13.08 |    0.23 | 0.0715 |    1360 B |       14.17 |
| &#39;MediatR: 10 concurrent commands&#39;                   |   541.74 ns |  10.585 ns |  13.387 ns |  11.89 |    0.31 | 0.0982 |    1856 B |       19.33 |
| &#39;Dispatch: 100 concurrent commands&#39;                 | 5,584.43 ns | 103.568 ns | 101.717 ns | 122.53 |    2.46 | 0.6409 |   12160 B |      126.67 |
| &#39;MediatR: 100 concurrent commands&#39;                  | 5,146.08 ns | 100.895 ns | 116.190 ns | 112.92 |    2.71 | 0.9003 |   17064 B |      177.75 |
