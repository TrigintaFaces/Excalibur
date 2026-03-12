
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                 | Mean            | Error        | StdDev       | Ratio     | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
--------------------------------------- |----------------:|-------------:|-------------:|----------:|--------:|---------:|--------:|----------:|------------:|
 'Dispatch: Single command'             |        71.37 ns |     0.599 ns |     0.560 ns |      1.00 |    0.01 |   0.0139 |       - |     264 B |        1.00 |
 'MassTransit: Single command'          |    22,442.93 ns |   430.986 ns |   359.893 ns |    314.46 |    5.42 |   1.0986 |       - |   22112 B |       83.76 |
 'Dispatch: Event to 2 handlers'        |       114.37 ns |     0.585 ns |     0.547 ns |      1.60 |    0.01 |   0.0153 |       - |     288 B |        1.09 |
 'MassTransit: Event to 2 consumers'    |    24,672.04 ns |   293.630 ns |   245.195 ns |    345.70 |    4.24 |   2.1057 |  0.1221 |   39408 B |      149.27 |
 'Dispatch: 10 concurrent commands'     |       883.64 ns |    11.103 ns |    10.386 ns |     12.38 |    0.17 |   0.1230 |       - |    2320 B |        8.79 |
 'MassTransit: 10 concurrent commands'  |   127,912.95 ns | 1,400.867 ns | 1,241.832 ns |  1,792.27 |   21.69 |  11.4746 |  0.7324 |  219087 B |      829.88 |
 'Dispatch: 100 concurrent commands'    |     7,639.30 ns |    31.537 ns |    29.500 ns |    107.04 |    0.91 |   1.1444 |  0.0305 |   21760 B |       82.42 |
 'MassTransit: 100 concurrent commands' | 1,092,889.41 ns | 8,044.606 ns | 7,131.333 ns | 15,313.16 |  151.79 | 115.2344 | 25.3906 | 2184517 B |    8,274.69 |
 'Dispatch: Batch send (10)'            |       659.92 ns |     5.082 ns |     4.505 ns |      9.25 |    0.09 |   0.1011 |       - |    1920 B |        7.27 |
 'MassTransit: Batch send (10)'         |   128,911.55 ns |   700.275 ns |   620.776 ns |  1,806.26 |   16.17 |  11.4746 |  0.7324 |  219316 B |      830.74 |
