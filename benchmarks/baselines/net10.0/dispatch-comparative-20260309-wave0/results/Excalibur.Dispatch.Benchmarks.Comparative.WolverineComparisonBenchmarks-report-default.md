
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                    | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
 'Dispatch: Single command'                |     80.20 ns |   1.076 ns |   1.006 ns |   1.00 |    0.02 | 0.0139 |      - |     264 B |        1.00 |
 'Dispatch: Single command (ultra-local)'  |     41.49 ns |   0.402 ns |   0.356 ns |   0.52 |    0.01 | 0.0025 |      - |      48 B |        0.18 |
 'Wolverine: Single command (InvokeAsync)' |    202.41 ns |   1.696 ns |   1.504 ns |   2.52 |    0.04 | 0.0365 |      - |     688 B |        2.61 |
 'Wolverine: Single command (SendAsync)'   |  4,209.60 ns |  39.361 ns |  36.818 ns |  52.49 |    0.78 | 0.2365 |      - |    4512 B |       17.09 |
 'Dispatch: Event to 2 handlers'           |    119.12 ns |   1.272 ns |   1.128 ns |   1.49 |    0.02 | 0.0153 |      - |     288 B |        1.09 |
 'Wolverine: Event publish'                |  4,029.36 ns |  20.553 ns |  18.220 ns |  50.25 |    0.65 | 0.2365 |      - |    4512 B |       17.09 |
 'Dispatch: 10 concurrent commands'        |    975.46 ns |  13.541 ns |  11.307 ns |  12.16 |    0.20 | 0.1221 |      - |    2320 B |        8.79 |
 'Wolverine: 10 concurrent commands'       |  2,092.16 ns |  28.134 ns |  26.317 ns |  26.09 |    0.45 | 0.3738 |      - |    7088 B |       26.85 |
 'Dispatch: Query with return value'       |     86.45 ns |   0.600 ns |   0.501 ns |   1.08 |    0.01 | 0.0242 |      - |     456 B |        1.73 |
 'Wolverine: Query with return value'      |    265.48 ns |   2.702 ns |   2.527 ns |   3.31 |    0.05 | 0.0496 |      - |     936 B |        3.55 |
 'Dispatch: 100 concurrent commands'       |  8,374.11 ns |  40.369 ns |  35.786 ns | 104.42 |    1.34 | 1.1444 | 0.0305 |   21760 B |       82.42 |
 'Wolverine: 100 concurrent commands'      | 20,370.98 ns | 167.991 ns | 157.139 ns | 254.02 |    3.63 | 3.6926 |      - |   69728 B |      264.12 |
 'Dispatch: Batch queries (10)'            |  1,085.85 ns |  14.939 ns |  13.243 ns |  13.54 |    0.23 | 0.2060 |      - |    3880 B |       14.70 |
 'Wolverine: Batch queries (10)'           |  2,611.45 ns |  15.255 ns |  13.523 ns |  32.56 |    0.43 | 0.4387 |      - |    8312 B |       31.48 |
