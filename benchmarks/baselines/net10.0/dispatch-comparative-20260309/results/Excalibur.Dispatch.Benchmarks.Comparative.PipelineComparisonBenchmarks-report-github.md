```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean        | Error     | StdDev    | Median      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------ |------------:|----------:|----------:|------------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39;        |    357.4 ns |   7.16 ns |  16.32 ns |    364.0 ns |  1.00 |    0.07 | 0.0281 |     536 B |        1.00 |
| &#39;MediatR: 3 pipeline behaviors&#39;           |    163.1 ns |   2.95 ns |   2.76 ns |    162.0 ns |  0.46 |    0.02 | 0.0393 |     744 B |        1.39 |
| &#39;Wolverine: 3 middleware&#39;                 |    237.5 ns |   1.84 ns |   1.53 ns |    237.3 ns |  0.67 |    0.03 | 0.0405 |     768 B |        1.43 |
| &#39;MassTransit: 3 consume filters&#39;          |  2,234.2 ns |  44.32 ns | 110.37 ns |  2,280.7 ns |  6.27 |    0.44 | 0.2403 |    4568 B |        8.52 |
| &#39;Dispatch: 10 concurrent + 3 behaviors&#39;   |  3,543.5 ns |  70.89 ns | 167.09 ns |  3,634.5 ns |  9.94 |    0.68 | 0.2670 |    5072 B |        9.46 |
| &#39;MediatR: 10 concurrent + 3 behaviors&#39;    |  1,648.1 ns |  14.13 ns |  13.22 ns |  1,646.1 ns |  4.62 |    0.23 | 0.4139 |    7808 B |       14.57 |
| &#39;Wolverine: 10 concurrent + 3 middleware&#39; |  2,453.9 ns |  34.97 ns |  31.00 ns |  2,453.8 ns |  6.88 |    0.35 | 0.4158 |    7888 B |       14.72 |
| &#39;MassTransit: 10 concurrent + 3 filters&#39;  | 22,624.4 ns | 451.83 ns | 742.37 ns | 22,913.6 ns | 63.45 |    3.74 | 2.4109 |   45888 B |       85.61 |
