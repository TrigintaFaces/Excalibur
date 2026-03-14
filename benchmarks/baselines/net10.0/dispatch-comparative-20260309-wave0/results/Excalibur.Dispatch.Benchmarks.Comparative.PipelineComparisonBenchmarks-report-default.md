
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                    | Mean        | Error     | StdDev    | Median      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
------------------------------------------ |------------:|----------:|----------:|------------:|------:|--------:|-------:|----------:|------------:|
 'Dispatch: 3 middleware behaviors'        |    348.9 ns |   6.95 ns |  19.02 ns |    358.3 ns |  1.00 |    0.08 | 0.0281 |     536 B |        1.00 |
 'MediatR: 3 pipeline behaviors'           |    163.5 ns |   1.13 ns |   1.00 ns |    163.4 ns |  0.47 |    0.03 | 0.0393 |     744 B |        1.39 |
 'Wolverine: 3 middleware'                 |    238.9 ns |   2.73 ns |   2.28 ns |    238.7 ns |  0.69 |    0.04 | 0.0405 |     768 B |        1.43 |
 'MassTransit: 3 consume filters'          |  2,331.5 ns |  46.22 ns |  61.70 ns |  2,349.7 ns |  6.70 |    0.43 | 0.2403 |    4568 B |        8.52 |
 'Dispatch: 10 concurrent + 3 behaviors'   |  3,623.1 ns |  69.07 ns |  84.82 ns |  3,638.7 ns | 10.42 |    0.65 | 0.2670 |    5072 B |        9.46 |
 'MediatR: 10 concurrent + 3 behaviors'    |  1,656.9 ns |  18.65 ns |  15.57 ns |  1,654.6 ns |  4.76 |    0.28 | 0.4139 |    7808 B |       14.57 |
 'Wolverine: 10 concurrent + 3 middleware' |  2,528.6 ns |  20.69 ns |  19.36 ns |  2,532.2 ns |  7.27 |    0.42 | 0.4158 |    7888 B |       14.72 |
 'MassTransit: 10 concurrent + 3 filters'  | 23,624.9 ns | 455.74 ns | 608.39 ns | 23,804.7 ns | 67.92 |    4.29 | 2.4109 |   45888 B |       85.61 |
