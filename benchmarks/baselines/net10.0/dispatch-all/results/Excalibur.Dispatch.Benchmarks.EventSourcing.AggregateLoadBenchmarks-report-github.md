```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                      | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|---------------------------- |---------:|----------:|----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| LoadAggregate10Events       | 1.321 ms | 0.1124 ms | 0.3296 ms |  1.06 |    0.37 |       - |       - |   25.92 KB |        1.00 |
| LoadAggregate100Events      | 3.024 ms | 0.0599 ms | 0.1303 ms |  2.43 |    0.59 |  7.8125 |       - |  166.95 KB |        6.44 |
| LoadAggregate1000Events     | 9.035 ms | 0.1795 ms | 0.2396 ms |  7.25 |    1.73 | 78.1250 | 31.2500 | 1573.19 KB |       60.69 |
| LoadAggregateFromVersion950 | 2.113 ms | 0.0548 ms | 0.1607 ms |  1.70 |    0.42 |  3.9063 |       - |    86.9 KB |        3.35 |
| ConcurrentLoad10Aggregates  | 7.019 ms | 0.1393 ms | 0.3338 ms |  5.64 |    1.37 | 78.1250 | 31.2500 | 1668.43 KB |       64.36 |
