
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                 | Mean            | Error         | StdDev        | Ratio     | RatioSD  | Gen0    | Allocated | Alloc Ratio |
--------------------------------------- |----------------:|--------------:|--------------:|----------:|---------:|--------:|----------:|------------:|
 'Dispatch: Single command'             |        68.76 ns |      0.449 ns |      0.420 ns |      1.00 |     0.01 |  0.0015 |     120 B |        1.00 |
 'MassTransit: Single command'          |    22,845.79 ns |    193.389 ns |    180.896 ns |    332.29 |     3.22 |  0.2747 |   22126 B |      184.38 |
 'Dispatch: Event to 2 handlers'        |        80.64 ns |      0.232 ns |      0.193 ns |      1.17 |     0.01 |  0.0018 |     144 B |        1.20 |
 'MassTransit: Event to 2 consumers'    |    32,137.11 ns |  2,780.271 ns |  8,110.183 ns |    467.43 |   117.43 |  0.4883 |   39388 B |      328.23 |
 'Dispatch: 10 concurrent commands'     |       782.25 ns |     14.888 ns |     14.622 ns |     11.38 |     0.22 |  0.0114 |     912 B |        7.60 |
 'MassTransit: 10 concurrent commands'  |   212,637.89 ns |  4,197.986 ns | 11,059.174 ns |  3,092.77 |   160.96 |  2.6855 |  219130 B |    1,826.08 |
 'Dispatch: 100 concurrent commands'    |     7,251.98 ns |    123.503 ns |    109.482 ns |    105.48 |     1.66 |  0.1373 |    7392 B |       61.60 |
 'MassTransit: 100 concurrent commands' | 1,848,919.31 ns | 36,670.666 ns | 84,256.960 ns | 26,892.08 | 1,226.70 | 27.3438 | 2185631 B |   18,213.59 |
 'Dispatch: Batch send (10)'            |       617.28 ns |     11.714 ns |     12.029 ns |      8.98 |     0.18 |  0.0057 |     480 B |        4.00 |
 'MassTransit: Batch send (10)'         |   220,691.05 ns |  4,520.701 ns | 13,329.389 ns |  3,209.90 |   193.89 |  2.6855 |  219275 B |    1,827.29 |
