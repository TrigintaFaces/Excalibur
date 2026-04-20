```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                                  | Mean       | Error     | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------------------------- |-----------:|----------:|---------:|------:|--------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                      |  17.970 μs |  5.003 μs | 3.309 μs |  1.03 |    0.28 |     552 B |        1.00 |
| &#39;Dispatch (ultra-local): Single command&#39;                |   8.310 μs |  2.719 μs | 1.798 μs |  0.48 |    0.14 |    1704 B |        3.09 |
| &#39;Wolverine (in-process): Single command InvokeAsync&#39;    |  33.120 μs |  2.711 μs | 1.793 μs |  1.91 |    0.39 |    2016 B |        3.65 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;          |  19.680 μs |  3.749 μs | 2.480 μs |  1.13 |    0.26 |    7968 B |       14.43 |
| &#39;Wolverine (in-process): Notification to 2 handlers&#39;    | 129.650 μs | 14.342 μs | 9.486 μs |  7.46 |    1.57 |   15576 B |       28.22 |
| &#39;Dispatch (local): Query with return&#39;                   |  15.860 μs |  5.142 μs | 3.401 μs |  0.91 |    0.26 |   10152 B |       18.39 |
| &#39;Wolverine (in-process): Query with return InvokeAsync&#39; |  34.540 μs |  3.844 μs | 2.542 μs |  1.99 |    0.42 |    2904 B |        5.26 |
| &#39;Dispatch (local): 10 concurrent commands&#39;              |  24.530 μs |  2.878 μs | 1.904 μs |  1.41 |    0.30 |    3952 B |        7.16 |
| &#39;Wolverine (in-process): 10 concurrent commands&#39;        |  41.920 μs |  4.226 μs | 2.796 μs |  2.41 |    0.50 |   13648 B |       24.72 |
| &#39;Dispatch (local): 100 concurrent commands&#39;             |  46.770 μs |  4.410 μs | 2.917 μs |  2.69 |    0.56 |   31456 B |       56.99 |
| &#39;Wolverine (in-process): 100 concurrent commands&#39;       | 109.222 μs |  6.006 μs | 3.574 μs |  6.29 |    1.26 |   76144 B |      137.94 |
