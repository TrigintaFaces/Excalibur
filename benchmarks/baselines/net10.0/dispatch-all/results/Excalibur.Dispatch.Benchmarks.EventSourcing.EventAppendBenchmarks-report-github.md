```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                        | Mean       | Error      | StdDev     | Median     | Ratio  | RatioSD | Allocated   | Alloc Ratio |
|------------------------------ |-----------:|-----------:|-----------:|-----------:|-------:|--------:|------------:|------------:|
| AppendSingleEvent             |   7.609 ms |  0.2163 ms |  0.6206 ms |   7.552 ms |   1.01 |    0.11 |       20 KB |        1.00 |
| AppendTenEvents               |  15.503 ms |  0.3086 ms |  0.8238 ms |  15.396 ms |   2.05 |    0.19 |   121.38 KB |        6.07 |
| AppendHundredEvents           |  99.714 ms |  8.5906 ms | 23.6611 ms |  90.993 ms |  13.19 |    3.29 |  1133.94 KB |       56.69 |
| AppendThousandEvents          | 788.519 ms | 19.2952 ms | 55.0504 ms | 778.862 ms | 104.29 |   10.95 | 11259.21 KB |      562.85 |
| ConcurrentAppendTenAggregates |  98.483 ms | 32.8665 ms | 96.9075 ms |  23.264 ms |  13.03 |   12.83 |    199.3 KB |        9.96 |
| AppendToExistingAggregate     |  13.543 ms |  0.3048 ms |  0.8941 ms |  13.444 ms |   1.79 |    0.18 |    73.98 KB |        3.70 |
