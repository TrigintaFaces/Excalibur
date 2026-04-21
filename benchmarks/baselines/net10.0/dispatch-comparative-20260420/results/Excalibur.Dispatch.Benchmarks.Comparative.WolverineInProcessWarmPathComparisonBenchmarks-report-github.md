```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                  | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------------------------------------------------- |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                      |     74.83 ns |   0.432 ns |   0.383 ns |   1.00 |    0.01 | 0.0139 |      - |     264 B |        1.00 |
| &#39;Dispatch (ultra-local): Single command&#39;                |     34.23 ns |   0.691 ns |   0.739 ns |   0.46 |    0.01 | 0.0013 |      - |      24 B |        0.09 |
| &#39;Wolverine (in-process): Single command InvokeAsync&#39;    |    197.75 ns |   1.480 ns |   1.236 ns |   2.64 |    0.02 | 0.0355 |      - |     672 B |        2.55 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;          |    120.28 ns |   1.119 ns |   0.992 ns |   1.61 |    0.02 | 0.0153 |      - |     288 B |        1.09 |
| &#39;Wolverine (in-process): Notification to 2 handlers&#39;    |  6,455.11 ns |  74.242 ns |  61.995 ns |  86.27 |    0.90 | 0.2975 | 0.2518 |    5640 B |       21.36 |
| &#39;Dispatch (local): Query with return&#39;                   |     89.45 ns |   1.773 ns |   1.821 ns |   1.20 |    0.02 | 0.0242 |      - |     456 B |        1.73 |
| &#39;Wolverine (in-process): Query with return InvokeAsync&#39; |    267.92 ns |   3.173 ns |   2.968 ns |   3.58 |    0.04 | 0.0496 |      - |     936 B |        3.55 |
| &#39;Dispatch (local): 10 concurrent commands&#39;              |    942.99 ns |   4.571 ns |   3.817 ns |  12.60 |    0.08 | 0.1230 |      - |    2320 B |        8.79 |
| &#39;Wolverine (in-process): 10 concurrent commands&#39;        |  2,129.25 ns |  28.388 ns |  26.555 ns |  28.46 |    0.37 | 0.3662 |      - |    6928 B |       26.24 |
| &#39;Dispatch (local): 100 concurrent commands&#39;             |  8,173.28 ns |  66.086 ns |  61.817 ns | 109.23 |    0.96 | 1.1444 | 0.0305 |   21760 B |       82.42 |
| &#39;Wolverine (in-process): 100 concurrent commands&#39;       | 21,169.25 ns | 247.972 ns | 231.953 ns | 282.92 |    3.31 | 3.6011 |      - |   68128 B |      258.06 |
