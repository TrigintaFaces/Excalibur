
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                                           | Mean         | Error       | StdDev      | Ratio  | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
----------------------------------------------------------------- |-------------:|------------:|------------:|-------:|--------:|-------:|-------:|-------:|----------:|------------:|
 'Dispatch (local): Single command'                               |     178.2 ns |     2.32 ns |     2.17 ns |   1.00 |    0.02 | 0.0045 |      - |      - |     352 B |        1.00 |
 'MassTransit Mediator (in-process): Single command'              |   4,120.8 ns |   131.65 ns |   388.17 ns |  23.12 |    2.19 | 0.0458 |      - |      - |    3544 B |       10.07 |
 'Dispatch (local): Notification to 2 handlers'                   |     261.5 ns |     5.02 ns |     5.78 ns |   1.47 |    0.04 | 0.0048 |      - |      - |     376 B |        1.07 |
 'MassTransit Mediator (in-process): Notification to 2 consumers' |   5,742.8 ns |   198.61 ns |   585.59 ns |  32.22 |    3.29 | 0.0534 |      - |      - |    4176 B |       11.86 |
 'Dispatch (local): Query with return'                            |     117.7 ns |     0.82 ns |     0.77 ns |   0.66 |    0.01 | 0.0069 |      - |      - |     544 B |        1.55 |
 'MassTransit Mediator (in-process): Query with return'           |   6,553.7 ns |   105.49 ns |    93.52 ns |  36.77 |    0.67 | 0.2670 | 0.0153 | 0.0153 |  472995 B |    1,343.74 |
 'Dispatch (local): 10 concurrent commands'                       |   1,196.9 ns |    11.88 ns |    11.11 ns |   6.72 |    0.10 | 0.0401 |      - |      - |    3200 B |        9.09 |
 'MassTransit Mediator (in-process): 10 concurrent commands'      |  14,750.7 ns |   108.46 ns |    96.15 ns |  82.77 |    1.11 | 0.4578 |      - |      - |   35648 B |      101.27 |
 'Dispatch (local): 100 concurrent commands'                      |  10,905.2 ns |    72.94 ns |    64.66 ns |  61.19 |    0.80 | 0.3967 |      - |      - |   30560 B |       86.82 |
 'MassTransit Mediator (in-process): 100 concurrent commands'     | 147,353.3 ns | 1,826.85 ns | 1,708.83 ns | 826.82 |   13.48 | 4.3945 |      - |      - |  355330 B |    1,009.46 |
