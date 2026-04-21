```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                          | Mean        | Error     | StdDev    | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |------------:|----------:|----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;              |    70.87 ns |  0.671 ns |  0.628 ns |   1.00 |    0.01 | 0.0126 |      - |     240 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;  |    71.40 ns |  0.700 ns |  0.655 ns |   1.01 |    0.01 | 0.0126 |      - |     240 B |        1.00 |
| &#39;Dispatch: Single command ultra-local API&#39;      |    34.56 ns |  0.402 ns |  0.376 ns |   0.49 |    0.01 | 0.0013 |      - |      24 B |        0.10 |
| &#39;MediatR: Single command handler&#39;               |    44.20 ns |  0.308 ns |  0.273 ns |   0.62 |    0.01 | 0.0080 |      - |     152 B |        0.63 |
| &#39;Dispatch: Notification to 3 handlers&#39;          |   117.36 ns |  0.665 ns |  0.590 ns |   1.66 |    0.02 | 0.0126 |      - |     240 B |        1.00 |
| &#39;MediatR: Notification to 3 handlers&#39;           |    94.47 ns |  1.894 ns |  1.860 ns |   1.33 |    0.03 | 0.0327 |      - |     616 B |        2.57 |
| &#39;Dispatch: Query with return value&#39;             |    76.61 ns |  1.267 ns |  1.245 ns |   1.08 |    0.02 | 0.0178 |      - |     336 B |        1.40 |
| &#39;Dispatch: Query strict direct-local&#39;           |    76.62 ns |  0.633 ns |  0.592 ns |   1.08 |    0.01 | 0.0178 |      - |     336 B |        1.40 |
| &#39;Dispatch: Query with return value (typed API)&#39; |    79.26 ns |  0.906 ns |  0.803 ns |   1.12 |    0.01 | 0.0229 |      - |     432 B |        1.80 |
| &#39;Dispatch: Query ultra-local API&#39;               |    56.63 ns |  0.770 ns |  0.720 ns |   0.80 |    0.01 | 0.0102 |      - |     192 B |        0.80 |
| &#39;MediatR: Query with return value&#39;              |    51.81 ns |  1.056 ns |  1.174 ns |   0.73 |    0.02 | 0.0157 |      - |     296 B |        1.23 |
| &#39;Dispatch: Ultra-local singleton-promoted&#39;      |    33.67 ns |  0.214 ns |  0.200 ns |   0.48 |    0.00 | 0.0013 |      - |      24 B |        0.10 |
| &#39;Dispatch: Query singleton-promoted&#39;            |    57.79 ns |  0.577 ns |  0.540 ns |   0.82 |    0.01 | 0.0102 |      - |     192 B |        0.80 |
| &#39;Dispatch: 10 concurrent commands&#39;              |   826.80 ns |  5.810 ns |  4.852 ns |  11.67 |    0.12 | 0.1097 |      - |    2080 B |        8.67 |
| &#39;MediatR: 10 concurrent commands&#39;               |   529.14 ns |  5.950 ns |  5.274 ns |   7.47 |    0.10 | 0.0982 |      - |    1856 B |        7.73 |
| &#39;Dispatch: 100 concurrent commands&#39;             | 7,293.79 ns | 44.497 ns | 37.157 ns | 102.93 |    1.01 | 1.0223 | 0.0229 |   19360 B |       80.67 |
| &#39;MediatR: 100 concurrent commands&#39;              | 5,014.96 ns | 54.999 ns | 51.446 ns |  70.77 |    0.93 | 0.9003 |      - |   17064 B |       71.10 |
