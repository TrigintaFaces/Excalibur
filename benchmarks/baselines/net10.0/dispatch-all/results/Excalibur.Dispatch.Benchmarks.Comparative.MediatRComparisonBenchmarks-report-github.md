```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                                 | Mean          | Error        | StdDev       | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------------------------- |--------------:|-------------:|-------------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;     |   1,771.86 ns |    17.230 ns |    14.388 ns |   1.00 |    0.01 | 0.0820 |      - |    1576 B |        1.00 |
| &#39;MediatR: Single command handler&#39;      |      41.29 ns |     0.685 ns |     0.641 ns |   0.02 |    0.00 | 0.0080 |      - |     152 B |        0.10 |
| &#39;Dispatch: Notification to 3 handlers&#39; |   5,191.43 ns |   101.164 ns |   157.500 ns |   2.93 |    0.09 | 0.1907 |      - |    3712 B |        2.36 |
| &#39;MediatR: Notification to 3 handlers&#39;  |     107.42 ns |     2.118 ns |     2.522 ns |   0.06 |    0.00 | 0.0327 |      - |     616 B |        0.39 |
| &#39;Dispatch: Query with return value&#39;    |     278.23 ns |     4.718 ns |     5.795 ns |   0.16 |    0.00 | 0.0429 |      - |     808 B |        0.51 |
| &#39;MediatR: Query with return value&#39;     |      51.38 ns |     0.616 ns |     0.546 ns |   0.03 |    0.00 | 0.0157 |      - |     296 B |        0.19 |
| &#39;Dispatch: 10 concurrent commands&#39;     |  18,167.26 ns |   252.568 ns |   236.253 ns |  10.25 |    0.15 | 0.7935 |      - |   15473 B |        9.82 |
| &#39;MediatR: 10 concurrent commands&#39;      |     517.17 ns |     8.251 ns |     7.314 ns |   0.29 |    0.00 | 0.1001 |      - |    1888 B |        1.20 |
| &#39;Dispatch: 100 concurrent commands&#39;    | 177,226.80 ns | 1,593.466 ns | 1,412.566 ns | 100.03 |    1.10 | 8.0566 | 0.2441 |  153003 B |       97.08 |
| &#39;MediatR: 100 concurrent commands&#39;     |   4,656.57 ns |    57.502 ns |    44.894 ns |   2.63 |    0.03 | 0.9079 |      - |   17096 B |       10.85 |
