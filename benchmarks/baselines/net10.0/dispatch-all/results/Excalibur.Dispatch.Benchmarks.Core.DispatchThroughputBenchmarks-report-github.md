```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3


```
| Method                      | Mean         | Error      | StdDev     | Median       | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |-------------:|-----------:|-----------:|-------------:|------:|--------:|-------:|----------:|------------:|
| FullDispatch_WithMiddleware |    247.36 ns |   4.980 ns |   4.891 ns |    246.29 ns |  1.00 |    0.03 | 0.0372 |     704 B |        1.00 |
| Dispatch_NoAwait            |    220.09 ns |   3.115 ns |   3.198 ns |    219.79 ns |  0.89 |    0.02 | 0.0334 |     632 B |        0.90 |
| CreateContext               |     13.24 ns |   0.407 ns |   1.162 ns |     12.70 ns |  0.05 |    0.00 | 0.0115 |     216 B |        0.31 |
| Throughput_100Sequential    | 22,113.38 ns | 174.723 ns | 163.436 ns | 22,107.09 ns | 89.43 |    1.82 | 3.3569 |   63273 B |       89.88 |
| Throughput_10Parallel       |  2,426.98 ns |  21.885 ns |  19.400 ns |  2,419.59 ns |  9.82 |    0.20 | 0.3586 |    6792 B |        9.65 |
| Throughput_100Parallel      | 23,806.12 ns | 471.749 ns | 504.766 ns | 23,606.61 ns | 96.28 |    2.70 | 3.4790 |   65833 B |       93.51 |
| MixedWorkload               | 23,492.02 ns | 452.792 ns | 484.482 ns | 23,297.51 ns | 95.01 |    2.63 | 3.3569 |   63273 B |       89.88 |
