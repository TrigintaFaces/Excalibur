```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                 | Mean            | Error         | StdDev        | Ratio     | RatioSD | Gen0    | Gen1   | Gen2   | Allocated | Alloc Ratio |
|--------------------------------------- |----------------:|--------------:|--------------:|----------:|--------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;             |        74.12 ns |      0.610 ns |      0.571 ns |      1.00 |    0.01 |  0.0033 |      - |      - |     264 B |        1.00 |
| &#39;MassTransit: Single command&#39;          |    22,615.67 ns |     96.811 ns |     80.841 ns |    305.12 |    2.50 |  0.2747 |      - |      - |   22190 B |       84.05 |
| &#39;Dispatch: Event to 2 handlers&#39;        |       114.71 ns |      0.785 ns |      0.734 ns |      1.55 |    0.01 |  0.0066 | 0.0002 | 0.0002 |    1844 B |        6.98 |
| &#39;MassTransit: Event to 2 consumers&#39;    |    26,851.31 ns |    535.273 ns |    525.709 ns |    362.27 |    7.39 |  0.4883 |      - |      - |   39536 B |      149.76 |
| &#39;Dispatch: 10 concurrent commands&#39;     |       880.29 ns |      5.028 ns |      4.457 ns |     11.88 |    0.11 |  0.0324 | 0.0010 | 0.0010 |         - |        0.00 |
| &#39;MassTransit: 10 concurrent commands&#39;  |   294,094.51 ns |  5,800.745 ns |  9,200.596 ns |  3,967.79 |  125.85 |  2.6855 |      - |      - |  219695 B |      832.18 |
| &#39;Dispatch: 100 concurrent commands&#39;    |    13,405.73 ns |    171.504 ns |    152.034 ns |    180.86 |    2.39 |  0.2747 |      - |      - |   21760 B |       82.42 |
| &#39;MassTransit: 100 concurrent commands&#39; | 2,513,199.57 ns | 49,312.548 ns | 56,788.403 ns | 33,906.96 |  789.25 | 27.3438 |      - |      - | 2191146 B |    8,299.80 |
| &#39;Dispatch: Batch send (10)&#39;            |     1,143.54 ns |     13.998 ns |     13.094 ns |     15.43 |    0.21 |  0.0248 |      - |      - |    1920 B |        7.27 |
| &#39;MassTransit: Batch send (10)&#39;         |   291,198.10 ns |  5,727.955 ns | 11,171.937 ns |  3,928.71 |  152.04 |  2.4414 |      - |      - |  219922 B |      833.04 |
