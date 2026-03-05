```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                  | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------------------------------------------------- |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                      |     80.94 ns |   1.653 ns |   2.262 ns |   1.00 |    0.04 | 0.0139 |      - |     264 B |        1.00 |
| &#39;Dispatch (ultra-local): Single command&#39;                |     35.17 ns |   0.426 ns |   0.378 ns |   0.43 |    0.01 | 0.0025 |      - |      48 B |        0.18 |
| &#39;Wolverine (in-process): Single command InvokeAsync&#39;    |    181.63 ns |   2.942 ns |   2.752 ns |   2.25 |    0.07 | 0.0355 |      - |     672 B |        2.55 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;          |    132.53 ns |   2.092 ns |   1.957 ns |   1.64 |    0.05 | 0.0153 |      - |     288 B |        1.09 |
| &#39;Wolverine (in-process): Notification to 2 handlers&#39;    |  4,054.71 ns |  71.069 ns |  63.001 ns |  50.13 |    1.55 | 0.2365 |      - |    4512 B |       17.09 |
| &#39;Dispatch (local): Query with return&#39;                   |     94.55 ns |   1.721 ns |   2.049 ns |   1.17 |    0.04 | 0.0254 |      - |     480 B |        1.82 |
| &#39;Wolverine (in-process): Query with return InvokeAsync&#39; |    245.07 ns |   4.581 ns |   4.499 ns |   3.03 |    0.10 | 0.0496 |      - |     936 B |        3.55 |
| &#39;Dispatch (local): 10 concurrent commands&#39;              |    984.90 ns |  17.209 ns |  15.255 ns |  12.18 |    0.38 | 0.1230 |      - |    2320 B |        8.79 |
| &#39;Wolverine (in-process): 10 concurrent commands&#39;        |  2,069.23 ns |  33.090 ns |  29.334 ns |  25.58 |    0.78 | 0.3662 |      - |    6928 B |       26.24 |
| &#39;Dispatch (local): 100 concurrent commands&#39;             |  8,652.03 ns |  85.017 ns |  70.993 ns | 106.97 |    3.02 | 1.1444 | 0.0305 |   21760 B |       82.42 |
| &#39;Wolverine (in-process): 100 concurrent commands&#39;       | 19,549.55 ns | 383.621 ns | 340.070 ns | 241.70 |    7.71 | 3.6011 |      - |   68128 B |      258.06 |
