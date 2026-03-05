
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                                  | Mean           | Error        | StdDev        | Ratio   | RatioSD | Gen0   | Allocated | Alloc Ratio |
-------------------------------------------------------- |---------------:|-------------:|--------------:|--------:|--------:|-------:|----------:|------------:|
 'Dispatch (local): Single command'                      |    38,756.2 ns |    763.43 ns |   1,142.67 ns |   1.001 |    0.04 | 0.0610 |    6506 B |        1.00 |
 'Wolverine (in-process): Single command InvokeAsync'    |       260.3 ns |      5.80 ns |      16.92 ns |   0.007 |    0.00 | 0.0086 |     672 B |        0.10 |
 'Dispatch (local): Notification to 2 handlers'          |    86,528.7 ns |  2,274.99 ns |   6,490.66 ns |   2.235 |    0.18 | 0.1221 |   12929 B |        1.99 |
 'Wolverine (in-process): Notification to 2 handlers'    |     4,693.7 ns |    147.83 ns |     414.52 ns |   0.121 |    0.01 | 0.0534 |    4512 B |        0.69 |
 'Dispatch (local): Query with return'                   |    41,192.4 ns |    750.84 ns |   1,168.97 ns |   1.064 |    0.04 | 0.0610 |    6737 B |        1.04 |
 'Wolverine (in-process): Query with return InvokeAsync' |       368.9 ns |      7.91 ns |      22.30 ns |   0.010 |    0.00 | 0.0119 |         - |        0.00 |
 'Dispatch (local): 10 concurrent commands'              |   404,808.2 ns |  8,041.17 ns |  12,519.13 ns |  10.454 |    0.44 | 0.4883 |   64736 B |        9.95 |
 'Wolverine (in-process): 10 concurrent commands'        |     2,806.8 ns |     64.02 ns |     184.72 ns |   0.072 |    0.01 | 0.0877 |    6928 B |        1.06 |
 'Dispatch (local): 100 concurrent commands'             | 4,088,296.3 ns | 80,701.77 ns | 149,585.91 ns | 105.575 |    4.88 | 7.8125 |  645922 B |       99.28 |
 'Wolverine (in-process): 100 concurrent commands'       |    26,876.3 ns |    532.77 ns |   1,255.81 ns |   0.694 |    0.04 | 0.8850 |   68128 B |       10.47 |
