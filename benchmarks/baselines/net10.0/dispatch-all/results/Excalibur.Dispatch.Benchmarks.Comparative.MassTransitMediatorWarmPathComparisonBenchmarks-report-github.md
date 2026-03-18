```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                           | Mean          | Error        | StdDev       | Ratio    | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------------------------------------------------------- |--------------:|-------------:|-------------:|---------:|--------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                               |      48.04 ns |     0.677 ns |     0.601 ns |     1.00 |    0.02 |  0.0102 |      - |     192 B |        1.00 |
| &#39;MassTransit Mediator (in-process): Single command&#39;              |   1,241.95 ns |    24.837 ns |    69.235 ns |    25.86 |    1.47 |  0.1869 |      - |    3544 B |       18.46 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;                   |     129.13 ns |     1.464 ns |     1.369 ns |     2.69 |    0.04 |  0.0198 |      - |     376 B |        1.96 |
| &#39;MassTransit Mediator (in-process): Notification to 2 consumers&#39; |   1,685.68 ns |    33.319 ns |    64.194 ns |    35.10 |    1.39 |  0.2213 | 0.0019 |    4176 B |       21.75 |
| &#39;Dispatch (local): Query with return&#39;                            |      60.67 ns |     1.223 ns |     2.386 ns |     1.26 |    0.05 |  0.0204 |      - |     384 B |        2.00 |
| &#39;MassTransit Mediator (in-process): Query with return&#39;           |  15,451.49 ns |   893.321 ns | 2,633.976 ns |   321.71 |   54.73 |  0.6104 | 0.0153 |   11651 B |       60.68 |
| &#39;Dispatch (local): 10 concurrent commands&#39;                       |     638.12 ns |    12.545 ns |    12.882 ns |    13.29 |    0.31 |  0.0849 |      - |    1600 B |        8.33 |
| &#39;MassTransit Mediator (in-process): 10 concurrent commands&#39;      |  12,383.31 ns |   247.019 ns |   498.991 ns |   257.83 |   10.75 |  1.8921 |      - |   35648 B |      185.67 |
| &#39;Dispatch (local): 100 concurrent commands&#39;                      |   5,316.93 ns |   103.875 ns |   119.622 ns |   110.70 |    2.77 |  0.7706 | 0.0153 |   14560 B |       75.83 |
| &#39;MassTransit Mediator (in-process): 100 concurrent commands&#39;     | 121,137.76 ns | 2,414.601 ns | 5,831.533 ns | 2,522.15 |  124.36 | 18.7988 |      - |  355329 B |    1,850.67 |
