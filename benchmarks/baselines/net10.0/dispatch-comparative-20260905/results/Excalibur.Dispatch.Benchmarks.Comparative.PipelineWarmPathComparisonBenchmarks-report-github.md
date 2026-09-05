```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39;        |     71.68 ns |   0.954 ns |   0.892 ns |   1.00 |    0.02 | 0.0126 |     240 B |        1.00 |
| &#39;MediatR: 3 pipeline behaviors&#39;           |    124.87 ns |   2.509 ns |   6.783 ns |   1.74 |    0.10 | 0.0360 |     680 B |        2.83 |
| &#39;Wolverine: 3 middleware&#39;                 |    236.34 ns |   3.182 ns |   2.977 ns |   3.30 |    0.06 | 0.0360 |     680 B |        2.83 |
| &#39;MassTransit: 3 consume filters&#39;          |  2,128.02 ns |  31.015 ns |  27.494 ns |  29.69 |    0.52 | 0.2403 |    4568 B |       19.03 |
| &#39;Dispatch: 10 concurrent + 3 behaviors&#39;   |    888.19 ns |  12.150 ns |  10.770 ns |  12.39 |    0.21 | 0.1116 |    2112 B |        8.80 |
| &#39;MediatR: 10 concurrent + 3 behaviors&#39;    |  1,314.09 ns |  26.111 ns |  71.037 ns |  18.34 |    1.01 | 0.3796 |    7168 B |       29.87 |
| &#39;Wolverine: 10 concurrent + 3 middleware&#39; |  2,432.01 ns |  33.975 ns |  31.780 ns |  33.94 |    0.59 | 0.3700 |    7008 B |       29.20 |
| &#39;MassTransit: 10 concurrent + 3 filters&#39;  | 21,023.12 ns | 418.005 ns | 795.298 ns | 293.35 |   11.54 | 2.4109 |   45888 B |      191.20 |
