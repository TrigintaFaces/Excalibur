
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                 | Mean            | Error         | StdDev        | Median          | Ratio     | RatioSD  | Gen0     | Gen1    | Allocated | Alloc Ratio |
--------------------------------------- |----------------:|--------------:|--------------:|----------------:|----------:|---------:|---------:|--------:|----------:|------------:|
 'Dispatch: Single command'             |        72.68 ns |      0.687 ns |      0.609 ns |        72.53 ns |      1.00 |     0.01 |   0.0139 |       - |     264 B |        1.00 |
 'MassTransit: Single command'          |    23,002.41 ns |    435.135 ns |    517.997 ns |    22,953.04 ns |    316.52 |     7.42 |   1.0986 |       - |   22111 B |       83.75 |
 'Dispatch: Event to 2 handlers'        |       115.77 ns |      1.314 ns |      1.229 ns |       115.37 ns |      1.59 |     0.02 |   0.0153 |       - |     288 B |        1.09 |
 'MassTransit: Event to 2 consumers'    |    23,487.47 ns |  1,321.719 ns |  3,834.546 ns |    22,082.03 ns |    323.20 |    52.58 |   2.1057 |  0.1221 |   39389 B |      149.20 |
 'Dispatch: 10 concurrent commands'     |       882.48 ns |      8.787 ns |      7.789 ns |       883.99 ns |     12.14 |     0.14 |   0.1230 |       - |    2320 B |        8.79 |
 'MassTransit: 10 concurrent commands'  |   150,963.63 ns |  5,460.331 ns | 16,014.206 ns |   147,009.20 ns |  2,077.33 |   219.97 |  11.4746 |  0.7324 |  219089 B |      829.88 |
 'Dispatch: 100 concurrent commands'    |     7,679.24 ns |     28.694 ns |     26.840 ns |     7,676.66 ns |    105.67 |     0.93 |   1.1444 |  0.0305 |   21760 B |       82.42 |
 'MassTransit: 100 concurrent commands' | 1,265,028.06 ns | 28,257.951 ns | 81,530.655 ns | 1,265,880.37 ns | 17,407.37 | 1,125.29 | 115.2344 | 27.3438 | 2184242 B |    8,273.64 |
 'Dispatch: Batch send (10)'            |       662.73 ns |      4.813 ns |      4.019 ns |       663.46 ns |      9.12 |     0.09 |   0.1011 |       - |    1920 B |        7.27 |
 'MassTransit: Batch send (10)'         |   152,462.98 ns |  5,450.513 ns | 16,070.961 ns |   147,868.53 ns |  2,097.96 |   220.77 |  11.4746 |  0.7324 |  219313 B |      830.73 |
