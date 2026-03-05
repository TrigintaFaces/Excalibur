```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3


```
| Method                      | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------- |-------------:|-----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| FullDispatch_WithMiddleware |    258.53 ns |   4.781 ns |   3.993 ns |  1.00 |    0.02 | 0.0467 |     880 B |        1.00 |
| Dispatch_NoAwait            |    241.87 ns |   3.977 ns |   3.720 ns |  0.94 |    0.02 | 0.0429 |     808 B |        0.92 |
| CreateContext               |     37.78 ns |   0.783 ns |   1.635 ns |  0.15 |    0.01 | 0.0255 |     480 B |        0.55 |
| Throughput_100Sequential    | 22,397.89 ns | 208.359 ns | 173.989 ns | 86.65 |    1.44 | 4.2725 |   80874 B |       91.90 |
| Throughput_10Parallel       |  2,548.47 ns |  42.322 ns |  33.042 ns |  9.86 |    0.19 | 0.4539 |    8552 B |        9.72 |
| Throughput_100Parallel      | 23,564.65 ns | 433.975 ns | 384.707 ns | 91.17 |    1.97 | 4.4250 |   83434 B |       94.81 |
| MixedWorkload               | 22,710.13 ns | 427.341 ns | 399.735 ns | 87.86 |    1.98 | 4.2725 |   80874 B |       91.90 |
