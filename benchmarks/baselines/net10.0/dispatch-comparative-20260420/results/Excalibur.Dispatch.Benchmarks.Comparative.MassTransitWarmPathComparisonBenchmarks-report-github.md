```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                 | Mean            | Error         | StdDev        | Ratio     | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|--------------------------------------- |----------------:|--------------:|--------------:|----------:|--------:|---------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;             |        78.08 ns |      0.495 ns |      0.439 ns |      1.00 |    0.01 |   0.0139 |       - |     264 B |        1.00 |
| &#39;MassTransit: Single command&#39;          |    22,849.31 ns |  1,822.442 ns |  5,228.928 ns |    292.66 |   66.66 |   1.0986 |       - |   21943 B |       83.12 |
| &#39;Dispatch: Event to 2 handlers&#39;        |       118.73 ns |      0.652 ns |      0.578 ns |      1.52 |    0.01 |   0.0153 |       - |     288 B |        1.09 |
| &#39;MassTransit: Event to 2 consumers&#39;    |    24,610.24 ns |  1,358.783 ns |  4,006.403 ns |    315.21 |   51.11 |   2.0752 |  0.1526 |   39116 B |      148.17 |
| &#39;Dispatch: 10 concurrent commands&#39;     |       912.67 ns |      7.297 ns |      6.468 ns |     11.69 |    0.10 |   0.1230 |       - |    2320 B |        8.79 |
| &#39;MassTransit: 10 concurrent commands&#39;  |   143,458.32 ns |  2,821.178 ns |  5,634.198 ns |  1,837.44 |   72.17 |  11.4746 |  0.7324 |  217845 B |      825.17 |
| &#39;Dispatch: 100 concurrent commands&#39;    |     7,903.19 ns |     67.425 ns |     63.070 ns |    101.23 |    0.96 |   1.1444 |  0.0305 |   21760 B |       82.42 |
| &#39;MassTransit: 100 concurrent commands&#39; | 1,222,661.94 ns | 23,782.700 ns | 58,339.373 ns | 15,660.07 |  747.17 | 115.2344 | 25.3906 | 2171820 B |    8,226.59 |
| &#39;Dispatch: Batch send (10)&#39;            |       672.30 ns |      3.636 ns |      3.036 ns |      8.61 |    0.06 |   0.1011 |       - |    1920 B |        7.27 |
| &#39;MassTransit: Batch send (10)&#39;         |   141,338.51 ns |  2,761.527 ns |  4,047.811 ns |  1,810.29 |   51.95 |  11.4746 |  0.7324 |  218045 B |      825.93 |
