
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                    | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|-------:|----------:|------------:|
 'Dispatch: Single command'                |     80.86 ns |   0.497 ns |   0.465 ns |   1.00 |    0.01 | 0.0033 |      - |      - |     264 B |        1.00 |
 'Dispatch: Single command (ultra-local)'  |     39.43 ns |   0.352 ns |   0.329 ns |   0.49 |    0.00 | 0.0006 |      - |      - |      48 B |        0.18 |
 'Wolverine: Single command (InvokeAsync)' |    214.48 ns |   2.389 ns |   2.118 ns |   2.65 |    0.03 | 0.0134 | 0.0005 | 0.0005 |   76178 B |      288.55 |
 'Wolverine: Single command (SendAsync)'   |  3,900.31 ns |  14.732 ns |  12.302 ns |  48.24 |    0.31 | 0.0534 |      - |      - |    4512 B |       17.09 |
 'Dispatch: Event to 2 handlers'           |    119.37 ns |   0.477 ns |   0.423 ns |   1.48 |    0.01 | 0.0036 |      - |      - |     288 B |        1.09 |
 'Wolverine: Event publish'                |  7,849.49 ns | 156.289 ns | 301.116 ns |  97.08 |    3.73 | 0.0534 |      - |      - |    4512 B |       17.09 |
 'Dispatch: 10 concurrent commands'        |  1,468.21 ns |  28.590 ns |  37.175 ns |  18.16 |    0.46 | 0.0286 |      - |      - |    2320 B |        8.79 |
 'Wolverine: 10 concurrent commands'       |  3,833.38 ns |  76.625 ns |  75.256 ns |  47.41 |    0.94 | 0.1373 | 0.0038 | 0.0038 |         - |        0.00 |
 'Dispatch: Query with return value'       |    171.76 ns |   1.677 ns |   1.400 ns |   2.12 |    0.02 | 0.0081 |      - |      - |     456 B |        1.73 |
 'Wolverine: Query with return value'      |    482.56 ns |   8.434 ns |   7.889 ns |   5.97 |    0.10 | 0.0114 |      - |      - |     936 B |        3.55 |
 'Dispatch: 100 concurrent commands'       | 13,780.10 ns | 266.179 ns | 326.891 ns | 170.42 |    4.07 | 0.2747 |      - |      - |   21760 B |       82.42 |
 'Wolverine: 100 concurrent commands'      | 37,536.13 ns | 681.580 ns | 604.203 ns | 464.23 |    7.67 | 0.8545 |      - |      - |   69729 B |      264.12 |
 'Dispatch: Batch queries (10)'            |  1,853.82 ns |  34.595 ns |  35.527 ns |  22.93 |    0.45 | 0.0687 |      - |      - |    3880 B |       14.70 |
 'Wolverine: Batch queries (10)'           |  4,844.69 ns |  87.073 ns |  81.448 ns |  59.92 |    1.03 | 0.1068 |      - |      - |    8312 B |       31.48 |
