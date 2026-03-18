```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                          | Mean        | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;              |    41.43 ns |   0.430 ns |   0.381 ns |   1.00 |    0.01 | 0.0089 |      - |     168 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;  |    41.27 ns |   0.327 ns |   0.290 ns |   1.00 |    0.01 | 0.0089 |      - |     168 B |        1.00 |
| &#39;Dispatch: Single command ultra-local API&#39;      |    31.81 ns |   0.161 ns |   0.151 ns |   0.77 |    0.01 | 0.0013 |      - |      24 B |        0.14 |
| &#39;MediatR: Single command handler&#39;               |    43.79 ns |   0.770 ns |   0.683 ns |   1.06 |    0.02 | 0.0080 |      - |     152 B |        0.90 |
| &#39;Dispatch: Notification to 3 handlers&#39;          |   112.92 ns |   1.000 ns |   0.835 ns |   2.73 |    0.03 | 0.0126 |      - |     240 B |        1.43 |
| &#39;MediatR: Notification to 3 handlers&#39;           |    94.37 ns |   1.977 ns |   5.829 ns |   2.28 |    0.14 | 0.0327 |      - |     616 B |        3.67 |
| &#39;Dispatch: Query with return value&#39;             |    52.89 ns |   0.482 ns |   0.402 ns |   1.28 |    0.01 | 0.0140 |      - |     264 B |        1.57 |
| &#39;Dispatch: Query with return value (typed API)&#39; |    54.93 ns |   0.930 ns |   0.870 ns |   1.33 |    0.02 | 0.0191 |      - |     360 B |        2.14 |
| &#39;Dispatch: Query ultra-local API&#39;               |    53.36 ns |   0.687 ns |   0.609 ns |   1.29 |    0.02 | 0.0102 |      - |     192 B |        1.14 |
| &#39;MediatR: Query with return value&#39;              |    50.23 ns |   0.944 ns |   0.837 ns |   1.21 |    0.02 | 0.0157 |      - |     296 B |        1.76 |
| &#39;Dispatch: Ultra-local singleton-promoted&#39;      |    32.01 ns |   0.200 ns |   0.177 ns |   0.77 |    0.01 | 0.0013 |      - |      24 B |        0.14 |
| &#39;Dispatch: Query singleton-promoted&#39;            |    52.97 ns |   0.612 ns |   0.543 ns |   1.28 |    0.02 | 0.0102 |      - |     192 B |        1.14 |
| &#39;Dispatch: 10 concurrent commands&#39;              |   562.00 ns |  10.824 ns |  11.116 ns |  13.57 |    0.29 | 0.0715 |      - |    1360 B |        8.10 |
| &#39;MediatR: 10 concurrent commands&#39;               |   526.07 ns |   2.967 ns |   2.478 ns |  12.70 |    0.13 | 0.0982 |      - |    1856 B |       11.05 |
| &#39;Dispatch: 100 concurrent commands&#39;             | 4,523.59 ns |  58.158 ns |  54.401 ns | 109.20 |    1.60 | 0.6409 | 0.0153 |   12160 B |       72.38 |
| &#39;MediatR: 100 concurrent commands&#39;              | 5,135.04 ns | 101.307 ns | 108.397 ns | 123.96 |    2.78 | 0.9003 |      - |   17064 B |      101.57 |
