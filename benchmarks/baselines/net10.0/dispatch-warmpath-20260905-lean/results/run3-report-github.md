```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                              | Mean        | Error     | StdDev    | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------------------- |------------:|----------:|----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;                  |    60.04 ns |  0.693 ns |  0.615 ns |   1.00 |    0.01 | 0.0050 |      96 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;      |    59.37 ns |  0.541 ns |  0.506 ns |   0.99 |    0.01 | 0.0050 |      96 B |        1.00 |
| &#39;Dispatch: Single command (context-less 2-arg)&#39;     |    65.95 ns |  0.390 ns |  0.364 ns |   1.10 |    0.01 | 0.0050 |      96 B |        1.00 |
| &#39;MediatR: Single command handler&#39;                   |    42.78 ns |  0.850 ns |  1.164 ns |   0.71 |    0.02 | 0.0080 |     152 B |        1.58 |
| &#39;Dispatch: Notification to 3 handlers&#39;              |   158.24 ns |  1.033 ns |  0.966 ns |   2.64 |    0.03 | 0.0050 |      96 B |        1.00 |
| &#39;MediatR: Notification to 3 handlers&#39;               |    99.56 ns |  1.968 ns |  3.063 ns |   1.66 |    0.05 | 0.0327 |     616 B |        6.42 |
| &#39;Dispatch: Query with return value&#39;                 |    82.13 ns |  1.190 ns |  1.113 ns |   1.37 |    0.02 | 0.0101 |     192 B |        2.00 |
| &#39;Dispatch: Query strict direct-local&#39;               |    82.59 ns |  0.235 ns |  0.208 ns |   1.38 |    0.01 | 0.0101 |     192 B |        2.00 |
| &#39;Dispatch: Query with return value (typed API)&#39;     |    73.52 ns |  0.710 ns |  0.629 ns |   1.22 |    0.02 | 0.0153 |     288 B |        3.00 |
| &#39;Dispatch: Query (context-less 2-arg)&#39;              |    80.71 ns |  1.079 ns |  1.009 ns |   1.34 |    0.02 | 0.0153 |     288 B |        3.00 |
| &#39;MediatR: Query with return value&#39;                  |    47.78 ns |  0.956 ns |  0.981 ns |   0.80 |    0.02 | 0.0119 |     224 B |        2.33 |
| &#39;Dispatch: Singleton-promoted (context-less 2-arg)&#39; |    67.76 ns |  0.330 ns |  0.292 ns |   1.13 |    0.01 | 0.0050 |      96 B |        1.00 |
| &#39;Dispatch: Query singleton-promoted&#39;                |    79.29 ns |  1.087 ns |  0.908 ns |   1.32 |    0.02 | 0.0153 |     288 B |        3.00 |
| &#39;Dispatch: 10 concurrent commands&#39;                  |   724.27 ns |  7.772 ns |  7.270 ns |  12.07 |    0.17 | 0.0715 |    1360 B |       14.17 |
| &#39;MediatR: 10 concurrent commands&#39;                   |   552.09 ns |  6.811 ns |  6.371 ns |   9.20 |    0.14 | 0.0982 |    1856 B |       19.33 |
| &#39;Dispatch: 100 concurrent commands&#39;                 | 6,923.02 ns | 61.320 ns | 57.358 ns | 115.33 |    1.46 | 0.6409 |   12160 B |      126.67 |
| &#39;MediatR: 100 concurrent commands&#39;                  | 5,184.97 ns | 91.159 ns | 85.271 ns |  86.37 |    1.61 | 0.9003 |   17064 B |      177.75 |
