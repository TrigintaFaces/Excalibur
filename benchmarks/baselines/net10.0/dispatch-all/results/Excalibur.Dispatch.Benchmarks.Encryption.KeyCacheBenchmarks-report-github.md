```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method          | Mean        | Error     | StdDev    | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------- |------------:|----------:|----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| CacheHit        |    37.28 ns |  1.311 ns |  3.866 ns |   1.01 |    0.15 | 0.0025 |      - |      48 B |        1.00 |
| CacheMiss       |    36.71 ns |  0.767 ns |  0.853 ns |   1.00 |    0.11 | 0.0034 |      - |      64 B |        1.33 |
| CacheSet        |   564.95 ns |  4.707 ns |  4.403 ns |  15.32 |    1.59 | 0.0353 |      - |     678 B |       14.12 |
| CacheSetWithTtl |   555.07 ns |  3.768 ns |  3.525 ns |  15.05 |    1.56 | 0.0353 |      - |     678 B |       14.12 |
| GetOrAddHit     |    69.86 ns |  1.398 ns |  1.866 ns |   1.89 |    0.20 | 0.0101 |      - |     192 B |        4.00 |
| GetOrAddMiss    | 1,130.42 ns | 26.081 ns | 76.901 ns |  30.65 |    3.79 | 0.0744 | 0.0343 |    1391 B |       28.98 |
| CacheRemove     |   674.33 ns | 13.515 ns | 15.564 ns |  18.28 |    1.93 | 0.0467 |      - |     888 B |       18.50 |
| SequentialReads | 4,562.67 ns | 83.424 ns | 99.310 ns | 123.70 |   13.05 | 0.2518 |      - |    4800 B |      100.00 |
