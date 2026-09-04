```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39;        |     70.60 ns |   1.385 ns |   1.295 ns |   1.00 |    0.02 | 0.0126 |     240 B |        1.00 |
| &#39;MediatR: 3 pipeline behaviors&#39;           |    164.03 ns |   2.433 ns |   2.276 ns |   2.32 |    0.05 | 0.0393 |     744 B |        3.10 |
| &#39;Wolverine: 3 middleware&#39;                 |    234.18 ns |   0.912 ns |   0.762 ns |   3.32 |    0.06 | 0.0360 |     680 B |        2.83 |
| &#39;MassTransit: 3 consume filters&#39;          |  2,222.07 ns |  44.330 ns |  66.351 ns |  31.48 |    1.08 | 0.2403 |    4568 B |       19.03 |
| &#39;Dispatch: 10 concurrent + 3 behaviors&#39;   |    898.67 ns |   4.950 ns |   3.865 ns |  12.73 |    0.23 | 0.1116 |    2112 B |        8.80 |
| &#39;MediatR: 10 concurrent + 3 behaviors&#39;    |  1,762.72 ns |  28.941 ns |  30.966 ns |  24.98 |    0.61 | 0.4139 |    7808 B |       32.53 |
| &#39;Wolverine: 10 concurrent + 3 middleware&#39; |  2,440.97 ns |  14.926 ns |  13.231 ns |  34.59 |    0.63 | 0.3700 |    7008 B |       29.20 |
| &#39;MassTransit: 10 concurrent + 3 filters&#39;  | 21,990.43 ns | 435.169 ns | 784.700 ns | 311.58 |   12.26 | 2.4109 |   45888 B |      191.20 |
