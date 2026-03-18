```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean        | Error     | StdDev    | Median      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------ |------------:|----------:|----------:|------------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39;        |    285.4 ns |   7.09 ns |  20.80 ns |    294.9 ns |  1.01 |    0.11 | 0.0205 |     392 B |        1.00 |
| &#39;MediatR: 3 pipeline behaviors&#39;           |    119.4 ns |   2.38 ns |   4.10 ns |    118.2 ns |  0.42 |    0.03 | 0.0361 |     680 B |        1.73 |
| &#39;Wolverine: 3 middleware&#39;                 |    236.6 ns |   2.08 ns |   1.95 ns |    236.9 ns |  0.83 |    0.06 | 0.0405 |     768 B |        1.96 |
| &#39;MassTransit: 3 consume filters&#39;          |  1,974.4 ns |  39.04 ns | 104.88 ns |  2,012.8 ns |  6.96 |    0.64 | 0.2403 |    4568 B |       11.65 |
| &#39;Dispatch: 10 concurrent + 3 behaviors&#39;   |  2,998.1 ns |  59.97 ns | 168.15 ns |  3,073.9 ns | 10.56 |    1.00 | 0.1907 |    3632 B |        9.27 |
| &#39;MediatR: 10 concurrent + 3 behaviors&#39;    |  1,283.4 ns |  25.45 ns |  51.99 ns |  1,284.9 ns |  4.52 |    0.39 | 0.3796 |    7168 B |       18.29 |
| &#39;Wolverine: 10 concurrent + 3 middleware&#39; |  2,429.6 ns |  40.24 ns |  44.72 ns |  2,425.0 ns |  8.56 |    0.67 | 0.4158 |    7888 B |       20.12 |
| &#39;MassTransit: 10 concurrent + 3 filters&#39;  | 19,805.1 ns | 391.79 ns | 834.94 ns | 20,064.3 ns | 69.78 |    6.04 | 2.4109 |   45888 B |      117.06 |
