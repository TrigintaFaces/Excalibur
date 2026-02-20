```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;                |     66.53 ns |   0.398 ns |   0.373 ns |   1.00 |    0.01 | 0.0063 |     120 B |        1.00 |
| &#39;Wolverine: Single command (InvokeAsync)&#39; |    202.64 ns |   2.712 ns |   2.404 ns |   3.05 |    0.04 | 0.0365 |     688 B |        5.73 |
| &#39;Wolverine: Single command (SendAsync)&#39;   |  3,809.65 ns |  19.898 ns |  16.616 ns |  57.27 |    0.39 | 0.2365 |    4512 B |       37.60 |
| &#39;Dispatch: Event to 2 handlers&#39;           |     74.02 ns |   0.465 ns |   0.412 ns |   1.11 |    0.01 | 0.0076 |     144 B |        1.20 |
| &#39;Wolverine: Event publish&#39;                |  3,883.43 ns |  36.319 ns |  32.195 ns |  58.38 |    0.56 | 0.2365 |    4512 B |       37.60 |
| &#39;Dispatch: 10 concurrent commands&#39;        |    731.81 ns |   4.034 ns |   3.576 ns |  11.00 |    0.08 | 0.0477 |     912 B |        7.60 |
| &#39;Wolverine: 10 concurrent commands&#39;       |  2,056.64 ns |  33.510 ns |  31.345 ns |  30.92 |    0.49 | 0.3738 |    7088 B |       59.07 |
| &#39;Dispatch: Query with return value&#39;       |     97.74 ns |   0.471 ns |   0.441 ns |   1.47 |    0.01 | 0.0186 |     352 B |        2.93 |
| &#39;Wolverine: Query with return value&#39;      |    263.13 ns |   2.724 ns |   2.414 ns |   3.96 |    0.04 | 0.0496 |     936 B |        7.80 |
| &#39;Dispatch: 100 concurrent commands&#39;       |  6,912.43 ns |  37.062 ns |  32.855 ns | 103.91 |    0.74 | 0.3891 |    7392 B |       61.60 |
| &#39;Wolverine: 100 concurrent commands&#39;      | 20,519.28 ns | 252.355 ns | 236.053 ns | 308.44 |    3.82 | 3.6926 |   69728 B |      581.07 |
| &#39;Dispatch: Batch queries (10)&#39;            |  1,058.66 ns |   3.242 ns |   2.874 ns |  15.91 |    0.10 | 0.1526 |    2872 B |       23.93 |
| &#39;Wolverine: Batch queries (10)&#39;           |  2,718.39 ns |  23.004 ns |  20.392 ns |  40.86 |    0.37 | 0.4387 |    8312 B |       69.27 |
