
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                                           | Mean          | Error      | StdDev     | Ratio    | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
----------------------------------------------------------------- |--------------:|-----------:|-----------:|---------:|--------:|--------:|-------:|----------:|------------:|
 'Dispatch (local): Single command'                               |      67.20 ns |   0.346 ns |   0.307 ns |     1.00 |    0.01 |  0.0063 |      - |     120 B |        1.00 |
 'MassTransit Mediator (in-process): Single command'              |   1,185.70 ns |  23.504 ns |  54.005 ns |    17.65 |    0.80 |  0.1869 |      - |    3544 B |       29.53 |
 'Dispatch (local): Notification to 2 handlers'                   |      83.36 ns |   0.405 ns |   0.338 ns |     1.24 |    0.01 |  0.0076 |      - |     144 B |        1.20 |
 'MassTransit Mediator (in-process): Notification to 2 consumers' |   1,705.15 ns |  33.611 ns |  59.744 ns |    25.38 |    0.89 |  0.2213 |      - |    4176 B |       34.80 |
 'Dispatch (local): Query with return'                            |      95.19 ns |   1.241 ns |   1.036 ns |     1.42 |    0.02 |  0.0186 |      - |     352 B |        2.93 |
 'MassTransit Mediator (in-process): Query with return'           |  18,771.04 ns | 363.020 ns | 388.428 ns |   279.35 |    5.76 |  0.6104 |      - |   11658 B |       97.15 |
 'Dispatch (local): 10 concurrent commands'                       |     779.68 ns |   7.302 ns |   6.097 ns |    11.60 |    0.10 |  0.0477 |      - |     912 B |        7.60 |
 'MassTransit Mediator (in-process): 10 concurrent commands'      |  11,924.61 ns | 237.753 ns | 469.302 ns |   177.46 |    6.96 |  1.8921 |      - |   35648 B |      297.07 |
 'Dispatch (local): 100 concurrent commands'                      |   7,037.86 ns |  88.689 ns |  78.620 ns |   104.74 |    1.22 |  0.3891 |      - |    7392 B |       61.60 |
 'MassTransit Mediator (in-process): 100 concurrent commands'     | 120,396.41 ns | 807.415 ns | 674.228 ns | 1,791.75 |   12.47 | 18.7988 | 0.1221 |  355329 B |    2,961.07 |
