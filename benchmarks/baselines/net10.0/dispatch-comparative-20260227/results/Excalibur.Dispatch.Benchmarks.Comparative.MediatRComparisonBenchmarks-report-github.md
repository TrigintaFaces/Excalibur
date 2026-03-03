```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                          | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|------------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;              |    127.78 ns |   0.667 ns |   0.624 ns |   1.00 |    0.01 | 0.0033 |      - |      - |     264 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;  |    131.29 ns |   1.469 ns |   1.374 ns |   1.03 |    0.01 | 0.0033 |      - |      - |     264 B |        1.00 |
| &#39;Dispatch: Single command ultra-local API&#39;      |     46.64 ns |   0.300 ns |   0.280 ns |   0.37 |    0.00 | 0.0006 |      - |      - |      48 B |        0.18 |
| &#39;MediatR: Single command handler&#39;               |     44.18 ns |   0.392 ns |   0.367 ns |   0.35 |    0.00 | 0.0020 |      - |      - |     152 B |        0.58 |
| &#39;Dispatch: Notification to 3 handlers&#39;          |    155.93 ns |   0.947 ns |   0.886 ns |   1.22 |    0.01 | 0.0041 |      - |      - |     312 B |        1.18 |
| &#39;MediatR: Notification to 3 handlers&#39;           |    110.55 ns |   1.191 ns |   1.114 ns |   0.87 |    0.01 | 0.0080 |      - |      - |     616 B |        2.33 |
| &#39;Dispatch: Query with return value&#39;             |    135.51 ns |   1.250 ns |   1.169 ns |   1.06 |    0.01 | 0.0045 |      - |      - |     360 B |        1.36 |
| &#39;Dispatch: Query with return value (typed API)&#39; |    154.60 ns |   1.578 ns |   1.317 ns |   1.21 |    0.01 | 0.0064 |      - |      - |     496 B |        1.88 |
| &#39;Dispatch: Query ultra-local API&#39;               |     71.25 ns |   0.641 ns |   0.600 ns |   0.56 |    0.01 | 0.0027 |      - |      - |     216 B |        0.82 |
| &#39;MediatR: Query with return value&#39;              |     57.88 ns |   0.278 ns |   0.260 ns |   0.45 |    0.00 | 0.0038 |      - |      - |     296 B |        1.12 |
| &#39;Dispatch: Ultra-local singleton-promoted&#39;      |     46.13 ns |   0.221 ns |   0.184 ns |   0.36 |    0.00 | 0.0006 |      - |      - |      48 B |        0.18 |
| &#39;Dispatch: Query singleton-promoted&#39;            |     78.68 ns |   1.536 ns |   1.768 ns |   0.62 |    0.01 | 0.0027 |      - |      - |     216 B |        0.82 |
| &#39;Dispatch: 10 concurrent commands&#39;              |  1,459.28 ns |  28.909 ns |  34.414 ns |  11.42 |    0.27 | 0.0286 |      - |      - |    2320 B |        8.79 |
| &#39;MediatR: 10 concurrent commands&#39;               |    604.34 ns |  11.984 ns |  22.509 ns |   4.73 |    0.18 | 0.0277 |      - |      - |    1856 B |        7.03 |
| &#39;Dispatch: 100 concurrent commands&#39;             | 13,306.71 ns | 262.923 ns | 312.991 ns | 104.14 |    2.44 | 0.2747 |      - |      - |   21760 B |       82.42 |
| &#39;MediatR: 100 concurrent commands&#39;              |  5,608.03 ns | 106.697 ns | 134.938 ns |  43.89 |    1.05 | 0.2365 | 0.0076 | 0.0076 |         - |        0.00 |
