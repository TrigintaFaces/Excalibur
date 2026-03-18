```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                  | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|-------------------------------------------------------- |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                      |     50.55 ns |   0.990 ns |   1.178 ns |   1.00 |    0.03 | 0.0102 |      - |     192 B |        1.00 |
| &#39;Dispatch (ultra-local): Single command&#39;                |     40.57 ns |   0.730 ns |   0.683 ns |   0.80 |    0.02 | 0.0025 |      - |      48 B |        0.25 |
| &#39;Wolverine (in-process): Single command InvokeAsync&#39;    |    189.15 ns |   3.170 ns |   3.523 ns |   3.74 |    0.11 | 0.0355 |      - |     672 B |        3.50 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;          |    115.79 ns |   0.802 ns |   0.750 ns |   2.29 |    0.05 | 0.0153 |      - |     288 B |        1.50 |
| &#39;Wolverine (in-process): Notification to 2 handlers&#39;    |  3,986.93 ns |  40.924 ns |  36.278 ns |  78.91 |    1.89 | 0.2365 |      - |    4512 B |       23.50 |
| &#39;Dispatch (local): Query with return&#39;                   |     63.26 ns |   1.219 ns |   1.897 ns |   1.25 |    0.05 | 0.0204 |      - |     384 B |        2.00 |
| &#39;Wolverine (in-process): Query with return InvokeAsync&#39; |    252.46 ns |   4.327 ns |   5.473 ns |   5.00 |    0.15 | 0.0496 |      - |     936 B |        4.88 |
| &#39;Dispatch (local): 10 concurrent commands&#39;              |    643.40 ns |  12.675 ns |  12.449 ns |  12.73 |    0.37 | 0.0849 |      - |    1600 B |        8.33 |
| &#39;Wolverine (in-process): 10 concurrent commands&#39;        |  2,050.55 ns |  11.842 ns |  10.497 ns |  40.59 |    0.92 | 0.3662 |      - |    6928 B |       36.08 |
| &#39;Dispatch (local): 100 concurrent commands&#39;             |  5,422.31 ns | 101.940 ns |  95.355 ns | 107.32 |    3.00 | 0.7706 | 0.0153 |   14560 B |       75.83 |
| &#39;Wolverine (in-process): 100 concurrent commands&#39;       | 19,674.40 ns | 276.312 ns | 258.462 ns | 389.41 |    9.97 | 3.6011 |      - |   68128 B |      354.83 |
