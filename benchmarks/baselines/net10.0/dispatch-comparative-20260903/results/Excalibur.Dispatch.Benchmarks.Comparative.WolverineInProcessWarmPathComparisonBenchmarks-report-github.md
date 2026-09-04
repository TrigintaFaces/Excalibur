```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                        | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------------------------------------- |-------------:|-----------:|-----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                            |     49.73 ns |   0.609 ns |   0.508 ns |   1.00 |    0.01 | 0.0051 |      96 B |        1.00 |
| &#39;Dispatch (ultra-local): Single command&#39;                      |     32.88 ns |   0.331 ns |   0.293 ns |   0.66 |    0.01 | 0.0013 |      24 B |        0.25 |
| &#39;Wolverine (in-process): Single command InvokeAsync&#39;          |    189.91 ns |   1.422 ns |   1.260 ns |   3.82 |    0.04 | 0.0310 |     584 B |        6.08 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;                |    114.57 ns |   0.827 ns |   0.733 ns |   2.30 |    0.03 | 0.0050 |      96 B |        1.00 |
| &#39;Wolverine (in-process): Notification to 2 handlers (inline)&#39; |    203.55 ns |   2.268 ns |   2.010 ns |   4.09 |    0.06 | 0.0317 |     600 B |        6.25 |
| &#39;Dispatch (local): Query with return&#39;                         |     63.88 ns |   0.483 ns |   0.452 ns |   1.28 |    0.02 | 0.0153 |     288 B |        3.00 |
| &#39;Wolverine (in-process): Query with return InvokeAsync&#39;       |    254.04 ns |   4.834 ns |   4.964 ns |   5.11 |    0.11 | 0.0410 |     776 B |        8.08 |
| &#39;Dispatch (local): 10 concurrent commands&#39;                    |    617.38 ns |   3.541 ns |   2.957 ns |  12.42 |    0.13 | 0.0715 |    1360 B |       14.17 |
| &#39;Wolverine (in-process): 10 concurrent commands&#39;              |  2,021.82 ns |  29.585 ns |  26.226 ns |  40.66 |    0.65 | 0.3204 |    6048 B |       63.00 |
| &#39;Dispatch (local): 100 concurrent commands&#39;                   |  5,816.19 ns |  18.954 ns |  17.729 ns | 116.96 |    1.19 | 0.6409 |   12160 B |      126.67 |
| &#39;Wolverine (in-process): 100 concurrent commands&#39;             | 20,216.55 ns | 282.749 ns | 236.108 ns | 406.56 |    6.06 | 3.1433 |   59328 B |      618.00 |
