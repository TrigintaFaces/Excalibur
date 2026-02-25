```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                                    | Mean            | Error         | StdDev        | Median          | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------ |----------------:|--------------:|--------------:|----------------:|---------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;                |      1,819.0 ns |      34.50 ns |      42.37 ns |      1,804.5 ns |     1.00 |    0.03 | 0.0839 |    1608 B |        1.00 |
| &#39;Wolverine: Single command (InvokeAsync)&#39; |        201.7 ns |       2.55 ns |       2.26 ns |        201.5 ns |     0.11 |    0.00 | 0.0355 |     672 B |        0.42 |
| &#39;Wolverine: Single command (SendAsync)&#39;   | 15,589,384.8 ns | 194,166.78 ns | 181,623.72 ns | 15,663,659.4 ns | 8,574.73 |  214.45 |      - |    5800 B |        3.61 |
| &#39;Dispatch: Event to 2 handlers&#39;           |      3,797.3 ns |      78.39 ns |     209.23 ns |      3,721.5 ns |     2.09 |    0.12 | 0.1564 |    2960 B |        1.84 |
| &#39;Wolverine: Event publish&#39;                | 15,536,860.7 ns | 213,649.86 ns | 199,848.21 ns | 15,597,242.2 ns | 8,545.84 |  218.43 |      - |    5781 B |        3.60 |
| &#39;Dispatch: 10 concurrent commands&#39;        |     18,064.9 ns |     317.90 ns |     366.10 ns |     17,946.6 ns |     9.94 |    0.30 | 0.8240 |   15793 B |        9.82 |
| &#39;Wolverine: 10 concurrent commands&#39;       |      2,479.1 ns |      76.07 ns |     224.31 ns |      2,427.5 ns |     1.36 |    0.13 | 0.3662 |    6928 B |        4.31 |
| &#39;Dispatch: Query with return value&#39;       |        370.8 ns |      19.47 ns |      57.42 ns |        358.3 ns |     0.20 |    0.03 | 0.0429 |     808 B |        0.50 |
| &#39;Wolverine: Query with return value&#39;      |              NA |            NA |            NA |              NA |        ? |       ? |     NA |        NA |           ? |
| &#39;Dispatch: 100 concurrent commands&#39;       |    193,652.7 ns |   5,398.85 ns |  15,576.92 ns |    189,501.7 ns |   106.52 |    8.85 | 7.8125 |  156203 B |       97.14 |
| &#39;Wolverine: 100 concurrent commands&#39;      |     21,696.8 ns |     427.56 ns |     843.96 ns |     21,822.9 ns |    11.93 |    0.53 | 3.6011 |   68128 B |       42.37 |
| &#39;Dispatch: Batch queries (10)&#39;            |      2,975.0 ns |      54.52 ns |     140.74 ns |      2,975.2 ns |     1.64 |    0.09 | 0.4120 |    7792 B |        4.85 |
| &#39;Wolverine: Batch queries (10)&#39;           |              NA |            NA |            NA |              NA |        ? |       ? |     NA |        NA |           ? |

Benchmarks with issues:
  WolverineComparisonBenchmarks.'Wolverine: Query with return value': DefaultJob
  WolverineComparisonBenchmarks.'Wolverine: Batch queries (10)': DefaultJob
