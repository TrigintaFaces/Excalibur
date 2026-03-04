
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                    | Mean           | Error        | StdDev        | Ratio   | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
------------------------------------------ |---------------:|-------------:|--------------:|--------:|--------:|-------:|-------:|-------:|----------:|------------:|
 'Dispatch: Single command'                |    36,342.2 ns |    702.81 ns |     721.74 ns |   1.000 |    0.03 | 0.0610 |      - |      - |    6506 B |        1.00 |
 'Wolverine: Single command (InvokeAsync)' |       257.8 ns |      5.12 ns |       8.69 ns |   0.007 |    0.00 | 0.0088 |      - |      - |     688 B |        0.11 |
 'Wolverine: Single command (SendAsync)'   |     4,086.8 ns |     70.67 ns |      84.13 ns |   0.112 |    0.00 | 0.0534 |      - |      - |    4512 B |        0.69 |
 'Dispatch: Event to 2 handlers'           |    74,796.6 ns |  1,484.84 ns |   1,877.85 ns |   2.059 |    0.06 | 0.1221 |      - |      - |   12915 B |        1.99 |
 'Wolverine: Event publish'                |     4,084.0 ns |     77.51 ns |      79.60 ns |   0.112 |    0.00 | 0.0534 |      - |      - |    4512 B |        0.69 |
 'Dispatch: 10 concurrent commands'        |   371,666.9 ns |  6,977.64 ns |   7,755.63 ns |  10.231 |    0.29 | 0.4883 |      - |      - |   64738 B |        9.95 |
 'Wolverine: 10 concurrent commands'       |     2,659.0 ns |     45.43 ns |      54.08 ns |   0.073 |    0.00 | 0.0916 |      - |      - |    7088 B |        1.09 |
 'Dispatch: Query with return value'       |    37,288.5 ns |    381.47 ns |     356.83 ns |   1.026 |    0.02 | 0.0610 |      - |      - |    6737 B |        1.04 |
 'Wolverine: Query with return value'      |       340.5 ns |      6.62 ns |      14.10 ns |   0.009 |    0.00 | 0.0134 | 0.0010 | 0.0010 |  658728 B |      101.25 |
 'Dispatch: 100 concurrent commands'       | 3,923,081.0 ns | 77,802.52 ns | 148,027.40 ns | 107.988 |    4.53 | 7.8125 |      - |      - |  646485 B |       99.37 |
 'Wolverine: 100 concurrent commands'      |    29,724.0 ns |  1,301.55 ns |   3,817.22 ns |   0.818 |    0.11 | 0.8850 |      - |      - |   69728 B |       10.72 |
 'Dispatch: Batch queries (10)'            |   427,521.1 ns | 11,982.41 ns |  33,992.11 ns |  11.768 |    0.96 | 0.4883 |      - |      - |   66692 B |       10.25 |
 'Wolverine: Batch queries (10)'           |     3,159.2 ns |     51.61 ns |      45.75 ns |   0.087 |    0.00 | 0.1411 |      - |      - |    8312 B |        1.28 |
