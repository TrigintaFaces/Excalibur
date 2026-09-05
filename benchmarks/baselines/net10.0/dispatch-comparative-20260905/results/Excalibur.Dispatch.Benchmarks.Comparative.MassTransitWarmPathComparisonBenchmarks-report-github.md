```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                          | Mean            | Error         | StdDev         | Median          | Ratio     | RatioSD  | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------------------------------ |----------------:|--------------:|---------------:|----------------:|----------:|---------:|---------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;                      |        46.25 ns |      0.665 ns |       0.590 ns |        46.24 ns |      1.00 |     0.02 |   0.0051 |       - |      96 B |        1.00 |
| &#39;Dispatch (tuned direct-local): Single command&#39; |        55.08 ns |      1.132 ns |       1.623 ns |        54.81 ns |      1.19 |     0.04 |   0.0051 |       - |      96 B |        1.00 |
| &#39;MassTransit: Single command&#39;                   |    17,118.48 ns |  1,246.646 ns |   3,516.187 ns |    16,485.57 ns |    370.16 |    75.79 |   1.0986 |       - |   22080 B |      230.00 |
| &#39;Dispatch: Event to 2 handlers&#39;                 |       112.10 ns |      1.572 ns |       1.313 ns |       111.72 ns |      2.42 |     0.04 |   0.0050 |       - |      96 B |        1.00 |
| &#39;MassTransit: Event to 2 consumers&#39;             |    32,799.39 ns |  1,979.862 ns |   5,743.940 ns |    32,238.26 ns |    709.24 |   123.93 |   2.1057 |  0.1526 |   39377 B |      410.18 |
| &#39;Dispatch: 10 concurrent commands&#39;              |       604.90 ns |     12.075 ns |      18.800 ns |       604.29 ns |     13.08 |     0.43 |   0.0715 |       - |    1360 B |       14.17 |
| &#39;MassTransit: 10 concurrent commands&#39;           |   187,233.96 ns |  9,482.715 ns |  27,960.000 ns |   186,156.87 ns |  4,048.67 |   603.90 |  11.4746 |  0.7324 |  219151 B |    2,282.82 |
| &#39;Dispatch: 100 concurrent commands&#39;             |     5,777.73 ns |    113.380 ns |     179.833 ns |     5,720.28 ns |    124.94 |     4.13 |   0.6409 |       - |   12160 B |      126.67 |
| &#39;MassTransit: 100 concurrent commands&#39;          | 1,522,021.74 ns | 60,710.302 ns | 179,005.707 ns | 1,512,128.91 ns | 32,911.56 | 3,874.40 | 113.2813 | 27.3438 | 2185202 B |   22,762.52 |
| &#39;Dispatch: Batch send (10)&#39;                     |       512.30 ns |     24.250 ns |      71.501 ns |       482.04 ns |     11.08 |     1.55 |   0.0505 |       - |     960 B |       10.00 |
| &#39;MassTransit: Batch send (10)&#39;                  |   160,922.30 ns |  6,709.789 ns |  19,783.965 ns |   156,855.48 ns |  3,479.72 |   428.00 |  11.4746 |  0.7324 |  219296 B |    2,284.33 |
