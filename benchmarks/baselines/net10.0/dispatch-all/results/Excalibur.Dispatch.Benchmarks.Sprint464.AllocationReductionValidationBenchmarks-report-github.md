```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                                 | Mean          | Error        | StdDev        | Ratio   | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------------- |--------------:|-------------:|--------------:|--------:|--------:|--------:|-------:|----------:|------------:|
| SingleDispatch                         |   3,244.49 ns |    93.307 ns |    266.210 ns |   1.006 |    0.11 |  0.2823 |      - |    5368 B |        1.00 |
| Batch100Dispatches                     | 328,836.41 ns | 8,305.689 ns | 24,359.149 ns | 102.006 |   11.03 | 28.3203 |      - |  536914 B |      100.02 |
| CreateContext                          |     308.11 ns |     7.185 ns |     21.072 ns |   0.096 |    0.01 |  0.0858 |      - |    1616 B |        0.30 |
| CollectionWithCapacity                 |      19.31 ns |     0.420 ns |      0.735 ns |   0.006 |    0.00 |  0.0051 |      - |      96 B |        0.02 |
| CollectionWithoutCapacity              |      37.88 ns |     0.864 ns |      2.520 ns |   0.012 |    0.00 |  0.0114 |      - |     216 B |        0.04 |
| CollectionWithTryGetNonEnumeratedCount |      31.20 ns |     0.655 ns |      1.655 ns |   0.010 |    0.00 |  0.0072 |      - |     136 B |        0.03 |
| ConcurrentDispatches                   |  33,021.55 ns |   709.537 ns |  2,047.177 ns |  10.243 |    1.03 |  2.8687 | 0.0305 |   54156 B |       10.09 |
