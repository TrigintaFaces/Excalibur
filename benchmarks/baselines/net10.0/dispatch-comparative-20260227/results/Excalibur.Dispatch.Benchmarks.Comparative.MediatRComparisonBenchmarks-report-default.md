
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                          | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
------------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|-------:|----------:|------------:|
 'Dispatch: Single command handler'              |    133.33 ns |   2.599 ns |   2.992 ns |   1.00 |    0.03 | 0.0033 |      - |      - |     264 B |        1.00 |
 'Dispatch: Single command strict direct-local'  |    141.06 ns |   2.859 ns |   5.776 ns |   1.06 |    0.05 | 0.0033 |      - |      - |     264 B |        1.00 |
 'Dispatch: Single command ultra-local API'      |     48.65 ns |   0.525 ns |   0.491 ns |   0.37 |    0.01 | 0.0006 |      - |      - |      48 B |        0.18 |
 'MediatR: Single command handler'               |     49.55 ns |   0.977 ns |   1.431 ns |   0.37 |    0.01 | 0.0020 |      - |      - |     152 B |        0.58 |
 'Dispatch: Notification to 3 handlers'          |    176.51 ns |   3.576 ns |   5.876 ns |   1.32 |    0.05 | 0.0041 |      - |      - |     312 B |        1.18 |
 'MediatR: Notification to 3 handlers'           |    150.72 ns |   2.972 ns |   5.283 ns |   1.13 |    0.05 | 0.0079 |      - |      - |     616 B |        2.33 |
 'Dispatch: Query with return value'             |    153.71 ns |   3.112 ns |   5.996 ns |   1.15 |    0.05 | 0.0045 |      - |      - |     360 B |        1.36 |
 'Dispatch: Query with return value (typed API)' |    187.19 ns |   3.782 ns |   7.010 ns |   1.40 |    0.06 | 0.0064 |      - |      - |     496 B |        1.88 |
 'Dispatch: Query ultra-local API'               |     75.35 ns |   1.462 ns |   1.849 ns |   0.57 |    0.02 | 0.0029 | 0.0001 | 0.0001 |         - |        0.00 |
 'MediatR: Query with return value'              |     69.70 ns |   1.415 ns |   3.524 ns |   0.52 |    0.03 | 0.0038 |      - |      - |     296 B |        1.12 |
 'Dispatch: Ultra-local singleton-promoted'      |     49.45 ns |   0.995 ns |   1.146 ns |   0.37 |    0.01 | 0.0006 |      - |      - |      48 B |        0.18 |
 'Dispatch: Query singleton-promoted'            |     80.62 ns |   1.623 ns |   3.351 ns |   0.60 |    0.03 | 0.0037 |      - |      - |     216 B |        0.82 |
 'Dispatch: 10 concurrent commands'              |  1,456.31 ns |  27.440 ns |  30.499 ns |  10.93 |    0.33 | 0.0286 |      - |      - |    2320 B |        8.79 |
 'MediatR: 10 concurrent commands'               |    630.92 ns |  12.483 ns |  26.057 ns |   4.73 |    0.22 | 0.0238 |      - |      - |    1856 B |        7.03 |
 'Dispatch: 100 concurrent commands'             | 13,930.97 ns | 259.175 ns | 363.327 ns | 104.53 |    3.53 | 0.2747 |      - |      - |   21760 B |       82.42 |
 'MediatR: 100 concurrent commands'              |  5,866.95 ns | 115.977 ns | 254.572 ns |  44.02 |    2.13 | 0.2899 |      - |      - |   17064 B |       64.64 |
