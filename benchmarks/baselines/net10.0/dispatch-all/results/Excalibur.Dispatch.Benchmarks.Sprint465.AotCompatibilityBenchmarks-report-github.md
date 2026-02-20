```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                          | Mean         | Error       | StdDev      | Median       | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |-------------:|------------:|------------:|-------------:|-------:|--------:|-------:|----------:|------------:|
| ActivateHandler_CachedDelegates |    11.901 ns |   0.3303 ns |   0.9634 ns |    11.715 ns |   1.01 |    0.11 | 0.0013 |      24 B |        1.00 |
| ActivateHandler_Batch100        | 1,084.860 ns |  40.3254 ns | 116.3481 ns | 1,063.609 ns |  91.73 |   12.08 | 0.1268 |    2400 B |      100.00 |
| TypeCheck_AotSafe               |     1.578 ns |   0.0913 ns |   0.2649 ns |     1.567 ns |   0.13 |    0.02 |      - |         - |        0.00 |
| TypeCheck_Reflection            |     8.341 ns |   0.2645 ns |   0.7674 ns |     8.111 ns |   0.71 |    0.08 | 0.0017 |      32 B |        1.33 |
| TypeCheck_AotSafe_Batch1000     | 2,123.876 ns | 149.9388 ns | 442.0980 ns | 1,948.206 ns | 179.57 |   39.76 |      - |         - |        0.00 |
| TypeCheck_Reflection_Batch1000  | 8,466.904 ns | 189.6493 ns | 550.2071 ns | 8,457.641 ns | 715.88 |   71.90 | 1.6937 |   32000 B |    1,333.33 |
| DirectServiceResolution         |     7.576 ns |   0.4110 ns |   1.1794 ns |     7.405 ns |   0.64 |    0.11 | 0.0013 |      24 B |        1.00 |
| ConcurrentActivation            | 2,240.322 ns |  43.7878 ns |  59.9373 ns | 2,238.102 ns | 189.42 |   15.36 | 0.1411 |    2663 B |      110.96 |
