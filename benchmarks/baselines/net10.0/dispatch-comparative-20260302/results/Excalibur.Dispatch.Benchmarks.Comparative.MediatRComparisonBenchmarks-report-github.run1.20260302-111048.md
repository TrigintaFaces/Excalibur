```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                          | Mean        | Error      | StdDev    | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |------------:|-----------:|----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;              |    78.24 ns |   1.515 ns |  1.488 ns |   1.00 |    0.03 | 0.0126 |      - |     240 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;  |    78.40 ns |   1.213 ns |  1.075 ns |   1.00 |    0.02 | 0.0126 |      - |     240 B |        1.00 |
| &#39;Dispatch: Single command ultra-local API&#39;      |    29.72 ns |   0.222 ns |  0.197 ns |   0.38 |    0.01 | 0.0013 |      - |      24 B |        0.10 |
| &#39;MediatR: Single command handler&#39;               |    40.59 ns |   0.638 ns |  0.566 ns |   0.52 |    0.01 | 0.0080 |      - |     152 B |        0.63 |
| &#39;Dispatch: Notification to 3 handlers&#39;          |   127.07 ns |   1.177 ns |  1.101 ns |   1.62 |    0.03 | 0.0126 |      - |     240 B |        1.00 |
| &#39;MediatR: Notification to 3 handlers&#39;           |    88.71 ns |   1.784 ns |  4.824 ns |   1.13 |    0.06 | 0.0327 |      - |     616 B |        2.57 |
| &#39;Dispatch: Query with return value&#39;             |    81.75 ns |   1.256 ns |  1.113 ns |   1.05 |    0.02 | 0.0178 |      - |     336 B |        1.40 |
| &#39;Dispatch: Query with return value (typed API)&#39; |    97.92 ns |   1.966 ns |  3.003 ns |   1.25 |    0.04 | 0.0242 |      - |     456 B |        1.90 |
| &#39;Dispatch: Query ultra-local API&#39;               |    49.35 ns |   0.708 ns |  0.662 ns |   0.63 |    0.01 | 0.0102 |      - |     192 B |        0.80 |
| &#39;MediatR: Query with return value&#39;              |    46.47 ns |   0.758 ns |  0.778 ns |   0.59 |    0.01 | 0.0157 |      - |     296 B |        1.23 |
| &#39;Dispatch: Ultra-local singleton-promoted&#39;      |    30.41 ns |   0.200 ns |  0.177 ns |   0.39 |    0.01 | 0.0013 |      - |      24 B |        0.10 |
| &#39;Dispatch: Query singleton-promoted&#39;            |    53.18 ns |   0.655 ns |  0.613 ns |   0.68 |    0.01 | 0.0102 |      - |     192 B |        0.80 |
| &#39;Dispatch: 10 concurrent commands&#39;              |   921.98 ns |  18.255 ns | 26.181 ns |  11.79 |    0.40 | 0.1097 |      - |    2080 B |        8.67 |
| &#39;MediatR: 10 concurrent commands&#39;               |   497.09 ns |   5.681 ns |  5.036 ns |   6.36 |    0.13 | 0.0982 |      - |    1856 B |        7.73 |
| &#39;Dispatch: 100 concurrent commands&#39;             | 8,282.24 ns | 103.228 ns | 96.560 ns | 105.89 |    2.31 | 1.0223 | 0.0153 |   19360 B |       80.67 |
| &#39;MediatR: 100 concurrent commands&#39;              | 4,987.21 ns |  43.352 ns | 40.552 ns |  63.76 |    1.29 | 0.9003 |      - |   17064 B |       71.10 |
