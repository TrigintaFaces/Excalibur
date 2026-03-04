```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                  | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|-------------------------------------------------------- |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                      |    132.26 ns |   2.601 ns |   2.433 ns |   1.00 |    0.03 | 0.0033 |      - |      - |     264 B |        1.00 |
| &#39;Dispatch (ultra-local): Single command&#39;                |     61.27 ns |   1.189 ns |   1.627 ns |   0.46 |    0.01 | 0.0006 |      - |      - |      48 B |        0.18 |
| &#39;Wolverine (in-process): Single command InvokeAsync&#39;    |    368.19 ns |   5.028 ns |   4.703 ns |   2.78 |    0.06 | 0.0091 | 0.0005 | 0.0005 |         - |        0.00 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;          |    219.40 ns |   2.412 ns |   2.138 ns |   1.66 |    0.03 | 0.0036 |      - |      - |     288 B |        1.09 |
| &#39;Wolverine (in-process): Notification to 2 handlers&#39;    |  3,954.40 ns |  30.278 ns |  28.322 ns |  29.91 |    0.57 | 0.0534 |      - |      - |    4512 B |       17.09 |
| &#39;Dispatch (local): Query with return&#39;                   |     96.88 ns |   1.307 ns |   1.223 ns |   0.73 |    0.02 | 0.0061 | 0.0001 | 0.0001 |  102289 B |      387.46 |
| &#39;Wolverine (in-process): Query with return InvokeAsync&#39; |    289.44 ns |   2.239 ns |   1.984 ns |   2.19 |    0.04 | 0.0119 |      - |      - |     936 B |        3.55 |
| &#39;Dispatch (local): 10 concurrent commands&#39;              |    940.32 ns |  12.639 ns |  11.823 ns |   7.11 |    0.15 | 0.0324 | 0.0010 | 0.0010 |         - |        0.00 |
| &#39;Wolverine (in-process): 10 concurrent commands&#39;        |  2,192.44 ns |  14.189 ns |  12.578 ns |  16.58 |    0.31 | 0.0877 |      - |      - |    6928 B |       26.24 |
| &#39;Dispatch (local): 100 concurrent commands&#39;             |  8,249.13 ns | 115.937 ns | 102.775 ns |  62.39 |    1.35 | 0.2747 |      - |      - |   21760 B |       82.42 |
| &#39;Wolverine (in-process): 100 concurrent commands&#39;       | 22,060.96 ns | 247.081 ns | 219.031 ns | 166.85 |    3.39 | 1.1597 |      - |      - |   68128 B |      258.06 |
