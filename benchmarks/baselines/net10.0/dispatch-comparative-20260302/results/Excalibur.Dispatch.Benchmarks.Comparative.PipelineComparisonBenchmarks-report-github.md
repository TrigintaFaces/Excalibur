```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------ |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39;        |    316.7 ns |   6.35 ns |   6.80 ns |  1.00 |    0.03 | 0.0067 |     536 B |        1.00 |
| &#39;MediatR: 3 pipeline behaviors&#39;           |    178.0 ns |   0.63 ns |   0.56 ns |  0.56 |    0.01 | 0.0095 |     744 B |        1.39 |
| &#39;Wolverine: 3 middleware&#39;                 |    255.7 ns |   1.58 ns |   1.40 ns |  0.81 |    0.02 | 0.0100 |     768 B |        1.43 |
| &#39;MassTransit: 3 consume filters&#39;          |  2,347.6 ns |  20.34 ns |  19.02 ns |  7.42 |    0.17 | 0.0572 |    4568 B |        8.52 |
| &#39;Dispatch: 10 concurrent + 3 behaviors&#39;   |  4,096.1 ns |  77.25 ns |  79.33 ns | 12.94 |    0.37 | 0.0610 |    5072 B |        9.46 |
| &#39;MediatR: 10 concurrent + 3 behaviors&#39;    |  1,853.9 ns |   8.42 ns |   7.03 ns |  5.86 |    0.13 | 0.1011 |    7808 B |       14.57 |
| &#39;Wolverine: 10 concurrent + 3 middleware&#39; |  2,652.0 ns |  28.86 ns |  26.99 ns |  8.38 |    0.20 | 0.0992 |    7888 B |       14.72 |
| &#39;MassTransit: 10 concurrent + 3 filters&#39;  | 23,432.5 ns | 256.88 ns | 240.28 ns | 74.03 |    1.73 | 0.5798 |   45888 B |       85.61 |
