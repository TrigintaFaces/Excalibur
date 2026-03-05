
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                          | Mean        | Error     | StdDev    | Ratio  | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
------------------------------------------------ |------------:|----------:|----------:|-------:|--------:|-------:|-------:|-------:|----------:|------------:|
 'Dispatch: Single command handler'              |    75.32 ns |  0.732 ns |  0.684 ns |   1.00 |    0.01 | 0.0031 |      - |      - |     240 B |        1.00 |
 'Dispatch: Single command strict direct-local'  |    70.63 ns |  0.484 ns |  0.453 ns |   0.94 |    0.01 | 0.0031 |      - |      - |     240 B |        1.00 |
 'Dispatch: Single command ultra-local API'      |    31.54 ns |  0.244 ns |  0.229 ns |   0.42 |    0.00 | 0.0003 |      - |      - |      24 B |        0.10 |
 'MediatR: Single command handler'               |    47.27 ns |  0.373 ns |  0.331 ns |   0.63 |    0.01 | 0.0020 |      - |      - |     152 B |        0.63 |
 'Dispatch: Notification to 3 handlers'          |   118.65 ns |  0.493 ns |  0.385 ns |   1.58 |    0.01 | 0.0031 |      - |      - |     240 B |        1.00 |
 'MediatR: Notification to 3 handlers'           |   119.24 ns |  0.892 ns |  0.790 ns |   1.58 |    0.02 | 0.0079 |      - |      - |     616 B |        2.57 |
 'Dispatch: Query with return value'             |    83.57 ns |  0.816 ns |  0.764 ns |   1.11 |    0.01 | 0.0043 |      - |      - |     336 B |        1.40 |
 'Dispatch: Query with return value (typed API)' |    90.97 ns |  0.572 ns |  0.535 ns |   1.21 |    0.01 | 0.0056 |      - |      - |     432 B |        1.80 |
 'Dispatch: Query ultra-local API'               |    58.27 ns |  0.378 ns |  0.295 ns |   0.77 |    0.01 | 0.0029 | 0.0001 | 0.0001 |     165 B |        0.69 |
 'MediatR: Query with return value'              |    62.38 ns |  0.556 ns |  0.520 ns |   0.83 |    0.01 | 0.0038 |      - |      - |     296 B |        1.23 |
 'Dispatch: Ultra-local singleton-promoted'      |    31.73 ns |  0.243 ns |  0.215 ns |   0.42 |    0.00 | 0.0003 |      - |      - |      24 B |        0.10 |
 'Dispatch: Query singleton-promoted'            |    58.23 ns |  0.747 ns |  0.699 ns |   0.77 |    0.01 | 0.0035 | 0.0001 | 0.0001 |         - |        0.00 |
 'Dispatch: 10 concurrent commands'              |   879.24 ns |  9.735 ns |  8.630 ns |  11.67 |    0.15 | 0.0277 | 0.0010 | 0.0010 |  361864 B |    1,507.77 |
 'MediatR: 10 concurrent commands'               |   544.39 ns |  3.374 ns |  2.991 ns |   7.23 |    0.07 | 0.0238 |      - |      - |    1856 B |        7.73 |
 'Dispatch: 100 concurrent commands'             | 7,539.10 ns | 27.728 ns | 23.154 ns | 100.10 |    0.92 | 0.2441 |      - |      - |   19360 B |       80.67 |
 'MediatR: 100 concurrent commands'              | 5,160.23 ns | 60.924 ns | 56.989 ns |  68.52 |    0.95 | 0.2213 |      - |      - |   17064 B |       71.10 |
