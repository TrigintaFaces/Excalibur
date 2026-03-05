```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                          | Mean        | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;              |    80.78 ns |   1.339 ns |   1.118 ns |   1.00 |    0.02 | 0.0126 |      - |     240 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;  |    79.08 ns |   1.059 ns |   0.990 ns |   0.98 |    0.02 | 0.0126 |      - |     240 B |        1.00 |
| &#39;Dispatch: Single command ultra-local API&#39;      |    30.31 ns |   0.164 ns |   0.137 ns |   0.38 |    0.01 | 0.0013 |      - |      24 B |        0.10 |
| &#39;MediatR: Single command handler&#39;               |    41.08 ns |   0.813 ns |   1.057 ns |   0.51 |    0.01 | 0.0080 |      - |     152 B |        0.63 |
| &#39;Dispatch: Notification to 3 handlers&#39;          |   128.90 ns |   1.199 ns |   1.063 ns |   1.60 |    0.03 | 0.0126 |      - |     240 B |        1.00 |
| &#39;MediatR: Notification to 3 handlers&#39;           |    88.74 ns |   1.780 ns |   3.676 ns |   1.10 |    0.05 | 0.0327 |      - |     616 B |        2.57 |
| &#39;Dispatch: Query with return value&#39;             |    82.92 ns |   1.690 ns |   1.660 ns |   1.03 |    0.02 | 0.0178 |      - |     336 B |        1.40 |
| &#39;Dispatch: Query with return value (typed API)&#39; |    99.04 ns |   1.480 ns |   1.312 ns |   1.23 |    0.02 | 0.0242 |      - |     456 B |        1.90 |
| &#39;Dispatch: Query ultra-local API&#39;               |    51.48 ns |   0.488 ns |   0.432 ns |   0.64 |    0.01 | 0.0102 |      - |     192 B |        0.80 |
| &#39;MediatR: Query with return value&#39;              |    44.31 ns |   0.903 ns |   1.040 ns |   0.55 |    0.01 | 0.0157 |      - |     296 B |        1.23 |
| &#39;Dispatch: Ultra-local singleton-promoted&#39;      |    30.54 ns |   0.356 ns |   0.333 ns |   0.38 |    0.01 | 0.0013 |      - |      24 B |        0.10 |
| &#39;Dispatch: Query singleton-promoted&#39;            |    52.34 ns |   0.608 ns |   0.569 ns |   0.65 |    0.01 | 0.0102 |      - |     192 B |        0.80 |
| &#39;Dispatch: 10 concurrent commands&#39;              |   957.35 ns |  14.790 ns |  13.835 ns |  11.85 |    0.23 | 0.1087 |      - |    2080 B |        8.67 |
| &#39;MediatR: 10 concurrent commands&#39;               |   518.37 ns |   7.547 ns |   6.302 ns |   6.42 |    0.11 | 0.0982 |      - |    1856 B |        7.73 |
| &#39;Dispatch: 100 concurrent commands&#39;             | 8,359.25 ns | 165.640 ns | 154.940 ns | 103.50 |    2.33 | 1.0223 | 0.0153 |   19360 B |       80.67 |
| &#39;MediatR: 100 concurrent commands&#39;              | 4,814.43 ns |  91.989 ns | 198.015 ns |  59.61 |    2.56 | 0.9003 |      - |   17064 B |       71.10 |
