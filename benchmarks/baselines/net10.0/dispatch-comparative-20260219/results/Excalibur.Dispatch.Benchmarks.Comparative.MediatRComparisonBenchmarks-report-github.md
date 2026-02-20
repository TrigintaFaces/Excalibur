```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                          | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;              |    118.79 ns |   2.028 ns |   1.798 ns |   1.00 |    0.02 | 0.0138 |      - |     264 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;  |    116.29 ns |   2.240 ns |   2.095 ns |   0.98 |    0.02 | 0.0138 |      - |     264 B |        1.00 |
| &#39;Dispatch: Single command ultra-local API&#39;      |     47.12 ns |   0.716 ns |   0.735 ns |   0.40 |    0.01 | 0.0025 |      - |      48 B |        0.18 |
| &#39;MediatR: Single command handler&#39;               |     40.92 ns |   0.422 ns |   0.395 ns |   0.34 |    0.01 | 0.0080 |      - |     152 B |        0.58 |
| &#39;Dispatch: Notification to 3 handlers&#39;          |    154.47 ns |   0.740 ns |   0.656 ns |   1.30 |    0.02 | 0.0165 |      - |     312 B |        1.18 |
| &#39;MediatR: Notification to 3 handlers&#39;           |     96.10 ns |   1.883 ns |   2.514 ns |   0.81 |    0.02 | 0.0327 |      - |     616 B |        2.33 |
| &#39;Dispatch: Query with return value&#39;             |    126.63 ns |   1.476 ns |   1.380 ns |   1.07 |    0.02 | 0.0191 |      - |     360 B |        1.36 |
| &#39;Dispatch: Query with return value (typed API)&#39; |    142.81 ns |   2.628 ns |   2.194 ns |   1.20 |    0.02 | 0.0262 |      - |     496 B |        1.88 |
| &#39;Dispatch: Query ultra-local API&#39;               |     66.94 ns |   0.832 ns |   0.778 ns |   0.56 |    0.01 | 0.0114 |      - |     216 B |        0.82 |
| &#39;MediatR: Query with return value&#39;              |     49.29 ns |   0.971 ns |   1.262 ns |   0.41 |    0.01 | 0.0157 |      - |     296 B |        1.12 |
| &#39;Dispatch: Ultra-local singleton-promoted&#39;      |     45.89 ns |   0.448 ns |   0.397 ns |   0.39 |    0.01 | 0.0025 |      - |      48 B |        0.18 |
| &#39;Dispatch: Query singleton-promoted&#39;            |     67.92 ns |   0.233 ns |   0.194 ns |   0.57 |    0.01 | 0.0114 |      - |     216 B |        0.82 |
| &#39;Dispatch: 10 concurrent commands&#39;              |  1,244.58 ns |   6.552 ns |   6.128 ns |  10.48 |    0.16 | 0.1221 |      - |    2320 B |        8.79 |
| &#39;MediatR: 10 concurrent commands&#39;               |    497.81 ns |   5.808 ns |   5.149 ns |   4.19 |    0.07 | 0.0982 |      - |    1856 B |        7.03 |
| &#39;Dispatch: 100 concurrent commands&#39;             | 12,107.20 ns | 183.027 ns | 152.836 ns | 101.94 |    1.92 | 1.1444 | 0.0305 |   21760 B |       82.42 |
| &#39;MediatR: 100 concurrent commands&#39;              |  4,797.88 ns |  61.466 ns |  47.989 ns |  40.40 |    0.70 | 0.9003 |      - |   17064 B |       64.64 |
