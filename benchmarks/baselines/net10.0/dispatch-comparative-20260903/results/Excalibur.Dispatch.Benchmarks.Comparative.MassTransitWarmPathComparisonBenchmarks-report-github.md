```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                          | Mean            | Error         | StdDev        | Median          | Ratio     | RatioSD  | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------------------------------ |----------------:|--------------:|--------------:|----------------:|----------:|---------:|---------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;                      |        52.24 ns |      0.403 ns |      0.357 ns |        52.23 ns |      1.00 |     0.01 |   0.0051 |       - |      96 B |        1.00 |
| &#39;Dispatch (tuned direct-local): Single command&#39; |        33.49 ns |      0.183 ns |      0.143 ns |        33.44 ns |      0.64 |     0.00 |   0.0013 |       - |      24 B |        0.25 |
| &#39;MassTransit: Single command&#39;                   |    16,279.74 ns |    535.728 ns |  1,502.238 ns |    15,880.79 ns |    311.62 |    28.68 |   1.0986 |       - |   21957 B |      228.72 |
| &#39;Dispatch: Event to 2 handlers&#39;                 |       117.38 ns |      1.026 ns |      0.910 ns |       116.95 ns |      2.25 |     0.02 |   0.0050 |       - |      96 B |        1.00 |
| &#39;MassTransit: Event to 2 consumers&#39;             |    29,615.11 ns |  1,709.146 ns |  4,764.415 ns |    30,455.32 ns |    566.89 |    90.81 |   2.0752 |  0.1221 |   39132 B |      407.62 |
| &#39;Dispatch: 10 concurrent commands&#39;              |       645.18 ns |     12.191 ns |     11.973 ns |       641.04 ns |     12.35 |     0.24 |   0.0715 |       - |    1360 B |       14.17 |
| &#39;MassTransit: 10 concurrent commands&#39;           |   160,720.25 ns |  4,433.907 ns | 12,359.962 ns |   158,058.22 ns |  3,076.47 |   236.24 |  11.4746 |  0.7324 |  217826 B |    2,269.02 |
| &#39;Dispatch: 100 concurrent commands&#39;             |     6,038.71 ns |     98.690 ns |     87.486 ns |     5,996.04 ns |    115.59 |     1.79 |   0.6409 |       - |   12160 B |      126.67 |
| &#39;MassTransit: 100 concurrent commands&#39;          | 1,320,088.54 ns | 34,049.228 ns | 98,782.991 ns | 1,290,054.49 ns | 25,268.86 | 1,889.14 | 115.2344 | 29.2969 | 2173189 B |   22,637.39 |
| &#39;Dispatch: Batch send (10)&#39;                     |       507.63 ns |      9.929 ns |      9.288 ns |       506.83 ns |      9.72 |     0.18 |   0.0505 |       - |     960 B |       10.00 |
| &#39;MassTransit: Batch send (10)&#39;                  |   158,315.86 ns |  3,515.007 ns |  9,856.458 ns |   156,625.49 ns |  3,030.45 |   188.76 |  11.4746 |  0.7324 |  218029 B |    2,271.14 |
