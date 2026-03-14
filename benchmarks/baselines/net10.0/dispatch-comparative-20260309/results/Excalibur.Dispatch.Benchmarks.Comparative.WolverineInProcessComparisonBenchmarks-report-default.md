
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                                  | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
-------------------------------------------------------- |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
 'Dispatch (local): Single command'                      |     79.75 ns |   0.536 ns |   0.475 ns |   1.00 |    0.01 | 0.0139 |      - |     264 B |        1.00 |
 'Dispatch (ultra-local): Single command'                |     42.17 ns |   0.857 ns |   1.115 ns |   0.53 |    0.01 | 0.0025 |      - |      48 B |        0.18 |
 'Wolverine (in-process): Single command InvokeAsync'    |    197.19 ns |   1.548 ns |   1.372 ns |   2.47 |    0.02 | 0.0355 |      - |     672 B |        2.55 |
 'Dispatch (local): Notification to 2 handlers'          |    116.36 ns |   1.432 ns |   1.339 ns |   1.46 |    0.02 | 0.0153 |      - |     288 B |        1.09 |
 'Wolverine (in-process): Notification to 2 handlers'    |  8,402.16 ns |  94.586 ns |  88.476 ns | 105.37 |    1.23 | 0.2365 |      - |    4512 B |       17.09 |
 'Dispatch (local): Query with return'                   |    155.51 ns |   1.586 ns |   1.324 ns |   1.95 |    0.02 | 0.0241 |      - |     456 B |        1.73 |
 'Wolverine (in-process): Query with return InvokeAsync' |    457.78 ns |   5.314 ns |   4.971 ns |   5.74 |    0.07 | 0.0496 |      - |     936 B |        3.55 |
 'Dispatch (local): 10 concurrent commands'              |  1,399.40 ns |   5.280 ns |   4.939 ns |  17.55 |    0.12 | 0.1221 |      - |    2320 B |        8.79 |
 'Wolverine (in-process): 10 concurrent commands'        |  3,628.70 ns |  36.321 ns |  32.197 ns |  45.50 |    0.47 | 0.3662 |      - |    6928 B |       26.24 |
 'Dispatch (local): 100 concurrent commands'             | 13,586.30 ns |  88.554 ns |  82.834 ns | 170.38 |    1.40 | 1.1444 | 0.0305 |   21760 B |       82.42 |
 'Wolverine (in-process): 100 concurrent commands'       | 38,118.81 ns | 504.138 ns | 471.571 ns | 478.02 |    6.35 | 3.6011 |      - |   68129 B |      258.06 |
