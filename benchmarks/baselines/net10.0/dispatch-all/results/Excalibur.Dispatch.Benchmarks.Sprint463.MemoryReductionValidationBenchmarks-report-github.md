```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                      | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------- |-------------:|------------:|------------:|------:|--------:|--------:|-------:|----------:|------------:|
| StandardDispatch            |   3,433.3 ns |   196.84 ns |   580.39 ns |  1.03 |    0.24 |  0.2823 |      - |   5.23 KB |        1.00 |
| PooledDispatch              |   2,798.3 ns |    53.62 ns |    73.40 ns |  0.84 |    0.13 |  0.2823 |      - |    5.2 KB |        1.00 |
| CreateContext_Standard      |     283.5 ns |     5.63 ns |     6.03 ns |  0.08 |    0.01 |  0.0858 |      - |   1.58 KB |        0.30 |
| CreateContext_Pooled        |     258.9 ns |     5.15 ns |     5.29 ns |  0.08 |    0.01 |  0.0844 |      - |   1.55 KB |        0.30 |
| StandardDispatch_Batch100   | 283,398.0 ns | 3,974.33 ns | 3,717.59 ns | 84.74 |   13.28 | 28.3203 |      - | 522.77 KB |      100.02 |
| PooledDispatch_Batch100     | 281,891.7 ns | 4,734.47 ns | 4,196.99 ns | 84.29 |   13.22 | 27.8320 |      - | 520.42 KB |       99.57 |
| ConcurrentDispatch_Standard |  28,350.4 ns |   374.47 ns |   350.28 ns |  8.48 |    1.33 |  2.8687 | 0.0305 |  52.73 KB |       10.09 |
| ConcurrentDispatch_Pooled   |  28,950.4 ns |   237.61 ns |   222.26 ns |  8.66 |    1.35 |  2.8381 | 0.0305 |   52.5 KB |       10.04 |
