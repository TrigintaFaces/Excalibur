```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                                 | Mean         | Error       | StdDev      | Median       | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------------------------- |-------------:|------------:|------------:|-------------:|---------:|--------:|-------:|----------:|------------:|
| ContainsInterface_Linq                 |     4.034 ns |   0.1710 ns |   0.4933 ns |     3.942 ns |     1.01 |    0.17 |      - |         - |          NA |
| ContainsInterface_ManualLoop           |     4.067 ns |   0.3489 ns |   1.0287 ns |     3.755 ns |     1.02 |    0.28 |      - |         - |          NA |
| ContainsInterface_Linq_Batch1000       | 4,910.639 ns | 278.2698 ns | 757.0537 ns | 4,666.182 ns | 1,233.72 |  234.64 |      - |         - |          NA |
| ContainsInterface_ManualLoop_Batch1000 | 3,973.122 ns |  90.0306 ns | 259.7589 ns | 3,901.519 ns |   998.18 |  128.88 |      - |         - |          NA |
| ImplementsGeneric_Linq                 |     2.636 ns |   0.0747 ns |   0.2120 ns |     2.585 ns |     0.66 |    0.09 |      - |         - |          NA |
| ImplementsGeneric_ManualLoop           |    10.834 ns |   0.2346 ns |   0.6302 ns |    10.728 ns |     2.72 |    0.34 | 0.0021 |      40 B |          NA |
| ImplementsGeneric_Linq_Batch1000       | 7,640.783 ns | 211.0146 ns | 612.1916 ns | 7,500.061 ns | 1,919.63 |  263.35 | 3.3951 |   64001 B |          NA |
| ImplementsGeneric_ManualLoop_Batch1000 | 9,222.380 ns | 236.4935 ns | 674.7293 ns | 9,142.360 ns | 2,316.98 |  308.69 | 2.1210 |   40001 B |          NA |
