```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39;        |     71.85 ns |   0.635 ns |   0.530 ns |   1.00 |    0.01 | 0.0126 |     240 B |        1.00 |
| &#39;MediatR: 3 pipeline behaviors&#39;           |    161.08 ns |   2.541 ns |   2.377 ns |   2.24 |    0.04 | 0.0393 |     744 B |        3.10 |
| &#39;Wolverine: 3 middleware&#39;                 |    237.87 ns |   4.092 ns |   3.827 ns |   3.31 |    0.06 | 0.0405 |     768 B |        3.20 |
| &#39;MassTransit: 3 consume filters&#39;          |  2,193.96 ns |  43.243 ns |  73.430 ns |  30.54 |    1.03 | 0.2403 |    4568 B |       19.03 |
| &#39;Dispatch: 10 concurrent + 3 behaviors&#39;   |    873.18 ns |   3.510 ns |   3.112 ns |  12.15 |    0.10 | 0.1116 |    2112 B |        8.80 |
| &#39;MediatR: 10 concurrent + 3 behaviors&#39;    |  1,703.62 ns |  23.191 ns |  21.693 ns |  23.71 |    0.34 | 0.4139 |    7808 B |       32.53 |
| &#39;Wolverine: 10 concurrent + 3 middleware&#39; |  2,493.65 ns |  27.516 ns |  24.392 ns |  34.71 |    0.41 | 0.4158 |    7888 B |       32.87 |
| &#39;MassTransit: 10 concurrent + 3 filters&#39;  | 22,216.05 ns | 419.000 ns | 600.918 ns | 309.20 |    8.52 | 2.4109 |   45888 B |      191.20 |
