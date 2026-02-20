```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                            | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1   | Allocated  | Alloc Ratio |
|---------------------------------- |-----------:|----------:|----------:|------:|--------:|--------:|-------:|-----------:|------------:|
| &#39;Dispatch: Container startup&#39;     |   5.855 μs | 0.1060 μs | 0.0940 μs |  1.00 |    0.02 |  1.4877 | 0.1297 |   27.45 KB |        1.00 |
| &#39;MediatR: Container startup&#39;      | 308.318 μs | 4.4839 μs | 3.9748 μs | 52.67 |    1.04 | 78.1250 | 1.4648 | 1435.86 KB |       52.32 |
| &#39;Dispatch: Startup + 10 handlers&#39; |   5.881 μs | 0.0437 μs | 0.0341 μs |  1.00 |    0.02 |  1.5259 | 0.1297 |   28.16 KB |        1.03 |
| &#39;MediatR: Startup + 10 handlers&#39;  | 325.203 μs | 6.1222 μs | 6.5507 μs | 55.56 |    1.38 | 78.1250 |      - | 1435.87 KB |       52.32 |
