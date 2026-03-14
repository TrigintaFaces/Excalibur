
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                                           | Mean          | Error        | StdDev       | Median        | Ratio    | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
----------------------------------------------------------------- |--------------:|-------------:|-------------:|--------------:|---------:|--------:|--------:|-------:|----------:|------------:|
 'Dispatch (local): Single command'                               |      93.71 ns |     0.379 ns |     0.336 ns |      93.75 ns |     1.00 |    0.00 |  0.0186 |      - |     352 B |        1.00 |
 'MassTransit Mediator (in-process): Single command'              |   1,415.51 ns |    27.980 ns |    43.561 ns |   1,423.05 ns |    15.10 |    0.46 |  0.1869 |      - |    3544 B |       10.07 |
 'Dispatch (local): Notification to 2 handlers'                   |     137.41 ns |     0.906 ns |     0.848 ns |     137.17 ns |     1.47 |    0.01 |  0.0198 |      - |     376 B |        1.07 |
 'MassTransit Mediator (in-process): Notification to 2 consumers' |   1,891.22 ns |    35.978 ns |    64.876 ns |   1,903.91 ns |    20.18 |    0.69 |  0.2213 |      - |    4176 B |       11.86 |
 'Dispatch (local): Query with return'                            |     111.28 ns |     2.095 ns |     1.959 ns |     111.54 ns |     1.19 |    0.02 |  0.0288 |      - |     544 B |        1.55 |
 'MassTransit Mediator (in-process): Query with return'           |  13,332.47 ns | 2,296.764 ns | 6,772.062 ns |   9,382.30 ns |   142.27 |   71.93 |  0.6180 | 0.0153 |   11661 B |       33.13 |
 'Dispatch (local): 10 concurrent commands'                       |   1,159.21 ns |    12.307 ns |    11.512 ns |   1,155.86 ns |    12.37 |    0.13 |  0.1698 |      - |    3200 B |        9.09 |
 'MassTransit Mediator (in-process): 10 concurrent commands'      |  14,558.47 ns |   286.605 ns |   635.097 ns |  14,731.41 ns |   155.35 |    6.74 |  1.8921 |      - |   35648 B |      101.27 |
 'Dispatch (local): 100 concurrent commands'                      |  10,086.57 ns |   159.901 ns |   124.840 ns |  10,041.68 ns |   107.63 |    1.33 |  1.6174 | 0.0458 |   30560 B |       86.82 |
 'MassTransit Mediator (in-process): 100 concurrent commands'     | 143,999.51 ns | 2,761.567 ns | 4,979.676 ns | 145,624.60 ns | 1,536.59 |   52.80 | 18.7988 |      - |  355330 B |    1,009.46 |
