```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                      | Mean         | Error       | StdDev      | Ratio  | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------- |-------------:|------------:|------------:|-------:|--------:|--------:|-------:|----------:|------------:|
| FullDispatch_WithMiddleware |   2,706.2 ns |    47.97 ns |    44.87 ns |   1.00 |    0.02 |  0.2823 |      - |   5.27 KB |        1.00 |
| Dispatch_NoAwait            |   2,707.6 ns |    52.78 ns |    77.36 ns |   1.00 |    0.03 |  0.2823 |      - |    5.2 KB |        0.99 |
| CreateContext               |     264.5 ns |     4.53 ns |     4.23 ns |   0.10 |    0.00 |  0.0858 |      - |   1.58 KB |        0.30 |
| Throughput_100Sequential    | 278,818.4 ns | 3,663.87 ns | 3,247.92 ns | 103.06 |    2.01 | 27.8320 |      - | 520.42 KB |       98.69 |
| Throughput_10Parallel       |  27,984.5 ns |   163.58 ns |   153.01 ns |  10.34 |    0.17 |  2.8381 | 0.0305 |   52.5 KB |        9.95 |
| Throughput_100Parallel      | 284,279.8 ns | 2,298.53 ns | 1,919.37 ns | 105.07 |    1.81 | 28.3203 | 1.4648 | 522.92 KB |       99.16 |
| MixedWorkload               | 274,891.6 ns | 4,175.76 ns | 3,701.71 ns | 101.60 |    2.09 | 27.8320 |      - | 520.42 KB |       98.69 |
