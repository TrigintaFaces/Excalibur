```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                  | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------------------------------------------------- |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                      |     82.08 ns |   1.429 ns |   1.337 ns |   1.00 |    0.02 | 0.0139 |      - |     264 B |        1.00 |
| &#39;Dispatch (ultra-local): Single command&#39;                |     35.63 ns |   0.171 ns |   0.143 ns |   0.43 |    0.01 | 0.0025 |      - |      48 B |        0.18 |
| &#39;Wolverine (in-process): Single command InvokeAsync&#39;    |    183.58 ns |   2.147 ns |   2.008 ns |   2.24 |    0.04 | 0.0355 |      - |     672 B |        2.55 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;          |    130.03 ns |   2.505 ns |   2.573 ns |   1.58 |    0.04 | 0.0153 |      - |     288 B |        1.09 |
| &#39;Wolverine (in-process): Notification to 2 handlers&#39;    |  4,078.94 ns |  65.761 ns |  58.295 ns |  49.70 |    1.04 | 0.2365 |      - |    4512 B |       17.09 |
| &#39;Dispatch (local): Query with return&#39;                   |    100.17 ns |   1.532 ns |   1.433 ns |   1.22 |    0.03 | 0.0254 |      - |     480 B |        1.82 |
| &#39;Wolverine (in-process): Query with return InvokeAsync&#39; |    246.43 ns |   4.468 ns |   4.180 ns |   3.00 |    0.07 | 0.0496 |      - |     936 B |        3.55 |
| &#39;Dispatch (local): 10 concurrent commands&#39;              |    992.41 ns |  10.188 ns |   9.530 ns |  12.09 |    0.22 | 0.1221 |      - |    2320 B |        8.79 |
| &#39;Wolverine (in-process): 10 concurrent commands&#39;        |  1,968.57 ns |  39.162 ns |  50.922 ns |  23.99 |    0.72 | 0.3662 |      - |    6928 B |       26.24 |
| &#39;Dispatch (local): 100 concurrent commands&#39;             |  8,997.43 ns |  62.241 ns |  58.220 ns | 109.64 |    1.86 | 1.1444 | 0.0305 |   21760 B |       82.42 |
| &#39;Wolverine (in-process): 100 concurrent commands&#39;       | 19,907.31 ns | 394.849 ns | 614.732 ns | 242.58 |    8.31 | 3.6011 |      - |   68128 B |      258.06 |
