
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                                           | Mean          | Error        | StdDev       | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
----------------------------------------------------------------- |--------------:|-------------:|-------------:|---------:|--------:|-------:|----------:|------------:|
 'Dispatch (local): Single command'                               |      74.00 ns |     1.483 ns |     1.875 ns |     1.00 |    0.04 | 0.0015 |     120 B |        1.00 |
 'MassTransit Mediator (in-process): Single command'              |   1,675.27 ns |    33.491 ns |    84.637 ns |    22.65 |    1.27 | 0.0610 |    3544 B |       29.53 |
 'Dispatch (local): Notification to 2 handlers'                   |      88.15 ns |     1.765 ns |     2.641 ns |     1.19 |    0.05 | 0.0018 |     144 B |        1.20 |
 'MassTransit Mediator (in-process): Notification to 2 consumers' |   2,291.49 ns |    45.156 ns |    92.241 ns |    30.98 |    1.45 | 0.0534 |    4176 B |       34.80 |
 'Dispatch (local): Query with return'                            |     126.22 ns |     2.524 ns |     3.620 ns |     1.71 |    0.06 | 0.0045 |     352 B |        2.93 |
 'MassTransit Mediator (in-process): Query with return'           |  12,090.98 ns |   424.959 ns | 1,205.537 ns |   163.49 |   16.72 | 0.1221 |   11662 B |       97.18 |
 'Dispatch (local): 10 concurrent commands'                       |     800.67 ns |    15.562 ns |    15.981 ns |    10.83 |    0.34 | 0.0114 |     912 B |        7.60 |
 'MassTransit Mediator (in-process): 10 concurrent commands'      |  16,816.48 ns |   334.264 ns |   953.673 ns |   227.38 |   14.01 | 0.4578 |   35648 B |      297.07 |
 'Dispatch (local): 100 concurrent commands'                      |   7,543.17 ns |   149.045 ns |   276.265 ns |   101.99 |    4.48 | 0.0916 |    7392 B |       61.60 |
 'MassTransit Mediator (in-process): 100 concurrent commands'     | 168,445.96 ns | 3,353.350 ns | 9,728.677 ns | 2,277.63 |  142.56 | 4.3945 |  355330 B |    2,961.08 |
