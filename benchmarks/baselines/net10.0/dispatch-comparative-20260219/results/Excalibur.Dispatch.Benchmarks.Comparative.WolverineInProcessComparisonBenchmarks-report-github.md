```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                  | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------------------------------- |-------------:|-----------:|-----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                      |     70.31 ns |   0.492 ns |   0.436 ns |   1.00 |    0.01 | 0.0063 |     120 B |        1.00 |
| &#39;Wolverine (in-process): Single command InvokeAsync&#39;    |    193.14 ns |   3.875 ns |   3.236 ns |   2.75 |    0.05 | 0.0355 |     672 B |        5.60 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;          |     75.95 ns |   0.440 ns |   0.390 ns |   1.08 |    0.01 | 0.0076 |     144 B |        1.20 |
| &#39;Wolverine (in-process): Notification to 2 handlers&#39;    |  4,138.96 ns |  35.834 ns |  33.519 ns |  58.87 |    0.58 | 0.2365 |    4512 B |       37.60 |
| &#39;Dispatch (local): Query with return&#39;                   |     97.43 ns |   1.282 ns |   1.136 ns |   1.39 |    0.02 | 0.0186 |     352 B |        2.93 |
| &#39;Wolverine (in-process): Query with return InvokeAsync&#39; |    259.53 ns |   1.879 ns |   1.757 ns |   3.69 |    0.03 | 0.0496 |     936 B |        7.80 |
| &#39;Dispatch (local): 10 concurrent commands&#39;              |    747.27 ns |  11.057 ns |   9.802 ns |  10.63 |    0.15 | 0.0477 |     912 B |        7.60 |
| &#39;Wolverine (in-process): 10 concurrent commands&#39;        |  1,985.19 ns |  19.440 ns |  18.185 ns |  28.24 |    0.30 | 0.3662 |    6928 B |       57.73 |
| &#39;Dispatch (local): 100 concurrent commands&#39;             |  7,033.04 ns |  45.538 ns |  38.026 ns | 100.04 |    0.79 | 0.3891 |    7392 B |       61.60 |
| &#39;Wolverine (in-process): 100 concurrent commands&#39;       | 19,976.37 ns | 284.734 ns | 252.409 ns | 284.14 |    3.86 | 3.6011 |   68128 B |      567.73 |
