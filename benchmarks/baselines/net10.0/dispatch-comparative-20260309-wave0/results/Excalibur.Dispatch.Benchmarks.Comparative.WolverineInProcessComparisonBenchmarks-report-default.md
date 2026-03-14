
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                                  | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
-------------------------------------------------------- |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
 'Dispatch (local): Single command'                      |     76.76 ns |   0.911 ns |   0.760 ns |   1.00 |    0.01 | 0.0139 |      - |     264 B |        1.00 |
 'Dispatch (ultra-local): Single command'                |     40.67 ns |   0.327 ns |   0.273 ns |   0.53 |    0.01 | 0.0025 |      - |      48 B |        0.18 |
 'Wolverine (in-process): Single command InvokeAsync'    |    195.68 ns |   0.948 ns |   0.792 ns |   2.55 |    0.03 | 0.0355 |      - |     672 B |        2.55 |
 'Dispatch (local): Notification to 2 handlers'          |    117.90 ns |   1.131 ns |   0.944 ns |   1.54 |    0.02 | 0.0153 |      - |     288 B |        1.09 |
 'Wolverine (in-process): Notification to 2 handlers'    |  4,076.10 ns |  53.800 ns |  50.325 ns |  53.10 |    0.81 | 0.2365 |      - |    4512 B |       17.09 |
 'Dispatch (local): Query with return'                   |     89.97 ns |   1.751 ns |   1.873 ns |   1.17 |    0.03 | 0.0242 |      - |     456 B |        1.73 |
 'Wolverine (in-process): Query with return InvokeAsync' |    266.80 ns |   2.627 ns |   2.457 ns |   3.48 |    0.05 | 0.0496 |      - |     936 B |        3.55 |
 'Dispatch (local): 10 concurrent commands'              |    973.83 ns |  12.405 ns |  10.996 ns |  12.69 |    0.18 | 0.1221 |      - |    2320 B |        8.79 |
 'Wolverine (in-process): 10 concurrent commands'        |  2,056.08 ns |  29.325 ns |  25.996 ns |  26.79 |    0.41 | 0.3662 |      - |    6928 B |       26.24 |
 'Dispatch (local): 100 concurrent commands'             |  8,239.44 ns |  61.054 ns |  54.123 ns | 107.34 |    1.22 | 1.1444 | 0.0305 |   21760 B |       82.42 |
 'Wolverine (in-process): 100 concurrent commands'       | 20,839.17 ns | 252.809 ns | 236.478 ns | 271.49 |    3.94 | 3.6011 |      - |   68128 B |      258.06 |
