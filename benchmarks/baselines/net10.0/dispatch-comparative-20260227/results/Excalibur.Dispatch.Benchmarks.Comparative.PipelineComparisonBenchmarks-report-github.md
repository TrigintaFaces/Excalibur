```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean        | Error     | StdDev    | Median      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------ |------------:|----------:|----------:|------------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39;        |    479.0 ns |   9.18 ns |  10.57 ns |    482.1 ns |  1.00 |    0.03 | 0.0610 |    1152 B |        1.00 |
| &#39;MediatR: 3 pipeline behaviors&#39;           |    124.6 ns |   2.05 ns |   1.82 ns |    124.7 ns |  0.26 |    0.01 | 0.0360 |     680 B |        0.59 |
| &#39;Wolverine: 3 middleware&#39;                 |    245.8 ns |   2.19 ns |   2.05 ns |    245.2 ns |  0.51 |    0.01 | 0.0405 |     768 B |        0.67 |
| &#39;MassTransit: 3 consume filters&#39;          |  2,059.6 ns |  40.50 ns |  71.99 ns |  2,094.1 ns |  4.30 |    0.18 | 0.2403 |    4568 B |        3.97 |
| &#39;Dispatch: 10 concurrent + 3 behaviors&#39;   |  5,493.6 ns | 107.19 ns | 110.07 ns |  5,525.3 ns | 11.47 |    0.35 | 0.5951 |   11232 B |        9.75 |
| &#39;MediatR: 10 concurrent + 3 behaviors&#39;    |  1,366.7 ns |  25.71 ns |  26.40 ns |  1,362.8 ns |  2.85 |    0.08 | 0.3796 |    7168 B |        6.22 |
| &#39;Wolverine: 10 concurrent + 3 middleware&#39; |  2,512.2 ns |  15.19 ns |  14.21 ns |  2,509.2 ns |  5.25 |    0.12 | 0.4158 |    7888 B |        6.85 |
| &#39;MassTransit: 10 concurrent + 3 filters&#39;  | 20,785.3 ns | 404.90 ns | 642.21 ns | 20,977.4 ns | 43.41 |    1.65 | 2.4109 |   45888 B |       39.83 |
