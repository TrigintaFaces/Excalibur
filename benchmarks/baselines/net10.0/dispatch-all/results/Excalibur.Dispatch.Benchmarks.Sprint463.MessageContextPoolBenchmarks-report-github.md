```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                             | Mean        | Error     | StdDev    | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------------------------- |------------:|----------:|----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| CreateContext_NoPooling            |    260.2 ns |   5.18 ns |   5.76 ns |   1.00 |    0.03 | 0.0844 |      - |    1592 B |        1.00 |
| CreateContext_Pooled_RentAndReturn |    619.7 ns |   3.17 ns |   2.97 ns |   2.38 |    0.05 | 0.0305 |      - |     576 B |        0.36 |
| CreateContext_Pooled_RentOnly      |    250.3 ns |   4.46 ns |   4.38 ns |   0.96 |    0.03 | 0.0844 |      - |    1592 B |        1.00 |
| ReturnContext_Pooled               |    542.5 ns |   2.19 ns |   2.04 ns |   2.09 |    0.05 |      - |      - |         - |        0.00 |
| CreateContext_Batch100_NoPooling   | 24,448.4 ns | 356.77 ns | 316.26 ns |  94.01 |    2.33 | 8.4534 |      - |  159200 B |      100.00 |
| CreateContext_Batch100_Pooled      | 62,033.6 ns | 147.55 ns | 123.21 ns | 238.52 |    5.12 | 3.1738 |      - |   60000 B |       37.69 |
| RealisticDispatch_Pooled           |  1,814.7 ns |   8.81 ns |   7.81 ns |   6.98 |    0.15 | 0.0477 |      - |     912 B |        0.57 |
| RealisticDispatch_NoPooling        |  1,612.7 ns |  19.91 ns |  16.63 ns |   6.20 |    0.15 | 0.1888 |      - |    3568 B |        2.24 |
| ConcurrentPoolAccess               |  4,541.1 ns |  39.92 ns |  35.39 ns |  17.46 |    0.40 | 0.4196 | 0.0076 |    7824 B |        4.91 |
