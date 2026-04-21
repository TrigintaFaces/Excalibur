```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                                  | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                      | 11.044 μs |  1.043 μs | 0.6207 μs |  1.00 |    0.08 |    4296 B |        1.00 |
| &#39;Dispatch (ultra-local): Single command&#39;                |  9.930 μs |  4.042 μs | 2.6733 μs |  0.90 |    0.24 |     360 B |        0.08 |
| &#39;Wolverine (in-process): Single command InvokeAsync&#39;    | 28.167 μs |  4.323 μs | 2.5725 μs |  2.56 |    0.26 |    1928 B |        0.45 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;          | 16.438 μs |  1.505 μs | 0.7873 μs |  1.49 |    0.10 |     576 B |        0.13 |
| &#39;Wolverine (in-process): Notification to 2 handlers&#39;    | 83.322 μs | 11.579 μs | 6.8907 μs |  7.57 |    0.72 |    4640 B |        1.08 |
| &#39;Dispatch (local): Query with return&#39;                   | 17.940 μs |  5.023 μs | 3.3221 μs |  1.63 |    0.30 |    6776 B |        1.58 |
| &#39;Wolverine (in-process): Query with return InvokeAsync&#39; | 35.720 μs |  4.880 μs | 3.2279 μs |  3.24 |    0.33 |   11552 B |        2.69 |
| &#39;Dispatch (local): 10 concurrent commands&#39;              | 15.361 μs |  1.295 μs | 0.7705 μs |  1.39 |    0.10 |    2320 B |        0.54 |
| &#39;Wolverine (in-process): 10 concurrent commands&#39;        | 36.350 μs |  4.153 μs | 2.1719 μs |  3.30 |    0.26 |   16752 B |        3.90 |
| &#39;Dispatch (local): 100 concurrent commands&#39;             | 25.544 μs |  2.862 μs | 1.7030 μs |  2.32 |    0.19 |   22392 B |        5.21 |
| &#39;Wolverine (in-process): 100 concurrent commands&#39;       | 99.528 μs |  7.260 μs | 4.3202 μs |  9.04 |    0.61 |   70032 B |       16.30 |
