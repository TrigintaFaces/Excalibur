```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                          | Mean        | Error     | StdDev    | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------ |------------:|----------:|----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;              |    69.99 ns |  1.332 ns |  1.246 ns |   1.00 |    0.02 | 0.0126 |      - |     240 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;  |    69.58 ns |  1.066 ns |  0.890 ns |   0.99 |    0.02 | 0.0126 |      - |     240 B |        1.00 |
| &#39;Dispatch: Single command ultra-local API&#39;      |    34.38 ns |  0.247 ns |  0.219 ns |   0.49 |    0.01 | 0.0013 |      - |      24 B |        0.10 |
| &#39;MediatR: Single command handler&#39;               |    41.38 ns |  0.311 ns |  0.260 ns |   0.59 |    0.01 | 0.0080 |      - |     152 B |        0.63 |
| &#39;Dispatch: Notification to 3 handlers&#39;          |   115.30 ns |  0.830 ns |  0.693 ns |   1.65 |    0.03 | 0.0126 |      - |     240 B |        1.00 |
| &#39;MediatR: Notification to 3 handlers&#39;           |    91.79 ns |  0.912 ns |  0.853 ns |   1.31 |    0.03 | 0.0327 |      - |     616 B |        2.57 |
| &#39;Dispatch: Query with return value&#39;             |    74.82 ns |  0.393 ns |  0.307 ns |   1.07 |    0.02 | 0.0178 |      - |     336 B |        1.40 |
| &#39;Dispatch: Query with return value (typed API)&#39; |    84.40 ns |  1.189 ns |  1.112 ns |   1.21 |    0.03 | 0.0229 |      - |     432 B |        1.80 |
| &#39;Dispatch: Query ultra-local API&#39;               |    52.57 ns |  0.678 ns |  0.635 ns |   0.75 |    0.02 | 0.0102 |      - |     192 B |        0.80 |
| &#39;MediatR: Query with return value&#39;              |    51.04 ns |  1.023 ns |  1.137 ns |   0.73 |    0.02 | 0.0157 |      - |     296 B |        1.23 |
| &#39;Dispatch: Ultra-local singleton-promoted&#39;      |    37.17 ns |  0.397 ns |  0.371 ns |   0.53 |    0.01 | 0.0013 |      - |      24 B |        0.10 |
| &#39;Dispatch: Query singleton-promoted&#39;            |    53.03 ns |  0.585 ns |  0.547 ns |   0.76 |    0.01 | 0.0102 |      - |     192 B |        0.80 |
| &#39;Dispatch: 10 concurrent commands&#39;              |   857.49 ns |  3.611 ns |  3.201 ns |  12.26 |    0.21 | 0.1097 |      - |    2080 B |        8.67 |
| &#39;MediatR: 10 concurrent commands&#39;               |   494.00 ns |  4.621 ns |  4.322 ns |   7.06 |    0.13 | 0.0982 |      - |    1856 B |        7.73 |
| &#39;Dispatch: 100 concurrent commands&#39;             | 7,593.95 ns | 53.346 ns | 47.290 ns | 108.53 |    1.95 | 1.0223 | 0.0229 |   19360 B |       80.67 |
| &#39;MediatR: 100 concurrent commands&#39;              | 4,670.82 ns | 41.704 ns | 36.969 ns |  66.75 |    1.24 | 0.9003 |      - |   17064 B |       71.10 |
