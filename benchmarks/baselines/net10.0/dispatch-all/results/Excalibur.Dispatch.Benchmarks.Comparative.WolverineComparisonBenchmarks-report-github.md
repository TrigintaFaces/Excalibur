```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=3  UnrollFactor=1  

```
| Method                                    | Mean       | Error      | StdDev     | Median     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------------ |-----------:|-----------:|-----------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;                |  14.733 μs | 202.056 μs | 11.0753 μs |   9.000 μs |  1.36 |    1.16 |     192 B |        1.00 |
| &#39;Dispatch: Single command (ultra-local)&#39;  |   3.033 μs |   5.267 μs |  0.2887 μs |   3.200 μs |  0.28 |    0.13 |      48 B |        0.25 |
| &#39;Wolverine: Single command (InvokeAsync)&#39; |  40.633 μs | 111.127 μs |  6.0913 μs |  44.100 μs |  3.76 |    1.82 |    1312 B |        6.83 |
| &#39;Wolverine: Single command (SendAsync)&#39;   |  72.300 μs | 498.588 μs | 27.3293 μs |  67.800 μs |  6.68 |    3.91 |   11472 B |       59.75 |
| &#39;Dispatch: Event to 2 handlers&#39;           |  18.733 μs |  17.999 μs |  0.9866 μs |  19.200 μs |  1.73 |    0.81 |    7344 B |       38.25 |
| &#39;Wolverine: Event publish&#39;                |  96.267 μs | 236.925 μs | 12.9867 μs |  92.200 μs |  8.90 |    4.28 |   10408 B |       54.21 |
| &#39;Dispatch: 10 concurrent commands&#39;        |  21.633 μs |  16.951 μs |  0.9292 μs |  21.900 μs |  2.00 |    0.93 |    7936 B |       41.33 |
| &#39;Wolverine: 10 concurrent commands&#39;       |  40.667 μs | 293.386 μs | 16.0815 μs |  38.800 μs |  3.76 |    2.24 |   10784 B |       56.17 |
| &#39;Dispatch: Query with return value&#39;       |  12.200 μs |  33.788 μs |  1.8520 μs |  12.900 μs |  1.13 |    0.55 |     672 B |        3.50 |
| &#39;Wolverine: Query with return value&#39;      |  33.250 μs |  78.829 μs |  4.3209 μs |  34.150 μs |  3.07 |    1.47 |    3960 B |       20.62 |
| &#39;Dispatch: 100 concurrent commands&#39;       |  37.367 μs |  22.811 μs |  1.2503 μs |  37.400 μs |  3.45 |    1.60 |   17200 B |       89.58 |
| &#39;Wolverine: 100 concurrent commands&#39;      | 138.867 μs | 184.779 μs | 10.1283 μs | 135.700 μs | 12.84 |    6.01 |   77072 B |      401.42 |
| &#39;Dispatch: Batch queries (10)&#39;            |  21.467 μs |  34.823 μs |  1.9088 μs |  22.200 μs |  1.98 |    0.93 |   10216 B |       53.21 |
| &#39;Wolverine: Batch queries (10)&#39;           |  32.167 μs |  45.803 μs |  2.5106 μs |  32.800 μs |  2.97 |    1.40 |   13592 B |       70.79 |
