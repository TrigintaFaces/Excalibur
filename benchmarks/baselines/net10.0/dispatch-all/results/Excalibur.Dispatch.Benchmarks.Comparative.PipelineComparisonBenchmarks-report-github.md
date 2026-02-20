```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                                  | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------- |------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39;      |  2,125.4 ns |  36.83 ns |  34.45 ns |  1.00 |    0.02 | 0.1030 |    1952 B |        1.00 |
| &#39;MediatR: 3 pipeline behaviors&#39;         |    126.6 ns |   2.45 ns |   3.01 ns |  0.06 |    0.00 | 0.0360 |     680 B |        0.35 |
| &#39;Dispatch: 10 concurrent + 3 behaviors&#39; | 21,582.3 ns | 347.76 ns | 325.29 ns | 10.16 |    0.22 | 1.0071 |   19233 B |        9.85 |
| &#39;MediatR: 10 concurrent + 3 behaviors&#39;  |  1,290.3 ns |  20.34 ns |  15.88 ns |  0.61 |    0.01 | 0.3796 |    7168 B |        3.67 |
